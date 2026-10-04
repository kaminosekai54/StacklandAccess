# Builds the mod in Release and creates release/StacklandAccess-<version>.zip
# (contents: a StacklandAccess folder to extract into the game's Mods folder).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$version = (Get-Content manifest.json -Raw | ConvertFrom-Json).version
dotnet build StacklandAccess.csproj -c Release -p:SkipInstall=true
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$out = Join-Path $root 'release'
$stage = Join-Path $out 'StacklandAccess'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path "$stage/native", "$stage/Sounds" | Out-Null

Copy-Item 'bin/Release/netstandard2.1/StacklandAccess.dll' $stage
Copy-Item manifest.json, StacklandAccess_strings.tsv, icon.png, LICENSE, THIRD_PARTY.md, README.md, README.fr.md, CHANGELOG.md $stage
Copy-Item native/*.dll "$stage/native"
Copy-Item Sounds/* "$stage/Sounds"
if (Test-Path workshop.txt) { Copy-Item workshop.txt $stage }

$zip = Join-Path $out "StacklandAccess-$version.zip"
Compress-Archive -Path $stage -DestinationPath $zip -Force
Write-Host "Release ready: $zip"
