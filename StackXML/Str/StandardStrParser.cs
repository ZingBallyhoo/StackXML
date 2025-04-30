using System;
using System.IO;

namespace StackXML.Str
{
    public class StandardStrParser : BaseStrParser
    {
        public static StandardStrParser s_instance = new StandardStrParser();
        
        public override T Parse<T>(ReadOnlySpan<char> span)
        {
            if (span.Length == 0 && typeof(T).IsPrimitive) return default; // todo: I had to handle this...
            
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)InterpretBool(span);
            }
            return base.Parse<T>(span);
        }

        public static bool InterpretBool(ReadOnlySpan<char> val)
        {
            if (val is "0") return false;
            if (val is "1") return true;
            
            if (val.Equals("no", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (val.Equals("yes", StringComparison.InvariantCultureIgnoreCase)) return true;

            if (val.Equals("false", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (val.Equals("true", StringComparison.InvariantCultureIgnoreCase)) return true;
            
            throw new InvalidDataException($"unknown boolean \"{val.ToString()}\"");
        }
    }
}