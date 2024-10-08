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
    public sealed class ThreadStateCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(ThreadStateCooker));

        public ThreadStateCooker()
            : base(DataCookerPath)
        {
            this.ThreadStateEvents = new List<ThreadStateEvent>();
        }

        public override string Description => "Thread State cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(ThreadStateEvent) }));

        [DataOutput]
        public List<ThreadStateEvent> ThreadStateEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            ThreadStateEvents.Add((ThreadStateEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
