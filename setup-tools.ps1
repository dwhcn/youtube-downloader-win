$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$toolsDirectory = Join-Path $PSScriptRoot 'tools'
$required = @('yt-dlp.exe', 'deno.exe', 'ffmpeg.exe', 'ffprobe.exe')
$missing = @($required | Where-Object { ![IO.File]::Exists((Join-Path $toolsDirectory $_)) })
if ($missing.Count -eq 0) {
    Write-Host 'The download tools are already installed.'
    return
}

if ($PSVersionTable.PSEdition -eq 'Desktop') {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}
Add-Type -AssemblyName System.IO.Compression.FileSystem

$scratch = Join-Path $toolsDirectory ('.setup-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($scratch) | Out-Null

function Save-VerifiedAsset {
    param([string]$Url, [string]$ChecksumUrl, [string]$Name, [string]$ChecksumPattern)

    $response = Invoke-WebRequest -Uri $ChecksumUrl -UseBasicParsing -TimeoutSec 30
    $checksum = $response.Content
    if ($checksum -is [byte[]]) { $checksum = [Text.Encoding]::ASCII.GetString($checksum) }
    $match = [regex]::Match($checksum, $ChecksumPattern)
    if (!$match.Success) { throw "Published SHA-256 missing for $Name." }

    $file = Join-Path $scratch $Name
    Write-Host "Downloading $Name..."
    Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 600 -OutFile $file
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $match.Groups[1].Value) {
        throw "SHA-256 mismatch for $Name. Run setup again to retry."
    }
    return $file
}

function Install-ZipEntries {
    param([string]$ZipPath, [hashtable]$Files)

    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($name in $Files.Keys) {
            $target = Join-Path $toolsDirectory $Files[$name]
            if ([IO.File]::Exists($target)) { continue }
            $entries = @($archive.Entries | Where-Object { $_.Name -ceq $name })
            if ($entries.Count -ne 1) { throw "$name missing or duplicated in the verified archive." }
            $staged = Join-Path $scratch $name
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entries[0], $staged, $false)
            [IO.File]::Move($staged, $target)
        }
    }
    finally {
        $archive.Dispose()
    }
}

try {
    if ($missing -contains 'yt-dlp.exe') {
        $base = 'https://github.com/yt-dlp/yt-dlp/releases/latest/download'
        $file = Save-VerifiedAsset "$base/yt-dlp.exe" "$base/SHA2-256SUMS" 'yt-dlp.exe' '(?im)^([0-9a-f]{64})[ \t]+\*?yt-dlp\.exe\r?$'
        [IO.File]::Move($file, (Join-Path $toolsDirectory 'yt-dlp.exe'))
    }

    if ($missing -contains 'deno.exe') {
        $base = 'https://github.com/denoland/deno/releases/latest/download'
        $name = 'deno-x86_64-pc-windows-msvc.zip'
        $file = Save-VerifiedAsset "$base/$name" "$base/$name.sha256sum" $name '(?i)\b([0-9a-f]{64})\b'
        Install-ZipEntries $file @{ 'deno.exe' = 'deno.exe' }
    }

    if ($missing -contains 'ffmpeg.exe' -or $missing -contains 'ffprobe.exe') {
        $name = 'ffmpeg-release-essentials.zip'
        $url = "https://www.gyan.dev/ffmpeg/builds/$name"
        $file = Save-VerifiedAsset $url "$url.sha256" $name '(?i)\b([0-9a-f]{64})\b'
        Install-ZipEntries $file @{
            'ffmpeg.exe' = 'ffmpeg.exe'
            'ffprobe.exe' = 'ffprobe.exe'
            'LICENSE' = 'ffmpeg-LICENSE.txt'
        }
    }

    Write-Host 'Tools are ready. Open YouTube Downloader.exe.'
}
finally {
    foreach ($file in Get-ChildItem -LiteralPath $scratch -Force -File) {
        Remove-Item -LiteralPath $file.FullName -Force
    }
    [IO.Directory]::Delete($scratch)
}
