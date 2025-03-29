using System;
using StackXML.Str;
using Xunit;

namespace StackXML.Tests
{
    public ref partial struct GeneratedClass
    {
        [StrField] public int m_int;
        [StrField] public double m_double;
        [StrField] public string m_string;
        [StrField] public SpanStr m_spanStr;
    }
    
    public static class StructuredStr
    {
        [Fact]
        public static void RoundTrip()
        {
            const char separator = '/';
            
            var input = new GeneratedClass
            {
                m_int = int.MaxValue,
                m_double = 3.14,
                m_string = "hello world",
                m_spanStr = new SpanStr("span string")
            };

            var writer = new StrWriter(separator);
            input.Serialize(ref writer);
            
            var builtString = writer.ToString();
            bool exceptionThrown = false;
            try
            {
                writer.PutRaw('\0');
                Assert.True(false); // lol
            } catch (ObjectDisposedException)
            {
                exceptionThrown = true;
                // good
            }
            Assert.True(exceptionThrown);
            var reader = new StrReader(builtString.AsSpan(), separator, StandardStrParser.s_instance);

            var output = new GeneratedClass();
            output.Deserialize(ref reader);
            
            Assert.Equal(input.m_int, output.m_int);
            Assert.Equal(input.m_double, output.m_double);
            Assert.Equal(input.m_string, output.m_string);
            Assert.Equal(input.m_spanStr.ToString(), output.m_spanStr.ToString());
        }
    }
}