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
    public sealed class PcwSnapSnapshotCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(PcwSnapSourceParser), nameof(PcwSnapSnapshotCooker));

        public PcwSnapSnapshotCooker()
            : base(DataCookerPath)
        {
            this.Snapshots = new List<PcwSnapSnapshotEvent>();
        }

        public override string Description => "Collects pcwsnap snapshot events.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(PcwSnapSnapshotEvent) }));

        [DataOutput]
        public List<PcwSnapSnapshotEvent> Snapshots { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            Snapshots.Add((PcwSnapSnapshotEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
