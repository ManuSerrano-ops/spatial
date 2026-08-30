Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$exe = 'G:\Proyecto Planos\phm\phm\uifigmastyle_UX_REDESIGN\publish-release-candidate\PlanoOpenSpaceIT.Windows.exe'
$matches = @(Get-CimInstance Win32_Process -Filter "Name = 'PlanoOpenSpaceIT.Windows.exe'" | Where-Object { $_.ExecutablePath -ieq $exe })
if ($matches.Count -ne 1) { throw "Expected exactly one RC instance at the exact publish path; found $($matches.Count)." }

$process = [System.Diagnostics.Process]::GetProcessById([int] $matches[0].ProcessId)
$process.Refresh()
if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw "RC PID $($process.Id) has no main window; no forced termination was attempted." }
if (-not $process.CloseMainWindow()) { throw "CloseMainWindow returned false for RC PID $($process.Id); no forced termination was attempted." }
if (-not $process.WaitForExit(15000)) { throw "RC PID $($process.Id) did not close within 15 seconds." }
Write-Output "close-running-rc: PASS; normal close completed for PID $($process.Id)."
