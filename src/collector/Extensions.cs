using System;
using System.Collections.Generic;
using System.Xml;

namespace Neo.Collector
{
    static class Extensions
    {
        public static IDisposable StartDocument(this XmlWriter writer)
        {
            writer.WriteStartDocument();
            return new DelegateDisposable(() => writer.WriteEndDocument());
        }

        public static IDisposable StartElement(this XmlWriter writer, string localName)
        {
            writer.WriteStartElement(localName);
            return new DelegateDisposable(() => writer.WriteEndElement());
        }
    }
}