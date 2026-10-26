# Fanatec CSL DD FFB fix — validated on PadForge 5.0 and 5.1

Current source: PadForge v5.1.0 plus the local CSL DD fix, file version
5.1.0.1. The upstream base commit is
`97182cb60eb3b158f90c9d6eb5e3b392f30db8b8`.

This repository began as an independent private snapshot of upstream
v5.0.0. It has now been updated to the validated 5.1 source and made public
at the owner's request. Attribution and the upstream license are retained.
The official fork used for contributions is
[rodolfogallo/PadForge](https://github.com/rodolfogallo/PadForge).

## Problem and evidence

The CSL DD (`VID_0EB7/PID_0020`) was recognized, but produced no force
feedback in PadForge or in-game. FFB worked in the Fanatec application.
Windows capability inspection found these output reports:

| Collection | Report ID | Total bytes |
|---|---|---|
| COL01 | 0x01 | 8 |
| COL02 | 0x02 | 8 |
| COL03 | 0xFF | 64 |

`FanatecRawHidWriter.BuildWheelReport` assumed ID zero, which the Windows
HID parser rejected for all three collections. For COL01/COL02, the buffer
length was already correct. `_appliedWheelFfb` also recorded requested
effects without checking write success, so identical requests could be
omitted after a failed delivery.

## Correction

- Discover the command endpoint independently of the input collection,
  constrained to the same PnP parent and actual device attributes.
- Validate the vendor output field and its report ID/length; prefer the
  primary eight-byte channel. Do not guess between ambiguous endpoints or
  use COL03's unrelated 64-byte protocol.
- Cache only complete successful writes, retry failures with bounded
  backoff, and invalidate applied state after output recovery.
- Configure Fanatec range/autocentring before the force, preserving v5.1's
  option to leave rotation range to the wheel.
- Disable all five effect slots on stop and add rate-limited diagnostics.
- Provide a metadata-only `--fanatec-hid-probe <output.json>` mode.

## Validation

**The hardware owner confirmed that patched versions 5.0.0.1 and 5.1.0.1
both restore FFB in the GUI and in-game.**

The 5.1 port passed 58 targeted tests covering endpoint selection, report
framing, failure/retry caching, stop behavior, pedal/shutdown regressions
and v5.1 rotation-range behavior. These tests did not send motor commands.
Compatibility with other Fanatec models has not been established.

## Build and use

Windows x64 with .NET SDK 10:

```powershell
dotnet test PadForge.Tests/PadForge.Tests.csproj -c Release --filter 'FullyQualifiedName~FanatecCslDdOutputTests|FullyQualifiedName~WheelFfbWriteCacheTests|FullyQualifiedName~AuditJuly26RoundTwentyThreeTests|FullyQualifiedName~EngineStopRumbleTests|FullyQualifiedName~WheelRange'
dotnet publish PadForge.App/PadForge.App.csproj -c Release -r win-x64 -o artifacts/csl-dd-win-x64
```

Use a backup or a separate folder for testing. Assign one Fanatec input
collection to the desired virtual slot; device recognition alone does not
create an association. Automatic upstream updates can replace the local
fix. No personal settings XML, machine logs or crash dumps are included.
