// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK.Extensibility.SourceParsing;
using Microsoft.Performance.SDK.Processing;
using System.Collections.Generic;
using System;
using System.Xml;
using System.IO;
using System.IO.Compression;
using System.Xml.Serialization;
using System.Threading;
using Microsoft.Performance.SDK;
using System.Diagnostics;
using System.Text;
using System.Linq;
using InstrumentsProcessor.Parsing.Events;
using InstrumentsProcessor.Parsing.TraceBundle;

namespace InstrumentsProcessor.Parsing
{
    public sealed class TraceSourceParser
        : SourceParser<Event, ParsingContext, Type>
    {
        private static readonly string TraceQueryResultName = "trace-query-result";
        private static readonly string NodeName = "node";
        private static readonly string SchemaName = "schema";
        private static readonly string RowName = "row";
        private static readonly string InfoName = "info";

        private static readonly HashSet<string> InternalSchemas = new HashSet<string>
            { "tick", "process-info", "thread-info", "kdebug-strings" };

        private static readonly EventDeserializerProvider eventDeserializerProvider = new EventDeserializerProvider(new IEventDeserializer[]
        {
            new EventDeserializer<TimeProfileEvent>(),
            new EventDeserializer<ThreadStateEvent>(),
            new EventDeserializer<DeviceThermalStateIntervalEvent>(),
            new EventDeserializer<SyscallNameMapEvent>(),
            new EventDeserializer<VirtualMemoryEvent>(),
            new EventDeserializer<SyscallEvent>(),
            new EventDeserializer<PotentialHangEvent>(),
            new EventDeserializer<CpuProfileEvent>(),
            new EventDeserializer<MetalGpuIntervalEvent>(),
            new EventDeserializer<DisplayVsyncIntervalEvent>(),
            new EventDeserializer<CountersProfileEvent>(),
            new EventDeserializer<AneHwIntervalEvent>(),
            new EventDeserializer<LifeCyclePeriodEvent>(),
            new EventDeserializer<OsSignpostEvent>(),
            new EventDeserializer<CswitchIntervalEvent>(),
            new EventDeserializer<CsrSwitchEvent>(),
            new EventDeserializer<VmFaultEvent>(),
            new EventDeserializer<DiskIoEvent>(),
            new EventDeserializer<TrRoleChangeEvent>(),
            new EventDeserializer<ProcessInfoEvent>(),
        });

        private ParsingContext context;
        private IEnumerable<IDataSource> dataSources;
        private DataSourceInfo dataSourceInfo;

        public TraceSourceParser(IEnumerable<IDataSource> dataSources)
        {
            context = new ParsingContext();

            // Store the datasources so we can parse them later
            this.dataSources = dataSources;
        }

        // The ID of this Parser.
        public override string Id => nameof(TraceSourceParser);

        // Information about the Data Sources being parsed.
        public override DataSourceInfo DataSourceInfo => this.dataSourceInfo;

        public override void ProcessSource(ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor, ILogger logger, IProgress<int> progress, CancellationToken cancellationToken)
        {
            Timestamp? firstEventTimestamp = null;
            Timestamp? lastEventTimestamp = null;
            DateTime? recordingStartUtc = null;

            // Track temp directories created from zip extraction so we can clean up
            var tempDirs = new List<string>();

            try
            {
                foreach (IDataSource dataSource in dataSources)
                {
                    if (dataSource is DirectoryDataSource directoryDataSource)
                    {
                        // .trace bundle directory (unzipped — e.g. copied from Mac as folder)
                        string dirPath = directoryDataSource.FullPath;
                        if (IsTraceBundleDirectory(dirPath))
                        {
                            ProcessTraceBundleDataSource(dirPath, ref firstEventTimestamp, ref lastEventTimestamp, dataProcessor, logger, progress, cancellationToken);
                        }
                    }
                    else if (dataSource is FileDataSource fileDataSource)
                    {
                        string path = fileDataSource.FullPath;
                        string ext = Path.GetExtension(path);

                        if (ext.Equals(".trace", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(path))
                        {
                            // .trace file is a zip archive — extract to temp and process the bundle inside
                            string extractedDir = ExtractTraceZip(path);
                            if (extractedDir != null)
                            {
                                tempDirs.Add(extractedDir);
                                // Find the .trace bundle directory inside the extracted content
                                string bundleDir = FindTraceBundleInExtracted(extractedDir);
                                if (bundleDir != null)
                                {
                                    ProcessTraceBundleDataSource(bundleDir, ref firstEventTimestamp, ref lastEventTimestamp, dataProcessor, logger, progress, cancellationToken);
                                }
                            }
                        }
                        else if (IsTraceBundleDirectory(path))
                        {
                            ProcessTraceBundleDataSource(path, ref firstEventTimestamp, ref lastEventTimestamp, dataProcessor, logger, progress, cancellationToken);
                        }
                        else
                        {
                            ProcessXmlDataSource(fileDataSource, ref firstEventTimestamp, ref lastEventTimestamp, ref recordingStartUtc, dataProcessor, progress, cancellationToken);
                        }
                    }
                }
            }
            finally
            {
                // Clean up temp directories
                foreach (var dir in tempDirs)
                {
                    try { Directory.Delete(dir, recursive: true); } catch { }
                }
            }

            long firstEventTimestampNanoseconds = firstEventTimestamp.HasValue ? firstEventTimestamp.Value.ToNanoseconds : 0;
            long lastEventTimestampnanoseconds = lastEventTimestamp.HasValue ? lastEventTimestamp.Value.ToNanoseconds : firstEventTimestampNanoseconds + 1;

            // Anchor the trace's wall-clock to the recording's actual start time (from info/summary/start-date in the xctrace XML)
            // rather than to load time. Using DateTime.UtcNow here causes WPA's session timeline to be offset by the elapsed time
            // between recording and loading, misaligning this trace with other simultaneously-collected sources
            DateTime firstEventWallClockUtc = recordingStartUtc ?? DateTime.UtcNow;
            dataSourceInfo = new DataSourceInfo(firstEventTimestampNanoseconds, lastEventTimestampnanoseconds, firstEventWallClockUtc);
        }

        /// <summary>
        /// Determine if the given path is a .trace bundle directory.
        /// A .trace bundle is any directory that contains a corespace subdirectory.
        /// </summary>
        internal static bool IsTraceBundleDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                return Directory.Exists(Path.Combine(path, "corespace"));
            }
            return false;
        }

        /// <summary>
        /// Check if a file is a zip archive by reading its magic bytes.
        /// </summary>
        internal static bool IsZipFile(string path)
        {
            if (!System.IO.File.Exists(path)) return false;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < 4) return false;
                var header = new byte[4];
                fs.Read(header, 0, 4);
                // ZIP magic: PK\x03\x04
                return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extract a .trace zip file to a temporary directory.
        /// Returns the temp directory path, or null on failure.
        /// </summary>
        private static string ExtractTraceZip(string traceZipPath)
        {
            if (!IsZipFile(traceZipPath)) return null;

            string tempDir = Path.Combine(Path.GetTempPath(), "WPA_TraceBundle_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                ZipFile.ExtractToDirectory(traceZipPath, tempDir);
                return tempDir;
            }
            catch
            {
                try { Directory.Delete(tempDir, true); } catch { }
                return null;
            }
        }

        /// <summary>
        /// After extracting a .trace zip, find the actual trace bundle directory inside.
        /// The zip may contain the bundle at the root level (corespace/ directly inside)
        /// or nested one level deep (e.g., MyTrace.trace/corespace/).
        /// </summary>
        private static string FindTraceBundleInExtracted(string extractedDir)
        {
            // Check if corespace is directly in the extracted root
            if (Directory.Exists(Path.Combine(extractedDir, "corespace")))
                return extractedDir;

            // Check one level deep for a .trace folder or any folder containing corespace
            foreach (var subDir in Directory.GetDirectories(extractedDir))
            {
                if (Directory.Exists(Path.Combine(subDir, "corespace")))
                    return subDir;
            }

            return null;
        }

        // ─── Binary .trace bundle parsing ───────────────────────────────

        private void ProcessTraceBundleDataSource(
            string traceDir,
            ref Timestamp? firstEventTimestamp,
            ref Timestamp? lastEventTimestamp,
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor,
            ILogger logger,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            var runs = FindRuns(traceDir);
            if (runs.Count == 0) return;

            for (int runIdx = 0; runIdx < runs.Count; runIdx++)
            {
                var (runName, corePath) = runs[runIdx];
                ProcessTraceRun(corePath, traceDir, ref firstEventTimestamp, ref lastEventTimestamp,
                    dataProcessor, logger, progress, cancellationToken);

                // Report progress per run
                progress?.Report((runIdx + 1) * 100 / runs.Count);
            }
        }

        private static List<(string name, string corePath)> FindRuns(string traceDir)
        {
            var corespace = Path.Combine(traceDir, "corespace");
            if (!Directory.Exists(corespace)) return new List<(string, string)>();

            return Directory.GetDirectories(corespace)
                .Select(d => Path.GetFileName(d))
                .Where(n => n.StartsWith("run") && n != "currentRun")
                .OrderBy(n => n)
                .Select(n => (n, Path.Combine(corespace, n, "core")))
                .Where(t => Directory.Exists(t.Item2))
                .ToList();
        }

        private void ProcessTraceRun(
            string corePath,
            string traceDir,
            ref Timestamp? firstEventTimestamp,
            ref Timestamp? lastEventTimestamp,
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor,
            ILogger logger,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            // Load uniquing
            var uniquingDir = Path.Combine(corePath, "uniquing");
            if (!Directory.Exists(uniquingDir)) return;
            var uniquing = new Uniquing(uniquingDir);

            // Parse table manager
            var tablesPlist = Path.Combine(corePath, "table-manager", "tables.plist");
            if (!File.Exists(tablesPlist)) return;

            var corespaceDir = Path.GetDirectoryName(Path.GetDirectoryName(corePath));
            var storesInfo = TableManager.ParseTablesPlist(tablesPlist, corespaceDir);

            // Build process name map and ref→(pid,name) map from process-info stores
            Dictionary<int, (long pid, string name)> processRefMap;
            var pidNames = NameResolver.BuildPidNameMap(storesInfo, uniquing, out processRefMap);

            // Build thread ref→(tid,pid) map from thread-info stores
            var threadRefMap = NameResolver.BuildThreadRefMap(storesInfo, uniquing);

            // Load symbol catalog for address → image+offset resolution
            var symbols = SymbolCatalog.Load(traceDir);

            // Merge external dSYMs from INSTRUMENTS_SYMBOL_PATH (semicolon-separated,
            // like _NT_SYMBOL_PATH). This lets users symbolicate images that Instruments
            // did not have symbols for at capture time (e.g. Microsoft Edge Helper).
            // Each dSYM is matched to a loaded image by UUID; symbols are placed at the
            // image's runtime __TEXT vmaddr recorded in the trace's .symbolsarchive.
            MergeExternalDsyms(symbols, logger);

            // Intern cache to deduplicate Thread/Process/String objects across events
            var internCache = new TraceBundleEventFactory.InternCache();

            // Filter to processable stores for progress tracking
            var processableStores = new List<StoreInfo>();
            foreach (var si in storesInfo)
            {
                if (string.IsNullOrEmpty(si.SchemaName)) continue;
                if (si.Side != 0) continue;
                if (InternalSchemas.Contains(si.SchemaName)) continue;
                if (!TraceBundleEventFactory.IsSchemaSupported(si.SchemaName)) continue;
                processableStores.Add(si);
            }

            // Process each store
            for (int storeIdx = 0; storeIdx < processableStores.Count; storeIdx++)
            {
                var si = processableStores[storeIdx];
                if (cancellationToken.IsCancellationRequested) break;

                var schema = new StoreSchema(si.StorePath, si.SchemaName);
                if (schema.RowSize == 0) continue;

                // Build column mapping for this schema → event type
                var mappingResult = TraceBundleEventFactory.TryBuildMapping(si.SchemaName, schema);
                if (mappingResult == null) continue;

                var (eventType, mappings) = mappingResult.Value;

                // Read rows and emit events
                var rows = BulkstoreReader.ReadRows(si.StorePath, schema);
                foreach (var row in rows)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    Event e = TraceBundleEventFactory.CreateEvent(
                        eventType, mappings, row, uniquing, pidNames, schema, internCache, symbols, processRefMap, threadRefMap);

                    dataProcessor.ProcessDataElement(e, context, cancellationToken);

                    if (firstEventTimestamp == null || firstEventTimestamp.Value > e.Timestamp)
                    {
                        firstEventTimestamp = e.Timestamp;
                    }

                    if (lastEventTimestamp == null || lastEventTimestamp.Value < e.Timestamp)
                    {
                        lastEventTimestamp = e.Timestamp;
                    }
                }

                // Report progress per store within this run
                progress?.Report((storeIdx + 1) * 100 / processableStores.Count);
            }
        }

        /// <summary>
        /// Environment variable listing directories (or single files) to search for
        /// external dSYM bundles. Semicolon-separated, matching the convention used
        /// by <c>_NT_SYMBOL_PATH</c> on Windows.
        /// </summary>
        internal const string InstrumentsSymbolPathEnvVar = "INSTRUMENTS_SYMBOL_PATH";

        /// <summary>
        /// Directory name (relative to the plugin DLL) that is always searched for
        /// external symbols in addition to any <see cref="InstrumentsSymbolPathEnvVar"/>
        /// entries. Ship symbols by dropping a <c>SymbolStore\</c> folder next to
        /// <c>InstrumentsProcessor.dll</c>.
        /// </summary>
        internal const string DefaultSymbolStoreFolderName = "SymbolStore";

        /// <summary>
        /// Resolve the auto-detected symbol store folder: <c>&lt;plugin-dir&gt;\SymbolStore</c>.
        /// Returns null when the DLL location cannot be determined or the folder does
        /// not exist.
        /// </summary>
        private static string GetPluginRelativeSymbolStorePath()
        {
            string dllPath = typeof(TraceSourceParser).Assembly.Location;
            if (string.IsNullOrEmpty(dllPath)) return null;

            string dllDir = Path.GetDirectoryName(dllPath);
            if (string.IsNullOrEmpty(dllDir)) return null;

            string candidate = Path.Combine(dllDir, DefaultSymbolStoreFolderName);
            return Directory.Exists(candidate) ? candidate : null;
        }

        /// <summary>
        /// Emit a line to both the WPA logger and a fixed log file. The file makes the
        /// output discoverable even when the user cannot easily find WPA's log surface.
        /// </summary>
        private static void EmitDiag(ILogger logger, StreamWriter file, string level, string message)
        {
            if (level == "warn") logger?.Warn(message);
            else logger?.Info(message);

            try
            {
                file?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}");
            }
            catch { /* best-effort */ }
        }

        /// <summary>
        /// Merge external dSYMs / symbol-store entries from every configured source:
        ///   1. The auto-detected <c>&lt;plugin-dir&gt;\SymbolStore</c> folder (always).
        ///   2. Any paths listed in <see cref="InstrumentsSymbolPathEnvVar"/>.
        /// Each dSYM/store entry is matched to a loaded image by UUID; unmatched
        /// entries are ignored because their runtime load address is unknown.
        /// </summary>
        private static void MergeExternalDsyms(SymbolCatalog symbols, ILogger logger)
        {
            // Assemble the effective search list: auto-detected store first, then env var.
            var searchPaths = new List<string>();

            string autoPath = GetPluginRelativeSymbolStorePath();
            if (!string.IsNullOrEmpty(autoPath)) searchPaths.Add(autoPath);

            string envVal = Environment.GetEnvironmentVariable(InstrumentsSymbolPathEnvVar);
            if (!string.IsNullOrWhiteSpace(envVal))
            {
                foreach (var raw in envVal.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = raw.Trim();
                    if (p.Length > 0) searchPaths.Add(p);
                }
            }

            if (searchPaths.Count == 0) return;

            // Best-effort side log so users can see resolution details without
            // needing to locate WPA's logger output.
            string logPath = Path.Combine(Path.GetTempPath(), "InstrumentsSymbols.log");
            StreamWriter file = null;
            try
            {
                file = new StreamWriter(logPath, append: true) { AutoFlush = true };
                file.WriteLine();
                file.WriteLine($"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} InstrumentsProcessor symbol merge (build 2026-07-14h: prefer manifest load_addr over trace shared-cache file-offset) =====");
            }
            catch { file = null; }

            try
            {
                int catalogImageCount = symbols.TextImageCount;
                EmitDiag(logger, file, "info",
                    $"[InstrumentsProcessor] Symbol sources ({searchPaths.Count}): " +
                    string.Join(" | ", searchPaths) +
                    $"  (auto-detected: '{autoPath ?? "<none>"}', {InstrumentsSymbolPathEnvVar}='{envVal ?? "<unset>"}')");
                EmitDiag(logger, file, "info",
                    $"[InstrumentsProcessor] Trace catalog holds {catalogImageCount} image(s) with __TEXT segments " +
                    "(from .symbolsarchive). Only dSYMs whose UUID matches one of these images can be applied.");

                if (catalogImageCount == 0)
                {
                    EmitDiag(logger, file, "warn",
                        "[InstrumentsProcessor] The trace bundle has no .symbolsarchive image " +
                        "entries. dSYM merging cannot infer runtime load addresses. " +
                        "Re-capture the trace on the Mac with Instruments Symbols configured, or " +
                        "run 'xctrace symbolicate' on the Mac against these dSYMs.");
                }

                int totalUnmatched = 0;
                int totalSeen = 0;
                foreach (var p in searchPaths)
                {
                    SymbolCatalog.MergeResult result;
                    try
                    {
                        result = symbols.MergeDsyms(p);
                    }
                    catch (Exception ex)
                    {
                        EmitDiag(logger, file, "warn",
                            $"[InstrumentsProcessor] Failed to load dSYMs from '{p}': {ex.Message}");
                        continue;
                    }

                    totalSeen += result.DsymImagesSeen;
                    totalUnmatched += result.UnmatchedImages;

                    if (result.DsymImagesSeen == 0)
                    {
                        EmitDiag(logger, file, "info",
                            $"[InstrumentsProcessor] No dSYM images found under '{p}'.");
                        continue;
                    }

                    EmitDiag(logger, file, "info",
                        $"[InstrumentsProcessor] dSYM merge from '{p}': " +
                        $"{result.MatchedImages}/{result.DsymImagesSeen} images matched by UUID, " +
                        $"{result.FunctionsAdded} function symbols added, " +
                        $"{result.UnmatchedImages} dSYM image(s) had no matching binary in the trace.");

                    foreach (var d in result.Details)
                    {
                        if (d.Matched)
                        {
                            EmitDiag(logger, file, "info",
                                $"[InstrumentsProcessor]   MATCH: {d.ImageName} " +
                                $"[{d.Uuid}] → {d.FunctionsAdded} symbols " +
                                $"(runtime __TEXT=0x{d.RuntimeTextBase:X}, " +
                                $"file __TEXT=0x{d.DsymTextBase:X}, " +
                                $"slide=0x{unchecked(d.RuntimeTextBase - d.DsymTextBase):X})");
                        }
                        else
                        {
                            EmitDiag(logger, file, "warn",
                                $"[InstrumentsProcessor]   NO MATCH: {d.ImageName} " +
                                $"[{d.Uuid}] — this UUID is not present in the trace's image list " +
                                "(different build than the traced binary, or Instruments did not " +
                                "record a load entry for it).");
                        }
                    }
                }

                if (totalUnmatched > 0 && totalSeen > 0)
                {
                    const int MaxImagesToLog = 500;
                    int i = 0;
                    EmitDiag(logger, file, "info",
                        $"[InstrumentsProcessor] Trace image catalog (up to {MaxImagesToLog} entries) " +
                        "— use this to verify dSYM UUIDs match:");
                    foreach (var img in symbols.EnumerateTextImages())
                    {
                        if (i++ >= MaxImagesToLog)
                        {
                            EmitDiag(logger, file, "info",
                                $"[InstrumentsProcessor]   ... ({catalogImageCount - MaxImagesToLog} more)");
                            break;
                        }
                        EmitDiag(logger, file, "info",
                            $"[InstrumentsProcessor]   {img.Uuid}  {img.ImageName}");
                    }
                }

                EmitDiag(logger, file, "info",
                    $"[InstrumentsProcessor] Symbol merge log written to '{logPath}'.");
            }
            finally
            {
                file?.Dispose();
            }
        }

        // ─── XML parsing (existing path) ────────────────────────────────

        public void ProcessXmlDataSource(FileDataSource fileDataSource, ref Timestamp? firstEventTimestamp, ref Timestamp? lastEventTimestamp, ref DateTime? recordingStartUtc,
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor, IProgress<int> progress, CancellationToken cancellationToken)
        {
            // pcwsnap files are handled by PcwSnapSourceParser
            if (fileDataSource.FullPath.EndsWith(".pcwsnap", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            XmlReader reader = GetXmlReader(fileDataSource, progress);

            // Create the XML parsing context
            XmlParsingContext xmlContext = new XmlParsingContext();

            // Read past root elements
            reader.Read();
            reader.Read();
            
            if (reader.Name == InfoName)
            {
                // Try to parse the info section first to extract counter names and the recording wall-clock anchor
                ParseInfoSection(reader, xmlContext);

                // Aggregate the earliest recording start across data sources, so multi-file loads still produce a coherent anchor.
                if (xmlContext.RecordingStartUtc.HasValue &&
                    (!recordingStartUtc.HasValue || xmlContext.RecordingStartUtc.Value < recordingStartUtc.Value))
                {
                    recordingStartUtc = xmlContext.RecordingStartUtc.Value;
                }
            }
            
            if (reader.Name != TraceQueryResultName)
            {
                reader.ReadToDescendant(TraceQueryResultName);
            }

            while (reader.Name == TraceQueryResultName)
            {
                if (reader.ReadToDescendant(NodeName))
                {
                    do
                    {
                        XmlReader subtree = reader.ReadSubtree();
                        ProcessNode(subtree, xmlContext, ref firstEventTimestamp, ref lastEventTimestamp, dataProcessor, cancellationToken);
                        subtree.Close();
                    } while (reader.ReadToNextSibling(NodeName));
                }
                
                reader.Read();
            }
        }

        public void ProcessNode(XmlReader reader, XmlParsingContext xmlContext, 
            ref Timestamp? firstEventTimestamp, ref Timestamp? lastEventTimestamp,
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor, CancellationToken cancellationToken)
        {
            if (!reader.ReadToDescendant(SchemaName))
            {
                return;
            }

            xmlContext.ObjectCache.Clear();
            Schema schema = (Schema)new XmlSerializer(typeof(Schema)).Deserialize(reader);

            if (!eventDeserializerProvider.TryGetDeserializer(schema, out IEventDeserializer eventDeserializer))
            {
                return;
            }

            XmlDocument doc = new XmlDocument();

            while (reader.Name == RowName)
            {
                XmlNode rowNode = doc.ReadNode(reader);
                Event e = eventDeserializer.Deserialize(rowNode, xmlContext, schema);

                dataProcessor.ProcessDataElement(e, context, cancellationToken);

                if (firstEventTimestamp == null || firstEventTimestamp.Value > e.Timestamp)
                {
                    firstEventTimestamp = e.Timestamp;
                }

                if (lastEventTimestamp == null || lastEventTimestamp.Value < e.Timestamp)
                {
                    lastEventTimestamp = e.Timestamp;
                }
            }
        }

        private static XmlReader GetXmlReader(FileDataSource fileDataSource, IProgress<int> progress)
        {
            // Create a stream to read the XML file that removes XML declarations, wraps everything in a single root element, and reports progress
            Stream stream = new CompositeStream(
                new List<Stream>
                {
                    new MemoryStream(Encoding.UTF8.GetBytes("<root>")),
                    new FilterStream(
                        new ProgressStream(
                            new FileStream(fileDataSource.FullPath, FileMode.Open, FileAccess.Read),
                            new FileInfo(fileDataSource.FullPath).Length,
                            progress),
                        new List<byte[]> { Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>") }),
                    new MemoryStream(Encoding.UTF8.GetBytes("</root>"))
                });

            XmlReaderSettings settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore
            };

            return XmlReader.Create(stream, settings);
        }

        private void ParseInfoSection(XmlReader reader, XmlParsingContext xmlContext)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode infoNode = doc.ReadNode(reader);
            List<string> counterNames = new List<string>();

            // Navigate through the XML structure to find Events and Formulas
            XmlNodeList eventsAndFormulasNodes = infoNode.SelectNodes(".//key[@name='Events and Formulas']/value");

            if (eventsAndFormulasNodes != null)
            {
                foreach (XmlNode valueNode in eventsAndFormulasNodes)
                {
                    if (!string.IsNullOrWhiteSpace(valueNode.InnerText))
                    {
                        counterNames.Add(valueNode.InnerText.Trim());
                    }
                }
            }

            xmlContext.SetCounterNames(counterNames);

            // Extract the recording's wall-clock start time (ISO-8601 with offset) from info/summary/start-date.
            // This anchors WPA's wall-clock for the trace to when it was *recorded*, not when it was *loaded*.
            XmlNode startDateNode = infoNode.SelectSingleNode(".//summary/start-date");
            if (startDateNode != null && !string.IsNullOrWhiteSpace(startDateNode.InnerText))
            {
                if (DateTimeOffset.TryParse(startDateNode.InnerText.Trim(), out DateTimeOffset startDateOffset))
                {
                    xmlContext.SetRecordingStartUtc(startDateOffset.UtcDateTime);
                }
            }
        }
    }
}