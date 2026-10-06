// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.PcwSnap;
using Microsoft.Performance.SDK;
using System;

namespace InstrumentsProcessor.Parsing.Events
{
    /// <summary>
    /// One row per process per snapshot, carrying rich ProcInfo metrics
    /// with deltas computed from the previous snapshot.
    /// Mirrors the snap_report.lua category breakdown: basic, memory, cpu, io, power.
    /// </summary>
    public class PcwSnapProcessEvent : Event
    {
        public PcwSnapProcessEvent(int snapshotIndex, Timestamp timestamp, string processName, ProcInfoEntry current, ProcInfoEntry previous)
        {
            SnapshotIndex = snapshotIndex;
            SnapshotTimestamp = timestamp;
            ProcessName = processName;
            Current = current;
            Previous = previous;
        }

        public override Timestamp Timestamp => SnapshotTimestamp;
        public override Type GetKey() => typeof(PcwSnapProcessEvent);

        public int SnapshotIndex { get; }
        public Timestamp SnapshotTimestamp { get; }
        public string ProcessName { get; }
        public ProcInfoEntry Current { get; }
        public ProcInfoEntry Previous { get; }

        // Identity
        public int Pid => (int)Current.Pid;

        // ---- Basic ----
        public long PhysFootprint => (long)Current.PhysFootprint;
        public double CpuSysSec => Current.SystemCpuSec;
        public double CpuUserSec => Current.UserCpuSec;
        public double DeltaCpuSys => Previous != null ? Current.SystemCpuSec - Previous.SystemCpuSec : 0;
        public double DeltaCpuUser => Previous != null ? Current.UserCpuSec - Previous.UserCpuSec : 0;

        // ---- Memory ----
        public long Resident => (long)Current.Resident;
        public long Internal => (long)Current.Internal;
        public long External => (long)Current.External;
        public long Compressed => (long)Current.Compressed;
        public long MaxResident => (long)Current.ResidentPeak;
        public long Virtual => (long)Current.Virtual;
        public long Reusable => (long)Current.Reusable;
        public long PurgeableNV => (long)Current.PurgeableNonvolatile;
        public long PurgeableNVComp => (long)Current.PurgeableNonvolatileCompressed;
        public long GfxFootprint => (long)Current.GraphicsFootprint;
        public long GfxFootprintComp => (long)Current.GraphicsFootprintCompressed;
        public long CompressedLifetime => (long)Current.CompressedLifetime;
        public long LifetimeMaxPhysFootprint => (long)Current.LifetimeMaxPhysFootprint;
        public int Decompressions => (int)Current.Decompressions;
        public int RegionCount => (int)Current.RegionCount;

        // Memory deltas
        public long DeltaPhysFootprint => Previous != null ? (long)Current.PhysFootprint - (long)Previous.PhysFootprint : 0;
        public long DeltaResident => Previous != null ? (long)Current.Resident - (long)Previous.Resident : 0;
        public long DeltaInternal => Previous != null ? (long)Current.Internal - (long)Previous.Internal : 0;
        public long DeltaExternal => Previous != null ? (long)Current.External - (long)Previous.External : 0;
        public long DeltaCompressed => Previous != null ? (long)Current.Compressed - (long)Previous.Compressed : 0;

        // ---- CPU ----
        public int Threads => (int)Current.Threads;
        public int ThreadsRunning => (int)Current.ThreadsRunning;
        public long Instructions => (long)Current.Instructions;
        public long Cycles => (long)Current.Cycles;
        public long PInstructions => (long)Current.PInstructions;
        public long PCycles => (long)Current.PCycles;
        public long ContextSwitches => (long)Current.ContextSwitches;
        public long Syscalls => (long)Current.Syscalls;
        public double IPC => Current.Cycles > 0 ? (double)Current.Instructions / Current.Cycles : 0;

        // CPU deltas
        public long DeltaInstructions => Previous != null ? (long)(Current.Instructions - Previous.Instructions) : 0;
        public long DeltaCycles => Previous != null ? (long)(Current.Cycles - Previous.Cycles) : 0;
        public long DeltaPInstructions => Previous != null ? (long)(Current.PInstructions - Previous.PInstructions) : 0;
        public long DeltaPCycles => Previous != null ? (long)(Current.PCycles - Previous.PCycles) : 0;
        public long DeltaContextSwitches => Previous != null ? (long)(Current.ContextSwitches - Previous.ContextSwitches) : 0;
        public long DeltaSyscalls => Previous != null ? (long)(Current.Syscalls - Previous.Syscalls) : 0;
        public double DeltaIPC
        {
            get
            {
                if (Previous == null) return 0;
                var di = Current.Instructions - Previous.Instructions;
                var dc = Current.Cycles - Previous.Cycles;
                return dc > 0 ? (double)di / dc : 0;
            }
        }

        // QoS breakdown (nanoseconds → seconds)
        public double QosUserInteractive => Current.QosUserInteractive / 1e9;
        public double QosUserInitiated => Current.QosUserInitiated / 1e9;
        public double QosDefault => Current.QosDefault / 1e9;
        public double QosUtility => Current.QosUtility / 1e9;
        public double QosBackground => Current.QosBackground / 1e9;
        public double QosMaintenance => Current.QosMaintenance / 1e9;
        public double RunnableTime => Current.RunnableTime / 1e9;

        // QoS deltas (nanoseconds → seconds)
        public double DeltaQosUserInteractive => Previous != null ? (Current.QosUserInteractive - Previous.QosUserInteractive) / 1e9 : 0;
        public double DeltaQosUserInitiated => Previous != null ? (Current.QosUserInitiated - Previous.QosUserInitiated) / 1e9 : 0;
        public double DeltaQosDefault => Previous != null ? (Current.QosDefault - Previous.QosDefault) / 1e9 : 0;
        public double DeltaQosUtility => Previous != null ? (Current.QosUtility - Previous.QosUtility) / 1e9 : 0;
        public double DeltaQosBackground => Previous != null ? (Current.QosBackground - Previous.QosBackground) / 1e9 : 0;
        public double DeltaQosMaintenance => Previous != null ? (Current.QosMaintenance - Previous.QosMaintenance) / 1e9 : 0;
        public double DeltaRunnableTime => Previous != null ? (Current.RunnableTime - Previous.RunnableTime) / 1e9 : 0;

        // ---- I/O ----
        public long LogicalWrites => (long)Current.LogicalWrites;

        // I/O deltas
        public long DeltaLogicalWrites => Previous != null ? (long)(Current.LogicalWrites - Previous.LogicalWrites) : 0;

        // ---- Power ----
        public long EnergyNj => (long)Current.EnergyNj;
        public long PEnergyNj => (long)Current.PEnergyNj;
        public long BilledEnergy => (long)Current.BilledEnergy;
        public long ServicedEnergy => (long)Current.ServicedEnergy;
        public long InterruptWakeups => (long)Current.InterruptWakeups;
        public long PkgIdleWakeups => (long)Current.PkgIdleWakeups;
        public long MsgSent => (long)Current.MsgSent;
        public long MsgRecv => (long)Current.MsgRecv;

        // Power deltas
        public long DeltaEnergyNj => Previous != null ? (long)(Current.EnergyNj - Previous.EnergyNj) : 0;
        public long DeltaPEnergyNj => Previous != null ? (long)(Current.PEnergyNj - Previous.PEnergyNj) : 0;
        public long DeltaInterruptWakeups => Previous != null ? (long)(Current.InterruptWakeups - Previous.InterruptWakeups) : 0;
        public long DeltaPkgIdleWakeups => Previous != null ? (long)(Current.PkgIdleWakeups - Previous.PkgIdleWakeups) : 0;
        public long DeltaMsgSent => Previous != null ? (long)(Current.MsgSent - Previous.MsgSent) : 0;
        public long DeltaMsgRecv => Previous != null ? (long)(Current.MsgRecv - Previous.MsgRecv) : 0;
    }
}
