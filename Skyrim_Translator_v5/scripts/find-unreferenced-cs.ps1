param(
  [string]$SolutionRoot = (Resolve-Path ".").Path
)

# Directories considered part of the active solution (SDK-style projects include *.cs by default)
$projectDirs = @(
  "XtrXmlTranslator.Core",
  "XtrXmlTranslator.App",
  "XtrXmlTranslator.Tests",
  "XtrXmlTranslator.Benchmarks",
  "XtrXmlTranslator.DebugHarness"
) | ForEach-Object { Join-Path $SolutionRoot $_ }

# Gather all cs files under repo
$allCs = Get-ChildItem -Path $SolutionRoot -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch "\\artifacts\\" }

# Partition: in-project vs out-of-project
$inProject = @()
$outProject = @()
foreach ($f in $allCs) {
  $parent = Split-Path -Parent $f.FullName
  $isIn = $false
  foreach ($pd in $projectDirs) {
    if ($parent.StartsWith($pd, [System.StringComparison]::OrdinalIgnoreCase)) { $isIn = $true; break }
  }
  if ($isIn) { $inProject += $f } else { $outProject += $f }
}

Write-Host "[INFO] Project directories:" -ForegroundColor Cyan
$projectDirs | ForEach-Object { Write-Host "  - $_" }

Write-Host "`n[INFO] In-project .cs files: $($inProject.Count)" -ForegroundColor Green
Write-Host "[INFO] Out-of-project .cs files (candidates to archive/remove): $($outProject.Count)" -ForegroundColor Yellow

if ($outProject.Count -gt 0) {
  $report = Join-Path $SolutionRoot "docs/current_state/unreferenced_cs_$(Get-Date -Format yyyyMMdd_HHmmss).txt"
  New-Item -ItemType Directory -Path (Split-Path -Parent $report) -Force | Out-Null
  $outProject | Select-Object -ExpandProperty FullName | Sort-Object | Set-Content -Path $report -Encoding utf8
  Write-Host "[INFO] Report saved: $report" -ForegroundColor Cyan
}

# Optional: print a few sample paths
$outProject | Select-Object -First 20 | ForEach-Object { Write-Host "  - " $_.FullName }

