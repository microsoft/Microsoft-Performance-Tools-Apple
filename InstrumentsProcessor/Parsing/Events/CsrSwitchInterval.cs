// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using Timestamp = Microsoft.Performance.SDK.Timestamp;
using TimestampDelta = Microsoft.Performance.SDK.TimestampDelta;

namespace InstrumentsProcessor.Parsing.Events
{
    /// <summary>
    /// A correlated interval produced by pairing a csr-switch-on event with
    /// the subsequent csr-switch-off event on the same CPU.
    /// </summary>
    public class CsrSwitchInterval
    {
        public Timestamp SwitchOnTime { get; set; }
        public Timestamp SwitchOffTime { get; set; }
        public TimestampDelta Duration { get; set; }
        public Process Process { get; set; }
        public Thread Thread { get; set; }
        public long Cpu { get; set; }
        public ulong DeltaInstructions { get; set; }
        public ulong DeltaCycles { get; set; }
        public Backtrace Stack { get; set; }
        public Backtrace KernelStack { get; set; }
    }
}
