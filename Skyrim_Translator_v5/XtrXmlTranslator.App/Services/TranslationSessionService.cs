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
    Action<int, int, int, long>? OnSummary = null // total, warn, err, paceWaitMs
);

public sealed class TranslationSessionService
{
    private readonly IConfiguration? _configuration;

    public TranslationSessionService()
    {
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

        InitRows(rows);

        var srx = BuildSrx(languageCode);
        var (segments, rowIndexOfSegment, rowMaskMaps) = BuildSegments(rows, glossary, compiled, srx, cb);

        var sysPrompt = BuildSystemPrompt(prompt, glossary, segments);

        if (!TryBuildGeminiOptions(secrets, sysPrompt, out var opt, cb)) return;
        var translator = translatorFactory.Create(opt, new InMemoryTm());

        var (includedSegments, mapIncludedToOriginal, skipPredicate) = FilterTagHeavySegments(segments, secrets);

        var subOutputs = await TranslateIncludedAsync(translator, includedSegments, mapIncludedToOriginal, rowIndexOfSegment, cb, ct);

        var outputs = ReassembleOutputs(segments, subOutputs, mapIncludedToOriginal, skipPredicate);

        ReportProgress(cb, total: segments.Count);

        PostProcessAndEmit(rows, glossary, compiled, rowIndexOfSegment, rowMaskMaps, outputs, cb);

        SummarizeAndLog(rows, segments.Count, cb, opt);
    }

    private static void InitRows(IList<TranslationRowVM> rows)
    {
        foreach (var row in rows)
        {
            row.TranslationText = string.Empty;
            row.TagOk = true;
            row.Warning = string.Empty;
            row.Status = RowStatus.Auto;
        }
    }

    private static SrxEngine BuildSrx(string languageCode)
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.ForLanguage(languageCode));
        return SrxCompiler.Compile(srxDoc, languageCode);
    }

    private static (List<TokenSegment> segments, List<int> rowIndexOfSegment, Dictionary<int, List<(string Token, string Original)>> rowMaskMaps)
        BuildSegments(IList<TranslationRowVM> rows, GlossaryStore glossary, GlossaryCompiled? compiled, SrxEngine srx, TranslationSessionCallbacks cb)
    {
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

        return (segments, rowIndexOfSegment, rowMaskMaps);
    }

    private static string BuildSystemPrompt(PromptConfig prompt, GlossaryStore glossary, List<TokenSegment> segments)
    {
        var batchSources = segments
            .SelectMany(seg => seg.Tokens)
            .Where(t => t.Type == InlineTokenType.Text)
            .Select(t => t.Value);
        var summary = GlossaryApply.SummarizeForBatch(batchSources, glossary, limit: 100);
        return PromptBuilder.BuildSystemInstruction(prompt, summary);
    }

    private bool TryBuildGeminiOptions(ISecretsProvider secrets, string sysPrompt, out GeminiOptions opt, TranslationSessionCallbacks cb)
    {
        opt = default!;
        int TryParseInt(string? s, int def) => int.TryParse(s, out var v) ? v : def;
        double TryParseDouble(string? s, double def) => double.TryParse(s, out var v) ? v : def;

        var apiKey = secrets.Get("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            cb.OnStatus?.Invoke("GEMINI_API_KEY 미설정: 번역 건너뜀");
            Log.Warning("GEMINI_API_KEY not set; skipping translation session");
            return false;
        }

        var gemini = new GeminiSettings();
        _configuration?.GetSection("Gemini")?.Bind(gemini);
        if (!string.IsNullOrWhiteSpace(gemini.Provider) && !gemini.Provider.Equals("AiGoogle", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("Gemini Provider set to {Provider} but only AiGoogle is supported. Falling back to AiGoogle.", gemini.Provider);
        }

        int retryMax = TryParseInt(secrets.Get("XTRANS_RETRY_MAX"), gemini.RetryMaxAttempts);
        int baseMs = TryParseInt(secrets.Get("XTRANS_RETRY_BASEMS"), (int)gemini.RetryBaseDelay.TotalMilliseconds);
        int hsMs = TryParseInt(secrets.Get("XTRANS_TIMEOUT_HANDSHAKE_MS"), (int)gemini.HttpHandshakeTimeout.TotalMilliseconds);
        string modelOverride = secrets.Get("XTRANS_GEMINI_MODEL") ?? string.Empty;
        double tempOverride = TryParseDouble(secrets.Get("XTRANS_GEMINI_TEMPERATURE"), gemini.Temperature);

        string model = string.IsNullOrWhiteSpace(modelOverride) ? gemini.Model : modelOverride;
        if (string.IsNullOrWhiteSpace(model)) { model = "gemini-2.5-flash"; Log.Warning("Gemini model not set; using default {Model}", model); }
        double temperature = tempOverride;
        if (temperature < 0 || temperature > 1) { Log.Warning("Gemini temperature out of range [0,1]: {Temp}. Using default 0.2.", temperature); temperature = 0.2; }
        int maxConcurrency = gemini.MaxConcurrency > 0 ? gemini.MaxConcurrency : 4;
        if (maxConcurrency != gemini.MaxConcurrency) Log.Warning("MaxConcurrency invalid: {Val}. Using default 4.", gemini.MaxConcurrency);
        int rpm = gemini.RequestsPerMinute > 0 ? gemini.RequestsPerMinute : 10;
        if (rpm != gemini.RequestsPerMinute) Log.Warning("RequestsPerMinute invalid: {Val}. Using default 10.", gemini.RequestsPerMinute);

        int handshakeRetries = 0;
        opt = new GeminiOptions
        {
            Provider = GeminiProvider.AiGoogle,
            ApiKey = apiKey,
            Model = model,
            ProjectId = null,
            Location = gemini.Location,
            Temperature = temperature,
            MaxConcurrency = maxConcurrency,
            RequestsPerMinute = rpm,
            SystemInstruction = sysPrompt,
            RetryMaxAttempts = retryMax,
            RetryBaseDelay = TimeSpan.FromMilliseconds(baseMs),
            HttpHandshakeTimeout = TimeSpan.FromMilliseconds(hsMs),
            HttpTimeout = gemini.HttpTimeout,
            OnHandshakeRetry = (attempt, delay, code) => { handshakeRetries = attempt; Log.Information("Handshake retry attempt={Attempt} delayMs={Delay} status={Status}", attempt, (int)delay.TotalMilliseconds, code?.ToString() ?? "-"); },
            OnHandshakeSuccess = code => Log.Information("Handshake success status={Status} totalRetries={Retries}", code, handshakeRetries),
            OnPaceWait = t => System.Threading.Interlocked.Add(ref _paceWaitTicks, t.Ticks)
        };
        return true;
    }

    private long _paceWaitTicks;

    private (List<TokenSegment> includedSegments, List<int> mapIncludedToOriginal, Func<TokenSegment, bool> skipPredicate) FilterTagHeavySegments(List<TokenSegment> segments, ISecretsProvider secrets)
    {
        bool SkipTagHeavy(TokenSegment s)
        {
            int textLen = s.Tokens.Where(t => t.Type == InlineTokenType.Text).Sum(t => (t.Value ?? string.Empty).Length);
            bool hasTag = s.Tokens.Any(t => t.Type == InlineTokenType.XmlLikeTag);
            int TryParseInt2(string? v, int d) => int.TryParse(v, out var x) ? x : d;

            // 1) 런타임 설정(세션 범위) 최우선
            var sp = AppServices.Provider;
            if (sp is not null)
            {
                var rtObj = sp.GetService(typeof(IRuntimeSettings));
                if (rtObj is IRuntimeSettings rt)
                {
                    int minCharsRt = rt.TagHeavyMinText;
                    bool enabledRt = rt.SkipTagHeavyEnabled;
                    return enabledRt && hasTag && textLen <= (minCharsRt > 0 ? minCharsRt : 0);
                }
            }

            // 2) 구성 우선 → 3) 레거시 환경변수 폴백
            bool enabledCfg = false; int minCharsCfg = 0;
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
        return (includedSegments, mapIncludedToOriginal, SkipTagHeavy);
    }

    private async Task<IReadOnlyList<string>> TranslateIncludedAsync(
        ITranslator translator,
        List<TokenSegment> includedSegments,
        List<int> mapIncludedToOriginal,
        List<int> rowIndexOfSegment,
        TranslationSessionCallbacks cb,
        CancellationToken ct)
    {
        int segWarn = 0, segErr = 0;
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
        return subOutputs;
    }

    private static string[] ReassembleOutputs(
        List<TokenSegment> segments,
        IReadOnlyList<string> subOutputs,
        List<int> mapIncludedToOriginal,
        Func<TokenSegment, bool> skipPredicate)
    {
        var outputs = new string[segments.Count];
        int k = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            if (k < mapIncludedToOriginal.Count && mapIncludedToOriginal[k] == i)
            {
                outputs[i] = subOutputs[k++];
            }
            else if (skipPredicate(segments[i]))
            {
                outputs[i] = string.Concat(segments[i].Tokens.Select(t => t.Value));
            }
            else
            {
                outputs[i] = string.Concat(segments[i].Tokens.Select(t => t.Value));
            }
        }
        return outputs;
    }

    private static void ReportProgress(TranslationSessionCallbacks cb, int total)
    {
        for (int i = 1; i <= total; i++)
        {
            cb.OnProgress?.Invoke((double)i / total * 100.0, $"{i}/{total} 세그먼트");
        }
    }

    private static void PostProcessAndEmit(
        IList<TranslationRowVM> rows,
        GlossaryStore glossary,
        GlossaryCompiled? compiled,
        List<int> rowIndexOfSegment,
        Dictionary<int, List<(string Token, string Original)>> rowMaskMaps,
        string[] outputs,
        TranslationSessionCallbacks cb)
    {
        var perRow = new Dictionary<int, System.Text.StringBuilder>();
        for (int segIdx = 0; segIdx < outputs.Length; segIdx++)
        {
            var rowIdx = rowIndexOfSegment[segIdx];
            if (!perRow.TryGetValue(rowIdx, out var sb)) { sb = new System.Text.StringBuilder(); perRow[rowIdx] = sb; }
            sb.Append(outputs[segIdx]);
        }

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
                text = compiled is not null
                    ? compiled.ApplyReplacement(text)
                    : GlossaryApply.ApplyPostReplace(text, glossary.Entries);

                var issues = Validator.Validate(row.SourceText, text);
                cb.OnRowFinal?.Invoke(rowIdx, text, issues);
            }
            catch (Exception ex)
            {
                cb.OnRowError?.Invoke(rowIdx, $"사후처리/검증 예외: {ex.Message}");
                Log.Error(ex, "Post-processing failed for row {Index}", row.Index);
            }
        }
    }

    private void SummarizeAndLog(IList<TranslationRowVM> rows, int totalSegments, TranslationSessionCallbacks cb, GeminiOptions opt)
    {
        int warnCount = rows.Count(r => !r.TagOk);
        int errCount = rows.Count(r => r.Status == RowStatus.Error);
        var paceMs = (long)TimeSpan.FromTicks(_paceWaitTicks).TotalMilliseconds;
        cb.OnSummary?.Invoke(totalSegments, warnCount, errCount, paceMs);
        cb.OnStatus?.Invoke("완료");
        Log.Information("Translate complete: segments={Total} warnings={Warn} errors={Err} paceWaitMs={Pace} provider={Provider} model={Model}", totalSegments, warnCount, errCount, paceMs, opt.Provider, opt.Model);
    }
}
