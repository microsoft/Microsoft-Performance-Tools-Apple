// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Module : IPropertyDeserializer
    {
        [CustomDeserialization]
        public string Name { get; private set; }

        [CustomDeserialization]
        public string UUID { get; private set; }

        [CustomDeserialization]
        public string Architecture { get; private set; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, System.Reflection.PropertyInfo property)
        {
            if (property.Name == "Name")
            {
                return node.Attributes.GetNamedItem("name").InnerText;
            }
            if (property.Name == "UUID")
            {
                return node.Attributes.GetNamedItem("UUID").InnerText;
            }
            if (property.Name == "Architecture")
            {
                return node.Attributes.GetNamedItem("arch").InnerText;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
