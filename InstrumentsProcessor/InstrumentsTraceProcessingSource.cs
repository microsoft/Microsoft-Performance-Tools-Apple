// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;
using System.IO;

namespace InstrumentsProcessor
{
    /// <summary>
    /// Handles .trace zip files (compressed bundles) via WPA file dialog.
    /// </summary>
    [ProcessingSource(
       "{a3f1c8e7-6d4b-4e2a-9c5f-8b7d6e3a1f09}",
       "iOS Trace Processor (Zip)",
       "Process compressed .trace files captured on Apple device")]
    [FileDataSource(
       ".trace",
       "Apple Instruments trace files (compressed)")]
    public class InstrumentsTraceZipProcessingSource : ProcessingSource
    {
        public InstrumentsTraceZipProcessingSource() : base()
        {
        }

        protected override bool IsDataSourceSupportedCore(IDataSource dataSource)
        {
            if (!(dataSource is FileDataSource fileDataSource))
            {
                return false;
            }

            string path = fileDataSource.FullPath;
            if (!Path.GetExtension(path).Equals(".trace", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return File.Exists(path);
        }

        protected override ICustomDataProcessor CreateProcessorCore(
            IEnumerable<IDataSource> dataSources,
            IProcessorEnvironment processorEnvironment,
            ProcessorOptions options)
        {
            var parser = new TraceSourceParser(dataSources);

            return new TraceProcessor(
                parser,
                options,
                this.ApplicationEnvironment,
                processorEnvironment);
        }

        public override ProcessingSourceInfo GetAboutInfo()
        {
            return ProcessingSourceInfoHelper.GetAboutInfo();
        }
    }

    /// <summary>
    /// Handles .trace directory bundles (unzipped macOS bundles on Windows).
    /// Open via: wpa.exe -i "D:\SampleTrace.trace"
    /// Or drag the .trace folder onto WPA.
    /// </summary>
    [ProcessingSource(
       "{b4e2d9f1-7a3c-4f5e-8d6b-9c1a0e2f3d47}",
       "iOS Trace Processor (Directory)",
       "Process .trace directory bundles — use CLI: wpa -i path.trace")]
    [DirectoryDataSource(
       "Apple Instruments trace bundles (.trace directories)")]
    public class InstrumentsTraceDirProcessingSource : ProcessingSource
    {
        public InstrumentsTraceDirProcessingSource() : base()
        {
        }

        protected override bool IsDataSourceSupportedCore(IDataSource dataSource)
        {
            if (!(dataSource is DirectoryDataSource directoryDataSource))
            {
                return false;
            }

            return TraceSourceParser.IsTraceBundleDirectory(directoryDataSource.FullPath);
        }

        protected override ICustomDataProcessor CreateProcessorCore(
            IEnumerable<IDataSource> dataSources,
            IProcessorEnvironment processorEnvironment,
            ProcessorOptions options)
        {
            var parser = new TraceSourceParser(dataSources);

            return new TraceProcessor(
                parser,
                options,
                this.ApplicationEnvironment,
                processorEnvironment);
        }

        public override ProcessingSourceInfo GetAboutInfo()
        {
            return ProcessingSourceInfoHelper.GetAboutInfo();
        }
    }

    /// <summary>
    /// Handles .trace directory bundles opened via their marker file.
    /// On Windows, .trace bundles appear as folders. The user navigates inside
    /// and selects "open.creq" — this plugin detects the parent .trace bundle
    /// and processes it.
    /// </summary>
    [ProcessingSource(
       "{c5f3e0a2-8b1d-4c6f-9e7a-0d2b4f6a8c13}",
       "iOS Trace Processor (Bundle)",
       "Navigate into a .trace folder and open 'open.creq' to load the trace")]
    [FileDataSource(
       ".creq",
       "Apple Instruments trace bundle marker (open.creq inside .trace folder)")]
    public class InstrumentsTraceMarkerProcessingSource : ProcessingSource
    {
        public InstrumentsTraceMarkerProcessingSource() : base()
        {
        }

        protected override bool IsDataSourceSupportedCore(IDataSource dataSource)
        {
            if (!(dataSource is FileDataSource fileDataSource))
            {
                return false;
            }

            // Only accept open.creq files that live inside a trace bundle
            string path = fileDataSource.FullPath;
            if (!Path.GetFileName(path).Equals("open.creq", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check that the parent directory is a trace bundle (has corespace/)
            string parentDir = Path.GetDirectoryName(path);
            return parentDir != null && TraceSourceParser.IsTraceBundleDirectory(parentDir);
        }

        protected override ICustomDataProcessor CreateProcessorCore(
            IEnumerable<IDataSource> dataSources,
            IProcessorEnvironment processorEnvironment,
            ProcessorOptions options)
        {
            // Redirect: replace the .creq FileDataSource with the parent trace bundle directory
            var traceBundleSources = new List<IDataSource>();
            foreach (var ds in dataSources)
            {
                if (ds is FileDataSource fds)
                {
                    string parentDir = Path.GetDirectoryName(fds.FullPath);
                    if (parentDir != null && TraceSourceParser.IsTraceBundleDirectory(parentDir))
                    {
                        traceBundleSources.Add(new DirectoryDataSource(parentDir));
                        continue;
                    }
                }
                traceBundleSources.Add(ds);
            }

            var parser = new TraceSourceParser(traceBundleSources);

            return new TraceProcessor(
                parser,
                options,
                this.ApplicationEnvironment,
                processorEnvironment);
        }

        public override ProcessingSourceInfo GetAboutInfo()
        {
            return ProcessingSourceInfoHelper.GetAboutInfo();
        }
    }

    internal static class ProcessingSourceInfoHelper
    {
        public static ProcessingSourceInfo GetAboutInfo()
        {
            return new ProcessingSourceInfo
            {
                Owners = new[]
                {
                    new ContactInfo
                    {
                        Name = "Author Name: Hani Nemati, Benjamin Galindo-Navarro",
                        Address = "Author Email",
                        EmailAddresses = new[]
                        {
                            "hanemati@microsoft.com",
                            "benjaming@microsoft.com"
                        },
                    },
                },
                LicenseInfo = new LicenseInfo()
                {
                    Name = "MIT",
                    Uri = "https://mit-license.org/",
                    Text = "The MIT License (MIT)"
                },
                ProjectInfo = new ProjectInfo()
                {
                    Uri = "https://github.com/microsoft/Microsoft-Performance-Tools-Apple"
                },
                CopyrightNotice = $"Copyright (C) {DateTime.Now.Year}",
                AdditionalInformation = null,
            };
        }
    }
}