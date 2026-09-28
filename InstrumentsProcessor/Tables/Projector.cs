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
            return backtrace != null && backtrace.Frames != null && backtrace.Frames.Count > 0 && backtrace.Frames[0] != null ? backtrace.Frames[0].Module?.Name ?? "Unknown" : "Unknown";
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

        public static long ThreadIdProjector(Thread thread)
        {
            return thread?.ThreadId?.Value ?? -1;
        }

        public static string ThreadNameProjector(Thread thread)
        {
            return thread?.Name ?? "Unknown";
        }

        public static long ProcessIdProjector(Process process)
        {
            return process?.ProcessId?.Value ?? -1;
        }

        public static string ProcessNameProjector(Process process)
        {
            return process?.Name ?? "Unknown";
        }

        public static string DeviceSessionProjector(Process process)
        {
            return process?.DeviceSession?.Value ?? "Unknown";
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

        public static long PriorityProjector(ThreadStateEvent e)
        {
            return e.Priority.Value;
        }

        public static string NoteProjector(ThreadStateEvent e)
        {
            return e.Note?.Value ?? string.Empty;
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

        internal static long SizeProjector(VirtualMemoryEvent e)
        {
            return e.Size.Value; 
        }

        internal static double SizeKBProjector(VirtualMemoryEvent e)
        {
            return e.Size.Value / 1024.0;
        }

        internal static double SizeMBProjector(VirtualMemoryEvent e)
        {
            return e.Size.Value / (1024.0 * 1024.0);
        }

        internal static double SizeGBProjector(VirtualMemoryEvent e)
        {
            return e.Size.Value / (1024.0 * 1024.0 * 1024.0);
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

        public static long FrameProjector(MetalGpuIntervalEvent e)
        {
            return e.Frame.Value;
        }

        public static TimestampDelta CpuToGpuLatencyProjector(MetalGpuIntervalEvent e)
        {
            return e.CpuToGpuLatency != null ? e.CpuToGpuLatency.Value : TimestampDelta.Zero;
        }

        public static long DepthProjector(MetalGpuIntervalEvent e)
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

        public static long ColorProjector(MetalGpuIntervalEvent e)
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

        public static long BytesProjector(MetalGpuIntervalEvent e)
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

        public static long ColorProjector(DisplayVsyncIntervalEvent e)
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

        public static Timestamp TimeStampProjector(CountersProfileEvent e)
        {
            return e.SampleTime.Value;
        }

        public static Thread ThreadProjector(CountersProfileEvent e)
        {
            return e.Thread;
        }

        public static Process ProcessProjector(CountersProfileEvent e)
        {
            return e.Process;
        }

        public static string CpuProjector(CountersProfileEvent e)
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

        public static Backtrace StackProjector(CountersProfileEvent e)
        {
            return e.Backtrace;
        }

        public static TimestampDelta WeightProjector(CountersProfileEvent e)
        {
            return e.Weight.Value;
        }

        // ANE Hardware Interval Event Projectors
        public static Timestamp StartTimeProjector(AneHwIntervalEvent e)
        {
            return e.StartTime.Value;
        }

        public static Timestamp StopTimeProjector(AneHwIntervalEvent e)
        {
            return e.StartTime.Value + e.Duration.Value;
        }

        public static TimestampDelta DurationProjector(AneHwIntervalEvent e)
        {
            return e.Duration.Value;
        }

        public static string ChannelNameProjector(AneHwIntervalEvent e)
        {
            return e.ChannelName?.Value ?? "Unknown";
        }

        public static long DepthProjector(AneHwIntervalEvent e)
        {
            return e.Depth?.Value ?? 0;
        }

        public static string LabelProjector(AneHwIntervalEvent e)
        {
            return e.Label?.Value ?? string.Empty;
        }

        public static string StateProjector(AneHwIntervalEvent e)
        {
            return e.State?.Value ?? "Unknown";
        }

        public static long ColorProjector(AneHwIntervalEvent e)
        {
            return e.Color?.Value ?? 0;
        }

        // Life Cycle Period Event Projectors
        public static Timestamp StartTimeProjector(LifeCyclePeriodEvent e)
        {
            return e.Start.Value;
        }

        public static Timestamp StopTimeProjector(LifeCyclePeriodEvent e)
        {
            return e.Start.Value + e.Duration.Value;
        }

        public static TimestampDelta DurationProjector(LifeCyclePeriodEvent e)
        {
            return e.Duration.Value;
        }

        public static string GroupProjector(LifeCyclePeriodEvent e)
        {
            return e.Group?.Value ?? "Unknown";
        }

        public static long LayoutIdProjector(LifeCyclePeriodEvent e)
        {
            return e.LayoutId?.Value ?? 0;
        }

        public static Process ProcessProjector(LifeCyclePeriodEvent e)
        {
            return e.Process;
        }

        public static string LifecyclePeriodProjector(LifeCyclePeriodEvent e)
        {
            return e.LifecyclePeriod?.Value ?? "Unknown";
        }

        public static string NarrativeProjector(LifeCyclePeriodEvent e)
        {
            return e.Narrative?.Value ?? string.Empty;
        }

        // OS Signpost Event Projectors
        // If value_ms exists: StartTime = EndTime - value_ms, Duration = value_ms
        // If no value_ms: StartTime = BeginTime, Duration = EndTime - BeginTime
        public static Timestamp StartTimeProjector(OsSignpostEvent e)
        {
            var endTime = e.EndTime ?? e.Time?.Value ?? default;
            double? valueMs = e.Metadata?.NumericValue;
            if (valueMs.HasValue)
            {
                long valueNs = (long)(valueMs.Value * 1_000_000);
                return endTime - new TimestampDelta(valueNs);
            }
            return e.BeginTime ?? e.Time?.Value ?? default;
        }

        public static Timestamp StopTimeProjector(OsSignpostEvent e)
        {
            return e.EndTime ?? e.Time?.Value ?? default;
        }

        public static TimestampDelta DurationProjector(OsSignpostEvent e)
        {
            double? valueMs = e.Metadata?.NumericValue;
            if (valueMs.HasValue)
            {
                long valueNs = (long)(valueMs.Value * 1_000_000);
                return new TimestampDelta(valueNs);
            }
            var endTime = e.EndTime ?? e.Time?.Value ?? default;
            var beginTime = e.BeginTime ?? e.Time?.Value ?? default;
            return endTime - beginTime;
        }

        public static string SignpostNameProjector(OsSignpostEvent e)
        {
            return e.SignpostName?.Value ?? string.Empty;
        }

        public static string EventTypeProjector(OsSignpostEvent e)
        {
            return e.EventType?.Value ?? string.Empty;
        }

        public static string SubsystemProjector(OsSignpostEvent e)
        {
            return e.Subsystem?.Value ?? string.Empty;
        }

        public static string CategoryProjector(OsSignpostEvent e)
        {
            return e.Category?.Value ?? string.Empty;
        }

        public static string EventNameProjector(OsSignpostEvent e)
        {
            return e.Metadata?.EventName ?? string.Empty;
        }

        public static string PageProjector(OsSignpostEvent e)
        {
            if (e.Metadata?.Parameters != null && e.Metadata.Parameters.TryGetValue("page", out string page))
            {
                return page;
            }

            return string.Empty;
        }

        public static string OuterProjector(OsSignpostEvent e)
        {
            if (e.Metadata?.Parameters != null && e.Metadata.Parameters.TryGetValue("outer", out string outer))
            {
                return outer;
            }

            return string.Empty;
        }

        public static string InstanceProjector(OsSignpostEvent e)
        {
            if (e.Metadata?.Parameters != null && e.Metadata.Parameters.TryGetValue("instance", out string instance))
            {
                return instance;
            }

            return string.Empty;
        }

        public static double ValueProjector(OsSignpostEvent e)
        {
            return e.Metadata?.NumericValue ?? 0;
        }

        public static string MessageProjector(OsSignpostEvent e)
        {
            return e.Metadata?.Message ?? string.Empty;
        }

        public static Process ProcessProjector(OsSignpostEvent e)
        {
            return e.Process;
        }

        public static Thread ThreadProjector(OsSignpostEvent e)
        {
            return e.Thread;
        }

        public static string ScopeProjector(OsSignpostEvent e)
        {
            return e.Scope?.Value ?? string.Empty;
        }

        public static long SignpostIdProjector(OsSignpostEvent e)
        {
            return e.SignpostId?.Value ?? 0;
        }

        // Context Switch Interval Event Projectors
        public static Timestamp StartTimeProjector(CswitchIntervalEvent e)
        {
            return e.StartTime.Value;
        }

        public static Timestamp StopTimeProjector(CswitchIntervalEvent e)
        {
            return e.StartTime.Value + e.Duration.Value;
        }

        public static TimestampDelta DurationProjector(CswitchIntervalEvent e)
        {
            return e.Duration.Value;
        }

        public static long LayoutIdProjector(CswitchIntervalEvent e)
        {
            return e.LayoutId?.Value ?? 0;
        }

        public static Process ProcessProjector(CswitchIntervalEvent e)
        {
            return e.Process;
        }

        public static Thread ThreadProjector(CswitchIntervalEvent e)
        {
            return e.Thread;
        }

        public static long CpuProjector(CswitchIntervalEvent e)
        {
            // Instruments encodes the CPU index as the row's layout-id for cswitch intervals.
            return e.LayoutId?.Value ?? (long)(e.Cpu?.Value ?? 0);
        }

        public static ulong DeltaInstructionsProjector(CswitchIntervalEvent e)
        {
            return e.DeltaInstructions?.Value ?? 0;
        }

        public static ulong DeltaCyclesProjector(CswitchIntervalEvent e)
        {
            return e.DeltaCycles?.Value ?? 0;
        }

        public static TimestampDelta DeltaTimeProjector(CswitchIntervalEvent e)
        {
            return e.DeltaTime?.Value ?? TimestampDelta.Zero;
        }

        public static Backtrace SwitchInStackProjector(CswitchIntervalEvent e)
        {
            return e.SwitchInStack;
        }

        public static Backtrace SwitchInKernelStackProjector(CswitchIntervalEvent e)
        {
            return e.SwitchInKernelStack;
        }

        // Virtual Memory Fault Event Projectors
        public static Timestamp StartTimeProjector(VmFaultEvent e)
        {
            return e.StartTime?.Value ?? default;
        }

        public static Timestamp StopTimeProjector(VmFaultEvent e)
        {
            return (e.StartTime?.Value ?? default) + (e.Duration?.Value ?? TimestampDelta.Zero);
        }

        public static TimestampDelta DurationProjector(VmFaultEvent e)
        {
            return e.Duration?.Value ?? TimestampDelta.Zero;
        }

        public static long LayoutIdProjector(VmFaultEvent e)
        {
            return e.LayoutId?.Value ?? 0;
        }

        public static Process ProcessProjector(VmFaultEvent e)
        {
            return e.Process;
        }

        public static Thread ThreadProjector(VmFaultEvent e)
        {
            return e.Thread;
        }

        public static string OperationProjector(VmFaultEvent e)
        {
            ulong code = e.Operation?.Value ?? 0;
            switch (code)
            {
                case 1: return "Zero-fill";
                case 2: return "Page-in";
                case 3: return "Copy-on-write (COW)";
                case 4: return "Cache-hit";
                case 6: return "Guard";
                case 7: return "Page-in (vnode dirty)";
                case 8: return "Page-in (vnode device)";
                case 9: return "Decompress";
                case 10: return "Decompress + swap-in";
                case 11: return "Copy-on-read";
                default: return code.ToString();
            }
        }

        public static TimestampDelta FaultDurationProjector(VmFaultEvent e)
        {
            return e.FaultDuration?.Value ?? TimestampDelta.Zero;
        }

        public static ulong SizeProjector(VmFaultEvent e)
        {
            return e.Size?.Value ?? 0;
        }

        // CSR Switch Event Projectors
        public static Timestamp TimeProjector(CsrSwitchEvent e)
        {
            return e.Time?.Value ?? default;
        }

        public static Process ProcessProjector(CsrSwitchEvent e)
        {
            return e.Process;
        }

        public static Thread ThreadProjector(CsrSwitchEvent e)
        {
            return e.Thread;
        }

        public static long CpuProjector(CsrSwitchEvent e)
        {
            return (long)(e.Cpu?.Value ?? 0);
        }

        public static ulong InstructionsProjector(CsrSwitchEvent e)
        {
            return e.Instructions?.Value ?? 0;
        }

        public static ulong CyclesProjector(CsrSwitchEvent e)
        {
            return e.Cycles?.Value ?? 0;
        }

        public static string EventTypeProjector(CsrSwitchEvent e)
        {
            return e.IsSwitchOn ? "Switch On" : "Switch Off";
        }

        // CSR Switch Interval Projectors
        public static Timestamp SwitchOnTimeProjector(CsrSwitchInterval i)
        {
            return i.SwitchOnTime;
        }

        public static Timestamp SwitchOffTimeProjector(CsrSwitchInterval i)
        {
            return i.SwitchOffTime;
        }

        public static TimestampDelta DurationProjector(CsrSwitchInterval i)
        {
            return i.Duration;
        }

        public static Process ProcessProjector(CsrSwitchInterval i)
        {
            return i.Process;
        }

        public static Thread ThreadProjector(CsrSwitchInterval i)
        {
            return i.Thread;
        }

        public static long CpuProjector(CsrSwitchInterval i)
        {
            return i.Cpu;
        }

        public static ulong DeltaInstructionsProjector(CsrSwitchInterval i)
        {
            return i.DeltaInstructions;
        }

        public static ulong DeltaCyclesProjector(CsrSwitchInterval i)
        {
            return i.DeltaCycles;
        }

        // Disk I/O Event Projectors
        public static Timestamp StartTimeProjector(DiskIoEvent e)
        {
            return e.StartTime?.Value ?? default;
        }

        public static Timestamp StopTimeProjector(DiskIoEvent e)
        {
            return (e.StartTime?.Value ?? default) + (e.Latency?.Value ?? TimestampDelta.Zero);
        }

        public static TimestampDelta LatencyProjector(DiskIoEvent e)
        {
            return e.Latency?.Value ?? TimestampDelta.Zero;
        }

        public static Process ProcessProjector(DiskIoEvent e)
        {
            return e.Process;
        }

        public static Thread ThreadProjector(DiskIoEvent e)
        {
            return e.Thread;
        }

        public static string OperationProjector(DiskIoEvent e)
        {
            return e.Operation?.Value ?? string.Empty;
        }

        public static string SyncModeProjector(DiskIoEvent e)
        {
            return e.SyncMode?.Value ?? string.Empty;
        }

        public static ulong TierProjector(DiskIoEvent e)
        {
            return e.Tier?.Value ?? 0;
        }

        public static string FlagsProjector(DiskIoEvent e)
        {
            return e.Flags?.Value ?? string.Empty;
        }

        public static long SizeProjector(DiskIoEvent e)
        {
            return e.Size?.Value ?? 0;
        }

        public static ulong ThroughputProjector(DiskIoEvent e)
        {
            return e.Throughput?.Value ?? 0;
        }

        public static ulong BlockNumberProjector(DiskIoEvent e)
        {
            return e.BlockNumber?.Value ?? 0;
        }

        public static string DeviceProjector(DiskIoEvent e)
        {
            return e.Device?.Value ?? string.Empty;
        }

        public static ulong QueueDepthProjector(DiskIoEvent e)
        {
            return e.QueueDepth?.Value ?? 0;
        }

        public static string BufTProjector(DiskIoEvent e)
        {
            return e.BufT?.Value ?? string.Empty;
        }

        public static string ErrorProjector(DiskIoEvent e)
        {
            return e.Error?.Value ?? string.Empty;
        }

        public static long ResidProjector(DiskIoEvent e)
        {
            return e.Resid?.Value ?? 0;
        }
    }
}
