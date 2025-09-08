[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

$root  = Split-Path $PSScriptRoot -Parent
$ts    = Get-Date -Format 'yyyyMMdd_HHmmss'
$art   = Join-Path $root 'artifacts'
$stage = Join-Path $art ("gpt5_pro_review_{0}" -f $ts)
$zip   = Join-Path $art ("gpt5-pro-review_{0}.zip" -f $ts)

New-Item -ItemType Directory -Path $art -Force  | Out-Null
New-Item -ItemType Directory -Path $stage -Force| Out-Null

$paths = @(
  'XtrXmlTranslator.Core',
  'XtrXmlTranslator.App',
  'XtrXmlTranslator.Tests',
  '.github',
  'XtrXmlTranslator.sln',
  'README.md'
)

$exclude = '\\bin\\|\\obj\\|\\artifacts\\|\\.git\\|\\.vs\\|\\.idea\\|\\.vscode|\\TestResults\\'

foreach ($p in $paths) {
  $full = Join-Path $root $p
  if (Test-Path $full) {
    if ((Get-Item $full).PSIsContainer) {
      Get-ChildItem -LiteralPath $full -Recurse -File |
        Where-Object { $_.FullName -notmatch $exclude } |
        ForEach-Object {
          $rel = $_.FullName.Substring($root.Length + 1)
          $dest = Join-Path $stage $rel
          $destDir = Split-Path $dest
          if (!(Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
          Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
        }
    } else {
      $destFile = Join-Path $stage $p
      $destDir  = Split-Path $destFile
      if (!(Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
      Copy-Item -LiteralPath $full -Destination $destFile -Force
    }
  }
}

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Output ("ZIP_PATH=" + $zip)

