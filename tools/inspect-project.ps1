param(
  [string]$Root = ".",
  [string]$OutDir = ".\_scan"
)

# 0) Préparation
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Root = (Resolve-Path $Root).Path

Write-Host "Scanning project at: $Root"

# 1) Arborescence (type tree)
$treeFile = Join-Path $OutDir "tree.txt"
Get-ChildItem -Path $Root -Recurse |
  Sort-Object FullName |
  ForEach-Object {
    $rel = Resolve-Path $_.FullName -Relative
    $type = if ($_.PSIsContainer) "[DIR] " else "      "
    "$type$rel"
  } | Out-File -Encoding UTF8 $treeFile

# 2) Comptage par extension
$byExt = Get-ChildItem -Path $Root -Recurse -File |
  Group-Object { $_.Extension.ToLower() } |
  Sort-Object Count -Descending |
  Select-Object Name,Count
$byExt | Format-Table | Out-String | Out-File -Encoding UTF8 (Join-Path $OutDir "by-extension.txt")

# 3) Inventaire C# (classes, namespaces, méthodes publiques)
$csFiles = Get-ChildItem -Path $Root -Recurse -Include *.cs -File
$inv = @()
foreach ($f in $csFiles) {
  $txt = Get-Content $f.FullName -Raw

  $ns = Select-String -InputObject $txt -Pattern '^\s*namespace\s+([A-Za-z0-9_.]+)' -AllMatches |
        ForEach-Object { $_.Matches.Value -replace '^\s*namespace\s+','' }

  $classes = Select-String -InputObject $txt -Pattern '^\s*(public|internal)?\s*(partial\s+)?(class|record)\s+([A-Za-z_][A-Za-z0-9_]*)' -AllMatches |
             ForEach-Object { $_.Matches.Groups[4].Value } | Sort-Object -Unique

  $publicMethods = Select-String -InputObject $txt -Pattern '^\s*public\s+(static\s+)?([A-Za-z0-9_<>,\[\]\?]+)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(' -AllMatches |
                   ForEach-Object { $_.Matches | ForEach-Object { $_.Value.Trim() } }

  $inv += [PSCustomObject]@{
    File           = (Resolve-Path $f.FullName -Relative)
    Namespaces     = ($ns -join "; ")
    Classes        = ($classes -join "; ")
    PublicMethods  = ($publicMethods -join " | ")
  }
}
$invFile = Join-Path $OutDir "cs-inventory.csv"
$inv | Export-Csv -NoTypeInformation -Encoding UTF8 $invFile

# 4) Inventaire XAML (Views/UserControls & leurs code-behind)
$xamlFiles = Get-ChildItem -Path $Root -Recurse -Include *.xaml -File
$xList = foreach ($x in $xamlFiles) {
  $txt = Get-Content $x.FullName -Raw
  $class = Select-String -InputObject $txt -Pattern 'x:Class="([^"]+)"' | ForEach-Object { $_.Matches[0].Groups[1].Value }
  $isWindow = ($txt -match '<Window\b')
  $isUC     = ($txt -match '<UserControl\b')
  [PSCustomObject]@{
    Xaml       = (Resolve-Path $x.FullName -Relative)
    XClass     = $class
    Kind       = if ($isWindow) { "Window" } elseif ($isUC) { "UserControl" } else { "Other" }
    CodeBehind = if (Test-Path ($x.FullName + ".cs")) { (Resolve-Path ($x.FullName + ".cs") -Relative) } else { "" }
  }
}
$xamlCsv = Join-Path $OutDir "xaml-inventory.csv"
$xList | Export-Csv -NoTypeInformation -Encoding UTF8 $xamlCsv

# 5) Détection simple des accès BDD (IDbConnection/CreateCommand/Execute*)
$dbHits = Select-String -Path $csFiles.FullName -Pattern 'Db\.Open\(|CreateCommand\(|ExecuteReader\(|ExecuteNonQuery\(|ExecuteScalar\(' -List |
  Select-Object Path, LineNumber, Line
$dbFile = Join-Path $OutDir "db-usages.txt"
$dbHits | ForEach-Object {
  "$($_.Path) : $($_.LineNumber) : $($_.Line.Trim())"
} | Out-File -Encoding UTF8 $dbFile

Write-Host "Scan complete. See folder: $OutDir"
