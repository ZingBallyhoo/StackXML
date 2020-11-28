using System;
using System.Security;
using System.Text;
using System.Xml.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace StackXML.Benchmark
{
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    public class XmlEncodeBenchmark
    {
        private const string s_strToEncode = "<test \'attribute\'=\'value\'/>";
        private const string s_worstCaseStr = "<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<";
        private const string s_bestCaseStr =  "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static string EncodeUsingWriteBuffer(string toEncode)
        {
            using var writeBuffer = XmlWriteBuffer.Create();
            writeBuffer.EncodeText(toEncode);
            return writeBuffer.ToStr();
        }

        [BenchmarkCategory("WriteBuffer"), Benchmark(Baseline = true)]
        public string WriteBuffer_BestCaseBaseline()
        {
            using var writeBuffer = XmlWriteBuffer.Create();
            writeBuffer.PutString(s_bestCaseStr);
            return writeBuffer.ToStr();
        }

        [BenchmarkCategory("WriteBuffer"), Benchmark] 
        public string WriteBuffer() => EncodeUsingWriteBuffer(s_strToEncode);
        [BenchmarkCategory("WriteBuffer"), Benchmark] 
        public string WriteBuffer_WorstCase() => EncodeUsingWriteBuffer(s_worstCaseStr);
        [BenchmarkCategory("WriteBuffer"), Benchmark] 
        public string WriteBuffer_BestCase() => EncodeUsingWriteBuffer(s_bestCaseStr);
        
        [BenchmarkCategory("SecurityElement"), Benchmark]
        public string SecurityElement_() => SecurityElement.Escape(s_strToEncode);
        [BenchmarkCategory("SecurityElement"), Benchmark]
        public string SecurityElement_BestCase() => SecurityElement.Escape(s_bestCaseStr);
        [BenchmarkCategory("SecurityElement"), Benchmark]
        public string SecurityElement_WorstCase() => SecurityElement.Escape(s_worstCaseStr);

        [BenchmarkCategory("XElement"), Benchmark]
        public string XElement()
        {
            // ReSharper disable once PossibleNullReferenceException
            return new XElement("t", s_strToEncode).LastNode.ToString();
        }
        
        [BenchmarkCategory("XText"), Benchmark]
        public string XText()
        {
            return new XText(s_strToEncode).ToString();
        }
        
        [BenchmarkCategory("XmlWriter"), Benchmark]
        public string XmlWriter()
        {
            var settings = new System.Xml.XmlWriterSettings 
            {
                ConformanceLevel = System.Xml.ConformanceLevel.Fragment
            };
            var builder = new StringBuilder();

            using var writer = System.Xml.XmlWriter.Create(builder, settings);
            writer.WriteString(s_strToEncode);

            return builder.ToString();
        }
        
        [BenchmarkCategory("Westwind"), Benchmark]
        public string Westwind()
        {
            // ReSharper disable once PossibleNullReferenceException
            return XmlString(s_strToEncode, false);
        }
        
        // https://weblog.west-wind.com/posts/2018/Nov/30/Returning-an-XML-Encoded-String-in-NET
        // https://github.com/RickStrahl/Westwind.Utilities/blob/master/Westwind.Utilities/Utilities/XmlUtils.cs#L66
        /*
        MIT License
        ===========
        
        Copyright (c) 2012-2020 West Wind Technologies
        
        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:
        
        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.
        
        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        */
        public static string XmlString(string text, bool isAttribute = false)
        {
            var sb = new StringBuilder(text.Length);

            foreach (var chr in text)
            {
                if (chr == '<')
                    sb.Append("&lt;");
                else if (chr == '>')
                    sb.Append("&gt;");
                else if (chr == '&')
                    sb.Append("&amp;");

                // special handling for quotes
                else if (isAttribute && chr == '\"')
                    sb.Append("&quot;");
                else if (isAttribute && chr == '\'')
                    sb.Append("&apos;");

                // Legal sub-chr32 characters
                else if (chr == '\n')
                    sb.Append(isAttribute ? "&#xA;" : "\n");
                else if (chr == '\r')
                    sb.Append(isAttribute ? "&#xD;" : "\r");
                else if (chr == '\t')
                    sb.Append(isAttribute ? "&#x9;" : "\t");

                else
                {
                    if (chr < 32)
                        throw new InvalidOperationException("Invalid character in Xml String. Chr " +
                                                            Convert.ToInt16(chr) + " is illegal.");
                    sb.Append(chr);
                }
            }

            return sb.ToString();
        }
    }
}