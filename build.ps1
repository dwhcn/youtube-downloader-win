$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputPath = Join-Path $PSScriptRoot 'YouTube Downloader.exe'
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File
$manifest = Join-Path $PSScriptRoot 'src\app.manifest'

& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 `
    "/out:$outputPath" "/win32manifest:$manifest" `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sourceFiles.FullName

if ($LASTEXITCODE -ne 0) { throw 'YouTube Downloader compilation failed.' }
Get-Item -LiteralPath $outputPath
