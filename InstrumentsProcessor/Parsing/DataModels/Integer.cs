// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
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
                string text = node.InnerText;

                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    return int.Parse(text.Substring(2), NumberStyles.HexNumber);
                }

                if (int.TryParse(text, out int decimalValue))
                {
                    return decimalValue;
                }

                // Fallback: try parsing as hex without 0x prefix
                return int.Parse(text, NumberStyles.HexNumber);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}