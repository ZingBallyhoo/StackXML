using System;
using CommunityToolkit.HighPerformance.Buffers;

namespace StackXML.Str
{
    public ref struct StrWriter
    {
        private ArrayPoolBufferWriter<char> m_writer;
        private readonly IStrFormatter m_formatter;
        
        public readonly char m_separator;
        public bool m_separatorAtEnd;

        private bool m_isFirst;
        
        public ReadOnlySpan<char> m_builtSpan => m_writer.WrittenSpan;

        public StrWriter(char separator, IStrFormatter? formatter=null)
        {
            m_writer = new ArrayPoolBufferWriter<char>(256);
            m_formatter = formatter ?? BaseStrFormatter.s_instance;
            
            m_separator = separator;
            m_separatorAtEnd = false;
            
            m_isFirst = true;
        }
        
        private void Resize()
        {
            m_writer.GetSpan(m_writer.Capacity*2);
        }

        private void AssertWriteable()
        {
            if (m_writer == null) throw new ObjectDisposedException("StrWriter");
        }

        private void PutSeparator()
        {
            if (m_isFirst) m_isFirst = false;
            else PutRaw(m_separator);
        }
        
        public void PutString(ReadOnlySpan<char> str)
        {
            PutSeparator();
            PutRaw(str);
        }
        
        public void Put(ReadOnlySpan<char> str)
        {
            PutString(str);
        }
        
        public void Put<T>(T value) where T : ISpanFormattable
        {
            PutSeparator();
            int charsWritten;
            while (!m_formatter.TryFormat(m_writer.GetSpan(), value, out charsWritten))
            {
                Resize();
            }
            m_writer.Advance(charsWritten);
        }
        
        public void PutRaw(char c)
        {
            AssertWriteable();
            m_writer.GetSpan(1)[0] = c;
            m_writer.Advance(1);
        }
        
        public void PutRaw(ReadOnlySpan<char> str)
        {
            AssertWriteable();
            str.CopyTo(m_writer.GetSpan(str.Length));
            m_writer.Advance(str.Length);
        }

        public void Finish(bool terminate)
        {
            if (m_separatorAtEnd) PutRaw(m_separator);
            if (terminate) PutRaw('\0');
        }

        public ReadOnlySpan<char> AsSpan(bool terminate=false)
        {
            AssertWriteable();
            
            Finish(terminate);
            return m_builtSpan;
        }
        
        public override string ToString()
        {
            AssertWriteable();

            Finish(false);
            var str = m_builtSpan.ToString();
            Dispose();
            return str;
        }
        
        
        public void Dispose()
        {
            m_writer?.Dispose();
            m_writer = null;
        }
    }
}