using StackXML.Str;
using Xunit;

namespace StackXML.Tests
{
    public class SpanStrTests
    {
        [Theory]
        [InlineData("a", 'a', true)]
        [InlineData("b", 'a', false)]
        [InlineData("bbbbbbbbbbbbbbbbbbbbbbbbba", 'a', true)]
        [InlineData("bbbbbbbbbbbbbabbbbbbbbbbbb", 'a', true)]
        public void Contains(string input, char c, bool expected)
        {
            var spanStr = new SpanStr(input);
            var result = spanStr.Contains(c);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("aa", "aa", true)]
        [InlineData("aa", "aab", false)]
        [InlineData("bb", "aa", false)]
        [InlineData("a", "a", true)]
        [InlineData("..,aabb1234", "..,aabb1234", true)]
        public void Equal(string input1, string input2, bool expected)
        {
            SpanStr spanStr1 = new SpanStr(input1);
            Assert.Equal(spanStr1 == input2, expected);
        }
    }
}