using System;
using System.ComponentModel;

namespace StackXML.Str
{
    public readonly ref struct SpanStr
    {
        public readonly ReadOnlySpan<char> m_data;

        public SpanStr(ReadOnlySpan<char> data)
        {
            m_data = data;
        }
        
        public SpanStr(string str)
        {
            m_data = str.AsSpan();
        }

        public int Length => m_data.Length;

        public bool Contains(char c)
        {
            //return m_data.Contains(c);
            return m_data.IndexOf(c) != -1;
        }

        public static bool operator ==(SpanStr left, SpanStr right)
        {
            return left.m_data.SequenceEqual(right);
        }

        public static bool operator !=(SpanStr left, SpanStr right) => !(left == right);

        public static bool operator ==(SpanStr left, string right)
        {
            return left.m_data.SequenceEqual(right);
        }

        public static bool operator !=(SpanStr left, string right) => !(left == right);

        public char this[int index] => m_data[index];

        public static implicit operator ReadOnlySpan<char>(SpanStr str)
        {
            return str.m_data;
        }
        
        public static explicit operator string(SpanStr str)  // explicit to prevent accidental allocs
        {
            return str.ToString();
        }

        public override string ToString()
        {
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