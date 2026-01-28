// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Extensibility.DataCooking;
using Microsoft.Performance.SDK.Extensibility.DataCooking.SourceDataCooking;
using InstrumentsProcessor.Parsing;
using InstrumentsProcessor.Parsing.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace InstrumentsProcessor.Cookers
{
    public sealed class ThreadStateAccumulator
    {
        public TimestampDelta Ready { get; set; }
        public TimestampDelta Waiting { get; set; }
    }

    public sealed class ThreadStateCooker
        : SourceDataCooker<Event, ParsingContext, Type>
    {
        public static readonly DataCookerPath DataCookerPath =
            DataCookerPath.ForSource(nameof(TraceSourceParser), nameof(ThreadStateCooker));

        private readonly Dictionary<UInt64, ThreadStateAccumulator> WaitingThreads =
            new Dictionary<UInt64, ThreadStateAccumulator>();

        private IdleTimeTracker IdleTracker =
            new IdleTimeTracker();

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
            var tsevent = (ThreadStateEvent)data;
            if (tsevent.Thread == null)
            {
                return DataProcessingResult.Ignored;
            }

            UInt64 threadKey = tsevent.Thread.ThreadUniqueId();
            WaitingThreads.TryGetValue(threadKey, out ThreadStateAccumulator tstate);

            if (tsevent.State.Value == "Running")
            {
                if (tstate != null)
                {
                    // Merge ready and waiting times
                    tsevent.Waiting = new Parsing.DataModels.TimestampDelta(tstate.Waiting);
                    tsevent.Ready = new Parsing.DataModels.TimestampDelta(tstate.Ready);
                    WaitingThreads.Remove(threadKey);
                }

                if (tsevent.Core != null)
                {
                    SynthesizeIdleTime(tsevent);
                    ThreadStateEvents.Add(tsevent);
                }
            }
            else if (tsevent.State.Value == "Terminated")
            {
                WaitingThreads.Remove(threadKey);
            }
            else
            {
                if (tstate == null)
                {
                    tstate = new ThreadStateAccumulator();
                    WaitingThreads.Add(threadKey, tstate);
                }

                if (tsevent.State.Value == "Runnable")
                {
                    tstate.Ready += tsevent.Duration.Value;
                }
                else if (
                    (tsevent.State.Value == "Blocked") ||
                    (tsevent.State.Value == "Idle") ||
                    (tsevent.State.Value == "Interrupted") ||
                    (tsevent.State.Value == "Preempted"))
                {
                    tstate.Waiting += tsevent.Duration.Value;
                }
                else if (tsevent.State.Value != "Unknown")
                {
                    Debug.Assert(false, "Unknown thread state");
                }
            }

            return DataProcessingResult.Processed;
        }

        private void SynthesizeIdleTime(ThreadStateEvent tsevent)
        {
            Timestamp curr = tsevent.Timestamp;
            TimestampDelta idleGap = IdleTracker.Advance(tsevent.Core.CoreId, curr, tsevent.Duration.Value);

            if (idleGap != TimestampDelta.Zero)
            {
                var idleEvent = ThreadStateEvent.MakeIdleEvent(tsevent.Core, curr - idleGap, idleGap);
                ThreadStateEvents.Add(idleEvent);
            }
        }
    }
}
