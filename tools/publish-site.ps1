# Met en ligne le site de la liste des cartes (GitHub Pages) en une commande.
# Avant : dans le jeu, appuyer sur Maj+F6 (le site est cree dans Documents\Stacklands cards site).
# Ce script : verifie l'export, le copie dans docs\, fait le commit et le push.
#   -Oui        : ne pose aucune question (push direct).
#   -SansPush   : copie et commit seulement.
param([switch]$Oui, [switch]$SansPush)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Ask($question) {
    if ($Oui) { return $true }
    $r = Read-Host "$question (O/n)"
    return ($r -eq '' -or $r -match '^[oOyY]')
}

# 1. L'export du jeu
$src = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Stacklands cards site'
$index = Join-Path $src 'index.html'
if (-not (Test-Path $index)) {
    Write-Host "Aucun site trouve dans $src."
    Write-Host "Lancez le jeu, appuyez sur Maj+F6, puis relancez ce script."
    exit 1
}
$exported = (Get-Item $index).LastWriteTime
Write-Host "Export du jeu trouve : $src (fait le $($exported.ToString('dd/MM/yyyy HH:mm')))."

# Export plus ancien que le mod installe : Maj+F6 n'a sans doute pas ete refait apres la mise a jour.
$installed = Join-Path $env:USERPROFILE 'AppData\LocalLow\sokpop\Stacklands\Mods\StacklandAccess\cards_viewer.html'
if ((Test-Path $installed) -and (Get-Item $installed).LastWriteTime -gt $exported) {
    Write-Host "Attention : le mod installe est plus recent que cet export. Pensez a refaire Maj+F6 dans le jeu."
    if (-not (Ask "Publier quand meme cet export ?")) { exit 1 }
}

# 2. Copie dans docs\
$dst = Join-Path $root 'docs'
if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
Copy-Item $src $dst -Recurse
# Fichiers internes du mod (empreinte du contenu du jeu) : inutiles en ligne.
Get-ChildItem $dst -Recurse -Force -Filter '.stacklandaccess-content' | Remove-Item -Force
$langs = (Get-ChildItem (Join-Path $dst 'data') -Filter '*.json' | Where-Object { $_.Name -ne 'languages.json' }).Count
$imgs = (Get-ChildItem (Join-Path $dst 'img') -File -ErrorAction SilentlyContinue).Count
Write-Host "Site copie dans docs : $langs langues, $imgs images."

# 3. Commit
git add -A docs
git diff --cached --quiet -- docs
if ($LASTEXITCODE -eq 0) {
    Write-Host "Le site en ligne est deja a jour : rien a publier."
    exit 0
}
$changed = (git diff --cached --name-only -- docs | Measure-Object).Count
git commit -q -m "Card website update ($(Get-Date -Format 'yyyy-MM-dd'))" -- docs
if ($LASTEXITCODE -ne 0) { Write-Host "Le commit a echoue."; exit 1 }
Write-Host "Commit fait ($changed fichiers modifies)."

# 4. Push
if ($SansPush) { Write-Host "Push non fait (option -SansPush). Faites 'git push' quand vous voulez."; exit 0 }
if (-not (Ask "Envoyer sur GitHub maintenant ?")) { Write-Host "Push non fait. Faites 'git push' quand vous voulez."; exit 0 }
git push
if ($LASTEXITCODE -ne 0) {
    Write-Host "Le push a echoue (connexion, ou GitHub a des changements que vous n'avez pas : faites 'git pull --rebase' puis relancez)."
    exit 1
}

# 5. Adresse du site
$remote = git remote get-url origin 2>$null
if ($remote -match 'github\.com[:/]([^/]+)/([^/.]+)') {
    Write-Host "Envoye. Le site sera a jour d'ici une a deux minutes : https://$($Matches[1].ToLower()).github.io/$($Matches[2])/"
} else {
    Write-Host "Envoye. GitHub Pages met le site a jour d'ici une a deux minutes."
}
