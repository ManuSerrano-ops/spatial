Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $projectRoot 'publish-current\PlanoOpenSpaceIT.Windows.exe'
$qaRoot = Join-Path $projectRoot 'qa-runtime-data'
$logs = Join-Path $qaRoot 'logs'
$configPath = Join-Path $projectRoot 'publish-current\config.json'
$startupTimeoutSeconds = 30
$exitTimeoutSeconds = 15

function Fail([string] $message) {
    Write-Error $message
    exit 1
}

function Get-AuditEntries([string] $path) {
    if (-not (Test-Path -LiteralPath $path)) { return @() }
    return @(Get-Content -LiteralPath $path |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_ | ConvertFrom-Json })
}

function Get-Descendants([int] $rootPid) {
    $all = @(Get-CimInstance Win32_Process)
    $pending = [System.Collections.Generic.Queue[int]]::new()
    $result = [System.Collections.Generic.List[object]]::new()
    $pending.Enqueue($rootPid)
    while ($pending.Count -gt 0) {
        $parentPid = $pending.Dequeue()
        foreach ($child in $all | Where-Object { [int] $_.ParentProcessId -eq $parentPid }) {
            $result.Add($child)
            $pending.Enqueue([int] $child.ProcessId)
        }
    }
    return @($result)
}

function Wait-ForStartup([int] $processId, [string] $auditPath) {
    $deadline = [DateTime]::UtcNow.AddSeconds($startupTimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        $entries = @(Get-AuditEntries $auditPath)
        $started = @($entries | Where-Object { $_.action -eq 'lifecycle.start' }).Count -gt 0
        $loaded = @($entries | Where-Object { $_.action -eq 'bridge.action' -and $_.bridgeAction -eq 'loadInitialData' -and $_.result -eq 'success' }).Count -gt 0
        $validation = @($entries | Where-Object { $_.action -eq 'validation.finished' }).Count -gt 0
        $analytics = @($entries | Where-Object { $_.action -eq 'analytics.finished' }).Count -gt 0
        $svg = @($entries | Where-Object { $_.action -eq 'plan.resource.diagnostic' -and $_.result -eq 'SVG 5/5' }).Count -gt 0
        if ($started -and $loaded -and $validation -and $analytics -and $svg) { return $entries }
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Runtime startup for PID $processId did not reach lifecycle.start, loadInitialData, Validation, Analytics and SVG 5/5 within $startupTimeoutSeconds seconds."
}

function Close-Normally([System.Diagnostics.Process] $process, [string] $auditPath) {
    $deadline = [DateTime]::UtcNow.AddSeconds($exitTimeoutSeconds)
    do {
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    $process.Refresh()
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
        throw "NOT TESTABLE: PID $($process.Id) did not expose a main WPF window. No forced termination was attempted."
    }

    $descendantsBefore = @(Get-Descendants $process.Id)
    $webViewPids = @($descendantsBefore | Where-Object { $_.Name -ieq 'msedgewebview2.exe' } | ForEach-Object { [int] $_.ProcessId })
    if (-not $process.CloseMainWindow()) {
        throw "NOT TESTABLE: CloseMainWindow() returned false for PID $($process.Id). No forced termination was attempted."
    }
    if (-not $process.WaitForExit($exitTimeoutSeconds * 1000)) {
        throw "FAILED: normal close did not terminate PID $($process.Id) within $exitTimeoutSeconds seconds."
    }

    Start-Sleep -Milliseconds 750
    $remainingWebView = @($webViewPids | Where-Object {
        try { Get-Process -Id $_ -ErrorAction Stop | Out-Null; $true } catch { $false }
    })
    if ($remainingWebView.Count -gt 0) {
        throw "FAILED: WebView2 descendants from PID $($process.Id) remained after normal close: $($remainingWebView -join ', ')."
    }

    $entries = @(Get-AuditEntries $auditPath)
    if (@($entries | Where-Object { $_.action -eq 'lifecycle.closing' }).Count -eq 0) {
        throw "FAILED: normal close terminated PID $($process.Id) but no lifecycle.closing entry was recorded."
    }
    return [PSCustomObject]@{ Pid = $process.Id; WebViewChildren = $webViewPids.Count }
}

if (-not (Test-Path -LiteralPath $exe)) { Fail "RC executable not found: $exe" }
if (-not (Test-Path -LiteralPath $configPath)) { Fail "RC config not found: $configPath" }
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
if ($config.networkRoot -ne $qaRoot) { Fail "RC networkRoot is not the required QA root." }

$existing = @(Get-CimInstance Win32_Process -Filter "Name = 'PlanoOpenSpaceIT.Windows.exe'" | Where-Object { $_.ExecutablePath -ieq $exe })
if ($existing.Count -ne 0) { Fail "Refusing lifecycle test: $($existing.Count) matching RC instance(s) already running." }

try {
    $first = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -PassThru
    $firstAudit = Join-Path $logs "audit-$($first.Id).log"
    $firstStartup = Wait-ForStartup $first.Id $firstAudit
    $firstClose = Close-Normally $first $firstAudit

    $second = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -PassThru
    $secondAudit = Join-Path $logs "audit-$($second.Id).log"
    $secondStartup = Wait-ForStartup $second.Id $secondAudit
    $errors = @($secondStartup | Where-Object { $_.level -eq 'error' -or ($_.action -eq 'bridge.action' -and $_.result -eq 'failure') })
    if ($errors.Count -gt 0) { throw "FAILED: reopen generated $($errors.Count) error/failure audit entries." }
    $secondClose = Close-Normally $second $secondAudit

    Write-Output "runtime-lifecycle-harness: PASS; close PID $($firstClose.Pid) WebView2 descendants $($firstClose.WebViewChildren); reopen PID $($secondClose.Pid) WebView2 descendants $($secondClose.WebViewChildren); Validation, Analytics and SVG 5/5 observed on both starts."
    exit 0
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
