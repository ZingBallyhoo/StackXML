using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace StackXML.Benchmark
{
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    [RPlotExporter]
    [BenchmarkCategory(nameof(SimpleLoadBenchmark))]
    public partial class SimpleLoadBenchmark
    {
        private const string s_strToDecode = "<test attribute=\'value\'/>";

        // XmlType and XmlAttribute from System.Xml.Serialization
        [XmlCls("test"), XmlType("test")]
        public partial class StructuredClass
        {
            [XmlField("attribute"), XmlAttribute("attribute")] public string m_attribute;
        }

        [Benchmark(Baseline=true)]
        public string ReadBuffer()
        {
            var parsed = XmlReadBuffer.ReadStatic<StructuredClass>(s_strToDecode);
            return parsed.m_attribute;
        }

        [Benchmark]
        public string XmlSerializer()
        {
            using var stringReader = new StringReader(s_strToDecode);
            using var xmlReader = XmlReader.Create(stringReader);
            var serializer = new XmlSerializer(typeof(StructuredClass));
            var parsed = (StructuredClass)serializer.Deserialize(xmlReader);
            return parsed.m_attribute;
        }
        
        [Benchmark]
        public string XmlReader_()
        {
            using var stringReader = new StringReader(s_strToDecode);
            using var xmlReader = XmlReader.Create(stringReader);
            while (xmlReader.Read())
            {
                if (!xmlReader.IsStartElement()) continue;
                if (xmlReader.Name != "test") continue;
                return xmlReader.GetAttribute("attribute");
            }
            throw new Exception();
        }

        [Benchmark]
        public string XmlDocument()
        {
            var xdoc = new XmlDocument();
            xdoc.LoadXml(s_strToDecode);
            var node = xdoc.FirstChild;
            return node.Attributes["attribute"].Value;
        }
        
        [Benchmark]
        public string XDocument_()
        {
            var xdoc = XDocument.Parse(s_strToDecode);
            var node = xdoc.Root;
            return node.Attribute("attribute").Value;
        }
    }
}