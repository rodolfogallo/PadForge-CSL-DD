# Metadata-only entry point. It does not start PadForge's input/output engine.
$ErrorActionPreference = 'Stop'
$exePath = Join-Path $PSScriptRoot 'PadForge.exe'
if (-not (Test-Path -LiteralPath $exePath)) {
    throw 'Put this script beside the published PadForge.exe.'
}
$reportPath = Join-Path $PSScriptRoot 'fanatec-hid-probe.json'
$process = Start-Process -FilePath $exePath -ArgumentList ('--fanatec-hid-probe "{0}"' -f $reportPath) -Verb RunAs -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw ('HID metadata probe failed. Exit code: ' + $process.ExitCode) }
Write-Output ('Metadata report: ' + $reportPath)
