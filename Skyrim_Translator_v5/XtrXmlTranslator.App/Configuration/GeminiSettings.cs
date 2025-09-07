using System;

namespace XtrXmlTranslator.App.Configuration;

public sealed class GeminiSettings
{
    public string Provider { get; set; } = "AiGoogle"; // Fixed: AiGoogle only (VertexAI unsupported)
    public string Model { get; set; } = "gemini-2.5-flash";
    public string? ProjectId { get; set; }
    public string Location { get; set; } = "us-central1";
    public double Temperature { get; set; } = 0.2;
    public int MaxConcurrency { get; set; } = 4;
    public int RequestsPerMinute { get; set; } = 10;
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public int RetryMaxAttempts { get; set; } = 5;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan HttpHandshakeTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public string? SystemInstruction { get; set; }
}

