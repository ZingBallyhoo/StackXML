using System;
using System.Collections.Generic;

namespace StackXML.Str
{
    public ref struct StrReader
    {
        private readonly ReadOnlySpan<char> m_str;
        private readonly IStrParser m_parser;
        private MemoryExtensions.SpanSplitEnumerator<char> m_enumerator;
        
        private bool m_moved;
        private bool m_moveSuccess;
        
        public StrReader(ReadOnlySpan<char> str, char separator, IStrParser parser)
        {
            m_str = str;
            m_parser = parser;
            m_enumerator = str.Split(separator);
        }
        
        public ReadOnlySpan<char> GetString()
        {
            if (!TryMove()) return default;
            return m_str[ConsumeRange()];
        }
        
        public SpanStr GetSpanString()
        {
            if (!TryMove()) return default;
            return new SpanStr(m_str[ConsumeRange()]);
        }
        
        public T Get<T>() where T : ISpanParsable<T>
        {
            return m_parser.Parse<T>(GetString());
        }

        public IReadOnlyList<string> ReadToEnd()
        {
            List<string> lst = new List<string>();
            while (HasRemaining())
            {
                var str = GetString();
                lst.Add(str.ToString());
            }
            return lst;
        }
        
        private Range ConsumeRange()
        {
            var result = m_enumerator.Current;
            m_moved = false;
            return result;
        }
        
        private bool TryMove()
        {
            if (m_moved) return m_moveSuccess;
            
            m_moveSuccess = m_enumerator.MoveNext();
            m_moved = true;
            return m_moveSuccess;
        }

        public bool HasRemaining()
        {
            return TryMove();
        }
    }
}