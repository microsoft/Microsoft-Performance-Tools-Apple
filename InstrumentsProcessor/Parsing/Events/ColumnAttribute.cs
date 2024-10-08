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

        public ColumnAttribute(string name, string engineeringType)
        {
            Name = name;
            EngineeringType = engineeringType;
        }
    }
}