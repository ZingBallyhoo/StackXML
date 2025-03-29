using System;
using System.Globalization;

namespace StackXML.Str
{
    public class BaseStrParser : IStrParser
    {
        public static BaseStrParser s_instance = new BaseStrParser();

        public virtual T Parse<T>(ReadOnlySpan<char> span) where T : ISpanParsable<T>
        {
            return T.Parse(span, CultureInfo.InvariantCulture);
        }
    }
}