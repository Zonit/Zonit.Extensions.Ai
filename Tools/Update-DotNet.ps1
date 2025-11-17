<#
.SYNOPSIS
Updates .NET target frameworks and manages NuGet packages with Central Package Management.

.DESCRIPTION
This script:
1. Scans all .csproj files and collects used packages
2. Adds TargetFrameworks to .csproj files
3. Removes TargetFramework/TargetFrameworks from Directory.Packages.props (should only be in .csproj)
4. Creates package list in Directory.Packages.props
5. Creates target framework groups based on parameters (e.g., net8.0, net9.0, net10.0)
6. Places all found packages in framework-specific groups
7. Fetches package versions from NuGet:
   - For Microsoft packages: looks for minor version matching target framework (e.g., 8.x.x for net8.0)
   - For other packages: gets latest compatible version
8. Checks for duplicates: if package version is identical across all frameworks, moves it to common ItemGroup
9. Preserves additional attributes (PrivateAssets, etc.) and child elements

.PARAMETER TargetFrameworks
List of target frameworks to support (e.g., "net8.0", "net9.0", "net10.0")

.EXAMPLE
.\Update-DotNet.ps1 net8.0 net9.0 net10.0

.EXAMPLE
.\Update-DotNet.ps1 -TargetFrameworks net8.0,net9.0,net10.0

.NOTES
Created for Zonit organization-wide .NET updates
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
        
        # Extract target major version from framework
        if ($TargetFramework -match 'net(\d+)\.') {
            $targetMajor = [int]$matches[1]
        } else {
            Write-Warning "Cannot extract major version from $TargetFramework"
            return $null
        }
        
        # Parse all versions and validate they are FULL semantic versions
        $allVersions = $versions | ForEach-Object {
            try {
                # CRITICAL: Reject major-only versions (e.g., "4" -> reject, "4.0.0" -> OK)
                if ($_ -match '^\d+$') {
                    Write-Verbose "Rejecting major-only version: $_"
                    return $null
                }
                
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
        
        # Strategy for Microsoft packages vs third-party:
        # 1. For Microsoft.* packages that follow .NET versioning - try to match major version (e.g., 8.x.x for net8.0)
        # 2. For packages that don't follow .NET versioning - get latest compatible
        
        $best = $null
        
        # Check if this is a Microsoft package that typically follows .NET versioning
        $followsDotNetVersioning = $PackageId -match '^Microsoft\.(Extensions|AspNetCore|EntityFrameworkCore|JSInterop)\.'
        
        if ($followsDotNetVersioning) {
            # Strategy: Try to find version with Major == targetMajor (e.g., 8.x.x for net8.0)
            # First try: exact major version match (preferred)
            if ($AllowPrerelease) {
                $best = $allVersions | Where-Object { 
                    $_.Version.Major -eq $targetMajor 
                } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
            } else {
                $best = $allVersions | Where-Object { 
                    $_.Version.Major -eq $targetMajor -and -not $_.IsPrerelease 
                } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
            }
            
            # Second try: if no exact match, find highest version where Major <= targetMajor
            if (-not $best) {
                if ($AllowPrerelease) {
                    $best = $allVersions | Where-Object { 
                        $_.Version.Major -le $targetMajor 
                    } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
                } else {
                    $best = $allVersions | Where-Object { 
                        $_.Version.Major -le $targetMajor -and -not $_.IsPrerelease 
                    } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
                }
            }
        }
        
        # If not a .NET-versioned package OR no version found with above strategy
        # Fall back to getting the latest compatible version
        if (-not $best) {
            if ($AllowPrerelease) {
                $best = $allVersions | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
            } else {
                $best = $allVersions | Where-Object { -not $_.IsPrerelease } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
            }
        }
        
        if (-not $best) {
            Write-Warning "No suitable version found for ${PackageId} targeting ${TargetFramework}"
            return $null
        }
        
        # CRITICAL: Double-check we're not returning major-only version
        $resultVersion = $best.OriginalString
        if ($resultVersion -match '^\d+$') {
            Write-Warning "CRITICAL: Rejecting major-only version '$resultVersion' for ${PackageId} - this would break compilation"
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
Write-Host "`n[1/5] Updating TargetFrameworks in .csproj files..." -ForegroundColor Yellow

# Use singular or plural based on count
$targetFrameworksString = $TargetFrameworks -join ';'
$elementName = if ($TargetFrameworks.Count -eq 1) { "TargetFramework" } else { "TargetFrameworks" }

$csprojs = Get-ChildItem -Recurse -Filter "*.csproj" | Where-Object { 
    $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' 
}

if ($csprojs.Count -eq 0) {
    Write-Error "No .csproj files found in the current directory or subdirectories"
    exit 1
}

foreach ($proj in $csprojs) {
    try {
        [xml]$xml = Get-Content $proj.FullName
        
        # Ensure Project element exists
        if (-not $xml.Project) {
            Write-Warning "  [SKIP] $($proj.Name): Invalid project file (no <Project> element)"
            continue
        }
        
        $propertyGroups = @($xml.Project.PropertyGroup)
        if ($propertyGroups.Count -eq 0) {
            # Create PropertyGroup if it doesn't exist
            $pg = $xml.CreateElement("PropertyGroup")
            $xml.Project.AppendChild($pg) | Out-Null
            $propertyGroups = @($pg)
        }
        
        $pg = $propertyGroups[0]
        
        # Check if TargetFramework or TargetFrameworks exists
        $tfNodes = @($pg.SelectNodes("TargetFramework"))
        $tfsNodes = @($pg.SelectNodes("TargetFrameworks"))
        $hasTargetFramework = ($tfNodes.Count -gt 0) -or ($tfsNodes.Count -gt 0)
        
        # Remove both singular and plural variants
        foreach ($node in $tfNodes) { $pg.RemoveChild($node) | Out-Null }
        foreach ($node in $tfsNodes) { $pg.RemoveChild($node) | Out-Null }
        
        # Add correct element name
        $tfNode = $xml.CreateElement($elementName)
        $tfNode.InnerText = $targetFrameworksString
        $pg.AppendChild($tfNode) | Out-Null
        
        $xml.Save($proj.FullName)
        
        if ($hasTargetFramework) {
            Write-Host "  [OK] $($proj.Name): Updated to $targetFrameworksString" -ForegroundColor Green
        } else {
            Write-Host "  [OK] $($proj.Name): Added $targetFrameworksString" -ForegroundColor Cyan
        }
    } catch {
        Write-Warning "  [FAIL] Failed to update $($proj.Name): $_"
    }
}

# ============================================================================
# STEP 2: Collect all packages with their metadata
# ============================================================================
Write-Host "`n[2/5] Collecting package references from all .csproj files..." -ForegroundColor Yellow

# Structure: packageId -> @{ Attributes, ChildElements }
$allPackagesMetadata = @{}

foreach ($proj in $csprojs) {
    try {
        [xml]$xml = Get-Content $proj.FullName
        $itemGroups = @($xml.Project.ItemGroup)
        
        foreach ($ig in $itemGroups) {
            if ($ig) {
                $packageRefs = @($ig.PackageReference)
                foreach ($pkg in $packageRefs) {
                    if ($pkg -and $pkg.Include) {
                        $packageId = $pkg.Include
                        
                        if (-not $allPackagesMetadata.ContainsKey($packageId)) {
                            # Store attributes (except Include and Version)
                            $attrs = @{
                            }
                            foreach ($attr in $pkg.Attributes) {
                                if ($attr.Name -notin @('Include', 'Version')) {
                                    $attrs[$attr.Name] = $attr.Value
                                }
                            }
                            
                            # Store child elements (like PrivateAssets, IncludeAssets, etc.)
                            $childElements = @()
                            foreach ($child in $pkg.ChildNodes) {
                                if ($child.NodeType -eq 'Element') {
                                    $childElements += [PSCustomObject]@{
                                        Name = $child.LocalName
                                        Value = $child.InnerText
                                    }
                                }
                            }
                            
                            $allPackagesMetadata[$packageId] = @{
                                Attributes = $attrs
                                ChildElements = $childElements
                            }
                        }
                    }
                }
            }
        }
    } catch {
        Write-Warning "  [FAIL] Failed to read packages from $($proj.Name): $_"
    }
}

$packageList = $allPackagesMetadata.Keys | Sort-Object
Write-Host "  Found $($packageList.Count) unique packages across all projects" -ForegroundColor Cyan

foreach ($pkg in $packageList) {
    $metadata = $allPackagesMetadata[$pkg]
    $attrInfo = if ($metadata.Attributes.Count -gt 0) { " [attrs: $($metadata.Attributes.Keys -join ', ')]" } else { "" }
    $childInfo = if ($metadata.ChildElements.Count -gt 0) { " [children: $($metadata.ChildElements.Name -join ', ')]" } else { "" }
    Write-Host "    - $pkg$attrInfo$childInfo" -ForegroundColor DarkGray
}

# ============================================================================
# STEP 3: Remove Version from PackageReference in .csproj
# ============================================================================
Write-Host "`n[3/5] Removing Version attributes from PackageReference in .csproj files..." -ForegroundColor Yellow

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
# STEP 4: Resolve package versions for each framework
# ============================================================================
Write-Host "`n[4/5] Resolving package versions for each framework..." -ForegroundColor Yellow

# Structure: framework -> packageId -> version
$packageVersionsByFramework = @{
}
$unresolvedPackages = @{
}

foreach ($tf in $TargetFrameworks) {
    # Auto-detect if framework requires prerelease packages
    $allowPrereleaseForFramework = Test-RequiresPrerelease -TargetFramework $tf
    
    $packageVersionsByFramework[$tf] = @{
    }
    
    Write-Host "  Resolving versions for $tf..." -ForegroundColor Cyan
    
    foreach ($packageId in $packageList) {
        # Fetch best compatible version from NuGet based on framework requirements
        $version = Get-BestPackageVersion -PackageId $packageId -TargetFramework $tf -AllowPrerelease $allowPrereleaseForFramework
        
        if ($version) {
            $packageVersionsByFramework[$tf][$packageId] = $version
            $prereleaseLabel = if ($version -match '-') { " (prerelease)" } else { "" }
            Write-Host "    [$tf] $packageId -> $version$prereleaseLabel" -ForegroundColor DarkGray
        } else {
            # Track unresolved packages
            if (-not $unresolvedPackages.ContainsKey($packageId)) {
                $unresolvedPackages[$packageId] = @()
            }
            $unresolvedPackages[$packageId] += $tf
            Write-Warning "    [$tf] $packageId -> UNRESOLVED"
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

# ============================================================================
# STEP 5: Create/Update Directory.Packages.props
# ============================================================================
Write-Host "`n[5/5] Creating/Updating Directory.Packages.props..." -ForegroundColor Yellow

# CRITICAL FIX: Look for Directory.Packages.props in Source/ directory (Zonit convention)
$propsFile = $null
$searchPaths = @("Source", ".")

foreach ($searchPath in $searchPaths) {
    if (Test-Path $searchPath) {
        $found = Get-ChildItem -Path $searchPath -Filter "Directory.Packages.props" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            $propsFile = $found
            Write-Host "  Found existing Directory.Packages.props in: $searchPath" -ForegroundColor Cyan
            break
        }
    }
}

$oldVersions = @{

}

if (-not $propsFile) {
    # Create in Source/ directory if it exists, otherwise in root
    $propsPath = if (Test-Path "Source") { "Source\Directory.Packages.props" } else { "Directory.Packages.props" }
    
    # CRITICAL FIX: No XML declaration - MSBuild doesn't need it
    @"
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
"@ | Set-Content $propsPath
    $propsFile = Get-Item $propsPath
    Write-Host "  Created new Directory.Packages.props at: $propsPath" -ForegroundColor Green
} else {
    # Collect old versions for change tracking
    try {
        [xml]$existingXml = Get-Content $propsFile.FullName
        $existingItemGroups = @($existingXml.Project.ItemGroup)
        
        foreach ($ig in $existingItemGroups) {
            foreach ($pv in $ig.PackageVersion) {
                if ($pv.Include -and $pv.Version) {
                    if ($ig.Condition -and $ig.Condition -match "'\`$\(TargetFramework\)' == '(net\d+\.\d+)'") {
                        $framework = $matches[1]
                        $key = "$($pv.Include)|$framework"
                    } else {
                        $key = "$($pv.Include)|common"
                    }
                    $oldVersions[$key] = $pv.Version
                }
            }
        }
    } catch {
        Write-Verbose "Could not read existing packages from Directory.Packages.props"
    }
}

try {
    [xml]$xml = Get-Content $propsFile.FullName
    
    # Remove TargetFramework/TargetFrameworks from PropertyGroup (should only be in .csproj)
    Write-Host "  Removing TargetFramework/TargetFrameworks from Directory.Packages.props..." -ForegroundColor Cyan
    $propertyGroups = @($xml.Project.PropertyGroup)
    foreach ($pg in $propertyGroups) {
        if ($pg) {
            @($pg.SelectNodes("TargetFramework")) | ForEach-Object { 
                $pg.RemoveChild($_) | Out-Null
                Write-Host "    [OK] Removed TargetFramework" -ForegroundColor Gray
            }
            @($pg.SelectNodes("TargetFrameworks")) | ForEach-Object { 
                $pg.RemoveChild($_) | Out-Null
                Write-Host "    [OK] Removed TargetFrameworks" -ForegroundColor Gray
            }
        }
    }
    
    # Remove all existing ItemGroups with PackageVersion
    Write-Host "  Clearing existing package definitions..." -ForegroundColor Cyan
    $existingItemGroups = @($xml.Project.ItemGroup)
    foreach ($ig in $existingItemGroups) {
        if ($ig.PackageVersion) {
            $xml.Project.RemoveChild($ig) | Out-Null
        }
    }
    
    # ====================================================================================
    # KLUCZOWA LOGIKA: Sprawdzanie duplikatów
    # ====================================================================================
    Write-Host "  Analyzing package versions across frameworks..." -ForegroundColor Cyan
    
    $commonPackages = @{}           # Packages with same version across ALL frameworks
    $frameworkSpecificPackages = @{} # Packages with different versions per framework
    
    foreach ($packageId in $packageList) {
        $versions = @()
        $allResolved = $true
        
        # Collect versions for this package across all frameworks
        foreach ($tf in $TargetFrameworks) {
            if ($packageVersionsByFramework[$tf].ContainsKey($packageId)) {
                $versions += $packageVersionsByFramework[$tf][$packageId]
            } else {
                $allResolved = $false
            }
        }
        
        # Check if all frameworks have the same version
        $uniqueVersions = $versions | Select-Object -Unique
        
        if ($allResolved -and $uniqueVersions.Count -eq 1 -and $uniqueVersions[0]) {
            # ALL frameworks use the SAME version -> move to common ItemGroup
            $commonPackages[$packageId] = $uniqueVersions[0]
            Write-Host "    [COMMON] $packageId -> $($uniqueVersions[0]) (same across all frameworks)" -ForegroundColor Green
        } elseif ($uniqueVersions.Count -gt 1) {
            # Different versions per framework -> keep in framework-specific groups
            $frameworkSpecificPackages[$packageId] = $true
            Write-Host "    [SPECIFIC] $packageId (different versions per framework)" -ForegroundColor Yellow
        } elseif (-not $allResolved) {
            # Not resolved for all frameworks -> skip
            Write-Warning "    [SKIP] $packageId - not resolved for all frameworks"
        }
    }
    
    Write-Host "`n  Package distribution:" -ForegroundColor Cyan
    Write-Host "    Common packages: $($commonPackages.Count)" -ForegroundColor Green
    Write-Host "    Framework-specific packages: $($frameworkSpecificPackages.Count)" -ForegroundColor Yellow
    
    # ====================================================================================
    # Create COMMON ItemGroup (for packages with same version across all frameworks)
    # ====================================================================================
    if ($commonPackages.Count -gt 0) {
        Write-Host "`n  Creating common ItemGroup..." -ForegroundColor Cyan
        $commonItemGroup = $xml.CreateElement("ItemGroup")
        
        foreach ($packageId in ($commonPackages.Keys | Sort-Object)) {
            $version = $commonPackages[$packageId]
            
            # CRITICAL: Skip if version is invalid (null, "0", or major-only like "4")
            if (-not $version -or $version -eq "0" -or $version -match '^\d+$') {
                Write-Warning "    [SKIP] $packageId - invalid version: $version (would break compilation)"
                continue
            }
            
            $pkgVersion = $xml.CreateElement("PackageVersion")
            $pkgVersion.SetAttribute("Include", $packageId)
            $pkgVersion.SetAttribute("Version", $version)
            
            # Restore metadata (attributes and child elements)
            $metadata = $allPackagesMetadata[$packageId]
            
            # Restore attributes
            foreach ($attrName in $metadata.Attributes.Keys) {
                $pkgVersion.SetAttribute($attrName, $metadata.Attributes[$attrName])
            }
            
            # Restore child elements
            foreach ($childElement in $metadata.ChildElements) {
                $child = $xml.CreateElement($childElement.Name)
                $child.InnerText = $childElement.Value
                $pkgVersion.AppendChild($child) | Out-Null
            }
            
            $commonItemGroup.AppendChild($pkgVersion) | Out-Null
            
            $attrLabel = if ($metadata.Attributes.Count -gt 0) { " [+attrs]" } else { "" }
            $childLabel = if ($metadata.ChildElements.Count -gt 0) { " [+children]" } else { "" }
            Write-Host "    $packageId -> $version$attrLabel$childLabel" -ForegroundColor Gray
        }
        
        $xml.Project.AppendChild($commonItemGroup) | Out-Null
    }
    
    # ====================================================================================
    # Create FRAMEWORK-SPECIFIC ItemGroups (for packages with different versions)
    # ====================================================================================
    if ($frameworkSpecificPackages.Count -gt 0) {
        Write-Host "`n  Creating framework-specific ItemGroups..." -ForegroundColor Cyan
        
        foreach ($tf in $TargetFrameworks) {
            $itemGroup = $xml.CreateElement("ItemGroup")
            $itemGroup.SetAttribute("Condition", "'`$(TargetFramework)' == '$tf'")
            $hasPackages = $false
            
            foreach ($packageId in ($frameworkSpecificPackages.Keys | Sort-Object)) {
                if ($packageVersionsByFramework[$tf].ContainsKey($packageId)) {
                    $version = $packageVersionsByFramework[$tf][$packageId]
                    
                    # CRITICAL: Skip if version is invalid (null, "0", or major-only like "4")
                    if (-not $version -or $version -eq "0" -or $version -match '^\d+$') {
                        Write-Warning "    [$tf] [SKIP] $packageId - invalid version: $version (would break compilation)"
                        continue
                    }
                    
                    $pkgVersion = $xml.CreateElement("PackageVersion")
                    $pkgVersion.SetAttribute("Include", $packageId)
                    $pkgVersion.SetAttribute("Version", $version)
                    
                    # Restore metadata (attributes and child elements)
                    $metadata = $allPackagesMetadata[$packageId]
                    
                    # Restore attributes
                    foreach ($attrName in $metadata.Attributes.Keys) {
                        $pkgVersion.SetAttribute($attrName, $metadata.Attributes[$attrName])
                    }
                    
                    # Restore child elements
                    foreach ($childElement in $metadata.ChildElements) {
                        $child = $xml.CreateElement($childElement.Name)
                        $child.InnerText = $childElement.Value
                        $pkgVersion.AppendChild($child) | Out-Null
                    }
                    
                    $itemGroup.AppendChild($pkgVersion) | Out-Null
                    $hasPackages = $true
                    
                    $attrLabel = if ($metadata.Attributes.Count -gt 0) { " [+attrs]" } else { "" }
                    $childLabel = if ($metadata.ChildElements.Count -gt 0) { " [+children]" } else { "" }
                    Write-Host "    [$tf] $packageId -> $version$attrLabel$childLabel" -ForegroundColor DarkGray
                }
            }
            
            if ($hasPackages) {
                $xml.Project.AppendChild($itemGroup) | Out-Null
            }
        }
    }
    
    $xml.Save($propsFile.FullName)
    Write-Host "`n  [OK] Saved Directory.Packages.props" -ForegroundColor Green
    
} catch {
    Write-Error "Failed to update Directory.Packages.props: $_"
    exit 1
}

# ============================================================================
# SUMMARY & CHANGE TRACKING
# ============================================================================
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "[OK] Update Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

# Track changes
$packageChanges = @()

foreach ($packageId in $packageList) {
    # Check if in common or framework-specific
    if ($commonPackages.ContainsKey($packageId)) {
        $newVersion = $commonPackages[$packageId]
        $oldKey = "$packageId|common"
        $oldVersion = $oldVersions[$oldKey]
        
        if (-not $oldVersion -or $oldVersion -ne $newVersion) {
            $packageChanges += [PSCustomObject]@{
                Package = $packageId
                Framework = "common"
                OldVersion = if ($oldVersion) { $oldVersion } else { "(new)" }
                NewVersion = $newVersion
            }
        }
    } elseif ($frameworkSpecificPackages.ContainsKey($packageId)) {
        foreach ($tf in $TargetFrameworks) {
            if ($packageVersionsByFramework[$tf].ContainsKey($packageId)) {
                $newVersion = $packageVersionsByFramework[$tf][$packageId]
                $oldKey = "$packageId|$tf"
                $oldCommonKey = "$packageId|common"
                $oldVersion = if ($oldVersions.ContainsKey($oldKey)) { $oldVersions[$oldKey] } else { $oldVersions[$oldCommonKey] }
                
                if (-not $oldVersion -or $oldVersion -ne $newVersion) {
                    $packageChanges += [PSCustomObject]@{
                        Package = $packageId
                        Framework = $tf
                        OldVersion = if ($oldVersion) { $oldVersion } else { "(new)" }
                        NewVersion = $newVersion
                    }
                }
            }
        }
    }
}

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
    $changeSummary += "No package version changes - packages may already be up to date"
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
Write-Host "  Total packages: $($packageList.Count)" -ForegroundColor White
Write-Host "    - Common packages: $($commonPackages.Count)" -ForegroundColor Green
Write-Host "    - Framework-specific packages: $($frameworkSpecificPackages.Count)" -ForegroundColor Yellow
Write-Host "  Package versions changed: $($packageChanges.Count)" -ForegroundColor White

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  1. Run: dotnet restore" -ForegroundColor White
Write-Host "  2. Run: dotnet build" -ForegroundColor White
Write-Host "  3. Test and verify" -ForegroundColor White
Write-Host ""
