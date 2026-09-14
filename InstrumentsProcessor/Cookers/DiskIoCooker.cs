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
    public sealed class DiskIoCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(DiskIoCooker));

        public DiskIoCooker()
            : base(DataCookerPath)
        {
            this.DiskIoEvents = new List<DiskIoEvent>();
        }

        public override string Description => "Disk I/O cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(DiskIoEvent) }));

        [DataOutput]
        public List<DiskIoEvent> DiskIoEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            DiskIoEvents.Add((DiskIoEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
