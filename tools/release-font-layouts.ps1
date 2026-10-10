#!/usr/bin/env pwsh
# Called by both release chains (the mod's prepare-release.ps1, the Manager's build script): is the
# committed Font layout table still the one today's AssetRipper type tree dump gives?
#   up to date               -> exit 0
#   a Font layout changed    -> exit 1, with the remedy (regenerate, read the diff, commit common): a game
#                               of that version would be read wrongly
#   only newer versions known -> said in yellow, exit 0: read right, but called "newer than the table"
#                               (the Manager's packaging is also its everyday build: not a stop)
#   the dump is unreachable  -> said in yellow, exit 0: the table stays right for every version it knows
# Python's standard library only (urllib, lzma, zipfile): no virtual environment needed.

$generator = Join-Path $PSScriptRoot 'generate-font-layouts.py'
Write-Host "`nChecking the Font layouts against today's Unity type tree dump..." -ForegroundColor Yellow
$out = & python $generator --check 2>&1
$code = $LASTEXITCODE
$out | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
switch ($code) {
    0 { exit 0 }
    3 {
        Write-Host "  The dump could not be fetched: releasing with the committed table (right for every version it knows)." -ForegroundColor Yellow
        exit 0
    }
    4 {
        Write-Host "  Newer Unity versions are known, with no new Font layout. Take them: python common/tools/generate-font-layouts.py, then commit common." -ForegroundColor Yellow
        exit 0
    }
    default {
        Write-Host "  A Font layout changed in a newer Unity. Run: python common/tools/generate-font-layouts.py" -ForegroundColor Red
        Write-Host "  then read the diff of FontLayouts.Tables.g.cs, run the common checks, and commit common before releasing." -ForegroundColor Yellow
        exit 1
    }
}
