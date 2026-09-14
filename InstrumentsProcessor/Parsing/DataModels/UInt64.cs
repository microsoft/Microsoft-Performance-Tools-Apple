// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class UInt64 : IPropertyDeserializer
    {
        [CustomDeserialization]
        public ulong Value { get; private set; }

        public UInt64() { }
        internal UInt64(ulong value) { Value = value; }

        public object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property)
        {
            if (property.Name == "Value")
            {
                return System.UInt64.Parse(node.InnerText);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}