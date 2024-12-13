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
        private static XmlNodeDeserializer<Integer> ProcessIdDeserializer = new XmlNodeDeserializer<Integer>();
        [CustomDeserialization]
        public Integer ProcessId { get; private set;}

        [CustomDeserialization]
        public string Name { get; private set; }

        private static XmlNodeDeserializer<String> DeviceSessionDeserializer = new XmlNodeDeserializer<String>();
        [CustomDeserialization]
        public String DeviceSession { get; private set; }

        public object DeserializeProperty(XmlNode node, ObjectCache cache, PropertyInfo property)
        {
            if (property.Name == "ProcessId")
            {
                XmlNode propertyNode = node.ChildNodes.Count >= 1 ? node.ChildNodes[0] : null;

                return ProcessIdDeserializer.Deserialize(propertyNode, cache);
            }
            if (property.Name == "Name")
            {
                XElement root = XElement.Parse(node.OuterXml);

                return root.Attribute("fmt")?.Value;
            }
            if (property.Name == "DeviceSession")
            {
                XmlNode propertyNode = node.ChildNodes.Count >= 2 ? node.ChildNodes[1] : null;

                return DeviceSessionDeserializer.Deserialize(propertyNode, cache);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
