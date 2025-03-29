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
        
        public virtual bool ParseSubBody(ref XmlReadBuffer buffer, ReadOnlySpan<char> name, 
            ReadOnlySpan<char> bodySpan, ReadOnlySpan<char> innerBodySpan, 
            ref int end, ref int endInner)
        {
            return false;
        }
        
        public virtual bool ParseAttribute(ref XmlReadBuffer buffer, ReadOnlySpan<char> name, ReadOnlySpan<char> value)
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
    }
}