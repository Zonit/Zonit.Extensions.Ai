<#
.SYNOPSIS
Updates .NET target frameworks and manages NuGet packages with Central Package Management.

.DESCRIPTION
This script:
- Updates TargetFrameworks in all .csproj files
- Removes Version attributes from PackageReference (enables central management)
- Updates Directory.Packages.props with conditional ItemGroups per framework
- Finds best compatible package versions for each framework
- Automatically detects if framework requires prerelease packages

.PARAMETER TargetFrameworks
List of target frameworks to support (e.g., "net8.0", "net9.0", "net10.0")

.EXAMPLE
.\Update-DotNet.ps1 net8.0 net9.0 net10.0

.EXAMPLE
.\Update-DotNet.ps1 -TargetFrameworks net8.0,net9.0,net10.0

.NOTES
Created for Zonit organization-wide .NET updates
The script automatically detects preview .NET versions and uses prerelease packages only when necessary.
#>

param(
    [Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)]
    [string[]]$TargetFrameworks = @("net8.0", "net9.0", "net10.0")
)

$ErrorActionPreference = "Stop"

if ($TargetFrameworks.Count -eq 0) {
    Write-Error "Must provide at least one .NET version (e.g., .\Update-DotNet.ps1 net8.0 net9.0)"
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host ".NET Project Update" -ForegroundColor Cyan
Write-Host "Target Frameworks: $($TargetFrameworks -join ', ')" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ============================================================================
# FUNCTION: Detect if framework requires prerelease packages
# ============================================================================
function Test-RequiresPrerelease {
    param(
        [string]$TargetFramework,
        [string]$SamplePackageId = "Microsoft.Extensions.Logging"
    )
    
    try {
        Write-Verbose "Testing if $TargetFramework requires prerelease packages..."
        
        # Extract major version from framework
        if ($TargetFramework -notmatch 'net(\d+)\.') {
            return $false
        }
        $targetMajor = [int]$matches[1]
        
        # Check if stable packages exist for this framework version
        $url = "https://api.nuget.org/v3-flatcontainer/$($SamplePackageId.ToLower())/index.json"
        $response = Invoke-RestMethod $url -TimeoutSec 10 -ErrorAction SilentlyContinue
        
        if (-not $response -or -not $response.versions) {
            return $false
        }
        
        # Check if there are any stable versions matching the target major version
        $stableVersionsExist = $response.versions | Where-Object {
            $_ -notmatch '-' -and $_ -match '^(\d+)\.'
        } | ForEach-Object {
            try {
                $v = [version]($_ -replace '-.*', '')
                $v.Major -ge $targetMajor
            } catch {
                $false
            }
        } | Where-Object { $_ -eq $true }
        
        # If no stable versions exist for this major version, we need prerelease
        $needsPrerelease = -not $stableVersionsExist
        
        if ($needsPrerelease) {
            Write-Host "  ℹ️  $TargetFramework appears to be prerelease - will allow preview packages" -ForegroundColor Yellow
        }
        
        return $needsPrerelease
    } catch {
        Write-Verbose "Failed to detect prerelease requirement: $_"
        return $false
    }
}

# ============================================================================
# FUNCTION: Get best package version for target framework
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
        
        if (-not $versions -or $versions.Count -eq 0) {
            Write-Warning "No versions found for ${PackageId}"
            return $null
        }
        
        if ($TargetFramework -match 'net(\d+)\.?') {
            $targetMajor = [int]$matches[1]
        } else {
            Write-Warning "Cannot extract major version from $TargetFramework"
            return $null
        }
        
        $allVersions = $versions | ForEach-Object {
            try {
                $v = [version]($_ -replace '-.*', '')
                [PSCustomObject]@{
                    OriginalString = $_
                    Version = $v
                    IsPrerelease = $_ -match '-'
                }
            } catch { 
                Write-Verbose "Failed to parse version: $_"
                $null 
            }
        } | Where-Object { $_ -ne $null }
        
        if ($allVersions.Count -eq 0) {
            Write-Warning "No valid versions found for ${PackageId}"
            return $null
        }
        
        # Strategy (priority order):
        # 1. Latest stable with exact major version (semantic version, not major-only)
        # 2. Latest stable with lower major version
        # 3. Latest prerelease with exact major (if AllowPrerelease)
        # Never use major-only versions like "6" or "9" - always use semantic versions
        
        $best = $allVersions | Where-Object { 
            -not $_.IsPrerelease -and $_.Version.Major -eq $targetMajor 
        } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
        
        if (-not $best) {
            $best = $allVersions | Where-Object { 
                -not $_.IsPrerelease -and $_.Version.Major -lt $targetMajor 
            } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
        }
        
        if (-not $best -and $AllowPrerelease) {
            $best = $allVersions | Where-Object { 
                $_.Version.Major -eq $targetMajor 
            } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
        }
        
        if (-not $best) {
            Write-Warning "No suitable version found for ${PackageId} targeting ${TargetFramework}"
            return $null
        }
        
        # Ensure we return a full semantic version, never major-only
        $resultVersion = $best.OriginalString
        if ($resultVersion -match '^\d+$') {
            Write-Warning "Rejecting major-only version '$resultVersion' for ${PackageId} - this is too broad"
            return $null
        }
        
        return $resultVersion
    } catch {
        Write-Warning "Error getting version for ${PackageId}: $_"
        return $null
    }
}

# ============================================================================
# STEP 1: Update TargetFrameworks in .csproj
# ============================================================================
Write-Host "`n[1/4] Updating TargetFrameworks in .csproj files..." -ForegroundColor Yellow

# Use singular or plural based on count
$targetFrameworksString = $TargetFrameworks -join ';'
$elementName = if ($TargetFrameworks.Count -eq 1) { "TargetFramework" } else { "TargetFrameworks" }

$csprojs = Get-ChildItem -Recurse -Filter "*.csproj" | Where-Object { 
    $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' 
}

foreach ($proj in $csprojs) {
    try {
        [xml]$xml = Get-Content $proj.FullName
        $propertyGroups = @($xml.Project.PropertyGroup)
        if ($propertyGroups.Count -eq 0) {
            $pg = $xml.CreateElement("PropertyGroup")
            $xml.Project.AppendChild($pg) | Out-Null
            $propertyGroups = @($pg)
        }
        
        $pg = $propertyGroups[0]
        
        # Remove both singular and plural variants
        @($pg.SelectNodes("TargetFramework")) | ForEach-Object { $pg.RemoveChild($_) | Out-Null }
        @($pg.SelectNodes("TargetFrameworks")) | ForEach-Object { $pg.RemoveChild($_) | Out-Null }
        
        # Add correct element name
        $tfNode = $xml.CreateElement($elementName)
        $tfNode.InnerText = $targetFrameworksString
        $pg.AppendChild($tfNode) | Out-Null
        
        $xml.Save($proj.FullName)
        Write-Host "  ✓ $($proj.Name): Set to $targetFrameworksString" -ForegroundColor Green
    } catch {
        Write-Warning "  ✗ Failed to update $($proj.Name): $_"
    }
}

# ============================================================================
# STEP 2: Remove Version from PackageReference
# ============================================================================
Write-Host "`n[2/4] Removing Version attributes from PackageReference..." -ForegroundColor Yellow

foreach ($proj in $csprojs) {
    try {
        $content = Get-Content $proj.FullName -Raw
        $content = $content -replace '(<PackageReference\s+Include="[^"]+")(\s+Version="[^"]+")', '$1'
        $content | Set-Content $proj.FullName -NoNewline
        Write-Host "  ✓ $($proj.Name)" -ForegroundColor Gray
    } catch {
        Write-Warning "  ✗ Failed to process $($proj.Name): $_"
    }
}

# ============================================================================
# STEP 3: Collect all packages
# ============================================================================
Write-Host "`n[3/4] Collecting package references..." -ForegroundColor Yellow

$allPackages = @{}
foreach ($proj in $csprojs) {
    try {
        [xml]$xml = Get-Content $proj.FullName
        $itemGroups = @($xml.Project.ItemGroup)
        
        foreach ($ig in $itemGroups) {
            if ($ig) {
                $packageRefs = @($ig.PackageReference)
                foreach ($pkg in $packageRefs) {
                    if ($pkg -and $pkg.Include) { 
                        $allPackages[$pkg.Include] = $true 
                    }
                }
            }
        }
    } catch {
        Write-Warning "  ✗ Failed to read packages from $($proj.Name): $_"
    }
}

$packageList = $allPackages.Keys | Sort-Object
Write-Host "  Found $($packageList.Count) unique packages" -ForegroundColor Cyan

# ============================================================================
# STEP 4: Update Directory.Packages.props
# ============================================================================
Write-Host "`n[4/4] Updating Directory.Packages.props..." -ForegroundColor Yellow

$propsFile = Get-ChildItem -Filter "Directory.Packages.props" -Recurse | Select-Object -First 1
if (-not $propsFile) {
    $propsPath = "Directory.Packages.props"
    @"
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
"@ | Set-Content $propsPath
    $propsFile = Get-Item $propsPath
    Write-Host "  Created new Directory.Packages.props" -ForegroundColor Green
}

# Track package version changes for report
$packageChanges = @()

# Initialize variables before try block to ensure they're always available for report
$commonPackages = @{}
$frameworkSpecificPackages = @{}

try {
    [xml]$xml = Get-Content $propsFile.FullName
    
    # Collect OLD versions and attributes before removing
    $oldVersions = @{}
    $packageAttributes = @{}
    
    # Get all ItemGroups (both conditional and unconditional)
    $existingItemGroups = @($xml.Project.ItemGroup)
    
    foreach ($ig in $existingItemGroups) {
        # Collect versions and attributes from all ItemGroups
        foreach ($pv in $ig.PackageVersion) {
            if ($pv.Include -and $pv.Version) {
                if ($ig.Condition -and $ig.Condition -match "'\`\$\(TargetFramework\)' == '(net\d+\.\d+)'") {
                    $framework = $matches[1]
                    $key = "$($pv.Include)|$framework"
                } else {
                    $key = "$($pv.Include)|common"
                }
                $oldVersions[$key] = $pv.Version
                
                # Store all attributes except Include and Version
                $attrs = @{}
                foreach ($attr in $pv.Attributes) {
                    if ($attr.Name -notin @('Include', 'Version')) {
                        $attrs[$attr.Name] = $attr.Value
                    }
                }
                if ($attrs.Count -gt 0) {
                    $packageAttributes[$key] = $attrs
                }
            }
        }
        
        # Remove ItemGroups that contain PackageVersion elements
        # This removes both conditional and unconditional ItemGroups with package definitions
        if ($ig.PackageVersion) {
            $xml.Project.RemoveChild($ig) | Out-Null
        }
    }
    
    # Collect package versions for each framework
    $packageVersionsByFramework = @{}
    foreach ($tf in $TargetFrameworks) {
        # Auto-detect if framework requires prerelease packages
        $allowPrereleaseForFramework = Test-RequiresPrerelease -TargetFramework $tf
        
        $packageVersionsByFramework[$tf] = @{}
        
        Write-Host "  Resolving versions for $tf..." -ForegroundColor Cyan
        
        foreach ($packageId in $packageList) {
            # Always fetch latest version from NuGet
            $version = Get-BestPackageVersion -PackageId $packageId -TargetFramework $tf -AllowPrerelease $allowPrereleaseForFramework
            if ($version) {
                $packageVersionsByFramework[$tf][$packageId] = $version
            }
            Start-Sleep -Milliseconds 100
        }
    }
    
    # Determine which packages can use a common version
    $commonPackages = @{}
    $frameworkSpecificPackages = @{}
    
    foreach ($packageId in $packageList) {
        $versions = @()
        foreach ($tf in $TargetFrameworks) {
            if ($packageVersionsByFramework[$tf].ContainsKey($packageId)) {
                $versions += $packageVersionsByFramework[$tf][$packageId]
            }
        }
        
        $uniqueVersions = $versions | Select-Object -Unique
        
        if ($uniqueVersions.Count -eq 1 -and $uniqueVersions[0]) {
            # All frameworks use the same version
            $commonPackages[$packageId] = $uniqueVersions[0]
        } elseif ($uniqueVersions.Count -gt 0) {
            # Different versions per framework
            $frameworkSpecificPackages[$packageId] = $true
        }
    }
    
    # Create unconditional ItemGroup for common packages
    if ($commonPackages.Count -gt 0) {
        Write-Host "  Creating common package versions..." -ForegroundColor Cyan
        $itemGroup = $xml.CreateElement("ItemGroup")
        
        foreach ($packageId in ($commonPackages.Keys | Sort-Object)) {
            $version = $commonPackages[$packageId]
            
            # Skip if version is invalid
            if (-not $version -or $version -eq "0") {
                Write-Warning "    Skipping $packageId - invalid version: $version"
                continue
            }
            
            $pkgVersion = $xml.CreateElement("PackageVersion")
            $pkgVersion.SetAttribute("Include", $packageId)
            $pkgVersion.SetAttribute("Version", $version)
            
            # Restore additional attributes if they existed
            $attrKey = "$packageId|common"
            if ($packageAttributes.ContainsKey($attrKey)) {
                foreach ($attrName in $packageAttributes[$attrKey].Keys) {
                    $pkgVersion.SetAttribute($attrName, $packageAttributes[$attrKey][$attrName])
                }
            }
            
            $itemGroup.AppendChild($pkgVersion) | Out-Null
            
            # Track changes - check both common key and all framework-specific keys
            $oldVersion = $null
            $oldKey = "$packageId|common"
            
            # First try to find old version in common configuration
            if ($oldVersions.ContainsKey($oldKey)) {
                $oldVersion = $oldVersions[$oldKey]
            }
            
            # If not found in common, check all framework-specific configurations
            if (-not $oldVersion) {
                foreach ($tf in $TargetFrameworks) {
                    $fwKey = "$packageId|$tf"
                    if ($oldVersions.ContainsKey($fwKey)) {
                        $oldVersion = $oldVersions[$fwKey]
                        break  # Use first found version
                    }
                }
            }
            
            # Track changes: both version updates AND new additions
            if (-not $oldVersion -or $oldVersion -ne $version) {
                $packageChanges += [PSCustomObject]@{
                    Package = $packageId
                    Framework = "all"
                    OldVersion = if ($oldVersion) { $oldVersion } else { "(new)" }
                    NewVersion = $version
                }
            }
            
            $prereleaseLabel = if ($version -match '-') { " (prerelease)" } else { "" }
            $changeLabel = if ($oldVersion -and $oldVersion -ne $version) { " (was $oldVersion)" } else { "" }
            $attrLabel = if ($packageAttributes.ContainsKey($attrKey)) { " [+attrs]" } else { "" }
            Write-Host "    - $packageId → $version$prereleaseLabel$changeLabel$attrLabel" -ForegroundColor Gray
        }
        
        if ($itemGroup.HasChildNodes) {
            $xml.Project.AppendChild($itemGroup) | Out-Null
        }
    }
    
    # Create conditional ItemGroups for framework-specific packages
    if ($frameworkSpecificPackages.Count -gt 0) {
        Write-Host "  Creating framework-specific package versions..." -ForegroundColor Yellow
        
        foreach ($tf in $TargetFrameworks) {
            $itemGroup = $xml.CreateElement("ItemGroup")
            $itemGroup.SetAttribute("Condition", "'`$(TargetFramework)' == '$tf'")
            $hasPackages = $false
            
            foreach ($packageId in ($frameworkSpecificPackages.Keys | Sort-Object)) {
                if ($packageVersionsByFramework[$tf].ContainsKey($packageId)) {
                    $version = $packageVersionsByFramework[$tf][$packageId]
                    
                    # Skip if version is invalid
                    if (-not $version -or $version -eq "0") {
                        Write-Warning "    [$tf] Skipping $packageId - invalid version: $version"
                        continue
                    }
                    
                    $pkgVersion = $xml.CreateElement("PackageVersion")
                    $pkgVersion.SetAttribute("Include", $packageId)
                    $pkgVersion.SetAttribute("Version", $version)
                    
                    # Restore additional attributes if they existed (check both framework-specific and common)
                    $attrKey = "$packageId|$tf"
                    $attrCommonKey = "$packageId|common"
                    if ($packageAttributes.ContainsKey($attrKey)) {
                        foreach ($attrName in $packageAttributes[$attrKey].Keys) {
                            $pkgVersion.SetAttribute($attrName, $packageAttributes[$attrKey][$attrName])
                        }
                    } elseif ($packageAttributes.ContainsKey($attrCommonKey)) {
                        foreach ($attrName in $packageAttributes[$attrCommonKey].Keys) {
                            $pkgVersion.SetAttribute($attrName, $packageAttributes[$attrCommonKey][$attrName])
                        }
                    }
                    
                    $itemGroup.AppendChild($pkgVersion) | Out-Null
                    $hasPackages = $true
                    
                    # Track changes - check both framework-specific key and common key
                    $oldVersion = $null
                    $oldKey = "$packageId|$tf"
                    
                    # First try to find old version in framework-specific configuration
                    if ($oldVersions.ContainsKey($oldKey)) {
                        $oldVersion = $oldVersions[$oldKey]
                    }
                    
                    # If not found, check common configuration
                    if (-not $oldVersion) {
                        $commonKey = "$packageId|common"
                        if ($oldVersions.ContainsKey($commonKey)) {
                            $oldVersion = $oldVersions[$commonKey]
                        }
                    }
                    
                    # Track changes: both version updates AND new additions
                    if (-not $oldVersion -or $oldVersion -ne $version) {
                        $packageChanges += [PSCustomObject]@{
                            Package = $packageId
                            Framework = $tf
                            OldVersion = if ($oldVersion) { $oldVersion } else { "(new)" }
                            NewVersion = $version
                        }
                    }
                    
                    $prereleaseLabel = if ($version -match '-') { " (prerelease)" } else { "" }
                    $changeLabel = if ($oldVersion -and $oldVersion -ne $version) { " (was $oldVersion)" } else { "" }
                    $attrLabel = if ($packageAttributes.ContainsKey($attrKey) -or $packageAttributes.ContainsKey($attrCommonKey)) { " [+attrs]" } else { "" }
                    Write-Host "    [$tf] $packageId → $version$prereleaseLabel$changeLabel$attrLabel" -ForegroundColor DarkGray
                }
            }
            
            if ($hasPackages) {
                $xml.Project.AppendChild($itemGroup) | Out-Null
            }
        }
    }
    
    $xml.Save($propsFile.FullName)
    Write-Host "  ✓ Saved Directory.Packages.props" -ForegroundColor Green
    Write-Host "    Common packages: $($commonPackages.Count)" -ForegroundColor Gray
    Write-Host "    Framework-specific packages: $($frameworkSpecificPackages.Count)" -ForegroundColor Gray
    
} catch {
    Write-Error "Failed to update Directory.Packages.props: $_"
    exit 1
}

# ============================================================================
# SUMMARY
# ============================================================================
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "✓ Update Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

# Generate detailed change summary
$changeSummary = @()
if ($packageChanges.Count -gt 0) {
    Write-Host "`nPackage Version Changes:" -ForegroundColor Cyan
    
    # Group by package for cleaner display
    $grouped = $packageChanges | Group-Object Package
    foreach ($group in $grouped) {
        $changes = $group.Group | Sort-Object Framework
        $pkg = $group.Name
        
        # If all frameworks have same change, show compact format
        if (($changes | Select-Object -ExpandProperty OldVersion -Unique).Count -eq 1 -and 
            ($changes | Select-Object -ExpandProperty NewVersion -Unique).Count -eq 1) {
            $old = $changes[0].OldVersion
            $new = $changes[0].NewVersion
            Write-Host "  $pkg : $old → $new" -ForegroundColor White
            $changeSummary += "$pkg : $old → $new"
        } else {
            # Different versions per framework
            Write-Host "  $pkg :" -ForegroundColor White
            foreach ($change in $changes) {
                Write-Host "    [$($change.Framework)] $($change.OldVersion) → $($change.NewVersion)" -ForegroundColor Gray
                $changeSummary += "$pkg [$($change.Framework)]: $($change.OldVersion) → $($change.NewVersion)"
            }
        }
    }
} else {
    Write-Host "`nNo package version changes detected" -ForegroundColor Yellow
    $changeSummary += "No package version changes - this might be a new setup or no updates available"
}

# Join the summary into a single string for bash compatibility
$changeSummaryText = $changeSummary -join "`n"

# Generate report for PR automation
$report = @{
    Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    TargetFrameworks = $TargetFrameworks
    ProjectsFound = $csprojs.Count
    PackagesFound = $packageList.Count
    PackageChanges = $packageChanges
    ChangeSummaryText = $changeSummaryText
    Summary = @{
        ProjectsUpdated = $csprojs.Count
        PackagesConfigured = $packageList.Count
        PackagesChanged = $packageChanges.Count
        CommonPackages = $commonPackages.Count
        FrameworkSpecificPackages = $frameworkSpecificPackages.Count
        FrameworksSet = $targetFrameworksString
        DirectoryPackagesPropsUpdated = [bool]$propsFile
    }
}

$reportPath = "dotnet-update-report.json"
$report | ConvertTo-Json -Depth 10 | Set-Content $reportPath -Encoding UTF8
Write-Host "`nReport saved to: $reportPath" -ForegroundColor Gray

Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "  Projects updated: $($csprojs.Count)" -ForegroundColor White
Write-Host "  Target frameworks: $targetFrameworksString" -ForegroundColor White
Write-Host "  Packages configured: $($packageList.Count)" -ForegroundColor White
Write-Host "  Package versions changed: $($packageChanges.Count)" -ForegroundColor White

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  1. Run: dotnet restore" -ForegroundColor White
Write-Host "  2. Run: dotnet build" -ForegroundColor White
Write-Host "  3. Test and verify" -ForegroundColor White
Write-Host ""
