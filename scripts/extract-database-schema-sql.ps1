$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root 'GrowDiary.Web/Infrastructure/DatabaseInitializer.Schema.cs'
$targetPath = Join-Path $root 'GrowDiary.Web/Infrastructure/DatabaseInitializer.CoreSchemaSql.cs'

$content = [System.IO.File]::ReadAllText($sourcePath, [System.Text.Encoding]::UTF8)

$existingIndexLiteral = $null
if ([System.IO.File]::Exists($targetPath)) {
  $existingTarget = [System.IO.File]::ReadAllText($targetPath, [System.Text.Encoding]::UTF8)
  if ($existingTarget.Contains('IX_Grows_TentId_Status')) {
    $existingLiteralStart = $existingTarget.IndexOf('"""', [System.StringComparison]::Ordinal)
    $existingLiteralEndStart = $existingTarget.IndexOf('    """', $existingLiteralStart + 3, [System.StringComparison]::Ordinal)
    if ($existingLiteralStart -ge 0 -and $existingLiteralEndStart -ge 0) {
      $existingIndexLiteral = $existingTarget.Substring($existingLiteralStart, $existingLiteralEndStart + '    """'.Length - $existingLiteralStart)
      $content = $content.Replace('        command.CommandText = CoreSchemaSql;', '        command.CommandText = GrowIndexSql;')
    }
  }
}

$assignmentStart = $content.IndexOf('        command.CommandText = """', [System.StringComparison]::Ordinal)
if ($assignmentStart -lt 0) {
  throw 'Core schema SQL assignment start not found.'
}

$literalStart = $content.IndexOf('"""', $assignmentStart, [System.StringComparison]::Ordinal)
$closingStart = $content.IndexOf('        """;', $literalStart + 3, [System.StringComparison]::Ordinal)
if ($closingStart -lt 0) {
  throw 'Core schema SQL assignment end not found.'
}

$literalEnd = $closingStart + '        """'.Length
$literal = $content.Substring($literalStart, $literalEnd - $literalStart)
$executeStart = $content.IndexOf('        command.ExecuteNonQuery();', $literalEnd, [System.StringComparison]::Ordinal)
if ($executeStart -lt 0) {
  throw 'Core schema SQL execute call not found.'
}

$assignmentEnd = $executeStart + '        command.ExecuteNonQuery();'.Length

$replacement = '        command.CommandText = CoreSchemaSql;
        command.ExecuteNonQuery();'
$nextContent = $content.Substring(0, $assignmentStart) + $replacement + $content.Substring($assignmentEnd)

$indexConstant = if ($existingIndexLiteral) {
  @"

    private const string GrowIndexSql = $existingIndexLiteral;
"@
} else {
  ''
}

$targetContent = @"
namespace GrowDiary.Web.Infrastructure;

public sealed partial class DatabaseInitializer
{
    private const string CoreSchemaSql = $literal;
$indexConstant
}
"@

[System.IO.File]::WriteAllText($sourcePath, $nextContent, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($targetPath, $targetContent, [System.Text.Encoding]::UTF8)

Write-Host 'Extracted CoreSchemaSql from DatabaseInitializer.Schema.cs.'
