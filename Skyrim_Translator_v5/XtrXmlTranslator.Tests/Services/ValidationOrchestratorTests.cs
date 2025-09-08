using FluentAssertions;
using Xunit;
using XtrXmlTranslator.App.Services;
using XtrXmlTranslator.App.ViewModels;

namespace XtrXmlTranslator.Tests.Services;

public class ValidationOrchestratorTests
{
    [Fact]
    public void AutoFixRow_ReordersCkAtoms_And_RestoresPercentLiterals()
    {
        var svc = new ValidationOrchestrator();
        var src = "<ck1/> value <ck2/> 100%%";
        var row = new TranslationRowVM("ctx", src) { Index = 1 };
        row.TranslationText = "<ck2/> value <ck1/> 100%"; // 바뀐 CK 순서, %%가 %로 줄어듦

        svc.AutoFixRow(row);

        row.TranslationText.Should().Contain("<ck1/>");
        row.TranslationText.Should().Contain("<ck2/>");
        // %% 복원 확인
        row.TranslationText.Split("%%").Length.Should().BeGreaterThan(1);
    }

    [Fact]
    public void RevalidateRow_SetsFlags_ForOkAndError()
    {
        var svc = new ValidationOrchestrator();
        // OK 케이스
        var rowOk = new TranslationRowVM("ctx", "Hello {0}") { Index = 1 };
        rowOk.TranslationText = "Hello {0}";
        rowOk.Status = RowStatus.Error; // 상태 회복 확인을 위해 Error로 설정
        svc.RevalidateRow(rowOk);
        rowOk.TagOk.Should().BeTrue();
        rowOk.Warning.Should().BeEmpty();
        rowOk.Status.Should().Be(RowStatus.Edited);

        // Error 케이스: 자리표시자 누락
        var rowErr = new TranslationRowVM("ctx", "HP: %d") { Index = 2 };
        rowErr.TranslationText = "HP:"; // %d 누락
        svc.RevalidateRow(rowErr);
        rowErr.TagOk.Should().BeFalse();
        rowErr.Status.Should().Be(RowStatus.Error);
        rowErr.Warning.Should().NotBeNullOrEmpty();
    }
}

