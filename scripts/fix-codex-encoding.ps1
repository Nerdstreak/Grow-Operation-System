# One-off repair for encoding damage introduced during the grow-detail split:
#   * Mojibake: original UTF-8 bytes were decoded as Windows-1252 and re-saved as UTF-8.
#   * UTF-8 BOM written at the start of split CSS/TS modules.
# Strategy: rebuild the exact mojibake->correct map by replaying the corruption,
# then do targeted replacements (legit single-char umlauts are never touched),
# and rewrite every React src text file as UTF-8 *without* BOM.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$srcRoot = Join-Path $root 'GrowDiary.React\src'

$utf8Read = [System.Text.Encoding]::UTF8
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$cp1252 = [System.Text.Encoding]::GetEncoding(1252)

# Characters that may have been corrupted (German letters + common punctuation).
# Expressed as code points only -> no non-ASCII literals in this file (PS 5.1 safe).
$targets = @(
  0x00FC, 0x00F6, 0x00E4, 0x00C4, 0x00D6, 0x00DC, 0x00DF,  # u o a A O U ss (umlauts)
  0x00B7, 0x00B0, 0x00BB, 0x00AB, 0x00A0,                  # middot degree >> << nbsp
  0x2013, 0x2014, 0x2026,                                  # en-dash em-dash ellipsis
  0x201E, 0x201C, 0x201D, 0x2018, 0x2019, 0x2022           # quotes + bullet
)

# corrupt(c) = UTF-8 bytes of c, then decoded as Windows-1252.
$pairs = New-Object System.Collections.Generic.List[object]
foreach ($cp in $targets) {
  $ch = [string][char]$cp
  $moji = $cp1252.GetString($utf8Read.GetBytes($ch))
  if ($moji -ne $ch) {
    $pairs.Add([pscustomobject]@{ From = $moji; To = $ch })
  }
}
# Longest mojibake sequences first (3-char before 2-char).
$pairs = $pairs | Sort-Object { $_.From.Length } -Descending

function Test-HasBom([string]$path) {
  $b = [System.IO.File]::ReadAllBytes($path)
  return ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)
}

$files = Get-ChildItem -Path $srcRoot -Recurse -File -Include *.ts, *.tsx, *.css
$changed = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
  $path = $file.FullName
  $hadBom = Test-HasBom $path
  $orig = [System.IO.File]::ReadAllText($path, $utf8Read)   # BOM consumed on read
  $fixed = $orig
  foreach ($p in $pairs) { $fixed = $fixed.Replace($p.From, $p.To) }

  $mojiFixed = ($fixed -ne $orig)
  if ($mojiFixed -or $hadBom) {
    [System.IO.File]::WriteAllText($path, $fixed, $utf8NoBom)
    $tags = @()
    if ($mojiFixed) { $tags += 'mojibake' }
    if ($hadBom) { $tags += 'BOM' }
    $rel = $path.Substring($root.Length + 1)
    $changed.Add(('{0}  [{1}]' -f $rel, ($tags -join '+')))
  }
}

Write-Host ("Processed {0} files, changed {1}." -f $files.Count, $changed.Count)
foreach ($c in $changed) { Write-Host "  $c" }
