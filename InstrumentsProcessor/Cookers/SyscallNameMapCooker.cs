﻿// Copyright (c) Microsoft Corporation.
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
    public sealed class SyscallNameMapCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(SyscallNameMapCooker));

        public SyscallNameMapCooker()
            : base(DataCookerPath)
        {
            this.SyscallNameMapEvents = new List<SyscallNameMapEvent>();
        }

        public override string Description => "Syscall Name Map cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(SyscallNameMapEvent) }));

        [DataOutput]
        public List<SyscallNameMapEvent> SyscallNameMapEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            SyscallNameMapEvents.Add((SyscallNameMapEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}