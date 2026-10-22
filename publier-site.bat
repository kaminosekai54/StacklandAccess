@echo off
rem Met en ligne le site de la liste des cartes. Avant : Maj+F6 dans le jeu.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\publish-site.ps1" %*
pause
