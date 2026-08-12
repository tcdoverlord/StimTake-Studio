param(
    [string]$SourceFolder = "C:\Users\Tony\OneDrive\Desktop\20-actions\Daily_Pack_August_2026",
    [string]$OutputZip = "C:\Users\Tony\OneDrive\Desktop\StimTake-Daily-Pack-August-2026.zip"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=== StimTake Daily Pack August 2026 Builder ===" -ForegroundColor Magenta
Write-Host "Source: $SourceFolder"
Write-Host "Output: $OutputZip"
Write-Host ""

if (-not (Test-Path -LiteralPath $SourceFolder)) {
    throw "Source folder not found: $SourceFolder"
}

$actions = @(
    @{ Slot=1;  File="Daily-Pack-01-Spin-The-Wheel-overlay.html";                  Id="daily-spin-the-wheel";       Name="Spin The Wheel" },
    @{ Slot=2;  File="Daily-Pack-02-Lucky-Dice-overlay.html";                     Id="daily-lucky-dice";           Name="Lucky Dice" },
    @{ Slot=3;  File="Daily-Pack-03-Mystery-Box-overlay.html";                    Id="daily-mystery-box";          Name="Mystery Box" },
    @{ Slot=4;  File="Daily-Pack-04-Pick-A-Card-overlay.html";                    Id="daily-pick-a-card";          Name="Pick A Card" },
    @{ Slot=5;  File="Daily-Pack-05-Lucky-Slots-overlay.html";                    Id="daily-lucky-slots";          Name="Lucky Slots" },
    @{ Slot=6;  File="Daily-Pack-06-Time-Jackpot-overlay.html";                   Id="daily-time-jackpot";         Name="Time Jackpot" },
    @{ Slot=7;  File="Daily-Pack-07-Emoji-Race-overlay.html";                     Id="daily-emoji-race";           Name="Emoji Race" },
    @{ Slot=8;  File="Daily-Pack-08-Cup-Shuffle-Adult-Wins-overlay.html";         Id="daily-cup-shuffle";          Name="Cup Shuffle" },
    @{ Slot=9;  File="Daily-Pack-09-Bullseye-Reward-Reveal-overlay.html";         Id="daily-bullseye";             Name="Bullseye" },
    @{ Slot=10; File="Daily-Pack-10-Pick-A-Gem-overlay.html";                     Id="daily-pick-a-gem";           Name="Pick A Gem" },
    @{ Slot=11; File="Daily-Pack-11-Balloon-Pop-overlay.html";                    Id="daily-balloon-pop";          Name="Balloon Pop" },
    @{ Slot=12; File="Daily-Pack-12-Lucky-Locker-overlay.html";                   Id="daily-lucky-locker";         Name="Lucky Locker" },
    @{ Slot=13; File="Daily-Pack-13-Mystery-Tiles-Fixed-overlay.html";            Id="daily-mystery-tiles";        Name="Mystery Tiles" },
    @{ Slot=14; File="Daily-Pack-14-Rocket-Launch-Adult-overlay.html";            Id="daily-rocket-launch";        Name="Rocket Launch" },
    @{ Slot=15; File="Daily-Pack-15-Treasure-Chest-Final-Top-Reveal-overlay.html";Id="daily-treasure-chest";       Name="Treasure Chest" },
    @{ Slot=16; File="Daily-Pack-16-Lucky-Strike-Adult-Prizes-overlay.html";      Id="daily-lucky-strike";         Name="Lucky Strike" },
    @{ Slot=17; File="Daily-Pack-17-Lucky-Shot-True-Net-Entry-overlay.html";      Id="daily-lucky-shot";           Name="Lucky Shot" },
    @{ Slot=18; File="Daily-Pack-18-Star-Pick-Adult-Prizes-overlay.html";         Id="daily-star-pick";            Name="Star Pick" },
    @{ Slot=19; File="Daily-Pack-19-Mini-Race-overlay.html";                      Id="daily-mini-race";            Name="Mini Race" },
    @{ Slot=20; File="Daily-Pack-20-Final-Showdown-overlay.html";                 Id="daily-final-showdown";       Name="Final Showdown" }
)

# Validate every source file before making the pack.
$missing = @()

foreach ($action in $actions) {
    $src = Join-Path $SourceFolder $action.File

    if (-not (Test-Path -LiteralPath $src)) {
        $missing += $action.File
    }
}

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing files:" -ForegroundColor Red

    $missing | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Red
    }

    throw "Packaging stopped. No Daily Pack ZIP was created."
}

$work = Join-Path $env:TEMP (
    "StimTake-Daily-Pack-" + [guid]::NewGuid().ToString("N")
)

New-Item -ItemType Directory -Path $work | Out-Null

try {

    $actionsRoot = Join-Path $work "actions"
    $themeRoot   = Join-Path $work "theme"

    New-Item -ItemType Directory -Path $actionsRoot | Out-Null
    New-Item -ItemType Directory -Path $themeRoot   | Out-Null

    # Main StimTake pack manifest.
    $pack = [ordered]@{
        schema_version = 1
        product        = "StimTake Show Pack"
        name           = "StimTake Daily Pack - August 2026"
        id             = "stimtake-daily-pack-august-2026"
        version        = "1.0.0"
        theme          = "daily-pack"
        max_actions    = 20
    }

    $pack |
        ConvertTo-Json -Depth 6 |
        Set-Content `
            -LiteralPath (Join-Path $work "pack.json") `
            -Encoding UTF8

    # Theme metadata.
    $theme = [ordered]@{
        schema_version = 1
        name           = "Daily Pack"
        description    = "Twenty animated daily-use game overlays for StimTake Studio."
    }

    $theme |
        ConvertTo-Json -Depth 6 |
        Set-Content `
            -LiteralPath (Join-Path $themeRoot "theme.json") `
            -Encoding UTF8

    # Package each HTML overlay as one StimTake action.
    foreach ($action in $actions) {

        $slotText = "{0:D2}" -f $action.Slot
        $actionDir = Join-Path $actionsRoot ("action-" + $slotText)

        New-Item -ItemType Directory -Path $actionDir | Out-Null

        Copy-Item `
            -LiteralPath (Join-Path $SourceFolder $action.File) `
            -Destination (Join-Path $actionDir "overlay.html")

        $manifest = [ordered]@{
            schema_version  = 1
            slot            = $action.Slot
            id              = $action.Id
            name            = $action.Name
            type            = "overlay"
            overlay         = "overlay.html"
            duration        = $(if ($action.Slot -eq 20) { 30 } else { 10 })
            default_enabled = $true
        }

        $manifest |
            ConvertTo-Json -Depth 6 |
            Set-Content `
                -LiteralPath (Join-Path $actionDir "action.json") `
                -Encoding UTF8
    }

    # Verify structure before creating the ZIP.
    $actionJsonCount = (
        Get-ChildItem `
            -LiteralPath $actionsRoot `
            -Recurse `
            -Filter "action.json"
    ).Count

    $overlayCount = (
        Get-ChildItem `
            -LiteralPath $actionsRoot `
            -Recurse `
            -Filter "overlay.html"
    ).Count

    if ($actionJsonCount -ne 20 -or $overlayCount -ne 20) {
        throw "Verification failed. Expected 20 action manifests and 20 overlays."
    }

    # Protect any previous Daily Pack ZIP instead of silently losing it.
    if (Test-Path -LiteralPath $OutputZip) {

        $backup = "$OutputZip.previous-$(Get-Date -Format 'yyyyMMdd-HHmmss').zip"

        Copy-Item `
            -LiteralPath $OutputZip `
            -Destination $backup

        Write-Host ""
        Write-Host "Preserved previous ZIP as:" -ForegroundColor Yellow
        Write-Host "  $backup"

        Remove-Item -LiteralPath $OutputZip
    }

    # Build ZIP with pack.json at ZIP root.
    Compress-Archive `
        -Path (Join-Path $work "*") `
        -DestinationPath $OutputZip `
        -CompressionLevel Optimal

    $hash = (
        Get-FileHash `
            -LiteralPath $OutputZip `
            -Algorithm SHA256
    ).Hash

    Write-Host ""
    Write-Host "=== DAILY PACK CREATED ===" -ForegroundColor Green
    Write-Host $OutputZip -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Actions:          20"
    Write-Host "Action manifests: $actionJsonCount"
    Write-Host "HTML overlays:    $overlayCount"
    Write-Host "SHA-256:          $hash"
    Write-Host ""
    Write-Host "Next:"
    Write-Host "StimTake Studio -> ACTION DECK -> IMPORT SHOW PACK"
}
finally {

    if (Test-Path -LiteralPath $work) {
        Remove-Item `
            -LiteralPath $work `
            -Recurse `
            -Force
    }
}