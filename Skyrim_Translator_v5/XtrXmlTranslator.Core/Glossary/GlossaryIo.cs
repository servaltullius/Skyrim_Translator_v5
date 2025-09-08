using System.Text;

namespace XtrXmlTranslator.Core.Glossary;

public static class GlossaryIo
{
    public static GlossaryStore Load(string path)
    {
        var g = new GlossaryStore();
        char sep = path.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("#")) continue;
            var cells = line.Split(sep);
            if (cells.Length < 2) continue;
            var e = new GlossaryEntry
            {
                Source = cells[0].Trim(),
                Target = cells[1].Trim(),
                Type = cells.Length > 2 && Enum.TryParse<GlossaryType>(cells[2], true, out var t) ? t : GlossaryType.ENFORCE,
                CaseSensitive = cells.Length > 3 && bool.TryParse(cells[3], out var cs) && cs,
                Notes = cells.Length > 4 ? cells[4] : null
            };
            if (!string.IsNullOrEmpty(e.Source))
                g.Entries.Add(e);
        }
        return g;
    }

    public static void Save(GlossaryStore g, string path)
    {
        char sep = path.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
        using var w = new StreamWriter(path, false, Encoding.UTF8);
        w.WriteLine($"# Source{sep}Target{sep}Type(ENFORCE|PREFER|PROTECT){sep}CaseSensitive{sep}Notes");
        foreach (var e in g.Entries)
        {
            string t = e.Target ?? string.Empty;
            w.WriteLine(string.Join(sep, new[]
            {
                e.Source, t, e.Type.ToString(), e.CaseSensitive.ToString(), e.Notes ?? string.Empty
            }));
        }
    }
}
