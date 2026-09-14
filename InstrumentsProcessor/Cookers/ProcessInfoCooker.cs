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
    public sealed class ProcessInfoCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(ProcessInfoCooker));

        public ProcessInfoCooker()
            : base(DataCookerPath)
        {
            this.ProcessInfoEvents = new List<ProcessInfoEvent>();
        }

        public override string Description => "Process Info cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(ProcessInfoEvent) }));

        [DataOutput]
        public List<ProcessInfoEvent> ProcessInfoEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            ProcessInfoEvents.Add((ProcessInfoEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
