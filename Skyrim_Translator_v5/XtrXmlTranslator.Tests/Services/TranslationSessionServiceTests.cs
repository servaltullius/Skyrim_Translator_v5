using System.Collections.Concurrent;
using FluentAssertions;
using Xunit;
using XtrXmlTranslator.App.Services;
using XtrXmlTranslator.App.ViewModels;
using XtrXmlTranslator.Core.Glossary;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.TM;

namespace XtrXmlTranslator.Tests.Services;

public class TranslationSessionServiceTests
{
    private sealed class FakeSecrets : ISecretsProvider
    {
        public string? Get(string name) => name == "GEMINI_API_KEY" ? "dummy" : null;
    }

    private sealed class FakeTranslator : ITranslator
    {
        private readonly Func<string, string> _transform;
        public FakeTranslator(Func<string, string>? transform = null) => _transform = transform ?? (s => s);
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
                    var tr = _transform(runs[runIdx].Value ?? string.Empty);
                    translatedRuns[runIdx] = tr;
                    onDelta?.Invoke(segIdx, runIdx, tr);
                }
                outs.Add(InlineTokenizer.Reassemble(seg.Tokens, new Queue<string>(translatedRuns)));
            }
            return Task.FromResult<IReadOnlyList<string>>(outs);
        }
    }

    private sealed class FakeFactory : ITranslatorFactory
    {
        private readonly ITranslator _translator;
        public FakeFactory(ITranslator translator) => _translator = translator;
        public ITranslator Create(GeminiOptions options, ITmStore tm) => _translator;
    }

    [Fact]
    public async Task RunAsync_ProtectAndEnforce_AreApplied_And_FinalValidated()
    {
        var svc = new TranslationSessionService();
        var rows = new List<TranslationRowVM>
        {
            new TranslationRowVM("path/1","Dragonborn meets Skyrim.") { Index = 1 }
        };

        var glossary = new GlossaryStore();
        glossary.Entries.Add(new GlossaryEntry { Source = "Dragonborn", Type = GlossaryType.PROTECT });
        glossary.Entries.Add(new GlossaryEntry { Source = "Skyrim", Target = "스카이림", Type = GlossaryType.ENFORCE });
        var compiled = GlossaryCompiled.Build(glossary);

        var fake = new FakeTranslator(s => s); // 입력 그대로
        var factory = new FakeFactory(fake);

        var progress = new ConcurrentQueue<(double pct, string detail)>();
        var final = new ConcurrentQueue<string>();
        var warnings = new ConcurrentQueue<string>();
        var errors = new ConcurrentQueue<string>();

        await svc.RunAsync(
            rows: rows,
            languageCode: "en-US",
            prompt: new XtrXmlTranslator.Core.Prompt.PromptConfig(),
            glossary: glossary,
            compiled: compiled,
            secrets: new FakeSecrets(),
            translatorFactory: factory,
            ct: CancellationToken.None,
            cb: new TranslationSessionCallbacks(
                OnProgress: (p, d) => progress.Enqueue((p, d)),
                OnRowDelta: (i, delta) => { rows[i].TranslationText += delta; },
                OnRowWarning: (i, msg) => warnings.Enqueue(msg),
                OnRowError: (i, msg) => errors.Enqueue(msg),
                OnRowFinal: (i, text, issues) => final.Enqueue(text)
            )
        );

        warnings.Should().BeEmpty();
        errors.Should().BeEmpty();
        final.Should().HaveCount(1);
        final.TryDequeue(out var text).Should().BeTrue();
        text.Should().Contain("Dragonborn"); // PROTECT 유지
        text.Should().Contain("스카이림");   // ENFORCE 적용
    }
}

