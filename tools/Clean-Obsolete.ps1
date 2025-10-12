param(
  [string]$Root = ".",
  [ValidateSet("Report","Purge")]
  [string]$Mode = "Report"
)

# ====== helpers ======
function Get-SourceFiles {
  param($Root)
  Get-ChildItem -Path $Root -Recurse -File -Include *.cs,*.xaml |
    Where-Object {
      $_.FullName -notmatch "\\obj\\|\\bin\\|\\.trash_" -and
      $_.Name -notmatch '\.g\.cs$'
    }
}

function Get-AllText {
  param($Files)
  # lit toutes les sources comme référence (y compris XAML pour x:Class)
  ($Files | ForEach-Object { Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue }) -join "`n"
}

function Get-TypeDeclarations {
  param($Files)
  $out = @()
  foreach($f in $Files){
    if($f.Extension -ieq ".cs"){
      $matches = (Get-Content $f.FullName -Raw | Select-String -AllMatches -Pattern '^\s*(public|internal)\s+(class|enum|struct)\s+([A-Za-z_]\w*)' -ErrorAction SilentlyContinue).Matches
      foreach($m in $matches){
        $out += [PSCustomObject]@{
          TypeName = $m.Groups[3].Value
          File     = $f.FullName
        }
      }
    }
    elseif($f.Extension -ieq ".xaml"){
      # récupère la classe partielle depuis x:Class="Namespace.TypeName"
      $m = (Get-Content $f.FullName -Raw | Select-String -Pattern 'x:Class\s*=\s*"([^"]+)"' -ErrorAction SilentlyContinue).Matches
      foreach($mm in $m){
        $full = $mm.Groups[1].Value
        $typeName = $full.Split('.')[-1]
        $out += [PSCustomObject]@{
          TypeName = $typeName
          File     = $f.FullName
        }
      }
    }
  }
  $out
}

# ====== main ======
Push-Location $Root

$files = Get-SourceFiles -Root (Get-Location).Path
if(-not $files){ Write-Host "Aucun fichier trouvé."; Pop-Location; exit }

$allText = Get-AllText -Files $files
$decls   = Get-TypeDeclarations -Files $files

# Doublons de types
$dupGroups = $decls | Group-Object TypeName | Where-Object { $_.Count -gt 1 }

# Candidats non référencés
# 1) nom de fichier (BaseName) jamais cité
# 2) et aucun des types déclarés dans ce fichier n'est cité
$candidates = @()
foreach($f in $files){
  $base = [IO.Path]::GetFileNameWithoutExtension($f.Name)
  $hasFileRef = $allText -match "\b$([regex]::Escape($base))\b"

  $typesInFile = $decls | Where-Object { $_.File -eq $f.FullName } | Select-Object -ExpandProperty TypeName -ErrorAction SilentlyContinue
  $hasTypeRef = $false
  foreach($t in $typesInFile){
    if($allText -match "\b$([regex]::Escape($t))\b"){ $hasTypeRef = $true; break }
  }

  if(-not $hasFileRef -and -not $hasTypeRef){
    # garde quand même ce qui est indispensable au projet (App.xaml, MainWindow.xaml, *.csproj exclus de la recherche)
    if($f.Name -in @("App.xaml","App.xaml.cs","MainWindow.xaml","MainWindow.xaml.cs")){ continue }
    $candidates += $f.FullName
  }
}

# Sauvegarde rapport
$reportDir = Join-Path (Get-Location) ".cleanup_reports"
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$obsFile = Join-Path $reportDir "obsolete.txt"
$dupFile = Join-Path $reportDir "duplicate-types.txt"

$candidates | Sort-Object | Set-Content $obsFile -Encoding UTF8
if($dupGroups){
  $dupOut = @()
  foreach($g in $dupGroups){
    $dupOut += "=== Type: $($g.Name) ==="
    $dupOut += ($g.Group | Select-Object -ExpandProperty File)
    $dupOut += ""
  }
  $dupOut | Set-Content $dupFile -Encoding UTF8
}

Write-Host "Rapport créé:"
Write-Host " - Candidats obsolètes: $obsFile"
if($dupGroups){ Write-Host " - Doublons de types: $dupFile" } else { Write-Host " - Aucun doublon de types détecté." }

if($Mode -eq "Purge"){
  $trash = ".trash_{0:yyyyMMdd_HHmm}" -f (Get-Date)
  New-Item -ItemType Directory -Force -Path $trash | Out-Null

  $toRemove = Get-Content $obsFile -ErrorAction SilentlyContinue
  foreach($p in $toRemove){
    if(Test-Path $p){
      $rel = Resolve-Path $p
      $dest = Join-Path $trash ([IO.Path]::GetFileName($p))
      Move-Item -Force $p $dest
      Write-Host "Déplacé -> $dest"
    }
  }
  Write-Host "Purge terminée. Les fichiers sont dans '$trash'."
}

Pop-Location
