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
    public sealed class DeviceThermalStateIntervalCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(DeviceThermalStateIntervalCooker));

        public DeviceThermalStateIntervalCooker()
            : base(DataCookerPath)
        {
            this.DeviceThermalStateIntervalEvents = new List<DeviceThermalStateIntervalEvent>();
        }

        public override string Description => "Device Thermal State Interval cooker.";

        public override ReadOnlyHashSet<Type> DataKeys =>
            new ReadOnlyHashSet<Type>(new HashSet<Type>(new[] { typeof(DeviceThermalStateIntervalEvent) }));

        [DataOutput]
        public List<DeviceThermalStateIntervalEvent> DeviceThermalStateIntervalEvents { get; }

        public override DataProcessingResult CookDataElement(
            Event data,
            ParsingContext context,
            CancellationToken cancellationToken)
        {
            DeviceThermalStateIntervalEvents.Add((DeviceThermalStateIntervalEvent)data);

            return DataProcessingResult.Processed;
        }
    }
}
