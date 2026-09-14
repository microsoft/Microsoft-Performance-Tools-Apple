// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Extensibility.DataCooking;
using Microsoft.Performance.SDK.Extensibility.DataCooking.SourceDataCooking;
using System;
using System.Collections.Generic;
using System.Threading;

namespace InstrumentsProcessor.Cookers
{
    public sealed class VmFaultCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(VmFaultCooker));

        public VmFaultCooker()
            : base(DataCookerPath)
        {
            this.VmFaultEvents = new List<VmFaultEvent>();
        }

        public override string Description => "Virtual Memory Fault cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(VmFaultEvent) }));

        [DataOutput]
        public List<VmFaultEvent> VmFaultEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            VmFaultEvents.Add((VmFaultEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
