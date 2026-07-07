using System.Text;

namespace SummitAdapter.Soap;

/// <summary>
/// A <see cref="StringWriter"/> that reports UTF-8 so <c>XmlWriter</c> emits
/// <c>encoding="utf-8"</c> in the XML declaration (the default StringWriter reports UTF-16).
/// </summary>
internal sealed class Utf8StringWriter : StringWriter
{
    public override Encoding Encoding => Encoding.UTF8;
}
