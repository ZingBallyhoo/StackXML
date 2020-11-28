using System;
using BenchmarkDotNet.Running;

namespace StackXML.Benchmark
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            //var test = new XmlEncodeBenchmark();
            //test.WriteBuffer();
            //test.WriteBuffer_BestCase();
            //test.WriteBuffer_WorstCase();
            //test.WriteBuffer_BestCaseBaseline();
            //return;
            
            var test2 = new SimpleLoadBenchmark();
            var a = test2.ReadBuffer();
            var b = test2.XmlDocument();
            var c = test2.XmlDocument();
            var d = test2.XmlSerializer();
            var e = test2.XmlReader_();
            if (a != b || b != c || c != d || d != e) throw new Exception();

            //BenchmarkRunner.Run<XmlEncodeBenchmark>();
            BenchmarkRunner.Run<SimpleLoadBenchmark>();
        }
    }
}