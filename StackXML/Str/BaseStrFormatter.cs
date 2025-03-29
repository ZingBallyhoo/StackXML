using System;
using System.Globalization;

namespace StackXML.Str
{
    public class BaseStrFormatter : IStrFormatter
    {
        public static BaseStrFormatter s_instance = new BaseStrFormatter();

        public virtual bool TryFormat<T>(Span<char> dest, T value, out int charsWritten) where T : ISpanFormattable
        {
            return value.TryFormat(dest, out charsWritten, "", CultureInfo.InvariantCulture);
        }
    }
}