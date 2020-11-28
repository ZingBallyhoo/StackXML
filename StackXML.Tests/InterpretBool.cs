using System;
using System.IO;
using StackXML.Str;
using Xunit;

namespace StackXML.Tests
{
    public static class InterpretBool
    {
        [Theory]
        [InlineData("TRUE", true)]
        [InlineData("True", true)]
        [InlineData("truE", true)]
        [InlineData("FALSE", false)]
        [InlineData("False", false)]
        [InlineData("falsE", false)]
        [InlineData("0", false)]
        [InlineData("1", true)]
        [InlineData("01", false)] // todo: should these
        [InlineData("10", true)] // todo: should these
        [InlineData("yes", true)]
        [InlineData("no", false)]
        public static void Interpret(string str, bool expected)
        {
            var actual = StrReader.InterpretBool(str.AsSpan());
            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void InterpretError()
        {
            Assert.Throws<InvalidDataException>(() => StrReader.InterpretBool("yep"));
        }
    }
}