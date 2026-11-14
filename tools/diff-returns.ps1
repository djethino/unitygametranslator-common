<#
.SYNOPSIS
  Which public methods of the library return something DIFFERENT from what they returned at an earlier commit.

.DESCRIPTION
  A comparison that is replaced changes no answer; a method that RETURNS a string, a list or a number can change what a caller
  reads even when every verdict is the same (Markup.NameOf, 2026-11-14: its capitals broke a comparison to "br" in the mod).
  This builds the library at -Since and now, calls every public static method that takes no argument, one string or two
  (tools/return-probe: about 70 000 calls on a fixed list of texts) and lists the methods whose results differ. A difference
  is not an error; it is a decision someone must have taken. Methods with other parameters are NOT covered: read them.

  Not covered, on purpose: Secrets.* (a random salt).  New classes are ignored (nothing to compare with).

.EXAMPLE
  ./tools/diff-returns.ps1 -Since 9b96caf
#>
param([Parameter(Mandatory = $true)][string]$Since)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path ([IO.Path]::GetTempPath()) ("ugt-returns-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $old = Join-Path $work 'old'
    git -C $root worktree add --detach $old $Since 2>&1 | Out-Null
    $outOld = Join-Path $work 'old.txt'
    $outNew = Join-Path $work 'new.txt'
    # Each run in a copy of the probe: two builds of one project folder would share its obj/.
    foreach ($side in @(@{ Name = 'old'; Common = "$old/src/UnityGameTranslator.Common/UnityGameTranslator.Common.csproj"; Out = $outOld },
                        @{ Name = 'new'; Common = (Join-Path $root 'src/UnityGameTranslator.Common/UnityGameTranslator.Common.csproj'); Out = $outNew })) {
        $copy = Join-Path $work ('probe-' + $side.Name)
        Copy-Item (Join-Path $root 'tools/return-probe') $copy -Recurse -Exclude bin, obj
        dotnet run --project (Join-Path $copy 'ReturnProbe.csproj') -p:CommonProject="$($side.Common)" 2>$null | Set-Content $side.Out -Encoding utf8
    }

    $skip = '^(Secrets)\.'
    $shape = '^[A-Za-z0-9_]+\.[A-Za-z0-9_]+\(.*\) = '   # a build's own messages share the output
    $a = Get-Content $outOld -Encoding utf8 | Where-Object { $_ -match $shape -and $_ -notmatch $skip }
    $b = Get-Content $outNew -Encoding utf8 | Where-Object { $_ -match $shape -and $_ -notmatch $skip }
    $oldKeys = [System.Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)   # a PowerShell hashtable ignores case
    foreach ($line in $a) { $oldKeys[$line.Substring(0, $line.IndexOf(') = ') + 1)] = $line }
    $differing = @{}
    foreach ($line in $b) {
        $key = $line.Substring(0, [Math]::Max(0, $line.IndexOf(') = ') + 1))
        if ($oldKeys.ContainsKey($key) -and $oldKeys[$key] -cne $line) {
            $method = $key.Substring(0, $key.IndexOf('('))
            if (-not $differing.ContainsKey($method)) { $differing[$method] = @($oldKeys[$key], $line) }
        }
    }
    "{0} calls compared against {1}." -f $b.Count, $Since
    if ($differing.Count -eq 0) { 'No public return value differs.' }
    else {
        "{0} method(s) return something different (first example each):" -f $differing.Count
        $differing.GetEnumerator() | Sort-Object Name | ForEach-Object { "  $($_.Name)`n    was: $($_.Value[0])`n    now: $($_.Value[1])" }
    }
}
finally {
    git -C $root worktree remove --force $old 2>&1 | Out-Null
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
