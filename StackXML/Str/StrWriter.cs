using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;

namespace StackXML.Str
{
    public ref struct StrWriter
    {
        public readonly Span<char> m_buffer;
        public readonly char m_separator;
        public bool m_separatorAtEnd;

        private char[] m_backing;
        private int m_currIdx;
        private bool m_isFirst;
        
        public ReadOnlySpan<char> m_builtSpan => m_buffer.Slice(0, m_currIdx);
        
        public static int s_maxSize = 256;

        public StrWriter(char separator)
        {
            m_backing = ArrayPool<char>.Shared.Rent(s_maxSize);
            m_buffer = new Span<char>(m_backing);
            
            m_currIdx = 0;
            m_isFirst = true;
            
            m_separator = separator;
            m_separatorAtEnd = false;
        }

        private void AssertWriteable()
        {
            if (m_backing == null) throw new ObjectDisposedException("StrWriter");
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
        
        public void PutDouble(double val)
        {
            PutSeparator();
            // ReSharper disable once RedundantAssignment
            var success = val.TryFormat(m_buffer.Slice(m_currIdx), out var written, default, CultureInfo.InvariantCulture);
            Debug.Assert(success);
            m_currIdx += written;
        }
        
        public void PutInt(int val)
        {
            PutSeparator();
            // ReSharper disable once RedundantAssignment
            var success = val.TryFormat(m_buffer.Slice(m_currIdx), out var written, default, CultureInfo.InvariantCulture);
            Debug.Assert(success);
            m_currIdx += written;
        }
        
        public void PutRaw(char c)
        {
            AssertWriteable();
            m_buffer[m_currIdx++] = c;
        }
        
        public void PutRaw(ReadOnlySpan<char> str)
        {
            AssertWriteable();
            str.CopyTo(m_buffer.Slice(m_currIdx));
            m_currIdx += str.Length;
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
            if (m_backing != null)
            {
                ArrayPool<char>.Shared.Return(m_backing);
                m_backing = null;
            }
        }
    }
}