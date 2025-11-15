<#
.SYNOPSIS
Skrypt do zarządzania wersjami .NET i pakietami NuGet w projektach z central package management.

.DESCRIPTION
Skrypt:
- Przyjmuje listę wersji .NET jako argumenty (np. net8.0 net9.0 net10.0)
- Ustawia TargetFrameworks we wszystkich .csproj
- Usuwa atrybuty Version z PackageReference (centralne zarządzanie)
- Aktualizuje Directory.Packages.props z osobnymi ItemGroup dla każdej wersji .NET
- Dla każdej wersji .NET znajduje najlepsze dostępne wersje pakietów
- Usuwa puste ItemGroup z projektów

.PARAMETER TargetFrameworks
Lista target frameworks do wspierania (np. "net8.0", "net9.0", "net10.0")

.EXAMPLE
.\Update.ps1 net8.0 net9.0 net10.0

.EXAMPLE
.\Update.ps1 -TargetFrameworks net8.0,net9.0,net10.0
#>

param(
    [Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)]
    [string[]]$TargetFrameworks = @("net6.0", "net7.0", "net8.0", "net9.0", "net10.0")
)

$ErrorActionPreference = "Stop"

# Walidacja parametrów
if ($TargetFrameworks.Count -eq 0) {
    Write-Error "Musisz podać przynajmniej jedną wersję .NET (np: .\Update.ps1 net8.0 net9.0)"
    exit 1
}

Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "AKTUALIZACJA PROJEKTU DLA WERSJI .NET: $($TargetFrameworks -join ', ')" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan

# ============================================================================
# FUNKCJA: Pobieranie najlepszej wersji pakietu dla danej wersji .NET
# ============================================================================
function Get-BestPackageVersion {
    param(
        [string]$PackageId,
        [string]$TargetFramework,
        [bool]$AllowPrerelease
    )
    
    try {
        $url = "https://api.nuget.org/v3-flatcontainer/$($PackageId.ToLower())/index.json"
        $response = Invoke-RestMethod $url -TimeoutSec 15
        $versions = $response.versions
        
        # Wyciągnij major version z target framework (np. net8.0 -> 8)
        # Używamy regex aby znaleźć pierwszą liczbę po "net"
        if ($TargetFramework -match 'net(\d+)') {
            $targetMajor = [int]$matches[1]
        } else {
            Write-Warning "Nie można wyciągnąć major version z $TargetFramework"
            return $null
        }
        
        # Przetwórz wszystkie wersje
        $allVersions = $versions | ForEach-Object {
            try {
                $v = [version]($_ -replace '-.*', '')
                [PSCustomObject]@{
                    OriginalString = $_
                    Version = $v
                    IsPrerelease = $_ -match '-'
                }
            } catch {
                $null
            }
        } | Where-Object { $_ -ne $null }
        
        # Strategia wyboru (w kolejności priorytetu):
        # WAŻNE: Dla net8.0 używamy TYLKO pakietów 8.x lub niższych!
        # Dla net9.0 używamy TYLKO pakietów 9.x lub niższych!
        # 1. Najnowsza STABLE wersja z major == targetMajor
        # 2. Najnowsza PRERELEASE wersja z major == targetMajor (tylko jeśli AllowPrerelease)
        # 3. Najnowsza STABLE wersja z major < targetMajor
        # 4. Najnowsza dostępna stable wersja (jako fallback)
        
        $best = $null
        
        # 1. Stable z dokładnie tym major (najpierw próbujemy exact match)
        $candidates = $allVersions | Where-Object { 
            -not $_.IsPrerelease -and $_.Version.Major -eq $targetMajor 
        }
        if ($candidates) {
            $best = $candidates | 
                Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | 
                Select-Object -First 1
        }
        
        # 2. Prerelease z dokładnie tym major (jeśli dozwolone)
        if (-not $best -and $AllowPrerelease) {
            $candidates = $allVersions | Where-Object { 
                $_.Version.Major -eq $targetMajor 
            }
            if ($candidates) {
                $best = $candidates | 
                    Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | 
                    Select-Object -First 1
            }
        }
        
        # 3. Najnowsza stable z mniejszym major (WAŻNE: tylko mniejsze, nie większe!)
        if (-not $best) {
            $candidates = $allVersions | Where-Object { 
                -not $_.IsPrerelease -and $_.Version.Major -lt $targetMajor 
            }
            if ($candidates) {
                $best = $candidates | 
                    Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | 
                    Select-Object -First 1
            }
        }
        
        # 4. W ostateczności - najnowsza stable z major <= targetMajor
        # (to jest fallback dla pakietów które nie mają wersji zgodnej z targetMajor)
        if (-not $best) {
            $candidates = $allVersions | Where-Object { 
                -not $_.IsPrerelease -and $_.Version.Major -le $targetMajor
            }
            if ($candidates) {
                $best = $candidates | 
                    Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | 
                    Select-Object -First 1
            }
        }
        
        # 5. Jeśli nadal nic, weź najnowszą stable (dowolny major) - tylko jako ostateczność
        if (-not $best) {
            $candidates = $allVersions | Where-Object { -not $_.IsPrerelease }
            if ($candidates) {
                $best = $candidates | 
                    Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | 
                    Select-Object -First 1
                Write-Warning "Dla $PackageId/$TargetFramework nie znaleziono wersji <= $targetMajor, używam: $($best.OriginalString)"
            }
        }
        
        if ($best) {
            return $best.OriginalString
        } else {
            Write-Warning "Nie znaleziono żadnej wersji dla $PackageId"
            return $null
        }
        
    } catch {
        Write-Warning "Błąd pobierania wersji dla ${PackageId}: $($_.Exception.Message)"
        return $null
    }
}

# ============================================================================
# KROK 1: Ustawienie TargetFrameworks we wszystkich .csproj
# ============================================================================
Write-Host "`n[1/5] Aktualizacja TargetFrameworks w projektach .csproj..." -ForegroundColor Yellow

$targetFrameworksString = $TargetFrameworks -join ';'
$csprojs = Get-ChildItem -Recurse -Filter "*.csproj" | Where-Object { 
    $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' 
}

foreach ($proj in $csprojs) {
    [xml]$xml = Get-Content $proj.FullName
    $changed = $false
    
    # Znajdź lub utwórz PropertyGroup
    $propertyGroups = @($xml.Project.PropertyGroup)
    if ($propertyGroups.Count -eq 0 -or $null -eq $propertyGroups[0]) {
        $pg = $xml.CreateElement("PropertyGroup")
        $xml.Project.AppendChild($pg) | Out-Null
        $propertyGroups = @($pg)
    }
    
    $pg = $propertyGroups[0]
    
    # Usuń stare TargetFramework (pojedyncze)
    $tfNodes = @($pg.SelectNodes("TargetFramework"))
    foreach ($node in $tfNodes) {
        $pg.RemoveChild($node) | Out-Null
        $changed = $true
    }
    
    # Sprawdź/ustaw TargetFrameworks (mnoga)
    $tfsNodes = @($pg.SelectNodes("TargetFrameworks"))
    if ($tfsNodes.Count -eq 0) {
        $tfsNode = $xml.CreateElement("TargetFrameworks")
        $tfsNode.InnerText = $targetFrameworksString
        $pg.AppendChild($tfsNode) | Out-Null
        $changed = $true
        Write-Host "  ✓ $($proj.Name): Dodano TargetFrameworks=$targetFrameworksString" -ForegroundColor Green
    } else {
        $currentValue = $tfsNodes[0].InnerText
        if ($currentValue -ne $targetFrameworksString) {
            $tfsNodes[0].InnerText = $targetFrameworksString
            $changed = $true
            Write-Host "  ✓ $($proj.Name): Zmieniono $currentValue → $targetFrameworksString" -ForegroundColor Green
        }
    }
    
    # Usuń puste ItemGroup z .csproj
    $igroups = $xml.SelectNodes("//ItemGroup")
    foreach($ig in $igroups) {
        if ($ig.ChildNodes.Count -eq 0) {
            $parent = $ig.ParentNode
            $parent.RemoveChild($ig) | Out-Null
            $changed = $true
            Write-Host "  • $($proj.Name): Usunięto pusty ItemGroup" -ForegroundColor Gray
        }
    }
    
    if ($changed) {
        $xml.Save($proj.FullName)
    }
}

# ============================================================================
# KROK 2: Usunięcie atrybutów Version z PackageReference
# ============================================================================
Write-Host "`n[2/5] Usuwanie wersji z PackageReference (central package management)..." -ForegroundColor Yellow

foreach ($proj in $csprojs) {
    $content = Get-Content $proj.FullName -Raw
    $originalContent = $content
    
    # Usuń Version="..." z PackageReference
    $content = $content -replace '(<PackageReference\s+Include="[^"]+")(\s+Version="[^"]+")', '$1'
    
    if ($content -ne $originalContent) {
        $content | Set-Content $proj.FullName -NoNewline
        Write-Host "  ✓ $($proj.Name): Usunięto atrybuty Version" -ForegroundColor Green
    }
}

# ============================================================================
# KROK 3: Zebranie wszystkich używanych pakietów z .csproj
# ============================================================================
Write-Host "`n[3/5] Zbieranie listy pakietów z projektów..." -ForegroundColor Yellow

$allPackages = @{}
foreach ($proj in $csprojs) {
    [xml]$xml = Get-Content $proj.FullName
    $packageRefs = $xml.Project.ItemGroup.PackageReference
    foreach ($pkg in $packageRefs) {
        if ($pkg.Include) {
            $allPackages[$pkg.Include] = $true
        }
    }
}

$packageList = $allPackages.Keys | Sort-Object
Write-Host "  Znaleziono $($packageList.Count) unikalnych pakietów" -ForegroundColor Cyan

# ============================================================================
# KROK 4: Aktualizacja Directory.Packages.props
# ============================================================================
Write-Host "`n[4/5] Aktualizacja Directory.Packages.props..." -ForegroundColor Yellow

$propsFiles = Get-ChildItem -Recurse -Filter "Directory.Packages.props" | Where-Object { 
    $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' 
}

foreach ($propsFile in $propsFiles) {
    Write-Host "`n  Przetwarzanie: $($propsFile.FullName)" -ForegroundColor Cyan
    
    # Backup
    $backupPath = "$($propsFile.FullName).bak"
    Copy-Item $propsFile.FullName $backupPath -Force
    Write-Host "    • Utworzono backup: $backupPath" -ForegroundColor Gray
    
    [xml]$xml = Get-Content $propsFile.FullName
    
    # Zachowaj PropertyGroup
    $propertyGroup = $xml.Project.PropertyGroup
    
    # Usuń wszystkie warunkowe ItemGroup (z Condition na TargetFramework)
    $itemGroups = @($xml.Project.ItemGroup | Where-Object { 
        $_.Condition -match "TargetFramework" -or $_.PackageVersion.Count -gt 0
    })
    foreach ($ig in $itemGroups) {
        $xml.Project.RemoveChild($ig) | Out-Null
    }
    
    # Usuń puste ItemGroup z Directory.Packages.props
    $emptyGroups = @($xml.SelectNodes("//ItemGroup") | Where-Object { $_.ChildNodes.Count -eq 0 })
    foreach ($eg in $emptyGroups) {
        $parent = $eg.ParentNode
        $parent.RemoveChild($eg) | Out-Null
        Write-Host "    • Usunięto pusty ItemGroup" -ForegroundColor Gray
    }
    
    # Dla każdej wersji .NET utwórz osobny ItemGroup
    foreach ($tf in $TargetFrameworks) {
        Write-Host "    • Przetwarzanie $tf..." -ForegroundColor Yellow
        
        # Określ czy używać prerelease (dla net10.0 i nowszych)
        $tfMajor = [int]($tf -replace '[^\d]', '')
        $allowPrerelease = $tfMajor -ge 10
        
        $itemGroup = $xml.CreateElement("ItemGroup")
        $itemGroup.SetAttribute("Condition", "'`$(TargetFramework)' == '$tf'")
        
        foreach ($packageId in $packageList) {
            $version = Get-BestPackageVersion -PackageId $packageId -TargetFramework $tf -AllowPrerelease $allowPrerelease
            
            if ($version) {
                $pkgVersion = $xml.CreateElement("PackageVersion")
                $pkgVersion.SetAttribute("Include", $packageId)
                $pkgVersion.SetAttribute("Version", $version)
                $itemGroup.AppendChild($pkgVersion) | Out-Null
                
                $prereleaseLabel = if ($version -match '-') { " (prerelease)" } else { "" }
                Write-Host "      - $packageId → $version$prereleaseLabel" -ForegroundColor Gray
            } else {
                Write-Warning "      ✗ Nie można pobrać wersji dla $packageId"
            }
        }
        
        $xml.Project.AppendChild($itemGroup) | Out-Null
    }
    
    # Zapisz
    $xml.Save($propsFile.FullName)
    Write-Host "    ✓ Zapisano $($propsFile.FullName)" -ForegroundColor Green
}

# ============================================================================
# KROK 5: Sprzątanie
# ============================================================================
Write-Host "`n[5/5] Sprzątanie starych plików..." -ForegroundColor Yellow

# Usuń stare backupy starsze niż 7 dni
$oldBackups = Get-ChildItem -Recurse -Filter "*.bak" -ErrorAction SilentlyContinue | Where-Object { 
    $_.LastWriteTime -lt (Get-Date).AddDays(-7) 
}
foreach ($old in $oldBackups) {
    try {
        Remove-Item $old.FullName -Force
        Write-Host "  • Usunięto stary backup: $($old.Name)" -ForegroundColor Gray
    } catch {
        Write-Warning "Nie można usunąć: $($old.Name)"
    }
}

# Usuń pliki .NEW.xml jeśli istnieją
$newFiles = Get-ChildItem -Recurse -Filter "*.NEW.xml" -ErrorAction SilentlyContinue
foreach ($new in $newFiles) {
    try {
        Remove-Item $new.FullName -Force
        Write-Host "  • Usunięto: $($new.Name)" -ForegroundColor Gray
    } catch {
        Write-Warning "Nie można usunąć: $($new.Name)"
    }
}

# ============================================================================
# ZAKOŃCZENIE
# ============================================================================
Write-Host "`n" + ("=" * 80) -ForegroundColor Green
Write-Host "✓ ZAKOŃCZONO POMYŚLNIE!" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "`nKolejne kroki:" -ForegroundColor Cyan
Write-Host "  1. Uruchom: dotnet restore" -ForegroundColor White
Write-Host "  2. Uruchom: dotnet build" -ForegroundColor White
Write-Host "  3. Sprawdź czy wszystko działa poprawnie" -ForegroundColor White
Write-Host "`nBackupy Directory.Packages.props zostały zapisane z rozszerzeniem .bak" -ForegroundColor Yellow
Write-Host ""