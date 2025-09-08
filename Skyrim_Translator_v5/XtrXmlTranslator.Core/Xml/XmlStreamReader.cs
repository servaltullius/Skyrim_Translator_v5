using System.Xml;

namespace XtrXmlTranslator.Core.Xml
{
    /// <summary>
    /// XmlReader 보안/보존 설정 헬퍼 및 텍스트 스트리밍 유틸리티.
    /// </summary>
    public sealed class XmlStreamReader
    {
        public XmlReaderSettings CreateDefaultSettings()
        {
            return new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreProcessingInstructions = false,
                IgnoreWhitespace = false,
                XmlResolver = null
            };
        }

        public static XmlReaderSettings CreateSafeReaderSettings()
            => new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, // XXE 방어
                XmlResolver = null,
                IgnoreWhitespace = false,               // xml:space 의미 보존
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,
                CloseInput = false,
                CheckCharacters = true
            };

        /// <summary>
        /// 특정 요소 이름(로컬명)에 들어있는 텍스트 노드를 스트리밍으로 열거합니다.
        /// 주의: 이 메서드는 필터가 true인 요소에 대해 ReadElementContentAsString을 사용하므로
        /// 중첩 요소까지 함께 소비합니다. 루트 수준에서 필터를 true로 주면 전체가 한 건으로 수집될 수 있습니다.
        /// </summary>
        public static IEnumerable<(string Path, string Text)> EnumerateTexts(
            XmlReader reader,
            Func<string, bool> elementFilter)
        {
            var stack = new Stack<string>();
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    stack.Push(reader.LocalName);

                    if (elementFilter(reader.LocalName))
                    {
                        string content = reader.ReadElementContentAsString();
                        yield return (string.Join("/", stack.Reverse()), content);
                        stack.Pop(); // EndElement까지 소비됨
                    }
                    else if (reader.IsEmptyElement)
                    {
                        stack.Pop();
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement && stack.Count > 0)
                {
                    stack.Pop();
                }
            }
        }

        /// <summary>
        /// '리프(leaf) 요소'의 텍스트만 안전하게 스트리밍으로 열거합니다.
        /// - 하위에 다른 요소(Element)가 없는 요소만 대상으로 하며, 텍스트/CDATA만 포함된 경우에 Path와 Text를 반환합니다.
        /// - 필터(elementFilter)로 요소명을 선택할 수 있습니다. (ex: _ => true 로 전체 리프 수집)
        /// - 내부 구현은 Read/EndElement 기반이므로 상위 요소를 통째로 소비하지 않습니다.
        /// </summary>
        public static IEnumerable<(string Path, string Text)> EnumerateLeafTexts(
            XmlReader reader,
            Func<string, bool> elementFilter)
        {
            var nameStack = new Stack<string>();
            var hasChildElemStack = new Stack<bool>();
            var textStack = new Stack<System.Text.StringBuilder>();

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        // 상위 요소는 자식 요소를 가진 것으로 표시
                        if (hasChildElemStack.Count > 0)
                        {
                            var top = hasChildElemStack.Pop();
                            hasChildElemStack.Push(true);
                        }

                        nameStack.Push(reader.LocalName);
                        hasChildElemStack.Push(false);
                        textStack.Push(new System.Text.StringBuilder());

                        if (reader.IsEmptyElement)
                        {
                            var elemName = nameStack.Pop();
                            var hadChild = hasChildElemStack.Pop(); // 항상 false
                            var txt = textStack.Pop().ToString();

                            if (!hadChild && elementFilter(elemName))
                            {
                                var path = string.Join("/", nameStack.Reverse().Concat(new[] { elemName }));
                                yield return (path, txt);
                            }
                        }
                        break;

                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                        if (textStack.Count > 0)
                            textStack.Peek().Append(reader.Value);
                        break;

                    case XmlNodeType.EndElement:
                        if (nameStack.Count > 0)
                        {
                            var elemName = nameStack.Pop();
                            var hadChild = hasChildElemStack.Pop();
                            var txt = textStack.Pop().ToString();

                            if (!hadChild && elementFilter(elemName))
                            {
                                var path = string.Join("/", nameStack.Reverse().Concat(new[] { elemName }));
                                yield return (path, txt);
                            }
                        }
                        break;
                }
            }
        }
    }
}
