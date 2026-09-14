
# Microsoft Performance Tools Apple

> This repo contains Apple Analysis tools built with the [Microsoft Performance Toolkit SDK](https://github.com/microsoft/microsoft-performance-toolkit-sdk).

> Tools are built with open source .NET Core and can be run on the cmd-line or in the WPA GUI. All the logs that are supported are open source. 

## Tracing supported:

There are two options for capturing a trace on MacOS. The first is a UI-based trace viewer and capture tool called Instruments, while the second is a command-line tool called xctrace.
### [Instruments (formerly Xray)](https://forums.developer.apple.com/forums/tags/instruments) 
Instruments is a standalone application that comes with Xcode, and can be used independently or in conjunction with Xcode. Instruments shows a time line displaying any event occurring in the application, such as CPU activity variation, memory allocation, and network and file activity, together with graphs and statistics.
#### Install Instruments:
-   [Install Xcode on your Mac device]((https://developer.apple.com/support/xcode/)) 
-   Open the Instruments application from the menu bar at Xcode -> Open Developer Tool -> Instruments
![xcode](https://github.com/user-attachments/assets/264b77d6-7468-42dd-9cd3-925ace1663d9)

####  Capture a Trace with Instruments:
-   To start capturing a trace: Open Instruments -> File -> New
-   Choose your profile and the select Choose.

  ![543px-Available_profiles](https://github.com/user-attachments/assets/ea6bc1b6-90a0-4221-ae57-b174ebec15f6)

#### Configure Symbol Path in Instruments:

If it is necessary to load additional symbols, please refer to this section.
-   To enable symbols from 3rd party applications, Open Instruments -> Settings...
-   Choose Symbols and add the relevant paths to your local symbol files

![symbol_load](https://github.com/user-attachments/assets/99e33ce1-c0ca-4ce0-9811-74b31da2b091)

-   Instruments can save symbol information with the trace. The direct `.trace` loader can resolve these bundled symbols on Windows; missing names still require matching external symbols.

### Direct Trace Symbol Resolution

Open a `.trace` directory (or its `open.creq` marker) or a ZIP bundle named `.trace` with the direct loader. XML import remains supported and unchanged.

The loader reads runtime image mappings from the compressed `form.template` and version-7 `.symbolsarchive` files from `symbols/stores`. It resolves addresses using the recorded run, process, user/kernel address space, image UUID, architecture, and load lifetime. Shared-cache images use the cache UUID and recorded cache load address. No Mac-side XML export is required for this path.

Image signature start/end fields are treated as Mach load timestamps, with `start <= sample < end`; `0` and `Int64.MaxValue` are treated as unbounded. This interpretation and kernel symbol coordinates are working assumptions: user-space resolution has been checked against recorded stacks, while kernel resolution and unload/reload behavior have synthetic tests but still need real reference captures. Unsupported supplemental history or ambiguous ownership stays unresolved instead of using a guessed slide.

The stack keeps its original runtime addresses. Caller frames are looked up using the byte before the recorded return address so a function-end boundary does not select the next function; the leaf instruction address is used unchanged. Missing or placeholder names display the module and offset when ownership is known. A missing symbol archive does not mean the module was absent. Supported callstack types include `XRBacktraceTypeID`, tagged backtraces, and the observed 4-, 5-, and 8-element `XRCoreProfileCallstackTypeID` wrappers. Array references are resolved through the stored block index, excluding allocation padding.

`NA` can denote an absent stack or the end of a hierarchy. `Unknown!0x...` means no unambiguous image mapping was available, including recorded zero-address unwind gaps. `module+0x...` means ownership is known but no usable function name was found. These displays do not distinguish missing capture data from an unsupported encoding; use the diagnostic and parser warnings when investigating them.

The **CSR Context Switch** table includes expandable **Switch-On Stack** and **Switch-On Kernel Stack** columns. Stacks are joined from raw core-profile records only when event time, thread reference, CPU, switch direction, and instruction/cycle counters match exactly. The interval's stack belongs to its switch-on event; it does not describe every instruction executed during that interval. The **CSR Context Switch (Raw)** table exposes each switch-on/off event's own **Stack** and **Kernel Stack**. Missing or conflicting stack records remain empty; the existing interval durations and counter calculations are unchanged. Both stack columns support inversion using the usual column variants.

For visual verification, open `5Page_5Tabs_20_NoGC_NoPurge/20260707_155842_snap001_TabSwitchPaint_NFL1.trace/open.creq` with the latest plugin, add **CSR Context Switch**, filter **Process ID** to **722**, and expand **Switch-On Stack**. The switch-on at `4.381683583` seconds has 37 frames, including `libdispatch.dylib!__dispatch_client_callout`. Four frames in that stack have only module/offset names. Kernel stacks are empty in this capture. The raw table shows the same stack at the exact event timestamp.

Optional external dSYMs, Mach-O files, and `manifest.json`/`symbols.nm` stores can be supplied through `INSTRUMENTS_SYMBOL_PATH` (semicolon-separated) or the `SymbolStore` directory next to the plugin. Recorded mappings take precedence over external `load_addr`; external symbols must match the image UUID and architecture. Valid bundled names take precedence, and external names can fill missing or placeholder entries. Archives with incompatible layouts or corrupt bounds produce warnings rather than guessed names.

To inspect one recorded stack using the production resolver:

```powershell
dotnet run --project DiagTrace/DiagTrace.csproj -c Release -- <trace-path> --run 1 --pid 722 --stack-ref 10 --limit 1
```

Omit `--pid` and `--stack-ref` to sample available stacks; add `--kernel` to inspect kernel stacks. The diagnostic reports named, module-only, missing-map/archive, and ambiguous results separately. It does not infer missing CPU samples or construct new event tables. To launch WPA against a dependency-complete development build without using an older installed plugin, use `wpa.exe -nodefault -addsearchdir <plugin-directory> -i <trace-directory>/open.creq`.

For the optional real-trace regression test, set `INSTRUMENTS_TEST_TRACE` to the `20260707_155842_snap001_TabSwitchPaint_NFL1.trace` fixture and run `dotnet test InstrumentsProcessorTests/InstrumentsProcessorTests.csproj -c Release --filter FullyQualifiedName~SymbolResolutionTests`. Without that local fixture, the real-trace test is explicitly skipped; all synthetic tests still run.

### [xctrace](https://keith.github.io/xcode-man-pages/xctrace.1.html)

xctrace is used to record, import, export, and symbolicate Instruments' .trace files via the command line.

To use xctrace open a Terminal Window:  `'xctrace help [command]'`

To capture a trace with a Time Profile template:

`xctrace record --all-processes --template 'Time Profiler' --time-limit 5s`

For more info about xctrace please visit:  [xctrace documentation](https://keith.github.io/xcode-man-pages/xctrace.1.html)

## Capture Trace on MacOs:
- Use Instruments or xctrace to capture the trace. Note that we support some of tables as shown above.
- Download the [Trace Export script](https://github.com/microsoft/Microsoft-Performance-Tools-Apple/blob/main/trace-export.sh) to convert the captured trace into a compatible format for use with our plugin.
- Open a Terminal and go to your Download folder and run `chmode +x trace-export.sh`
- Run `./trace-export.sh --input <tracefile.trace>`

![485px-Terminal-exporter](https://github.com/user-attachments/assets/e2119700-68f8-44cf-9e4d-dc8dfb612dee)

## Install Microsoft Performance Toolkit Apple:
- Download the latest WPA UI. You can download it from [Windows Performance Analyzer (Preview)](https://www.microsoft.com/en-us/p/windows-performance-analyzer-preview/9n58qrw40dfw). 
- Download the latest Microsoft Performance Toolkit Apple [Releases](https://github.com/microsoft/Microsoft-Performance-Tools-Apple/releases)
- Extract the Microsoft-Performance-Tools-Apple.zip
- Open WPA UI and click Install Plugin
![photo_2024-12-11_10-51-56](https://github.com/user-attachments/assets/5af47401-44e2-4f03-b0fe-59da31baa25e)
- Browse to "%ExtractedFolder\Microsoft-Performance-Tools-Apple\Microsoft-Performance-Tools-Apple\MicrosoftPerfToolkitAddins\PTIX\Microsoft.Performance.Toolkit.Plugins.InstrumentsProcessor-1.0.1.ptix"

- Copy captured and exported trace <tracefile.xml> from you Mac device to your Windows machine and open it with the WPA.

![749px-IosPlugin](https://github.com/user-attachments/assets/dc0e8c71-e424-4303-8f48-bf0159df1b3e)

## How to capture a Trace and Open in WPA:

![](https://github.com/microsoft/Microsoft-Performance-Tools-Apple/blob/main/doc/IoSTracing_example.gif)


## Examples:

Capture a trace with 1 million instruction samples and attach Cycle Delta to that event. Also, add Time Profile to sample the running thread on the CPU every 1 ms. This profile can be used to calculate frequency, as well as instruction and cycle-level analysis.

First, open Instruments and create a new session, then choose Blank.

<img width="789" alt="1" src="https://github.com/user-attachments/assets/7af3ff47-65fe-4fc1-a66a-22369116f84a" />


From the open window, select +Instruments (see step 2) and find CPU Counters and Time Profile from the opened menu (see step 3).


<img width="818" alt="2" src="https://github.com/user-attachments/assets/ab99ead0-9c59-4074-8f84-39dfab3b977e" />


For CPU Counters, use Event-Based Sampling to collect a sample every 1 million instructions (see step 2) and attach Cycle (see step 3) to that. Then, in Record Settings, choose Deferred (see step 4).


<img width="1500" alt="3" src="https://github.com/user-attachments/assets/252f036a-810a-481d-9795-621222b93cdc" />

Capture the trace and save it. Then run:
`./trace-export.sh --input <tracefile.trace>`
to export it for WPA.

You can find the template here: 
`TraceTemplate\CPUCounterWithTimeProfile.tracetemplate`


# Developer Prerequisites

## Runtime prereqs
- [.NET Core Runtime 3.1.x](https://dotnet.microsoft.com/download/dotnet-core/3.1)

## Dev prereqs
- [.NET Core SDK 3.1.x](https://dotnet.microsoft.com/download/dotnet-core/3.1)
- [Visual Studio](https://visualstudio.microsoft.com/), [VSCode](https://visualstudio.microsoft.com/), or your favorite editor!

# Download
- **For plugins Download** see [Releases](https://github.com/microsoft/Microsoft-Performance-Tools-Apple/releases)

- **NuGet Pkgs** see [PerformanceToolkitPlugins](https://www.nuget.org/profiles/PerformanceToolkitPlugins)

- **(Windows Only GUI - Install)** Using the WPA GUI to load these tools as plugins
  - Download the latest Store [Windows Performance Analyzer (Preview)](https://www.microsoft.com/en-us/p/windows-performance-analyzer-preview/9n58qrw40dfw)

# How to run the tools
The tools can be run in several modes:

- **Cross-platform with .NET Core** (Any OS that .NET Core supports)
  - Used as a library to process traces / logs programatically in a .NET Core language like C#
- **(Windows Only - Run)** Using the WPA GUI to load these tools as plugins
  - WPA needs to be told where to find these additional plugins. 
  - In Command Prompt with -addsearchdir and -i trace file:
      ```dos
        wpa.exe -addsearchdir %USERPROFILE%\Downloads\Microsoft-Performance-Tools-Apple\Microsoft-Performance-Tools-Apple\MicrosoftPerfToolkitAddins\ -i c:\PATH\TO\instruments-trace.xml
     ```
  - OR with Env Variable to pick file from UI
       ```dos
        SET WPA_ADDITIONAL_SEARCH_DIRECTORIES=%USERPROFILE%\Downloads\Microsoft-Performance-Tools-Apple\Microsoft-Performance-Tools-Apple\MicrosoftPerfToolkitAddins\
        wpa.exe
      ```
  - Optional Troubleshooting - Verify that this WPA version supports plugins
    - In Command Prompt - Example:
        ```dos
        wpa.exe /?
        "C:\Program Files\WindowsApps\Microsoft.WindowsPerformanceAnalyzerPreview_10.0.22504.0_x64__8wekyb3d8bbwe\10\Windows Performance Toolkit\wpa.exe" /?
        ```
    - Verify that these 2 command line WPA options are supported:
      - OPTIONS: **-addsearchdir PATH**. Adds a directory path to the plugin search path. ....
      - ENVIRONMENT VARIABLES: **WPA_ADDITIONAL_SEARCH_DIRECTORIES** - A semicolon (;) delimited list of additional directories to search for plugins. Equivalent to the -addsearchdir option.
- **(Windows) Command-line dumping to a text format** based on the WPA UI (say CSV) (wpaexporter.exe)
    ```dos
    "C:\Program Files\WindowsApps\Microsoft.WindowsPerformanceAnalyzerPreview_10.0.22504.0_x64__8wekyb3d8bbwe\10\Windows Performance Toolkit\wpaexporter.exe" -addsearchdir PLUGIN_FOLDER -i traceFile
    ```
# How do I use WPA in general?
If you want to learn how to use the GUI UI in general see [WPA MSDN Docs](https://docs.microsoft.com/en-us/windows-hardware/test/wpt/windows-performance-analyzer)

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
