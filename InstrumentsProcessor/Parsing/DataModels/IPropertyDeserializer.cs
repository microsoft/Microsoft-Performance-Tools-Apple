// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Xml;

namespace InstrumentsProcessor.Parsing.DataModels
{
    /// <summary>
    /// The IPropertyDeserializer interface provides a mechanism for custom deserialization of properties.
    /// Classes implementing this interface can define specific deserialization logic for properties marked with the CustomDeserialization attribute.
    /// </summary>
    public interface IPropertyDeserializer
    {
        object DeserializeProperty(XmlNode node, XmlParsingContext context, PropertyInfo property);
    }
}
