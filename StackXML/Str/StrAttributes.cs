using System;

namespace StackXML.Str
{
    [AttributeUsage(AttributeTargets.Field)]
    public class StrFieldAttribute : Attribute
    {
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class StrOptionalAttribute : Attribute
    {
        private readonly int m_group;

        public StrOptionalAttribute(int group = 0)
        {
            m_group = group;
        }
    }
}