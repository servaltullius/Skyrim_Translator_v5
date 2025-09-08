using Xunit;

public class IntegrationPlaceholderTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_Placeholder()
    {
        // 실제 통합 테스트는 GEMINI_API_KEY가 설정된 환경에서만 실행하도록 CI에서 분리합니다.
        Assert.True(true);
    }
}

