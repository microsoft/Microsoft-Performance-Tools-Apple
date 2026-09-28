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
    public sealed class PcwSnapProcessCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(PcwSnapSourceParser), nameof(PcwSnapProcessCooker));

        public PcwSnapProcessCooker()
            : base(DataCookerPath)
        {
            this.Processes = new List<PcwSnapProcessEvent>();
        }

        public override string Description => "Collects per-process ProcInfo snapshots.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(PcwSnapProcessEvent) }));

        [DataOutput]
        public List<PcwSnapProcessEvent> Processes { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            Processes.Add((PcwSnapProcessEvent)data);
            return DataProcessingResult.Processed;
        }
    }
}
