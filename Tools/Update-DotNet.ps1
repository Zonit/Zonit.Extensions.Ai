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
# =============================================================================
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
        $needsPrerelease = @($stableVersionsExist).Count -eq 0
        
        if ($needsPrerelease) {
            Write-Host "  [INFO] $TargetFramework appears to be prerelease - will allow preview packages" -ForegroundColor Yellow
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
        
        # Parse all versions
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
        
        # Improved strategy: Find latest compatible version regardless of version numbering scheme
        # This works for ALL packages:
        # - Microsoft packages with .NET-aligned versions (8.x, 9.x)
        # - Microsoft packages with independent versions (CodeAnalysis 4.x, AspNetCore 2.x)
        # - Third-party packages (Newtonsoft.Json 13.x, Serilog 3.x, etc.)
        # Priority order:
        # 1. Latest stable version (if not AllowPrerelease)
        # 2. Latest version including prerelease (if AllowPrerelease)
        
        $best = $null
        
        if ($AllowPrerelease) {
            # Allow any version (stable or prerelease)
            $best = $allVersions | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
        } else {
            # Only stable versions
            $best = $allVersions | Where-Object { -not $_.IsPrerelease } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
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
        Write-Host "  [OK] $($proj.Name): Set to $targetFrameworksString" -ForegroundColor Green
    } catch {
        Write-Warning "  [FAIL] Failed to update $($proj.Name): $_"
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
        Write-Host "  [OK] $($proj.Name)" -ForegroundColor Gray
    } catch {
        Write-Warning "  [FAIL] Failed to process $($proj.Name): $_"
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
        Write-Warning "  [FAIL] Failed to read packages from $($proj.Name): $_"
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
    
    # Remove TargetFrameworks from PropertyGroup if it exists (it should only be in .csproj files)
    $propertyGroups = @($xml.Project.PropertyGroup)
    foreach ($pg in $propertyGroups) {
        if ($pg) {
            @($pg.SelectNodes("TargetFramework")) | ForEach-Object { $pg.RemoveChild($_) | Out-Null }
            @($pg.SelectNodes("TargetFrameworks")) | ForEach-Object { $pg.RemoveChild($_) | Out-Null }
        }
    }
    
    # Collect OLD versions and ALL child elements/attributes before removing
    $oldVersions = @{}
    $packageAttributes = @{}
    $packageChildElements = @{}
    
    # Get all ItemGroups (both conditional and unconditional)
    $existingItemGroups = @($xml.Project.ItemGroup)
    
    foreach ($ig in $existingItemGroups) {
        # Collect versions, attributes, and child elements from all ItemGroups
        foreach ($pv in $ig.PackageVersion) {
            if ($pv.Include -and $pv.Version) {
                if ($ig.Condition -and $ig.Condition -match "'\`$\(TargetFramework\)' == '(net\d+\.\d+)'") {
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
                
                # Store all child elements (like PrivateAssets, IncludeAssets, etc.)
                $childElements = @()
                foreach ($child in $pv.ChildNodes) {
                    if ($child.NodeType -eq 'Element') {
                        $childElements += [PSCustomObject]@{
                            Name = $child.LocalName
                            Value = $child.InnerText
                        }
                    }
                }
                if ($childElements.Count -gt 0) {
                    $packageChildElements[$key] = $childElements
                }
            }
        }
    }
    
    # Remove ONLY conditional ItemGroups that match our target frameworks
    # This allows us to recreate them with updated package versions
    # PRESERVE unconditional ItemGroups (common packages without conditions)
    foreach ($ig in $existingItemGroups) {
        $shouldRemove = $false
        
        if ($ig.PackageVersion -and $ig.Condition) {
            # Check if this ItemGroup's condition matches any of our target frameworks
            foreach ($tf in $TargetFrameworks) {
                if ($ig.Condition -match [regex]::Escape($tf)) {
                    $shouldRemove = $true
                    break
                }
            }
        }
        
        if ($shouldRemove) {
            $xml.Project.RemoveChild($ig) | Out-Null
        }
    }
    
    # Collect package versions for each framework
    $packageVersionsByFramework = @{}
    $unresolvedPackages = @{}
    
    foreach ($tf in $TargetFrameworks) {
        # Auto-detect if framework requires prerelease packages
        $allowPrereleaseForFramework = Test-RequiresPrerelease -TargetFramework $tf
        
        $packageVersionsByFramework[$tf] = @{}
        
        Write-Host "  Resolving versions for $tf..." -ForegroundColor Cyan
        
        foreach ($packageId in $packageList) {
            # Fetch best compatible version from NuGet based on framework requirements
            $version = Get-BestPackageVersion -PackageId $packageId -TargetFramework $tf -AllowPrerelease $allowPrereleaseForFramework
            
            if ($version) {
                $packageVersionsByFramework[$tf][$packageId] = $version
            } else {
                # Track unresolved packages
                if (-not $unresolvedPackages.ContainsKey($packageId)) {
                    $unresolvedPackages[$packageId] = @()
                }
                $unresolvedPackages[$packageId] += $tf
                
                # Try to preserve old version if it exists
                $oldKey = "$packageId|$tf"
                $oldCommonKey = "$packageId|common"
                
                if ($oldVersions.ContainsKey($oldKey)) {
                    $preservedVersion = $oldVersions[$oldKey]
                    $packageVersionsByFramework[$tf][$packageId] = $preservedVersion
                    Write-Host "    [WARN] $packageId`: Could not resolve new version, preserving old version $preservedVersion for $tf" -ForegroundColor Yellow
                } elseif ($oldVersions.ContainsKey($oldCommonKey)) {
                    $preservedVersion = $oldVersions[$oldCommonKey]
                    $packageVersionsByFramework[$tf][$packageId] = $preservedVersion
                    Write-Host "    [WARN] $packageId`: Could not resolve new version, preserving old version $preservedVersion for $tf" -ForegroundColor Yellow
                } else {
                    Write-Warning "    [ERROR] $packageId`: Could not resolve version and no old version to preserve for $tf"
                }
            }
            Start-Sleep -Milliseconds 100
        }
    }
    
    # Warn about unresolved packages
    if ($unresolvedPackages.Count -gt 0) {
        Write-Host "`n  [WARN] Warning: Some packages could not be resolved from NuGet:" -ForegroundColor Yellow
        foreach ($pkg in $unresolvedPackages.Keys) {
            $frameworks = $unresolvedPackages[$pkg] -join ', '
            Write-Host "    - $pkg (for: $frameworks)" -ForegroundColor Yellow
        }
    }
    
    # Determine which packages can use a common version vs framework-specific
    # NOTE: We ALWAYS create conditional ItemGroups for target frameworks
    # even if all packages currently have the same version
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
            # All frameworks use the same version - but we still add to framework-specific
            # to maintain conditional ItemGroups per framework
            $frameworkSpecificPackages[$packageId] = $true
        } elseif ($uniqueVersions.Count -gt 1) {
            # Different versions per framework
            $frameworkSpecificPackages[$packageId] = $true
        } elseif ($versions.Count -eq 0) {
            # No version resolved for any framework - skip this package
            Write-Warning "    Skipping $packageId - no version could be resolved for any framework"
        }
    }
    
    # Create conditional ItemGroups for ALL packages (per framework)
    # This maintains the structure of having framework-specific package definitions
    Write-Host "  Creating framework-specific package versions..." -ForegroundColor Yellow
    
    foreach ($tf in $TargetFrameworks) {
        $itemGroup = $xml.CreateElement("ItemGroup")
        $itemGroup.SetAttribute("Condition", "'`$(TargetFramework)' == '$tf'")
        $hasPackages = $false
        
        foreach ($packageId in ($packageList | Sort-Object)) {
            if ($packageVersionsByFramework[$tf].ContainsKey($packageId)) {
                $version = $packageVersionsByFramework[$tf][$packageId]
                
                # Skip if version is invalid (null, "0", or major-only like "8")
                if (-not $version -or $version -eq "0" -or $version -match '^\d+$') {
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
                
                # Restore child elements (check both framework-specific and common)
                $childKey = "$packageId|$tf"
                $childCommonKey = "$packageId|common"
                if ($packageChildElements.ContainsKey($childKey)) {
                    foreach ($childElement in $packageChildElements[$childKey]) {
                        $child = $xml.CreateElement($childElement.Name)
                        $child.InnerText = $childElement.Value
                        $pkgVersion.AppendChild($child) | Out-Null
                    }
                } elseif ($packageChildElements.ContainsKey($childCommonKey)) {
                    foreach ($childElement in $packageChildElements[$childCommonKey]) {
                        $child = $xml.CreateElement($childElement.Name)
                        $child.InnerText = $childElement.Value
                        $pkgVersion.AppendChild($child) | Out-Null
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
                $childLabel = if ($packageChildElements.ContainsKey($childKey) -or $packageChildElements.ContainsKey($childCommonKey)) { " [+children]" } else { "" }
                $attrLabel = if ($packageAttributes.ContainsKey($attrKey) -or $packageAttributes.ContainsKey($attrCommonKey)) { " [+attrs]" } else { "" }
                Write-Host "    [$tf] $packageId -> $version$prereleaseLabel$changeLabel$attrLabel$childLabel" -ForegroundColor DarkGray
            }
        }
        
        if ($hasPackages) {
            $xml.Project.AppendChild($itemGroup) | Out-Null
        }
    }

    $xml.Save($propsFile.FullName)
    Write-Host "  [OK] Saved Directory.Packages.props" -ForegroundColor Green
    Write-Host "    Framework-specific package groups: $($TargetFrameworks.Count)" -ForegroundColor Gray
    Write-Host "    Total packages configured: $($packageList.Count)" -ForegroundColor Gray
    
} catch {
    Write-Error "Failed to update Directory.Packages.props: $_"
    exit 1
}

# ============================================================================
# SUMMARY
# ============================================================================
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "[OK] Update Complete!" -ForegroundColor Green
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
            Write-Host "  $pkg : $old -> $new" -ForegroundColor White
            $changeSummary += "$pkg : $old -> $new"
        } else {
            # Different versions per framework
            Write-Host "  $pkg :" -ForegroundColor White
            foreach ($change in $changes) {
                Write-Host "    [$($change.Framework)] $($change.OldVersion) -> $($change.NewVersion)" -ForegroundColor Gray
                $changeSummary += "$pkg [$($change.Framework)]: $($change.OldVersion) -> $($change.NewVersion)"
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
        FrameworkSpecificGroups = $TargetFrameworks.Count
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
