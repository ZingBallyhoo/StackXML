using System;

namespace StackXML
{
    
    [AttributeUsage(AttributeTargets.Field)]
    public class XmlField : Attribute
    {
        public readonly string m_name;
        
        public XmlField(string name)
        {
            m_name = name;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class XmlBody : Attribute
    {
        public readonly string? m_name;
        
        public XmlBody(string? name=null)
        {
            m_name = name;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public class XmlCls : Attribute
    {
        public readonly string m_name;
        
        public XmlCls(string name)
        {
            m_name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class XmlSplitStr : Attribute
    {
        public readonly char m_splitOn;
        
        public XmlSplitStr(char splitOn=',')
        {
            m_splitOn = splitOn;
        }
    }
}