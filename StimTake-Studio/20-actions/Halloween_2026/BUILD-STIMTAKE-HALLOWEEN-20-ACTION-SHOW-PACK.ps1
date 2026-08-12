param(
    [string]$SourceFolder = "C:\Users\Tony\OneDrive\Desktop\20-actions",
    [string]$OutputZip = "C:\Users\Tony\OneDrive\Desktop\StimTake-Halloween-20-Action-Show-Pack.zip"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=== StimTake Halloween 20-Action Show Pack Builder ===" -ForegroundColor Magenta
Write-Host "Source: $SourceFolder"
Write-Host "Output: $OutputZip"
Write-Host ""

if (-not (Test-Path -LiteralPath $SourceFolder)) {
    throw "Source folder not found: $SourceFolder"
}

$actions = @(
    @{ Slot=1;  File="01_Halloween_Witchs_Wheel_overlay.html";              Id="halloween-witchs-wheel";        Name="Witch's Wheel" },
    @{ Slot=2;  File="02_Halloween_Witchs_Lucky_Dice_overlay.html";         Id="halloween-witchs-lucky-dice";   Name="Witch's Lucky Dice" },
    @{ Slot=3;  File="03_Halloween_Pick_A_Pumpkin_overlay.html";            Id="halloween-pick-a-pumpkin";      Name="Pick a Pumpkin" },
    @{ Slot=4;  File="04_Halloween-Crystal-Ball-overlay.html";              Id="halloween-crystal-ball";        Name="Crystal Ball" },
    @{ Slot=5;  File="05_Halloween-Tarot-Draw-overlay.html";                Id="halloween-tarot-draw";          Name="Midnight Tarot" },
    @{ Slot=6;  File="06_Halloween-Witchs-Potion-overlay.html";             Id="halloween-witchs-potion";       Name="Witch's Potion" },
    @{ Slot=7;  File="07_Halloween-Spider-Drop-overlay.html";               Id="halloween-spider-drop";         Name="Spider Drop" },
    @{ Slot=8;  File="08_Halloween-Bat-Race-overlay.html";                  Id="halloween-bat-race";            Name="Bat Race" },
    @{ Slot=9;  File="09_Halloween-Trick-or-Treat-Bag-overlay.html";        Id="halloween-trick-or-treat-bag";  Name="Trick-or-Treat Bag" },
    @{ Slot=10; File="10_Halloween-Skull-Slots-overlay.html";               Id="halloween-skull-slots";         Name="Skull Slots" },
    @{ Slot=11; File="11_Halloween-Ghost-Hunt-overlay.html";                Id="halloween-ghost-hunt";          Name="Ghost Hunt" },
    @{ Slot=12; File="12_Halloween-Graveyard-Pick-overlay.html";            Id="halloween-graveyard-pick";      Name="Graveyard Pick" },
    @{ Slot=13; File="13_Halloween-Magic-8-Ball-overlay.html";              Id="halloween-magic-8-ball";        Name="Magic 8-Ball" },
    @{ Slot=14; File="14_Halloween-Candle-Challenge-overlay.html";          Id="halloween-candle-challenge";    Name="Candle Challenge" },
    @{ Slot=15; File="15_Halloween-Spell-Book-overlay.html";                Id="halloween-spell-book";          Name="Spell Book" },
    @{ Slot=16; File="16_Halloween-Black-Cat-Pick-overlay.html";            Id="halloween-black-cat-pick";      Name="Black Cat Pick" },
    @{ Slot=17; File="17_Halloween-Poison-Apple-overlay.html";              Id="halloween-poison-apple";        Name="Poison Apple" },
    @{ Slot=18; File="18_Halloween-Mystery-Coffin-overlay.html";            Id="halloween-mystery-coffin";      Name="Mystery Coffin" },
    @{ Slot=19; File="19_Halloween-Pumpkin-Slots-overlay.html";             Id="halloween-pumpkin-slots";       Name="Pumpkin Slots" },
    @{ Slot=20; File="20_Halloween-Final-Boss-Battle-overlay.html";         Id="halloween-final-boss-battle";   Name="Halloween Final Boss Battle" }
)

# Validate all 20 inputs before creating anything.
$missing = @()
foreach ($action in $actions) {
    $src = Join-Path $SourceFolder $action.File
    if (-not (Test-Path -LiteralPath $src)) {
        $missing += $action.File
    }
}

if ($missing.Count -gt 0) {
    Write-Host "Missing files:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    throw "Packaging stopped. No Show Pack ZIP was created."
}

$work = Join-Path $env:TEMP ("StimTake-Halloween-Pack-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null

try {
    $actionsRoot = Join-Path $work "actions"
    $themeRoot = Join-Path $work "theme"
    New-Item -ItemType Directory -Path $actionsRoot | Out-Null
    New-Item -ItemType Directory -Path $themeRoot | Out-Null

    $pack = [ordered]@{
        schema_version = 1
        product        = "StimTake Show Pack"
        name           = "StimTake Halloween 20-Action Show Pack"
        id             = "stimtake-halloween-20-action"
        version        = "1.0.0"
        theme          = "halloween-witchy"
        max_actions    = 20
    }

    $pack | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $work "pack.json") -Encoding UTF8

    $theme = [ordered]@{
        schema_version = 1
        name           = "Halloween Witchy"
        description    = "Purple, pink, orange, moonlit Halloween game overlays for StimTake Studio."
    }

    $theme | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $themeRoot "theme.json") -Encoding UTF8

    foreach ($action in $actions) {
        $slotText = "{0:D2}" -f $action.Slot
        $actionDir = Join-Path $actionsRoot ("action-" + $slotText)
        New-Item -ItemType Directory -Path $actionDir | Out-Null

        Copy-Item -LiteralPath (Join-Path $SourceFolder $action.File) `
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

        $manifest | ConvertTo-Json -Depth 6 |
            Set-Content -LiteralPath (Join-Path $actionDir "action.json") -Encoding UTF8
    }

    # Final structural verification before ZIP creation.
    $actionJsonCount = (Get-ChildItem -LiteralPath $actionsRoot -Recurse -Filter "action.json").Count
    $overlayCount = (Get-ChildItem -LiteralPath $actionsRoot -Recurse -Filter "overlay.html").Count

    if ($actionJsonCount -ne 20 -or $overlayCount -ne 20) {
        throw "Verification failed. Expected 20 action manifests and 20 overlays."
    }

    if (Test-Path -LiteralPath $OutputZip) {
        $backup = "$OutputZip.previous-$(Get-Date -Format 'yyyyMMdd-HHmmss').zip"
        Copy-Item -LiteralPath $OutputZip -Destination $backup
        Write-Host "Preserved previous ZIP as:" -ForegroundColor Yellow
        Write-Host "  $backup"
        Remove-Item -LiteralPath $OutputZip
    }

    Compress-Archive -Path (Join-Path $work "*") -DestinationPath $OutputZip -CompressionLevel Optimal

    $hash = (Get-FileHash -LiteralPath $OutputZip -Algorithm SHA256).Hash

    Write-Host ""
    Write-Host "=== SHOW PACK CREATED ===" -ForegroundColor Green
    Write-Host $OutputZip -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Actions: 20"
    Write-Host "Action manifests: $actionJsonCount"
    Write-Host "HTML overlays: $overlayCount"
    Write-Host "SHA-256: $hash"
    Write-Host ""
    Write-Host "Next: open StimTake Studio -> ACTION DECK -> IMPORT SHOW PACK"
}
finally {
    if (Test-Path -LiteralPath $work) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
