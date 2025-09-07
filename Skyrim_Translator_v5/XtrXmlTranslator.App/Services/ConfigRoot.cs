using System;
using Microsoft.Extensions.Configuration;

namespace XtrXmlTranslator.App.Services;

public sealed class ConfigRoot : IAppConfig
{
    private readonly IConfiguration _config;

    public ConfigRoot(IConfiguration config)
    {
        _config = config;
    }

    public string? Get(string key)
    {
        // 호환성: 기존 단일 키를 새 구조로 매핑
        if (string.Equals(key, "XTRANS_TRANSLATOR", StringComparison.OrdinalIgnoreCase))
        {
            var v = _config["Translator:Mode"];
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }

        // 일반 키 조회: 섹션 경로("A:B:C") 또는 평문 키 모두 지원
        var val = _config[key];
        if (!string.IsNullOrWhiteSpace(val)) return val;

        return null;
    }
}

