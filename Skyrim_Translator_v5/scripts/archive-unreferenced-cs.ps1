param(
  [string]$ReportPath
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path '.').Path
if (-not $ReportPath) {
  $latest = Get-ChildItem 'docs/current_state' -Filter 'unreferenced_cs_*.txt' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($null -eq $latest) { Write-Host '[WARN] No report found'; exit 0 }
  $ReportPath = $latest.FullName
}
if (-not (Test-Path -LiteralPath $ReportPath)) { Write-Host "[WARN] Report not found: $ReportPath"; exit 0 }

$moved = 0; $skipped = 0
Get-Content -LiteralPath $ReportPath | ForEach-Object {
  $line = $_
  if ([string]::IsNullOrWhiteSpace($line)) { return }
  if (-not (Test-Path -LiteralPath $line)) { $skipped++; return }
  $full = (Resolve-Path -LiteralPath $line).Path
  Push-Location $root
  try {
    $rel = (Resolve-Path -LiteralPath $full -Relative)
    # check tracked in git
    $tracked = git ls-files -- "$rel"
    if ([string]::IsNullOrWhiteSpace($tracked)) { $skipped++; return }
    $dest = Join-Path 'archive' $rel
    $destDir = Split-Path -Parent $dest
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    git mv -- "$rel" "$dest"
    $moved++
  }
  catch {
    Write-Host ("[WARN] Skip: {0} -> {1}" -f $line, $_.Exception.Message)
    $skipped++
  }
  finally { Pop-Location }
}
Write-Host ("[INFO] Moved files: {0}; Skipped: {1}" -f $moved, $skipped)

