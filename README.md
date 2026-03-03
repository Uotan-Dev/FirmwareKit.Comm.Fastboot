# SharpFastboot (FirmwareKit.Comm.Fastboot)

[![NuGet Version](https://img.shields.io/nuget/v/FirmwareKit.Comm.Fastboot.svg)](https://www.nuget.org/packages/FirmwareKit.Comm.Fastboot/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A high-performance C# implementation of the Android Fastboot protocol, aligned with the latest AOSP (Android Open Source Project) logic. Designed for firmware flashing, device management, and automation.

## Features

- **AOSP Aligned**: Core logic (Download, Erase, Flash, etc.) strictly follows the official Google `fastboot` implementation.
- **Multi-Transport Support**:
  - USB (Windows WinUSB & Legacy support)
  - Fastboot over TCP
  - Fastboot over UDP
- **Multi-Targeting**: Supports `.NET Standard 2.0/2.1`, `.NET 6.0`, `.NET 8.0`, and above.
- **Modern C# 12 Syntax**: Optimized for performance and readability while maintaining backward compatibility.

## Installation

Install via NuGet:

```bash
dotnet add package FirmwareKit.Comm.Fastboot
```

## Quick Start

### 1. Initialize Fastboot Utility

```csharp
using FirmwareKit.Comm.Fastboot;
using FirmwareKit.Comm.Fastboot.Usb.Windows;

// Initialize USB Transport (Example for Windows WinUSB)
var transport = new WinUSBDevice { DevicePath = "..." }; 
var fastboot = new FastbootUtil(transport);

// Get variable
var version = fastboot.GetVar("version");
Console.WriteLine($"Fastboot Version: {version}");
```

### 2. Flashing a Partition

```csharp
// Simple flash command
fastboot.Flash("boot", "path/to/boot.img");

// Alignment with AOSP handshaking logic ensure stability
fastboot.Erase("system");
```

## Developer Guide

### Cross-Platform Compatibility
- **.NET Standard 2.0**: Compatible with Unity and older .NET Framework 4.6.1+.
- **Modern .NET**: Fully supports Native AOT and trimming in .NET 8+.

## Credits
- Based on AOSP `system/core/fastboot`
- Part of the **FirmwareKit** ecosystem by **uotan-Dev**

## License
This project is licensed under the [MIT License](LICENSE).
