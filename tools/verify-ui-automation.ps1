[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "No existe el ejecutable: $ExecutablePath"
}

Add-Type -AssemblyName UIAutomationClient

$process = Start-Process -FilePath $ExecutablePath -PassThru
try {
    $window = $null
    for ($attempt = 0; $attempt -lt 30 -and $null -eq $window; $attempt++) {
        Start-Sleep -Milliseconds 500
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $process.Id)
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            $condition)
    }

    if ($null -eq $window) {
        throw "No apareció una ventana UI Automation para el proceso $($process.Id)."
    }

    $expectedWindowId = 'PlanoOpenSpaceIT.MainWindow'
    if ($window.Current.AutomationId -ne $expectedWindowId) {
        throw "AutomationId de ventana inesperado: '$($window.Current.AutomationId)'."
    }

    $webViewId = 'PlanoOpenSpaceIT.WebView'
    $webViewCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $webViewId)
    $webView = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $webViewCondition)
    if ($null -eq $webView) {
        throw "No se expuso el WebView2 con AutomationId '$webViewId'."
    }

    $expectedWebViewName = "Contenido de la aplicaci$([char]0x00F3)n Plano Open Space IT"
    if ($webView.Current.Name -ne $expectedWebViewName) {
        throw "Nombre UIA de WebView inesperado: '$($webView.Current.Name)'."
    }

    Write-Output "UI Automation: ventana '$expectedWindowId' y WebView '$webViewId' expuestos correctamente."
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { Stop-Process -Id $process.Id -Force }
    }
}
