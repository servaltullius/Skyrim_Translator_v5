using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using XtrXmlTranslator.App.ViewModels;
using XtrXmlTranslator.Core.Glossary;
using XtrXmlTranslator.Core.Prompt;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.TM;
using XtrXmlTranslator.Core.Validation;
using Microsoft.Extensions.Configuration;
using XtrXmlTranslator.App.Configuration;

namespace XtrXmlTranslator.App.Services;

public sealed record TranslationSessionCallbacks(
    Action<double, string>? OnProgress = null,
    Action<int, string>? OnRowDelta = null,
    Action<int, string>? OnRowWarning = null,
    Action<int, string>? OnRowError = null,
    Action<int, string, IReadOnlyList<ValidationIssue>>? OnRowFinal = null,
    Action<string>? OnStatus = null,
    Action<int,int,int,long>? OnSummary = null // total, warn, err, paceWaitMs
);

public sealed class TranslationSessionService
{
    private readonly IConfiguration? _configuration;

    public TranslationSessionService()
    {
        // 기본: 환경변수만 로드(prefix: XTRANS_)
        _configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables(prefix: "XTRANS_")
            .Build();
    }

    public TranslationSessionService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task RunAsync(
        IList<TranslationRowVM> rows,
        string languageCode,
        PromptConfig prompt,
        GlossaryStore glossary,
        GlossaryCompiled? compiled,
        ISecretsProvider secrets,
        ITranslatorFactory translatorFactory,
        CancellationToken ct,
        TranslationSessionCallbacks? cb = null)
    {
        cb ??= new TranslationSessionCallbacks();
        cb.OnStatus?.Invoke("번역 시작");
        Log.Information("Translate session start: rows={Rows} lang={Lang}", rows.Count, languageCode);

        // 초기화
        foreach (var row in rows)
        {
            row.TranslationText = string.Empty;
            row.TagOk = true;
            row.Warning = string.Empty;
            row.Status = RowStatus.Auto;
        }

        // SRX 준비
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.ForLanguage(languageCode));
        var srx = SrxCompiler.Compile(srxDoc, languageCode);

        // 마스킹/세그먼트 수집
        var segments = new List<TokenSegment>();
        var rowIndexOfSegment = new List<int>();
        var rowMaskMaps = new Dictionary<int, List<(string Token, string Original)>>();

        for (int idx = 0; idx < rows.Count; idx++)
        {
            var row = rows[idx];
            try
            {
                var toks = InlineTokenizer.Tokenize(row.SourceText);
                int gid = 0;
                var combinedMap = new List<(string Token, string Original)>();
                for (int j = 0; j < toks.Count; j++)
                {
                    if (toks[j].Type != InlineTokenType.Text) continue;
                    var res = compiled is not null
                        ? compiled.MaskProtected(toks[j].Value)
                        : GlossaryApply.MaskProtectedTerms(toks[j].Value, glossary.Entries);
                    var masked = res.masked; var map = res.map;
                    if (map.Count > 0)
                    {
                        foreach (var (tk, orig) in map)
                        {
                            var newTk = $"⟪T{gid++}⟫";
                            masked = masked.Replace(tk, newTk);
                            combinedMap.Add((newTk, orig));
                        }
                        toks[j] = new InlineToken(InlineTokenType.Text, masked);
                    }
                }
                rowMaskMaps[idx] = combinedMap;

                var segs = SrxSegmenter.SegmentTokens(toks, srx);
                segments.AddRange(segs);
                rowIndexOfSegment.AddRange(Enumerable.Repeat(idx, segs.Count));
            }
            catch (Exception ex)
            {
                cb.OnRowError?.Invoke(idx, $"세그먼트 오류: {ex.Message}");
                Log.Error(ex, "Segmentation error at row {Row}", idx);
            }
        }

        // 배치 요약/시스템 프롬프트
        var batchSources = segments
            .SelectMany(seg => seg.Tokens)
            .Where(t => t.Type == InlineTokenType.Text)
            .Select(t => t.Value);
        var summary = GlossaryApply.SummarizeForBatch(batchSources, glossary, limit: 100);
        var sysPrompt = PromptBuilder.BuildSystemInstruction(prompt, summary);

        // 환경/옵션
        int TryParseInt(string? s, int def) => int.TryParse(s, out var v) ? v : def;
        double TryParseDouble(string? s, double def) => double.TryParse(s, out var v) ? v : def;

        // 1) API 키 (환경변수/파일)
        var apiKey = secrets.Get("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            cb.OnStatus?.Invoke("GEMINI_API_KEY 미설정: 번역 건너뜀");
            Log.Warning("GEMINI_API_KEY not set; skipping translation session");
            return;
        }

        // 2) 구성 바인딩(Gemini)
        var gemini = new GeminiSettings();
        _configuration?.GetSection("Gemini")?.Bind(gemini);

        // 3) 레거시 환경 변수로 일부 값 덮어쓰기(있을 때만)
        int retryMax = TryParseInt(secrets.Get("XTRANS_RETRY_MAX"), gemini.RetryMaxAttempts);
        int baseMs = TryParseInt(secrets.Get("XTRANS_RETRY_BASEMS"), (int)gemini.RetryBaseDelay.TotalMilliseconds);
        int hsMs = TryParseInt(secrets.Get("XTRANS_TIMEOUT_HANDSHAKE_MS"), (int)gemini.HttpHandshakeTimeout.TotalMilliseconds);
        string modelOverride = secrets.Get("XTRANS_GEMINI_MODEL") ?? string.Empty;
        double tempOverride = TryParseDouble(secrets.Get("XTRANS_GEMINI_TEMPERATURE"), gemini.Temperature);

        // 4) GeminiOptions 구성
        int handshakeRetries = 0; int segWarn = 0, segErr = 0; long paceWaitTicks = 0;
        var opt = new GeminiOptions {
            Provider = string.Equals(gemini.Provider, "VertexAI", StringComparison.OrdinalIgnoreCase) ? GeminiProvider.VertexAI : GeminiProvider.AiGoogle,
            ApiKey = apiKey,
            Model = string.IsNullOrWhiteSpace(modelOverride) ? gemini.Model : modelOverride,
            ProjectId = gemini.ProjectId,
            Location = gemini.Location,
            Temperature = tempOverride,
            MaxConcurrency = gemini.MaxConcurrency,
            RequestsPerMinute = gemini.RequestsPerMinute,
            SystemInstruction = sysPrompt,
            RetryMaxAttempts = retryMax,
            RetryBaseDelay = TimeSpan.FromMilliseconds(baseMs),
            HttpHandshakeTimeout = TimeSpan.FromMilliseconds(hsMs),
            HttpTimeout = gemini.HttpTimeout,
            OnHandshakeRetry = (attempt, delay, code) => { handshakeRetries = attempt; Log.Information("Handshake retry attempt={Attempt} delayMs={Delay} status={Status}", attempt, (int)delay.TotalMilliseconds, code?.ToString() ?? "-" ); },
            OnHandshakeSuccess = code => Log.Information("Handshake success status={Status} totalRetries={Retries}", code, handshakeRetries),
            OnPaceWait = t => System.Threading.Interlocked.Add(ref paceWaitTicks, t.Ticks)
        };
        var tm = new InMemoryTm();
        var translator = translatorFactory.Create(opt, tm);

        int total = segments.Count;

        // 태그-헤비 세그먼트 스킵 옵션: XTRANS_SKIP_TAG_HEAVY(존재하면 활성), XTRANS_TAG_HEAVY_MIN_TEXT(기본 0)
        bool SkipTagHeavy(TokenSegment s)
        {
            int textLen = s.Tokens.Where(t => t.Type == InlineTokenType.Text).Sum(t => (t.Value ?? string.Empty).Length);
            bool hasTag = s.Tokens.Any(t => t.Type == InlineTokenType.XmlLikeTag);

            int TryParseInt2(string? v, int d) => int.TryParse(v, out var x) ? x : d;

            // 구성 우선 → 레거시 환경변수 폴백
            bool enabledCfg = false;
            int minCharsCfg = 0;
            if (_configuration is not null)
            {
                var sEnabled = _configuration["Translator:SkipTagHeavy"];
                var sMinChars = _configuration["Translator:TagHeavyMinText"];
                if (!string.IsNullOrWhiteSpace(sEnabled)) bool.TryParse(sEnabled, out enabledCfg);
                if (!string.IsNullOrWhiteSpace(sMinChars)) minCharsCfg = TryParseInt2(sMinChars, 0);
            }
            int minChars = minCharsCfg > 0 ? minCharsCfg : TryParseInt2(secrets.Get("XTRANS_TAG_HEAVY_MIN_TEXT"), 0);
            bool enabled = enabledCfg || !string.IsNullOrWhiteSpace(secrets.Get("XTRANS_SKIP_TAG_HEAVY"));
            return enabled && hasTag && textLen <= minChars;
        }

        var mapIncludedToOriginal = new List<int>();
        var includedSegments = new List<TokenSegment>();
        for (int i = 0; i < segments.Count; i++)
        {
            if (!SkipTagHeavy(segments[i]))
            {
                mapIncludedToOriginal.Add(i);
                includedSegments.Add(segments[i]);
            }
        }

        IReadOnlyList<string> subOutputs = Array.Empty<string>();
        if (includedSegments.Count > 0)
        {
            subOutputs = await translator.TranslateSegmentsAsync(
                includedSegments,
                onDelta: (subSegIdx, runIdx, delta) =>
                {
                    int segIdx = mapIncludedToOriginal[subSegIdx];
                    var rowIdx = rowIndexOfSegment[segIdx];
                    cb.OnRowDelta?.Invoke(rowIdx, delta);
                },
                ct: ct,
                onSegmentWarning: (subSegIdx, message) =>
                {
                    segWarn++;
                    int segIdx = mapIncludedToOriginal[subSegIdx];
                    var rowIdx = rowIndexOfSegment[segIdx];
                    cb.OnRowWarning?.Invoke(rowIdx, message);
                    Log.Warning("Segment warning seg={Seg} msg={Msg}", segIdx, message);
                },
                onSegmentError: (subSegIdx, message) =>
                {
                    segErr++;
                    int segIdx = mapIncludedToOriginal[subSegIdx];
                    var rowIdx = rowIndexOfSegment[segIdx];
                    cb.OnRowError?.Invoke(rowIdx, message);
                    Log.Error("Segment error seg={Seg} msg={Msg}", segIdx, message);
                });
        }

        // 전체 outputs 재구성(스킵된 세그먼트는 원문 유지)
        var outputs = new string[segments.Count];
        int k = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            if (k < mapIncludedToOriginal.Count && mapIncludedToOriginal[k] == i)
            {
                outputs[i] = subOutputs[k++];
            }
            else if (SkipTagHeavy(segments[i]))
            {
                outputs[i] = string.Concat(segments[i].Tokens.Select(t => t.Value));
            }
            else
            {
                // 안전장치(이상 경로)
                outputs[i] = string.Concat(segments[i].Tokens.Select(t => t.Value));
            }
        }

        // 진행률 마무리(구간성 표시)
        for (int i = 1; i <= total; i++)
        {
            cb.OnProgress?.Invoke((double)i / total * 100.0, $"{i}/{total} 세그먼트");
        }

        // 세그먼트 결과를 행별로 조립(태그 포함)
        var perRow = new Dictionary<int, System.Text.StringBuilder>();
for (int segIdx = 0; segIdx < outputs.Length; segIdx++)
        {
            var rowIdx = rowIndexOfSegment[segIdx];
            if (!perRow.TryGetValue(rowIdx, out var sb)) { sb = new System.Text.StringBuilder(); perRow[rowIdx] = sb; }
            sb.Append(outputs[segIdx]);
        }

        // 사후 처리(언마스크 → ENFORCE/PREFER → 검증)
        var affectedRows = rowIndexOfSegment.Distinct().ToList();
        foreach (var rowIdx in affectedRows)
        {
            var row = rows[rowIdx];
            try
            {
                var text = perRow.TryGetValue(rowIdx, out var sb) ? sb.ToString() : row.TranslationText;
                if (rowMaskMaps.TryGetValue(rowIdx, out var map) && map.Count > 0)
                {
                    text = GlossaryApply.UnmaskProtectedTerms(text, map);
                }
                // ENFORCE/PREFER 적용
                text = compiled is not null
                    ? compiled.ApplyReplacement(text)
                    : GlossaryApply.ApplyPostReplace(text, glossary.Entries);

                // 검증
                var issues = Validator.Validate(row.SourceText, text);
                cb.OnRowFinal?.Invoke(rowIdx, text, issues);
            }
            catch (Exception ex)
            {
                cb.OnRowError?.Invoke(rowIdx, $"사후처리/검증 예외: {ex.Message}");
                Log.Error(ex, "Post-processing failed for row {Index}", row.Index);
            }
        }

        int warnCount = rows.Count(r => !r.TagOk);
        int errCount = rows.Count(r => r.Status == RowStatus.Error);
        var paceMs = (long)TimeSpan.FromTicks(paceWaitTicks).TotalMilliseconds;
        cb.OnSummary?.Invoke(total, warnCount, errCount, paceMs);
        cb.OnStatus?.Invoke("완료");
        Log.Information("Translate complete: segments={Total} warnings={Warn} errors={Err} segWarnCallbacks={SegWarn} segErrCallbacks={SegErr} paceWaitMs={Pace}", total, warnCount, errCount, segWarn, segErr, paceMs);
    }
}
