// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK;

namespace InstrumentsProcessor.Parsing.Events
{
    public class InFocusInterval
    {
        public Timestamp StartTime { get; set; }

        public TimestampDelta Duration { get; set; }

        public long TargetPid { get; set; }

        public string TargetProcessName { get; set; }

        public ulong NewRole { get; set; }

        public string RoleName { get; set; }
    }
}
