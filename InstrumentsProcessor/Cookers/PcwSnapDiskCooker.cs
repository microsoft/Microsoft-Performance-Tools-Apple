// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK.Extensibility.DataCooking.SourceDataCooking;
using Microsoft.Performance.SDK.Extensibility.DataCooking;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK;
using System.Collections.Generic;
using System.Threading;
using System;
using InstrumentsProcessor.Parsing;
using InstrumentsProcessor.Parsing.Events;

namespace InstrumentsProcessor.Cookers
{
    public sealed class PcwSnapDiskCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(PcwSnapSourceParser), nameof(PcwSnapDiskCooker));

        public PcwSnapDiskCooker()
            : base(DataCookerPath)
        {
            this.DiskEvents = new List<PcwSnapDiskEvent>();
        }

        public override string Description => "Collects per-process disk I/O snapshots.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(PcwSnapDiskEvent) }));

        [DataOutput]
        public List<PcwSnapDiskEvent> DiskEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            DiskEvents.Add((PcwSnapDiskEvent)data);
            return DataProcessingResult.Processed;
        }
    }
}
