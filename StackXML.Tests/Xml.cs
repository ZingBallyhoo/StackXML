using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace StackXML.Tests
{
    [XmlCls("emptyClass")]
    public partial class EmptyClass
    {
    }
    
    [XmlCls(c_longName)]
    public partial class VeryLongName
    {
        public const string c_longName =
            "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";
        // length = 1025 (buffer len + 1)
    }

    [XmlCls("a")]
    public partial class StackSlam
    {
        [XmlBody("a")] public List<StackSlam> m_child;
    }

    [XmlCls("attrs")]
    public ref partial struct WithAttributes
    {
        [XmlField("int")] public int m_int;
        [XmlField("uint")] public uint m_uint;
        [XmlField("double")] public double m_double;
        [XmlField("bool")] public bool m_bool;
        [XmlField("byte")] public byte m_byte;
        [XmlField("string")] public ReadOnlySpan<char> m_string;
    }
    
    [XmlCls("stringbodies")]
    public partial class StringBodies
    {
        [XmlBody("a")] public string m_a;
        [XmlBody("b")] public string m_b;
        [XmlBody("null")] public string m_null;
        [XmlBody("empty")] public string m_empty;
    }
    
    [XmlCls("stringbody")]
    public partial class StringBody
    {
        [XmlBody] public string m_fullBody;
    }
    
    [XmlCls("stringbodyarray")]
    public partial class FullBodyArray
    {
        [XmlBody] public List<StringBody> m_bodies;
    }
    
    [XmlCls("primitivebodies")]
    public partial class PrimitiveBodies
    {
        [XmlBody("int")] public int m_int;
        [XmlBody("float")] public int m_float;
        [XmlBody("decimal")] public Decimal m_decimal;
    }
    
    public class TestCrashException : Exception
    {
    }

    [XmlCls("abort")]
    public partial class AbortClassBase
    {
        [XmlBody("abortIfPresent")] public string m_abortIfPresent;
        [XmlBody("crashIfPresent")] public string m_crashIfPresent;
    }

    public class AbortClass : AbortClassBase
    {
        public override bool ParseSubBody(ref XmlReadBuffer buffer, ReadOnlySpan<char> name, ReadOnlySpan<char> bodySpan, ReadOnlySpan<char> innerBodySpan,
            ref int end, ref int endInner)
        {
            switch (name)
            {
                case "abortIfPresent":
                {
                    buffer.m_abort = true;
                    return false;
                }
                case "crashIfPresent":
                {
                    throw new TestCrashException();
                }
                default:
                {
                    return base.ParseSubBody(ref buffer, name, bodySpan, innerBodySpan, ref end, ref endInner);
                }
            }
        }
    }
    
    public static class Xml
    {
        
        [Fact]
        public static void SerializeNull()
        {
            Assert.Throws<ArgumentNullException>(() => XmlWriteBuffer.SerializeStatic<IXmlSerializable>(null));
        }
        
        [Fact]
        public static void SerializeEmpty()
        {
            var empty = new EmptyClass();
            var result = XmlWriteBuffer.SerializeStatic(empty);
            Assert.Equal("<emptyClass/>", result);
        }
        
        [Fact]
        public static void SerializeLongName()
        {
            var longName = new VeryLongName();
            var result = XmlWriteBuffer.SerializeStatic(longName);
            Assert.Equal($"<{VeryLongName.c_longName}/>", result);
        }
        
        [Fact]
        public static void SerializeAttributes()
        {
            var truth = new WithAttributes
            {
                m_int = -1,
                m_uint = uint.MaxValue,
                m_double = 3.14,
                m_string = "david and tim<",
                m_bool = true,
                m_byte = 128
            };

            const string expected =
                "<attrs int='-1' uint='4294967295' double='3.14' bool='1' byte='128' string='david and tim&lt;'/>";
            const string expectedCompatible =
                "<attrs int='-1'uint='4294967295'double='3.14'bool='1'byte='128'string='david and tim&lt;'/>";
            const string expectedWithDecl =
                "<?xml version='1.0'?><attrs int='-1' uint='4294967295' double='3.14' bool='1' byte='128' string='david and tim&lt;'/>";
            const string expectedWithComment =
                "<?xml version='1.0'?><!--<attrs wont be parsed in comment>--><attrs int='-1' uint='4294967295' double='3.14' bool='1' byte='128' string='david and tim&lt;'/>";
            
            var result = XmlWriteBuffer.SerializeStatic(truth);
            Assert.Equal(expected, result);

            AssertEqualWithAttrs(expected, truth);
            AssertEqualWithAttrs(expectedCompatible, truth);
            AssertEqualWithAttrs(expectedWithDecl, truth);
            AssertEqualWithAttrs(expectedWithComment, truth);
        }

        private static void AssertEqualWithAttrs(string serialiezd, WithAttributes truth)
        {
            var deserialized = XmlReadBuffer.ReadStatic<WithAttributes>(serialiezd);
            Assert.Equal(truth.m_int, deserialized.m_int);
            Assert.Equal(truth.m_uint, deserialized.m_uint);
            Assert.Equal(truth.m_double, deserialized.m_double);
            Assert.Equal(truth.m_string.ToString(), deserialized.m_string.ToString());
        }
        
        [Theory]
        [InlineData(CDataMode.Off)]
        [InlineData(CDataMode.On)]
        [InlineData(CDataMode.OnEncoded)]
        public static void SerializeStringBodies(CDataMode cdataMode)
        {
            var truth = new StringBodies
            {
               m_a = "blah1<>&&",
               m_b = "blah2",
               m_null = null,
               m_empty = string.Empty
            };
            var result = XmlWriteBuffer.SerializeStatic(truth, cdataMode);
            
            var deserialized = XmlReadBuffer.ReadStatic<StringBodies>(result, cdataMode);
            Assert.Equal(truth.m_a, deserialized.m_a);
            Assert.Equal(truth.m_b, deserialized.m_b);
            //Assert.Equal(truth.m_null, deserialized.m_null); // todo: do we want to avoid writing nulls?? currently empty string
            Assert.Equal(truth.m_empty, deserialized.m_empty);
        }
        
        [Theory]
        [InlineData(CDataMode.Off)]
        [InlineData(CDataMode.On)]
        [InlineData(CDataMode.OnEncoded)]
        public static void SerializeStringBody(CDataMode cdataMode)
        {
            var truth = new StringBody()
            {
                m_fullBody = "asdjhasjkdhakjsdhjkahsdjhkasdhasd<>&&"
            };
            var result = XmlWriteBuffer.SerializeStatic(truth, cdataMode);
            
            var deserialized = XmlReadBuffer.ReadStatic<StringBody>(result, cdataMode);
            Assert.Equal(truth.m_fullBody, deserialized.m_fullBody);
        }
        
        [Theory]
        [InlineData(CDataMode.Off)]
        [InlineData(CDataMode.On)]
        [InlineData(CDataMode.OnEncoded)]
        public static void SerializeStringBodyArray(CDataMode cdataMode)
        {
            var truthArray = new FullBodyArray
            {
                m_bodies = 
                [
                    // doesn't matter what the inner type is
                    // i'm just reusing
                    new StringBody() 
                    {
                        m_fullBody = "first" 
                    },
                    new StringBody
                    {
                        m_fullBody = "second"
                    }
                ]
            };
            
            var result = XmlWriteBuffer.SerializeStatic(truthArray, cdataMode);
            
            var deserialized = XmlReadBuffer.ReadStatic<FullBodyArray>(result, cdataMode);
            Assert.Equal(truthArray.m_bodies[0].m_fullBody, deserialized.m_bodies[0].m_fullBody);
            Assert.Equal(truthArray.m_bodies[1].m_fullBody, deserialized.m_bodies[1].m_fullBody);
        }
        
        [Fact]
        public static void SerializePrimitiveBodies()
        {
            var truthArray = new PrimitiveBodies()
            {
                m_int = 888,
                m_float = 999,
                m_decimal = 1111
            };
            
            var result = XmlWriteBuffer.SerializeStatic(truthArray, CDataMode.Off);
            var deserialized = XmlReadBuffer.ReadStatic<PrimitiveBodies>(result, CDataMode.Off);
            
            Assert.Equal(truthArray.m_int, deserialized.m_int);
            Assert.Equal(truthArray.m_float, deserialized.m_float);
            Assert.Equal(truthArray.m_decimal, deserialized.m_decimal);
        }

        [Fact]
        public static void HandleUnknownBodies()
        {
            const string input = "<dave><tim></tim></dave>";
            Assert.Throws<InvalidDataException>(() => XmlReadBuffer.ReadStatic<EmptyClass>(input));
            Assert.Throws<InvalidDataException>(() => XmlReadBuffer.ReadStatic<VeryLongName>(input));
            Assert.Throws<InvalidDataException>(() => XmlReadBuffer.ReadStatic<StackSlam>(input));
            Assert.Throws<InvalidDataException>(() => XmlReadBuffer.ReadStatic<WithAttributes>(input));
            Assert.Throws<InvalidDataException>(() => XmlReadBuffer.ReadStatic<StringBodies>(input));
        }
        
        [Fact]
        public static void HandleUnknownAttributes()
        {
            const string input = "<attrs newAttr='anything'/>";
            XmlReadBuffer.ReadStatic<WithAttributes>(input); // ...nothing happens
        }

        [Fact]
        public static void SlamStackSerialize()
        {
            var head = BuildStackSlammer(1000);
            var serialized = XmlWriteBuffer.SerializeStatic(head);
        }
        
        private static int GetDefaultMaxDepth() => new XmlReadParams().m_maxDepth;
        
        [Fact]
        public static void SlamStackDeserialize()
        {
            var head = BuildStackSlammer(GetDefaultMaxDepth()-1);
            var serialized = XmlWriteBuffer.SerializeStatic(head);
            var deserialized = XmlReadBuffer.ReadStatic<StackSlam>(serialized);
        }
        
        [Fact]
        public static void SlamStackDeserializeError()
        {
            var head = BuildStackSlammer(GetDefaultMaxDepth());
            var serialized = XmlWriteBuffer.SerializeStatic(head);
            Assert.Throws<Exception>(() => XmlReadBuffer.ReadStatic<StackSlam>(serialized));
        }
        
        private static StackSlam BuildStackSlammer(int count)
        {
            var current = new StackSlam();
            var head = current;
            for (int i = 0; i < count-1; i++)
            {
                var next = new StackSlam();
                current.m_child = new List<StackSlam>
                {
                    next
                };
                current = next;
            }
            return head;
        }

        [Fact]
        public static void TestAbortNoCrash()
        {
            var str = "<abort><abortIfPresent>gonna do this first</abortIfPresent><crashIfPresent>yep</crashIfPresent></abort>";
            var parsed = XmlReadBuffer.ReadStatic<AbortClass>(str); // should succeed
        }
        
        [Fact]
        public static void TestAbortCrash()
        {
            var str = "<abort><crashIfPresent>yep</crashIfPresent></abort>";
            Assert.Throws<TestCrashException>(() => XmlReadBuffer.ReadStatic<AbortClass>(str)); // should crash
        }
    }
}