param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zipPath = Join-Path $PSScriptRoot 'CreatorCamPayload.zip'
$sources = @{
    'index.html' = Join-Path $PSScriptRoot 'PayloadV6\index.html'
    'scripts/overlay.js' = Join-Path $PSScriptRoot 'PayloadV6\scripts\overlay.js'
}

$archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    foreach ($entryName in $sources.Keys) {
        $sourcePath = $sources[$entryName]
        $sourceBytes = [System.IO.File]::ReadAllBytes($sourcePath)
        $entry = $archive.GetEntry($entryName)
        $same = $false
        if ($null -ne $entry -and $entry.Length -eq $sourceBytes.Length) {
            $stream = $entry.Open()
            try {
                $current = New-Object byte[] $entry.Length
                $read = 0
                while ($read -lt $current.Length) {
                    $count = $stream.Read($current, $read, $current.Length - $read)
                    if ($count -le 0) { break }
                    $read += $count
                }
                $same = $read -eq $sourceBytes.Length
                for ($index = 0; $same -and $index -lt $sourceBytes.Length; $index++) {
                    if ($current[$index] -ne $sourceBytes[$index]) { $same = $false }
                }
            }
            finally { $stream.Dispose() }
        }
        if ($same) { continue }
        if ($null -ne $entry) { $entry.Delete() }
        $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(2026, 8, 11, 0, 0, 0, [TimeSpan]::Zero)
        $stream = $entry.Open()
        try { $stream.Write($sourceBytes, 0, $sourceBytes.Length) }
        finally { $stream.Dispose() }
    }
}
finally { $archive.Dispose() }
