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
    public sealed class MetalGpuIntervalCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(MetalGpuIntervalCooker));

        public MetalGpuIntervalCooker()
            : base(DataCookerPath)
        {
            this.MetalGpuIntervalEvents = new List<MetalGpuIntervalEvent>();
        }

        public override string Description => "Metal GPU Interval Event cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(MetalGpuIntervalEvent) }));

        [DataOutput]
        public List<MetalGpuIntervalEvent> MetalGpuIntervalEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            MetalGpuIntervalEvents.Add((MetalGpuIntervalEvent)data);
            return DataProcessingResult.Processed;
        }
    }
}