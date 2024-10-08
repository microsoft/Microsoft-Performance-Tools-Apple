// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Process : IPropertyDeserializer
    {
        public Integer ProcessId { get; private set;}

        [CustomDeserialization]
        public string Name { get; private set; }

        public String DeviceSession { get; private set; }

        public object DeserializeProperty(XmlNode node, ObjectCache cache, PropertyInfo property)
        {
            if (property.Name == "Name")
            {
                XElement root = XElement.Parse(node.OuterXml);

                return root.Attribute("fmt")?.Value;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
