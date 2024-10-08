// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using InstrumentsProcessor.Parsing.Events;
using System.Text.RegularExpressions;
using Timestamp = Microsoft.Performance.SDK.Timestamp;
using TimestampDelta = Microsoft.Performance.SDK.TimestampDelta;


namespace InstrumentsProcessor.Tables
{
    internal class Projector
    {
        public static Timestamp TimeStampProjector(TimeProfileEvent e)
        {
            return e.SampleTime.Value;
        }

        public static Process ProcessProjector(TimeProfileEvent e)
        {
            return e.Process;
        }

        public static string ModuleProjector(Backtrace backtrace)
        {
            return backtrace != null && backtrace.Frames.Count > 0 && backtrace.Frames[0] != null ? backtrace.Frames[0].Module.Name : "Unknown";
        }

        public static string FunctionProjector(Backtrace backtrace)
        {
            return backtrace != null && backtrace.Frames.Count > 0 && backtrace.Frames[0] != null ? backtrace.Frames[0].Function.Name : "Unknown";
        }

        public static Thread ThreadProjector(TimeProfileEvent e)
        {
            return e.Thread;
        }

        public static Backtrace StackProjector(TimeProfileEvent e)
        {
            return e.Backtrace;
        }

        public static int ThreadIdProjector(Thread thread)
        {
            return thread?.ThreadId.Value ?? -1;
        }

        public static int ProcessIdProjector(Process process)
        {
            return process?.ProcessId?.Value ?? -1;
        }

        public static string ProcessNameProjector(Process process)
        {
            return process?.Name ?? "Unknown";
        }

        public static string DeviceSessionProjector(Process process)
        {
            return process?.DeviceSession.Value ?? "Unknown";
        }

        public static string CpuProjector(TimeProfileEvent e)
        {
            if (e.Core == null)
            {
                return "Unknown";
            }

            Regex regex = new Regex(@"CPU (\d+)");
            Match match = regex.Match(e.Core.Value);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return "Unknown";
        }

        public static string ProcessorClassProjector(TimeProfileEvent e)
        {
            if (e.Core == null)
            {
                return "Unknown";
            }
            
            Regex regex = new Regex(@"\((.*?)\)");
            Match match = regex.Match(e.Core.Value);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return "Unknown";
        }

        public static string StateProjector(TimeProfileEvent e)
        {
            return e.ThreadState.Value;
        }

        public static TimestampDelta WeightProjector(TimeProfileEvent e)
        {
            return e.Weight.Value;
        }

        public static Timestamp SwitchInTimeProjector(ThreadStateEvent e)
        {
            return e.StartTime.Value;
        }

        public static Timestamp SwitchOutTimeProjector(ThreadStateEvent e)
        {
            return e.StartTime.Value + e.Duration.Value;
        }

        public static TimestampDelta DurationProjector(ThreadStateEvent e)
        {
            return e.Duration.Value;
        }

        public static Thread ThreadProjector(ThreadStateEvent e)
        {
            return e.Thread;
        }

        public static string StateProjector(ThreadStateEvent e)
        {
            return e.State.Value;
        }

        public static Process ProcessProjector(ThreadStateEvent e)
        {
            return e.Process;
        }
        public static string CpuProjector(ThreadStateEvent e)
        {
            return e.Core != null ? e.Core.Value : "Unknown";
        }

        public static TimestampDelta CpuTimeProjector(ThreadStateEvent e)
        {
            return e.RunningTime?.Value != null ? e.RunningTime.Value : default; // TODO: handle the null case better
        }

        public static TimestampDelta WaitTimeProjector(ThreadStateEvent e)
        {
            return e.WaitTime?.Value != null ? e.WaitTime.Value : default; // TODO: handle the null case better
        }

        public static int PriorityProjector(ThreadStateEvent e)
        {
            return e.Priority.Value;
        }

        public static string NoteProjector(ThreadStateEvent e)
        {
            return e.Note.Value;
        }

        public static string SummaryProjector(ThreadStateEvent e)
        {
            return e.Summary.Value;
        }

        public static string ThermalStateProjector(DeviceThermalStateIntervalEvent e)
        {
            return e.ThermalState.Value;
        }

        public static Timestamp SwitchInTimeProjector(DeviceThermalStateIntervalEvent e)
        {
            return e.Start.Value;
        }

        public static Timestamp SwitchOutTimeProjector(DeviceThermalStateIntervalEvent e)
        {
            return e.Start.Value + e.Duration.Value;
        }

        internal static Timestamp StartTimeProjector(VirtualMemoryEvent e)
        {
            return e.StartTime.Value;
        }

        public static TimestampDelta DurationProjector(VirtualMemoryEvent e)
        {
            return e.Duration.Value;
        }

        public static Thread ThreadProjector(VirtualMemoryEvent e)
        {
            return e.Thread;
        }

        internal static string OperationProjector(VirtualMemoryEvent e)
        {
            return e.Operation.Value;
        }

        public static Process ProcessProjector(VirtualMemoryEvent e)
        {
            return e.Process;
        }

        public static TimestampDelta CpuTimeProjector(VirtualMemoryEvent e)
        {
            return e.CPUTime?.Value != null ? e.CPUTime.Value : default; // TODO: handle the null case better
        }

        public static TimestampDelta WaitTimeProjector(VirtualMemoryEvent e)
        {
            return e.WaitTime?.Value != null ? e.WaitTime.Value : default; // TODO: handle the null case better
        }

        internal static string AddressProjector(VirtualMemoryEvent e)
        {
            return e.Address.Value;
        }

        internal static int SizeProjector(VirtualMemoryEvent e)
        {
            return e.Size.Value; 
        }

        public static Backtrace StackProjector(VirtualMemoryEvent e)
        {
            return e.Stack;
        }

        internal static Timestamp StartTimeProjector(PotentialHangEvent e)
        {
            return e.StartTime.Value;
        }

        internal static Timestamp StopTimeProjector(PotentialHangEvent e)
        {
            return e.StartTime.Value + e.Duration.Value;
        }

        public static TimestampDelta DurationProjector(PotentialHangEvent e)
        {
            return e.Duration.Value;
        }

        internal static string HangTypeProjector(PotentialHangEvent e)
        {
            return e.HangType.Value;
        }

        public static Thread ThreadProjector(PotentialHangEvent e)
        {
            return e.Thread;
        }

        public static Process ProcessProjector(PotentialHangEvent e)
        {
            return e.Process;
        }

        public static string CallProjector(SyscallEvent e)
        {
            return e.Call.Value;
        }

        public static Timestamp TimeStampProjector(SyscallEvent e)
        {
            return e.Timestamp;
        }

        public static string ReturnProjector(SyscallEvent e)
        {
            return e.Return.Value;
        }

        public static TimestampDelta DurationProjector(SyscallEvent e)
        {
            return e.Duration.Value;
        }

        public static Process ProcessProjector(SyscallEvent e)
        {
            return e.Process;
        }

        public static string ErrorNumberProjector(SyscallEvent e)
        {
            return e.Errno.Value;
        }

        public static TimestampDelta CpuTimeProjector(SyscallEvent e)
        {
            return e.CPUTime.Value;
        }

        public static TimestampDelta WaitTimeProjector(SyscallEvent e)
        {
            return e.WaitTime?.Value != null ? e.WaitTime.Value : default; // TODO: handle the null case better
        }

        public static string NoteProjector(SyscallEvent e)
        {
            return e.Note.Value;
        }

        public static string SignatureProjector(SyscallEvent e)
        {
            return e.Signature.Value;
        }

        public static Backtrace StackProjector(SyscallEvent e)
        {
            return e.Stack;
        }

        public static Timestamp StartTimeProjector(SyscallEvent e)
        {
            return e.StartTime.Value;
        }

        public static Timestamp StopTimeProjector(SyscallEvent e)
        {
            return e.StartTime.Value + e.Duration.Value;
        }

        public static Thread ThreadProjector(SyscallEvent e)
        {
            return e.Thread;
        }

        public static Timestamp StartTimeProjector(MetalGpuIntervalEvent e)
        {
            return e.StartTime.Value;
        }

        internal static Timestamp StopTimeProjector(MetalGpuIntervalEvent e)
        {
            return e.StartTime.Value + e.Duration.Value;
        }

        public static TimestampDelta DurationProjector(MetalGpuIntervalEvent e)
        {
            return e.Duration.Value;
        }

        public static string ChannelNameProjector(MetalGpuIntervalEvent e)
        {
            return e.ChannelName.Value;
        }

        public static int FrameProjector(MetalGpuIntervalEvent e)
        {
            return e.Frame.Value;
        }

        public static TimestampDelta CpuToGpuLatencyProjector(MetalGpuIntervalEvent e)
        {
            return e.CpuToGpuLatency != null ? e.CpuToGpuLatency.Value : TimestampDelta.Zero;
        }

        public static int DepthProjector(MetalGpuIntervalEvent e)
        {
            return e.Depth.Value;
        }

        public static string LabelProjector(MetalGpuIntervalEvent e)
        {
            return e.Label.Value;
        }

        public static string StateProjector(MetalGpuIntervalEvent e)
        {
            return e.State.Value;
        }

        public static string ConnectionUUIDProjector(MetalGpuIntervalEvent e)
        {
            return e.ConnectionUUID.Value;
        }

        public static int ColorProjector(MetalGpuIntervalEvent e)
        {
            return e.Color.Value;
        }

        public static Process ProcessProjector(MetalGpuIntervalEvent e)
        {
            return e.Process;
        }

        public static string MetalDeviceProjector(MetalGpuIntervalEvent e)
        {
            return e.MetalDevice.Value;
        }

        public static string ChannelSubtitleProjector(MetalGpuIntervalEvent e)
        {
            return e.ChannnelSubtitle.Value;
        }

        public static string IOSurfaceAccessesProjector(MetalGpuIntervalEvent e)
        {
            return e.IOSurfaceAccesses.Value;
        }

        public static int BytesProjector(MetalGpuIntervalEvent e)
        {
            return e.Bytes.Value;
        }

        public static ulong CommandBufferIdProjector(MetalGpuIntervalEvent e)
        {
            return e.CommandBufferId.Value;
        }

        public static ulong EncoderIDProjector(MetalGpuIntervalEvent e)
        {
            return e.EncoderID.Value;
        }

        public static ulong GpuSubmissionIdProjector(MetalGpuIntervalEvent e)
        {
            return e.GpuSubmissionId.Value;
        }

        public static Timestamp StartTimeProjector(DisplayVsyncIntervalEvent e)
        {
            return e.TimeStamp.Value;
        }

        public static Timestamp StopTimeProjector(DisplayVsyncIntervalEvent e)
        {
            return e.TimeStamp.Value + e.Duration.Value;
        }

        public static TimestampDelta DurationProjector(DisplayVsyncIntervalEvent e)
        {
            return e.Duration.Value;
        }

        public static string DisplayNameProjector(DisplayVsyncIntervalEvent e)
        {
            return e.DisplayName.Value;
        }

        public static int ColorProjector(DisplayVsyncIntervalEvent e)
        {
            return e.Color.Value;
        }

        public static string LabelProjector(DisplayVsyncIntervalEvent e)
        {
            return e.Label.Value;
        }

        public static string EventProjector(DisplayVsyncIntervalEvent e)
        {
            return e.Event.Value;
        }
    }
}
