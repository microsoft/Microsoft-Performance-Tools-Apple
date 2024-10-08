// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace InstrumentsProcessor.Parsing.DataModels
{
    /// <summary>
    /// The CustomDeserializationAttribute class marks properties for custom deserialization logic.
    /// Objects implementing the IPropertyDeserializer interface should use this attribute to specify custom deserialization for the property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class CustomDeserializationAttribute : Attribute
    {
    }
}