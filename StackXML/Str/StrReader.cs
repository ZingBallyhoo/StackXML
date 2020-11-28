using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace StackXML.Str
{
    public ref struct StrReader
    {
        private ReadOnlySpan<char> m_str;
        private SpanSplitEnumerator<char> m_enumerator;
        
        public StrReader(ReadOnlySpan<char> str, char separator) : this(str, new SpanSplitEnumerator<char>(str, separator))
        {
        }

        public StrReader(ReadOnlySpan<char> str, SpanSplitEnumerator<char> enumerator)
        {
            m_str = str;
            m_enumerator = enumerator;
        }
        
        public SpanStr GetString()
        {
            if (!m_enumerator.MoveNext()) return default;
            return new SpanStr(m_str[m_enumerator.Current]);
        }

        public int GetInt()
        {
            var str = GetString();
            return ParseInt(str);
        }
        
        public double GetDouble()
        {
            // todo: loses some precision??
            // actionscipt Number is a double
            // "-13.255656138062477" -> -13.2556561380625
            
            var str = GetString();
            return ParseDouble(str);
        }

        public IReadOnlyList<string> ReadToEnd()
        {
            List<string> lst = new List<string>();
            while (HasRemaining())
            {
                var str = GetString();
                lst.Add((string)str);
            }
            return lst;
        }

        public bool HasRemaining()
        {
            return m_enumerator.CanMoveNext();
        }

        public static bool InterpretBool(ReadOnlySpan<char> val)
        {
            if (val[0] == '0') return false;
            if (val[0] == '1') return true;
            
            if (val.StartsWith("no", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (val.StartsWith("yes", StringComparison.InvariantCultureIgnoreCase)) return true;

            if (val.StartsWith("false", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (val.StartsWith("true", StringComparison.InvariantCultureIgnoreCase)) return true;
            
            throw new InvalidDataException($"unknown boolean \"{val.ToString()}\"");
        }
        
        public static int ParseInt(ReadOnlySpan<char> span)
        {
            if (span.Length == 0) return default; // todo: I had to handle this...
            return int.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
        
        public static uint ParseUInt(ReadOnlySpan<char> span)
        {
            return uint.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
        
        public static byte ParseByte(ReadOnlySpan<char> span)
        {
            if (span.Length == 0) return default; // todo: I had to handle this...
            return byte.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
        
        public static double ParseDouble(ReadOnlySpan<char> span)
        {
            return double.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}