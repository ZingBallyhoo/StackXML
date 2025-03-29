using System;

namespace StackXML.Str
{
    public interface IStrFormatter
    {
        bool TryFormat<T>(Span<char> dest, T value, out int charsWritten) where T : ISpanFormattable;
    }
}