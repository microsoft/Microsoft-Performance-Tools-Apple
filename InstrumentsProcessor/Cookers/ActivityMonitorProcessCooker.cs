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
    public sealed class ActivityMonitorProcessCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(ActivityMonitorProcessCooker));

        public ActivityMonitorProcessCooker() : base(DataCookerPath)
        {
            ActivityMonitorProcessEvents = new List<ActivityMonitorProcessEvent>();
        }

        public override string Description => "Activity Monitor per-process sampled resource usage.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(ActivityMonitorProcessEvent) }));

        [DataOutput]
        public List<ActivityMonitorProcessEvent> ActivityMonitorProcessEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data, ParsingContext context, CancellationToken cancellationToken)
        {
            ActivityMonitorProcessEvents.Add((ActivityMonitorProcessEvent)data);
            return DataProcessingResult.Processed;
        }
    }
}
