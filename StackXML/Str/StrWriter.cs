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

        public bool m_finished;

        private char[] m_backing;
        private int m_currIdx;
        private bool m_isFirst;
        
        public SpanStr Built => new SpanStr(m_buffer.Slice(0, m_currIdx));
        
        public static int s_maxSize = 256;

        public StrWriter(char separator)
        {
            m_backing = ArrayPool<char>.Shared.Rent(s_maxSize);
            m_buffer = new Span<char>(m_backing);
            
            m_currIdx = 0;
            m_isFirst = true;
            m_finished = false;
            
            m_separator = separator;
            m_separatorAtEnd = false;
        }

        private void AssertWriteable()
        {
            if (m_finished) throw new ObjectDisposedException("StrWriter");
            if (m_buffer == null) throw new ObjectDisposedException("StrWriter");
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

        public void Finish()
        {
            if (m_finished) return;
            if (m_separatorAtEnd) PutRaw(m_separator);
        }
        
        public void FinishZeroTerminated()
        {
            if (m_finished) return;
            Finish();
            PutRaw('\0');
        }

        public SpanStr BuildToSpanStr()
        {
            AssertWriteable();

            Finish();
            return Built;
        }
        
        public string BuildToStr()
        {
            AssertWriteable();

            Finish();
            var str = Built.ToString();
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