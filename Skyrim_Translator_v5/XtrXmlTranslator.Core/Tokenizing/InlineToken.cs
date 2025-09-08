namespace XtrXmlTranslator.Core.Tokenizing;

public enum InlineTokenType
{
    Text,
    XmlLikeTag,            // <b>...</b>, <Alias=...> 등
    PercentPlaceholder,    // %.0f, %d 등 CK Message 포맷
    BracePlaceholder,      // {0}, {Name}
    PercentBracePlaceholder // %{username}
}

public readonly record struct InlineToken(InlineTokenType Type, string Value);

