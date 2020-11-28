using Xunit;

namespace StackXML.Tests
{
    public class XmlEncodingTests
    {
        // thx to https://www.liquid-technologies.com/XML/EscapingData.aspx
        
        [Theory]
        [InlineData("<hello>", "&lt;hello&gt;")]
        [InlineData("if (age < 5)", "if (age &lt; 5)")]
        [InlineData("if (age > 5)", "if (age &gt; 5)")]
        [InlineData("if (age > 3 && age < 8)", "if (age &gt; 3 &amp;&amp; age &lt; 8)")]
        [InlineData("She said \"You're right\"", "She said \"You're right\"")]
        public void Encode(string input, string expected)
        {
            var encoded = EncodeStr(input, false);
            Assert.Equal(expected, encoded);
        }
        
        [Theory]
        [InlineData("He said \"OK\"", "He said &quot;OK&quot;")]
        [InlineData("She said \"You're right\"", "She said &quot;You&apos;re right&quot;")]
        [InlineData("Smith&Sons", "Smith&amp;Sons")]
        [InlineData("a>b", "a&gt;b")]
        [InlineData("a<b", "a&lt;b")]
        public void EncodeAttribute(string input, string expected)
        {
            var encoded = EncodeStr(input, true);
            Assert.Equal(expected, encoded);
        }

        private static string EncodeStr(string input, bool attribute)
        {
            using var buffer = XmlWriteBuffer.Create();
            buffer.EncodeText(input, attribute);
            return buffer.ToStr();
        }
    }
}