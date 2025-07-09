// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Integer : IPropertyDeserializer
    {
        [CustomDeserialization]
        public int Value { get; private set; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "Value")
            {
                return int.Parse(node.InnerText);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}