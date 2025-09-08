using System.Collections.Concurrent;

namespace XtrXmlTranslator.Core.Cache
{
    /// <summary>
    /// 동일 세그먼트 캐시(메모리). 키는 호출측에서 표준화된 텍스트로 구성.
    /// </summary>
    public sealed class SegmentCache
    {
        private readonly ConcurrentDictionary<string, string> _cache = new();

        public bool TryGet(string key, out string? value) => _cache.TryGetValue(key, out value);
        public void Set(string key, string translation) => _cache[key] = translation;
        public void Clear() => _cache.Clear();
    }
}
