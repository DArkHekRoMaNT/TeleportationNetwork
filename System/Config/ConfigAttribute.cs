using System;

namespace TeleportationNetwork
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ConfigAttribute : Attribute
    {
        public string Filename { get; }

        public ConfigAttribute(string filename)
        {
            Filename = filename;
        }
    }

}
