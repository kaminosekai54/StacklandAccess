# Copies the public card website made in the game with Shift+F6 into docs/ (served by GitHub Pages).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Stacklands cards site'
if (-not (Test-Path (Join-Path $src 'index.html'))) { throw "Run the game and press Shift+F6 first ($src not found)." }
$dst = Join-Path $root 'docs'
if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
Copy-Item $src $dst -Recurse
Write-Host "Website copied to $dst. Commit and push, then enable GitHub Pages (Settings > Pages > main branch, /docs folder)."
