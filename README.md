# Microsoft Performance Tools Linux / Android

> This repo contains Apple Analysis tools built with the [Microsoft Performance Toolkit SDK](https://github.com/microsoft/microsoft-performance-toolkit-sdk).

> Tools are built with open source .NET Core and can be run on the cmd-line or in the WPA GUI. All the logs that are supported are open source. 

> Tracing supported: 
- [Instruments](https://forums.developer.apple.com/forums/tags/instruments) (Kernel CPU scheduling, Processes, Threads, Syscalls, etc)

# Prerequisites

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

# How to capture a trace or logs
- TODO: fill in

# How to load the logs in the UI

- InstrumentsProcessor
  - WPA -> Open -> (Select Instruments xml trace file)

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