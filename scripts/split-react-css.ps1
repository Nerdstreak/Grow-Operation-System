$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root 'GrowDiary.React/src/index.css'
$stylesDir = Join-Path $root 'GrowDiary.React/src/styles'

$content = [System.IO.File]::ReadAllText($sourcePath, [System.Text.Encoding]::UTF8)
[System.IO.Directory]::CreateDirectory($stylesDir) | Out-Null

$sections = @(
  @{ File = '00-base.css'; Start = $null; End = '/* Grow Wizard */' },
  @{ File = '10-grow-wizard-legacy.css'; Start = '/* Grow Wizard */'; End = '/* Structure fix: Settings' },
  @{ File = '20-settings.css'; Start = '/* Structure fix: Settings'; End = 'FRONTEND-1 LIVE HOME' },
  @{ File = '30-live-home.css'; Start = 'FRONTEND-1 LIVE HOME'; End = '/* === Grow OS Frontend V1 Rebuild === */' },
  @{ File = '40-v1-core.css'; Start = '/* === Grow OS Frontend V1 Rebuild === */'; End = '/* === V1 POLISH PASS' },
  @{ File = '50-v1-polish.css'; Start = '/* === V1 POLISH PASS'; End = '/* === V1 RC2' },
  @{ File = '60-v1-rc2.css'; Start = '/* === V1 RC2'; End = '/* ADD-BACK-1' },
  @{ File = '70-addback-assistant.css'; Start = '/* ADD-BACK-1'; End = 'GROW-2' },
  @{ File = '80-grow-wizard-final.css'; Start = 'GROW-2'; End = '/* === OPS-1' },
  @{ File = '90-operations.css'; Start = '/* === OPS-1'; End = $null }
)

if ($content.IndexOf('/* Grow Wizard */', [System.StringComparison]::Ordinal) -lt 0) {
  $existingModulePaths = $sections | ForEach-Object { Join-Path $stylesDir $_.File }
  $missingModule = $existingModulePaths | Where-Object { -not [System.IO.File]::Exists($_) } | Select-Object -First 1
  if ($missingModule) {
    throw "index.css no longer contains split markers and existing CSS module is missing: $missingModule"
  }

  $content = ($existingModulePaths | ForEach-Object {
    [System.IO.File]::ReadAllText($_, [System.Text.Encoding]::UTF8).Trim()
  }) -join ([System.Environment]::NewLine + [System.Environment]::NewLine)
}

function Get-MarkerIndex([string]$text, [string]$marker) {
  $index = $text.IndexOf($marker, [System.StringComparison]::Ordinal)
  if ($index -lt 0) {
    throw "CSS split marker not found: $marker"
  }

  $commentStart = $text.LastIndexOf('/*', $index, [System.StringComparison]::Ordinal)
  if ($commentStart -ge 0) {
    $commentEnd = $text.LastIndexOf('*/', $index, [System.StringComparison]::Ordinal)
    if ($commentStart -gt $commentEnd) {
      return $commentStart
    }
  }

  return $index
}

$imports = New-Object System.Collections.Generic.List[string]

foreach ($section in $sections) {
  $start = if ($null -eq $section.Start) { 0 } else { Get-MarkerIndex $content $section.Start }
  $end = if ($null -eq $section.End) { $content.Length } else { Get-MarkerIndex $content $section.End }

  if ($end -le $start) {
    throw "Invalid CSS split range for $($section.File)"
  }

  $segment = $content.Substring($start, $end - $start).Trim()
  $targetPath = Join-Path $stylesDir $section.File
  [System.IO.File]::WriteAllText($targetPath, $segment + [System.Environment]::NewLine, [System.Text.Encoding]::UTF8)
  $imports.Add("@import './styles/$($section.File)';") | Out-Null
}

$entrypoint = ($imports -join [System.Environment]::NewLine) + [System.Environment]::NewLine
[System.IO.File]::WriteAllText($sourcePath, $entrypoint, [System.Text.Encoding]::UTF8)

Write-Host "Split index.css into $($sections.Count) style modules."
