using System;
using StackXML.Str;

namespace StackXML
{
    /// <summary>
    /// Abstract base class for types that can be read from and written to XML using <see cref="XmlReadBuffer"/> and <see cref="XmlWriteBuffer"/>
    /// </summary>
    public abstract class IXmlSerializable // todo: not actually an interface
    {
        /// <summary>Gets the name of the node to be written</summary>
        /// <returns>Name of the node to be written</returns>
        public abstract ReadOnlySpan<char> GetNodeName();

        public virtual bool ParseFullBody(ref XmlReadBuffer buffer, ReadOnlySpan<char> bodySpan, ref int end)
        {
            return false;
        }
        
        public virtual bool ParseSubBody(ref XmlReadBuffer buffer, ulong hash, 
            ReadOnlySpan<char> bodySpan, ReadOnlySpan<char> innerBodySpan, 
            ref int end, ref int endInner)
        {
            return false;
        }
        
        public virtual bool ParseAttribute(ref XmlReadBuffer buffer, ulong hash, SpanStr value)
        {
            return false;
        }

        public virtual void SerializeBody(ref XmlWriteBuffer buffer)
        {
        }

        public virtual void SerializeAttributes(ref XmlWriteBuffer buffer)
        {
        }

        public void Serialize(ref XmlWriteBuffer buffer)
        {
            var node = buffer.StartNodeHead(GetNodeName());
            SerializeAttributes(ref buffer);
            SerializeBody(ref buffer);
            buffer.EndNode(ref node);
        }

        /// <summary>Calculate fast hash of attribute/node name</summary>
        /// <param name="name">Name to hash</param>
        /// <returns>Hashed value</returns>
        public static ulong HashName(ReadOnlySpan<char> name)
        {
            var hashedValue = 0x2AAAAAAAAAAAAB67ul;
            for(var i = 0; i < name.Length; i++)
            {
                hashedValue += name[i];
                hashedValue *= 0x2AAAAAAAAAAAAB6Ful;
            }
            return hashedValue;
        }
    }
}