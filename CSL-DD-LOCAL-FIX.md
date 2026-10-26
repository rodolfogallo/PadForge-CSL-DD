# CSL DD local fix for PadForge 5.1.0

Base: upstream tag `v5.1.0`, commit `97182cb60eb3b158f90c9d6eb5e3b392f30db8b8`.
Local file version: `5.1.0.1`.

This ports the CSL DD correction previously validated by the hardware owner
with PadForge 5.0.0.1 in the GUI and in-game. The original 5.1 release still
uses report ID 0 for native Fanatec wheel output and records failed writes
as applied in its force-feedback cache.

The port resolves the 0EB7:0020 command collection from Windows HID
capabilities and the input collection's PnP parent. It uses the validated
report ID and eight-byte report size, preferring the primary command
collection. Only complete successful writes are cached. Failed deliveries
retry with backoff, and output handle recovery invalidates applied state.
Effect stops clear all five native slots. Diagnostics record endpoint,
report ID, lengths, completion status and skipped/retried delivery.

The v5.1 rotation-range behavior is preserved: when RotationRangeEnabled is
off, neither the new Fanatec initialization path nor the other native
writers send a rotation-range command. The existing v5.1 RangeLimits and
RealHidAccess test-host write guards are retained.

The additional `--fanatec-hid-probe <output.json>` mode reads HID metadata
with access-zero handles and exits before normal app/engine startup.

Regression validation uses FanatecCslDdOutputTests, WheelFfbWriteCacheTests,
the existing pedal and shutdown regressions, and WheelRangeLimitsTests.
No motor output was used by the agent for this validation. The hardware
owner subsequently confirmed that both 5.0.0.1 and 5.1.0.1 restore force
feedback in the PadForge GUI and in-game. This is user-reported hardware
validation; other Fanatec models have not been tested.

Local deployment replaces only PadForge.exe, with the original EXE and XML
backed up beforehand. It does not alter the installed XML, firmware,
Fanatec drivers or HidHide configuration. The normal application is not
automatically launched after replacement because its engine may apply
configured forces. If the old process is still running, exit it from the
tray and reopen PadForge to load 5.1.0.1.

Automatic updates retain the user's existing configuration and may replace
this local correction with a later upstream release.
