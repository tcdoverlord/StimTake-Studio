param(
    [string]$StudioExe = (Join-Path $PSScriptRoot 'outputs\v6\StimTake-Studio-6.0.exe'),
    [string]$DesignerExe = (Join-Path $PSScriptRoot 'outputs\v6\StimTake-Designer-1.0.exe')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class StimTakeUiNative {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
'@

function Wait-MainWindow([int]$ProcessId, [int]$TimeoutSeconds = 12) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($null -eq $process) { throw "Process $ProcessId exited before opening its main window." }
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            return [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for process $ProcessId main window."
}

function Find-Button([System.Windows.Automation.AutomationElement]$Root, [string]$Name) {
    $elements = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    $fallback = $null
    foreach ($element in $elements) {
        if (($element.Current.Name -replace '&', '') -ne $Name) { continue }
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button) { return $element }
        $invoke = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) { return $element }
        if ($null -eq $fallback) { $fallback = $element }
    }
    return $fallback
}

function Invoke-Button([System.Windows.Automation.AutomationElement]$Root, [string]$Name) {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $deadline) {
        $button = Find-Button $Root $Name
        if ($null -ne $button) {
            $pattern = $null
            if ($button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
                $pattern.Invoke()
            }
            else {
                try {
                    $point = $button.GetClickablePoint()
                    [StimTakeUiNative]::SetCursorPos([int]$point.X, [int]$point.Y) | Out-Null
                    [StimTakeUiNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                    [StimTakeUiNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                }
                catch {
                    if ($button.Current.NativeWindowHandle -eq 0) { throw }
                    [StimTakeUiNative]::SendMessage([IntPtr]$button.Current.NativeWindowHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
                }
            }
            return
        }
        Start-Sleep -Milliseconds 100
    }
    $names = @()
    $elements = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $elements) {
        if (![string]::IsNullOrWhiteSpace($element.Current.Name)) { $names += ($element.Current.ControlType.ProgrammaticName + '=' + $element.Current.Name) }
    }
    throw "Button '$Name' was not found in '$($Root.Current.Name)'. Elements: $($names -join '; ')"
}

function Wait-ProcessWindow([int]$ProcessId, [string]$TitlePattern, [int]$TimeoutSeconds = 10) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    while ([DateTime]::UtcNow -lt $deadline) {
        $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            $condition)
        foreach ($window in $windows) {
            if ($window.Current.Name -like $TitlePattern) { return $window }
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for window '$TitlePattern' in process $ProcessId."
}

function Wait-WindowWithButton([int]$ProcessId, [string]$ButtonName, [int]$TimeoutSeconds = 10) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    while ([DateTime]::UtcNow -lt $deadline) {
        $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            $condition)
        foreach ($window in $windows) {
            try {
                if ($null -ne (Find-Button $window $ButtonName)) { return $window }
            }
            catch [System.Windows.Automation.ElementNotAvailableException] { }
        }
        Start-Sleep -Milliseconds 100
    }
    $debug = @()
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        $condition)
    foreach ($window in $windows) {
        try { $debug += ($window.Current.Name + ' => ' + (Window-Text $window)) } catch { }
    }
    throw "Timed out waiting for button '$ButtonName' in a window owned by process $ProcessId. Windows: $($debug -join ' || ')"
}

function Close-Window([System.Windows.Automation.AutomationElement]$Window) {
    $pattern = $Window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    $pattern.Close()
}

function Window-Text([System.Windows.Automation.AutomationElement]$Window) {
    $values = @()
    $elements = $Window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $elements) {
        if (![string]::IsNullOrWhiteSpace($element.Current.Name)) { $values += $element.Current.Name }
    }
    return ($values -join ' | ')
}

if (Get-Process -Name 'StimTake-Studio-6.0','Creator-Cam-Overlay-Kit' -ErrorAction SilentlyContinue) {
    throw 'A StimTake/Creator Cam process is already running; the isolated UI test will not interfere with it.'
}
if (Get-NetTCPConnection -LocalAddress 127.0.0.1 -LocalPort 8787 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Port 8787 already has an owner; the isolated UI test will not interfere with it.'
}
if (!(Test-Path -LiteralPath $StudioExe) -or !(Test-Path -LiteralPath $DesignerExe)) {
    throw 'Build both V6 executables before running this UI test.'
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('StimTake-V6-UI-' + [Guid]::NewGuid().ToString('N'))
$studioProcess = $null
$designerProcess = $null
$previousRoot = $env:STIMTAKE_RUNTIME_ROOT
try {
    New-Item -ItemType Directory -Path (Join-Path $testRoot 'CreatorCamOverlayKit') -Force | Out-Null
    [IO.File]::WriteAllText(
        (Join-Path $testRoot 'CreatorCamOverlayKit\chaturbate-model-address-v1.txt'),
        'https://chaturbate.com/obsidian_stallion/',
        [Text.UTF8Encoding]::new($false))
    $env:STIMTAKE_RUNTIME_ROOT = $testRoot

    $studioProcess = Start-Process -FilePath $StudioExe -PassThru
    $studioWindow = Wait-MainWindow $studioProcess.Id
    if ($studioWindow.Current.Name -ne 'StimTake Studio 6.0') { throw 'Unexpected Studio window title.' }
    if ($null -ne (Find-Button $studioWindow 'Backstage')) { throw 'Backstage is exposed in the normal Studio UI.' }
    $statusResponse = Invoke-WebRequest -Uri 'http://127.0.0.1:8787/api/studio-status' -UseBasicParsing
    $statusJson = $statusResponse.Content | ConvertFrom-Json
    if ($statusJson.backend -ne 'RUNNING' -or $statusJson.model -ne 'obsidian_stallion') { throw 'Studio status endpoint did not report the expected backend/model.' }
    $indexResponse = Invoke-WebRequest -Uri 'http://127.0.0.1:8787/index.html' -UseBasicParsing
    if ($indexResponse.Content -notmatch 'TOP TIPPERS' -or $indexResponse.Content -notmatch 'action-layer' -or $indexResponse.Content -match 'creator-cam-stage') {
        throw 'OBS index does not contain the simplified transparent supporter/action layout.'
    }
    Write-Output 'PASS: Studio launched with one port-8787 backend, no Backstage UI, status endpoint, and simplified OBS page.'

    Close-Window $studioWindow
    if (!$studioProcess.WaitForExit(10000)) {
        Stop-Process -Id $studioProcess.Id -Force
        throw 'Studio did not shut down cleanly after closing its main window.'
    }
    Start-Sleep -Milliseconds 250
    if (Get-NetTCPConnection -LocalAddress 127.0.0.1 -LocalPort 8787 -State Listen -ErrorAction SilentlyContinue) {
        throw 'Port 8787 remained owned after Studio exited.'
    }
    Write-Output 'PASS: Studio clean shutdown released port 8787.'

    $designerProcess = Start-Process -FilePath $DesignerExe -PassThru
    $designerWindow = Wait-MainWindow $designerProcess.Id
    if ($designerWindow.Current.Name -ne 'StimTake Designer 1.0') { throw 'Unexpected Designer window title.' }
    Invoke-Button $designerWindow 'NEW PACK'
    $confirm = Wait-WindowWithButton $designerProcess.Id 'Yes'
    Invoke-Button $confirm 'Yes'
    Invoke-Button $designerWindow 'SAVE PACK'
    $saved = Wait-WindowWithButton $designerProcess.Id 'OK'
    Invoke-Button $saved 'OK'
    $workspace = Join-Path $testRoot 'StimTakeDesigner\workspace\my-show-pack'
    if (!(Test-Path -LiteralPath (Join-Path $workspace 'pack.json')) -or
        !(Test-Path -LiteralPath (Join-Path $workspace 'theme\theme.json')) -or
        !(Test-Path -LiteralPath (Join-Path $workspace 'actions\action-01\action.json'))) {
        throw 'Designer did not create the expected new workspace manifests.'
    }
    Write-Output 'PASS: Designer launched and created/saved a new one-action workspace.'

    Close-Window $designerWindow
    if (!$designerProcess.WaitForExit(10000)) {
        Stop-Process -Id $designerProcess.Id -Force
        throw 'Designer did not exit after closing its main window.'
    }
    $exportZip = Join-Path $testRoot 'designer-export.zip'
    $exportProcess = Start-Process -FilePath $DesignerExe -ArgumentList @('--build-pack', $workspace, $exportZip) -PassThru -Wait
    if ($exportProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $exportZip)) {
        throw "Designer command export failed with exit code $($exportProcess.ExitCode)."
    }
    Write-Output 'PASS: Designer executable validated and exported the saved workspace ZIP.'
}
finally {
    if ($null -ne $studioProcess -and !$studioProcess.HasExited) { Stop-Process -Id $studioProcess.Id -Force -ErrorAction SilentlyContinue }
    if ($null -ne $designerProcess -and !$designerProcess.HasExited) { Stop-Process -Id $designerProcess.Id -Force -ErrorAction SilentlyContinue }
    $env:STIMTAKE_RUNTIME_ROOT = $previousRoot
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
