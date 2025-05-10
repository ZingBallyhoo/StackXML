using System;

namespace StackXML.Str
{
    public static class StrClassExtensions
    {
        public static string AsString<T>(this T obj, char separator, IStrFormatter? formatter=null) where T : IStrClass, allows ref struct
        {
            var writer = new StrWriter(separator, formatter);
            try
            {
                obj.Serialize(ref writer);
                return writer.AsSpan().ToString();
            } finally
            {
                writer.Dispose();
            }
        }
        
        public static bool TryFormat<T>(this T obj, Span<char> destination, out int charsWritten, char separator, IStrFormatter? formatter=null) where T : IStrClass, allows ref struct
        {
            var writer = new StrWriter(separator, formatter);
            try
            {
                obj.Serialize(ref writer);
                var finishedSpan = writer.AsSpan();
                charsWritten = finishedSpan.Length;
                return finishedSpan.TryCopyTo(destination);
            } finally
            {
                writer.Dispose();
            }
        }
        
        public static void FullyDeserialize<T>(this T obj, ref StrReader reader) where T : class, IStrClass
        {
            obj.Deserialize(ref reader);
            if (reader.HasRemaining())
            {
                throw new Exception("DeserializeFinal: had trailing data");
            }
        }
        
        public static void FullyDeserialize<T>(ref this T obj, ref StrReader reader) where T : struct, IStrClass, allows ref struct
        {
            obj.Deserialize(ref reader);
            if (reader.HasRemaining())
            {
                throw new Exception("DeserializeFinal: had trailing data");
            }
        }
    }
}