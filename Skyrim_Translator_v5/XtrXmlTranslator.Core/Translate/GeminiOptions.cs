namespace XtrXmlTranslator.Core.Translate;

public enum GeminiProvider { AiGoogle, VertexAI }

public sealed class GeminiOptions
{
    public GeminiProvider Provider { get; init; } = GeminiProvider.AiGoogle;
    public string Model { get; init; } = "gemini-2.5-flash";
    public string? ApiKey { get; init; }
    public string? AccessToken { get; init; }
    public string? ProjectId { get; init; }
    public string Location { get; init; } = "us-central1";
    public double Temperature { get; init; } = 0.2;
    public string? SystemInstruction { get; init; }
    public int MaxConcurrency { get; init; } = 4;
    public int RequestsPerMinute { get; init; } = 10;
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(60);

    // Resilience settings
    public int RetryMaxAttempts { get; init; } = 5;
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan HttpHandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);

    // Optional handshake retry callback: (attemptNumber, delay, statusCode)
    public Action<int, TimeSpan, int?>? OnHandshakeRetry { get; init; }

    // Optional handshake success callback: (statusCode)
    public Action<int>? OnHandshakeSuccess { get; init; }

    // Optional RPM pacing wait callback
    public Action<TimeSpan>? OnPaceWait { get; init; }
}
