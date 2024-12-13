
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

-   Note: Symbol decoding is performed on the Mac when the trace is captured, and cannot be done on Windows later

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
- Browse to "%ExtractedFolder\Microsoft-Performance-Tools-Apple\Microsoft-Performance-Tools-Apple\MicrosoftPerfToolkitAddins\PTIX\Microsoft.Performance.Toolkit.Plugins.InstrumentsProcessor-1.0.0.ptix"

- Copy captured and exported trace <tracefile.xml> from you Mac device to your Windows machine and open it with the WPA.

![749px-IosPlugin](https://github.com/user-attachments/assets/dc0e8c71-e424-4303-8f48-bf0159df1b3e)

## How to capture a Trace and Open in WPA:

![](https://github.com/microsoft/Microsoft-Performance-Tools-Apple/blob/main/doc/IoSTracing_example.gif)

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
