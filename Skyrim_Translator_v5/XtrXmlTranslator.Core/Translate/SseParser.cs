using System.Text.Json;

namespace XtrXmlTranslator.Core.Translate;

public static class SseParser
{
    public static string? TryExtractTextDelta(string line)
    {
        if (!line.StartsWith("data: ")) return null;
        var payload = line.AsSpan(6).Trim();
        if (payload.SequenceEqual("".AsSpan()) || payload.SequenceEqual("[DONE]".AsSpan()))
            return null;

        try
        {
            var json = new string(payload);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
                return null;
            var cand0 = cands[0];
            if (!cand0.TryGetProperty("content", out var content)) return null;
            if (!content.TryGetProperty("parts", out var parts)) return null;

            string delta = "";
            foreach (var p in parts.EnumerateArray())
            {
                if (p.TryGetProperty("text", out var t)) delta += t.GetString() ?? "";
            }
            return delta.Length == 0 ? null : delta;
        }
        catch
        {
            return null;
        }
    }
}
