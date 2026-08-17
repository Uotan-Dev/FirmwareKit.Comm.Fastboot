# FirmwareKit.Comm.Fastboot

[![NuGet Version](https://img.shields.io/nuget/v/FirmwareKit.Comm.Fastboot.svg)](https://www.nuget.org/packages/FirmwareKit.Comm.Fastboot/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A high-performance C# implementation of the Android Fastboot protocol, aligned with the latest
AOSP (Android Open Source Project) logic. Designed for firmware flashing, device management, and
automation. Part of the **FirmwareKit** ecosystem.

## Features

- **AOSP Aligned**: Command framing, response parsing (`OKAY/FAIL/INFO/DATA/TEXT`), `download:%08x`
  size fields, TCP/UDP handshakes, and sparse chunked flashing follow the official Google
  `fastboot` implementation.
- **Multi-Transport**:
  - USB — via `FirmwareKit.Comm` (WinUSB / Linux / macOS native + libusb-dotnet backends)
  - Fastboot over TCP (`FB01` handshake, AOSP framing)
  - Fastboot over UDP (AOSP UDP protocol: header / sequence / retransmission)
- **Device Discovery**: interface-based matching (`0xff/0x42/0x03`, identical to AOSP), so any
  device exposing a standard fastboot interface is found regardless of VID/PID — Android phones,
  HarmonyOS bootloaders, U-Boot dev boards, Kindle, etc. An optional VID/PID whitelist
  (`devices.json`) covers legacy devices with non-standard interfaces.
- **Capability Probing**: `ProbeCapabilities()` detects the device feature set and degrades
  gracefully for command-subset devices (U-Boot boards: no logical partitions, no CRC, no
  userspace fastboot).
- **Rich Command Set**: flash / flashall / update / erase / format / fetch / upload / boot /
  logical partitions / super update / vbmeta flags / snapshot-update / gsi / flashing lock
  & unlock / oem (incl. U-Boot `ucmd`/`acmd`/`run`) / signature / sideload / stage / get_staged.
- **Multi-Targeting**: `netstandard2.0`, `net8.0`, `net10.0`.
  Native AOT and trimming compatible.

## Installation

```bash
dotnet add package FirmwareKit.Comm.Fastboot
```

> Latest published version on NuGet: **1.1.0**.
> See the dependency table in [NuGet Dependencies](#nuget-dependencies) for the published
> ecosystem packages this library builds upon.

## Quick Start

### 1. Connect over USB (automatic discovery)

```csharp
using FirmwareKit.Comm.Fastboot;
using FirmwareKit.Comm.Fastboot.Usb;

// Enumerate fastboot devices (standard interface 0xff/0x42/0x03).
var devices = UsbManager.GetAllDevices();
if (devices.Count == 0) throw new Exception("no fastboot device found");

using var driver = new FastbootDriver(devices[0]);   // takes ownership of the transport
```

Optional: enable the VID/PID whitelist fallback and/or load an external device manifest:

```csharp
UsbManager.MatchMode = UsbMatchMode.InterfaceOrKnownVidPid;   // interface first, whitelist fallback
UsbManager.LoadDeviceProfilesFromFile("devices.json");        // optional external manifest
```

Or wait for a specific device by serial number:

```csharp
using var driver = FastbootDriver.WaitForDevice(UsbManager.GetAllDevices, "serial123", timeoutSeconds: 30);
```

### 2. Connect over TCP / UDP (network fastboot: U-Boot, fastbootd)

```csharp
using FirmwareKit.Comm.Fastboot.Network;

using var tcp = new TcpTransport("192.168.1.10", 5554);       // FB01 handshake is automatic
using var driver = new FastbootDriver(tcp);
```

```csharp
using var udp = new UdpTransport("192.168.1.10", 5554);
using var driver = new FastbootDriver(udp);
```

### 3. Query and flash

```csharp
driver.ProbeCapabilities();                     // optional: cache device features for graceful degradation
string version = driver.GetVar("version");      // getvar:version
var all = driver.GetVarAll();                   // getvar:all -> dictionary

driver.FlashImage("boot", @"C:\images\boot.img");              // auto sparse chunking / logical resize
driver.FlashImage("system", stream);                           // flash from a stream
driver.FlashAll(@"C:\images\product_out", wipe: false);        // flashall (fastboot-info.txt aware)
driver.FlashVbmeta("vbmeta", @"C:\images\vbmeta.img", disableVerity: true);
driver.ErasePartition("userdata");
driver.Fetch("boot", @"C:\out\boot.img");                      // fetch partition image
driver.Reboot("bootloader");
```

Progress and events:

```csharp
driver.ReceivedFromDevice += (s, e) => Console.WriteLine($"INFO: {e.NewInfo}");
driver.DataTransferProgressChanged += (s, p) => Console.WriteLine($"progress: {p.Item1}/{p.Item2}");
driver.CommandCompleted += (s, e) => Console.WriteLine($"{e.Command} -> {e.Response.Result}");
```

## CLI (FirmwareKit.Comm.Fastboot.Cli)

The repository also ships a console front-end (`FirmwareKit.Comm.Fastboot.Cli`), published as a
Native AOT binary named **`fastboot`** so it stays a drop-in replacement for the official tool:

```bash
fastboot devices                          # list devices (SERIAL\tfastboot, official format)
fastboot -i 0x2207 devices                # filter by USB vendor id
fastboot -s tcp:192.168.1.10:5554 getvar all   # network fastboot
fastboot flash boot boot.img
fastboot flashall
fastboot fetch boot out.img
fastboot update-super super_empty.img
fastboot shutdown
fastboot oem ucmd 'mmc dev 0'             # U-Boot boards (CONFIG_FASTBOOT_OEM_RUN)
```

## NuGet Dependencies

This library is built on the following **published** FirmwareKit ecosystem packages:

| Package | Purpose | Latest published |
|---|---|---|
| [FirmwareKit.Comm](https://www.nuget.org/packages/FirmwareKit.Comm) | Cross-platform USB enumeration & sessions (WinUSB/Linux/macOS/libusb) | 1.2.1 |
| [FirmwareKit.Lp](https://www.nuget.org/packages/FirmwareKit.Lp) | Android super logical-partition metadata (parse / build / export) | 1.0.0 |
| [FirmwareKit.Sparse](https://www.nuget.org/packages/FirmwareKit.Sparse) | Android sparse image parsing, random access, resparsing, CRC | 1.1.0 |

Other **published** ecosystem packages you may find useful alongside fastboot:

| Package | Purpose |
|---|---|
| [FirmwareKit.AVB](https://www.nuget.org/packages/FirmwareKit.AVB) | VBMeta / AVB parsing, descriptors, footer, AB flow |
| [FirmwareKit.CPIO](https://www.nuget.org/packages/FirmwareKit.CPIO) | CPIO / TAR archive read / write / verify |
| [FirmwareKit.Comm.EDL](https://www.nuget.org/packages/FirmwareKit.Comm.EDL) | Qualcomm EDL mode (Sahara / Firehose) communication |

> Only packages published on NuGet are listed above. Additional FirmwareKit format parsers that
> are not yet published on NuGet are intentionally omitted from this document.

## Compatibility Notes

- **Protocol level**: byte-compatible with AOSP `fastboot` (framing, sizes, handshakes, sparse,
  CRC); the official client and this library can talk to the same device interchangeably.
- **Discovery**: standard interface `0xff/0x42/0x03` by default; optional VID/PID whitelist for
  legacy devices. Use `-i <VID>` in the CLI to filter by vendor id.
- **Non-Android devices** (U-Boot boards, HarmonyOS bootloaders, Kindle, …): discovered via the
  interface rule; advanced features degrade automatically via `ProbeCapabilities()`.

## Requirements

- .NET Standard 2.0: compatible with Unity and .NET Framework 4.6.1+.
- .NET 8+: fully supports Native AOT and trimming.

## Tests

```bash
dotnet test FirmwareKit.Comm.Fastboot.Tests/FirmwareKit.Comm.Fastboot.Tests.csproj
```

Coverage: protocol response parsing, download / sparse / CRC, TCP & UDP transports, boot image
builder, capability probing, device profile loading, and AOSP driver parity.

## Credits

- Based on AOSP `system/core/fastboot` (the reference implementation; see the audit notes in
  this repository's commit history for protocol parity details).
- Part of the **FirmwareKit** ecosystem by **uotan-Dev**.

## License

This project is licensed under the [MIT License](LICENSE).
