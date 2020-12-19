using System;
using System.ComponentModel;

namespace StackXML.Str
{
    public readonly ref struct SpanStr
    {
        private readonly ReadOnlySpan<char> m_data;
        private readonly string m_str;
        
        public int Length => m_str != null ? m_str.Length : m_data.Length;

        public SpanStr(ReadOnlySpan<char> data)
        {
            m_data = data;
            m_str = null;
        }
        
        public SpanStr(string str)
        {
            m_str = str;
            m_data = default;
        }

        public bool Contains(char c)
        {
            return ((ReadOnlySpan<char>)this).IndexOf(c) != -1;
        }
        
        public static bool operator ==(SpanStr left, SpanStr right)
        {
            return ((ReadOnlySpan<char>)left).SequenceEqual(right); // turn both into spans
        }
        public static bool operator !=(SpanStr left, SpanStr right) => !(left == right);
        
        public static bool operator ==(SpanStr left, string right)
        {
            return ((ReadOnlySpan<char>)left).SequenceEqual(right);
        }
        public static bool operator !=(SpanStr left, string right) => !(left == right);

        public readonly char this[int index] => ((ReadOnlySpan<char>)this)[index];

        public static implicit operator ReadOnlySpan<char>(SpanStr str)
        {
            if (str.m_str != null) return str.m_str.AsSpan();
            return str.m_data;
        }
        
        public static explicit operator string(SpanStr str)  // explicit to prevent accidental allocs
        {
            return str.ToString();
        }

        public override string ToString()
        {
            if (m_str != null) return m_str; 
            return m_data.ToString();
        }
        
        /// <summary>
        /// This method is not supported as spans cannot be boxed. To compare two spans, use operator==.
        /// <exception cref="System.NotSupportedException">
        /// Always thrown by this method.
        /// </exception>
        /// </summary>
        [Obsolete("Equals() on ReadOnlySpan will always throw an exception. Use == instead.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj) => throw new NotSupportedException();

        /// <summary>
        /// This method is not supported as spans cannot be boxed.
        /// <exception cref="System.NotSupportedException">
        /// Always thrown by this method.
        /// </exception>
        /// </summary>
        [Obsolete("GetHashCode() on ReadOnlySpan will always throw an exception.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() => throw new NotSupportedException();
    }
}