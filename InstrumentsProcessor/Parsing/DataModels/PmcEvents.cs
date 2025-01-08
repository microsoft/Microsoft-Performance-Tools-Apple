// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    public class PmcEvents : IPropertyDeserializer
    {
        [CustomDeserialization]
        public long ColumnOne { get; private set; }

        [CustomDeserialization]
        public long ColumnTwo { get; private set; }

        public object DeserializeProperty(XmlNode node, ObjectCache cache, PropertyInfo property)
        {
            if (property.Name == "ColumnOne")
            {
                string[] array = node.InnerText.Split(' ');

                return array.Length >= 1 ? long.Parse(array[0]) : 0;
            }
            else if (property.Name == "ColumnTwo")
            {
                string[] array = node.InnerText.Split(' ');

                return array.Length >= 2 ? long.Parse(array[1]) : 0;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}