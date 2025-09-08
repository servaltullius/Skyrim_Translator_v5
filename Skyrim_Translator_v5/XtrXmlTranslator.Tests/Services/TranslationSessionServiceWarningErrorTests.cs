using System.Collections.Concurrent;
using FluentAssertions;
using Xunit;
using XtrXmlTranslator.App.Services;
using XtrXmlTranslator.App.ViewModels;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.TM;
using XtrXmlTranslator.Core.Tokenizing;

namespace XtrXmlTranslator.Tests.Services;

public class TranslationSessionServiceWarningErrorTests
{
    private sealed class FakeSecrets : ISecretsProvider { public string? Get(string name) => "dummy"; }

    private sealed class WarningTranslator : ITranslator
    {
        public Task<IReadOnlyList<string>> TranslateSegmentsAsync(
            IReadOnlyList<XtrXmlTranslator.Core.Srx.TokenSegment> segments,
            Action<int, int, string>? onDelta = null,
            CancellationToken ct = default,
            Action<int, string>? onSegmentWarning = null,
            Action<int, string>? onSegmentError = null)
        {
            var outs = new List<string>(segments.Count);
            for (int segIdx = 0; segIdx < segments.Count; segIdx++)
            {
                var seg = segments[segIdx];
                var runs = seg.Tokens.Where(t => t.Type == InlineTokenType.Text).ToList();
                var translatedRuns = new string[runs.Count];
                for (int runIdx = 0; runIdx < runs.Count; runIdx++)
                {
                    var delta = runs[runIdx].Value ?? string.Empty;
                    translatedRuns[runIdx] = delta;
                    onDelta?.Invoke(segIdx, runIdx, delta);
                    onSegmentWarning?.Invoke(segIdx, "warn-synthetic");
                }
                outs.Add(InlineTokenizer.Reassemble(seg.Tokens, new Queue<string>(translatedRuns)));
            }
            return Task.FromResult<IReadOnlyList<string>>(outs);
        }
    }

    private sealed class PlaceholderDroppingTranslator : ITranslator
    {
        public Task<IReadOnlyList<string>> TranslateSegmentsAsync(
            IReadOnlyList<XtrXmlTranslator.Core.Srx.TokenSegment> segments,
            Action<int, int, string>? onDelta = null,
            CancellationToken ct = default,
            Action<int, string>? onSegmentWarning = null,
            Action<int, string>? onSegmentError = null)
        {
            var outs = new List<string>(segments.Count);
            for (int segIdx = 0; segIdx < segments.Count; segIdx++)
            {
                var seg = segments[segIdx];
                var runs = seg.Tokens.Where(t => t.Type == InlineTokenType.Text).ToList();
                var translatedRuns = new string[runs.Count];
                for (int runIdx = 0; runIdx < runs.Count; runIdx++)
                {
                    var src = runs[runIdx].Value ?? string.Empty;
                    var tr = src.Replace("%d", string.Empty).Replace("{0}", string.Empty);
                    translatedRuns[runIdx] = tr;
                    onDelta?.Invoke(segIdx, runIdx, tr);
                }
                // 의도적으로 플레이스홀더 토큰을 드롭한 상태를 시뮬레이션하기 위해
                // Reassemble을 사용하지 않고 텍스트 런만 이어붙여 반환한다.
                outs.Add(string.Concat(translatedRuns));
            }
            return Task.FromResult<IReadOnlyList<string>>(outs);
        }
    }

    private sealed class FakeFactory : ITranslatorFactory
    {
        private readonly ITranslator _t; public FakeFactory(ITranslator t) => _t = t;
        public ITranslator Create(GeminiOptions options, ITmStore tm) => _t;
    }

    [Fact]
    public async Task RunAsync_Propagates_OnRowWarning()
    {
        var svc = new TranslationSessionService();
        var rows = new List<TranslationRowVM> { new TranslationRowVM("ctx", "Hello %d") { Index = 1 } };
        var warnings = new ConcurrentQueue<string>();
        await svc.RunAsync(
            rows,
            languageCode: "en-US",
            prompt: new XtrXmlTranslator.Core.Prompt.PromptConfig(),
            glossary: new XtrXmlTranslator.Core.Glossary.GlossaryStore(),
            compiled: null,
            secrets: new FakeSecrets(),
            translatorFactory: new FakeFactory(new WarningTranslator()),
            ct: CancellationToken.None,
            cb: new TranslationSessionCallbacks(OnRowWarning: (i, msg) => warnings.Enqueue(msg))
        );
        warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunAsync_FinalValidation_IssuesCaptured_WhenPlaceholdersDropped()
    {
        var svc = new TranslationSessionService();
        var rows = new List<TranslationRowVM> { new TranslationRowVM("ctx", "HP: %d, {0}") { Index = 1 } };
        var issueCounts = new ConcurrentQueue<int>();
        var errors = new ConcurrentQueue<string>();
        var finals = new ConcurrentQueue<string>();
        await svc.RunAsync(
            rows,
            languageCode: "en-US",
            prompt: new XtrXmlTranslator.Core.Prompt.PromptConfig(),
            glossary: new XtrXmlTranslator.Core.Glossary.GlossaryStore(),
            compiled: null,
            secrets: new FakeSecrets(),
            translatorFactory: new FakeFactory(new PlaceholderDroppingTranslator()),
            ct: CancellationToken.None,
            cb: new TranslationSessionCallbacks(
                OnRowFinal: (i, text, issues) => { finals.Enqueue(text); issueCounts.Enqueue(issues.Count); },
                OnRowError: (i, msg) => errors.Enqueue(msg)
            )
        );
        if (issueCounts.IsEmpty)
        {
            Assert.Fail($"Diagnostics => OnRowError: {string.Join(" | ", errors)}; FinalText: {string.Join(" | ", finals)}");
        }
        issueCounts.TryDequeue(out var c).Should().BeTrue();
        if (c == 0)
        {
            Assert.Fail($"c==0 Diagnostics => Source: {rows[0].SourceText}; FinalText: {string.Join(" | ", finals)}");
        }
        c.Should().BeGreaterThan(0);
    }
}

