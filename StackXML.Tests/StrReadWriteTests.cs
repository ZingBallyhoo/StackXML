using System;
using System.Linq;
using StackXML.Str;
using Xunit;

namespace StackXML.Tests
{
    public static class StrReadWriteTests
    {
        private const string c_target = "hello world 5.6 255";

        [Fact]
        public static void Write()
        {
            using var writer = new StrWriter(' ');
            writer.PutString("hello");
            writer.PutString("world");
            writer.Put(5.6);
            writer.Put(255);

            var built = writer.ToString();
            
            Assert.Equal(c_target, built);
        }
        
        [Fact]
        public static void Read()
        {
            var reader = new StrReader(c_target.AsSpan(), ' ', StandardStrParser.s_instance);
            var hello = reader.GetString();
            var world = reader.GetString();
            var fivePointSix = reader.Get<double>();
            var ff = reader.Get<int>();
            
            Assert.Equal("hello", hello.ToString());
            Assert.Equal("world", world.ToString());
            Assert.Equal(5.6, fivePointSix);
            Assert.Equal(255, ff);
        }

        [Fact]
        public static void TestReadToEnd()
        {
            var actual = new[]
            {
                "1", "2", "3", "4", "5"
            };
            
            var input = "1,2,3,4,5";
            var reader = new StrReader(input.AsSpan(), ',', StandardStrParser.s_instance);

            var readToEnd = reader.ReadToEnd().ToArray();
            
            Assert.Equal(actual, readToEnd);
        }
    }
}