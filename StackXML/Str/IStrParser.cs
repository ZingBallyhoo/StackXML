using System;

namespace StackXML.Str
{
    public interface IStrParser
    {
        T Parse<T>(ReadOnlySpan<char> span) where T : ISpanParsable<T>;
    }
}