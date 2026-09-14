// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace InstrumentsProcessor.Parsing.Events
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public string EngineeringType { get; }

        /// <summary>
        /// Optional mnemonic for disambiguating schema columns that share the same
        /// display Name and EngineeringType (e.g. two "Duration" columns). When set,
        /// the deserializer prefers matching by (Mnemonic, EngineeringType).
        /// </summary>
        public string Mnemonic { get; set; }

        public ColumnAttribute(string name, string engineeringType)
        {
            Name = name;
            EngineeringType = engineeringType;
        }
    }
}