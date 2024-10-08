// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class Boolean : IPropertyDeserializer
    {
        [CustomDeserialization]
        public bool Value { get; private set; }

        public object DeserializeProperty(XmlNode node, ObjectCache cache, PropertyInfo property)
        {
            if (property.Name == "Value")
            {
                return node.InnerText != "0";
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}