using System;
using System.IO;
using System.Xml;
using FluentAssertions;
using XtrXmlTranslator.Core.Xml;
using Xunit;

public class XmlXxeTests
{
    [Fact]
    public void CreateSafeReaderSettings_Prohibits_DTD()
    {
        // External entity in DTD should be prohibited by settings
        var xml = "<!DOCTYPE foo [ <!ENTITY xxe SYSTEM 'file:///etc/passwd'> ]><root>&xxe;</root>";
        var settings = XmlStreamReader.CreateSafeReaderSettings();
        using var sr = new StringReader(xml);
        using var xr = XmlReader.Create(sr, settings);

        Action act = () =>
        {
            // Force read to try hitting the DTD/entity
            while (xr.Read()) { }
        };

        act.Should().Throw<XmlException>();
    }
}
