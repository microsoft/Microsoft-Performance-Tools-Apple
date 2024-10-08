// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK.Extensibility.DataCooking.SourceDataCooking;
using Microsoft.Performance.SDK.Extensibility.DataCooking;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK;
using System.Collections.Generic;
using System.Threading;
using InstrumentsProcessor.Parsing;
using InstrumentsProcessor.Parsing.Events;
using System;

namespace InstrumentsProcessor.Cookers
{
    public sealed class VirtualMemoryCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(VirtualMemoryCooker));

        public VirtualMemoryCooker()
            : base(DataCookerPath)
        {
            this.VirtualMemoryEvents = new List<VirtualMemoryEvent>();
        }

        public override string Description => "Syscall Name Map cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(VirtualMemoryEvent) }));

        [DataOutput]
        public List<VirtualMemoryEvent> VirtualMemoryEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            VirtualMemoryEvents.Add((VirtualMemoryEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
