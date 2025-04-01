using System;

namespace StackXML
{
    
    [AttributeUsage(AttributeTargets.Field)]
    public class XmlFieldAttribute : Attribute
    {
        public readonly string m_name;
        
        public XmlFieldAttribute(string name)
        {
            m_name = name;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class XmlBodyAttribute : Attribute
    {
        public readonly string? m_name;
        
        public XmlBodyAttribute(string? name=null)
        {
            m_name = name;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class XmlClsAttribute : Attribute
    {
        public readonly string m_name;
        
        public XmlClsAttribute(string name)
        {
            m_name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class XmlSplitStrAttribute : Attribute
    {
        public readonly char m_splitOn;
        
        public XmlSplitStrAttribute(char splitOn=',')
        {
            m_splitOn = splitOn;
        }
    }
}