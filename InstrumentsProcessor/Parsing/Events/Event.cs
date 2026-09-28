// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using System;

namespace InstrumentsProcessor.Parsing.Events
{
    public abstract class Event : IKeyedDataType<Type>
    {
        public abstract Timestamp Timestamp { get; }
        public abstract Type GetKey();

        /// <summary>
        /// The name of the schema this event was deserialized from (e.g. "csr-switch-on").
        /// Set automatically by the deserializer.
        /// </summary>
        public string SchemaName { get; set; }
    }
}
