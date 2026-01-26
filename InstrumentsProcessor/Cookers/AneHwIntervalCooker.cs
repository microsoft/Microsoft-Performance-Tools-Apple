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
    public sealed class AneHwIntervalCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(AneHwIntervalCooker));

        public AneHwIntervalCooker()
            : base(DataCookerPath)
        {
            this.AneHwIntervalEvents = new List<AneHwIntervalEvent>();
        }

        public override string Description => "Apple Neural Engine Hardware Interval cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(AneHwIntervalEvent) }));

        [DataOutput]
        public List<AneHwIntervalEvent> AneHwIntervalEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            AneHwIntervalEvents.Add((AneHwIntervalEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
