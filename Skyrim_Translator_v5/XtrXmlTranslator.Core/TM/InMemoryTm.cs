using System.Collections.Concurrent;

namespace XtrXmlTranslator.Core.TM;

public interface ITmStore
{
    bool TryGet(string key, out string value);
    void Put(string key, string value);
}

public sealed class InMemoryTm : ITmStore
{
    private readonly ConcurrentDictionary<string, string> _map = new();
    public bool TryGet(string key, out string value) => _map.TryGetValue(key, out value!);
    public void Put(string key, string value) => _map[key] = value;
}
