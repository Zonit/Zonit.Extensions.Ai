param(
    [Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)]
    [string[]]$TargetFrameworks = @("net8.0", "net9.0")
)

$ErrorActionPreference = "Stop"

if ($TargetFrameworks.Count -eq 0) {
    Write-Error "Must provide at least one .NET version"
    exit 1
}

Write-Host "Updating to: $($TargetFrameworks -join ', ')"

# Function to get best package version
function Get-BestPackageVersion {
    param([string]$PackageId, [string]$TargetFramework, [bool]$AllowPrerelease)
    
    try {
        $url = "https://api.nuget.org/v3-flatcontainer/$($PackageId.ToLower())/index.json"
        $response = Invoke-RestMethod $url -TimeoutSec 15
        $versions = $response.versions
        
        if ($TargetFramework -match 'net(\d+)') {
            $targetMajor = [int]$matches[1]
        } else {
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
            } catch { $null }
        } | Where-Object { $_ -ne $null }
        
        # Priority: stable with exact major, then lower major
        $best = $allVersions | Where-Object { 
            -not $_.IsPrerelease -and $_.Version.Major -eq $targetMajor 
        } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
        
        if (-not $best) {
            $best = $allVersions | Where-Object { 
                -not $_.IsPrerelease -and $_.Version.Major -lt $targetMajor 
            } | Sort-Object -Property @{Expression={$_.Version}; Descending=$true} | Select-Object -First 1
        }
        
        return $best.OriginalString
    } catch {
        Write-Warning "Error getting version for ${PackageId}: $_"
        return $null
    }
}

# Step 1: Update TargetFrameworks in .csproj
$targetFrameworksString = $TargetFrameworks -join ';'
$csprojs = Get-ChildItem -Recurse -Filter "*.csproj" | Where-Object { 
    $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' 
}

foreach ($proj in $csprojs) {
    [xml]$xml = Get-Content $proj.FullName
    $propertyGroups = @($xml.Project.PropertyGroup)
    if ($propertyGroups.Count -eq 0) {
        $pg = $xml.CreateElement("PropertyGroup")
        $xml.Project.AppendChild($pg) | Out-Null
        $propertyGroups = @($pg)
    }
    
    $pg = $propertyGroups[0]
    @($pg.SelectNodes("TargetFramework")) | ForEach-Object { $pg.RemoveChild($_) | Out-Null }
    
    $tfsNodes = @($pg.SelectNodes("TargetFrameworks"))
    if ($tfsNodes.Count -eq 0) {
        $tfsNode = $xml.CreateElement("TargetFrameworks")
        $tfsNode.InnerText = $targetFrameworksString
        $pg.AppendChild($tfsNode) | Out-Null
    } else {
        $tfsNodes[0].InnerText = $targetFrameworksString
    }
    
    $xml.Save($proj.FullName)
}

# Step 2: Remove Version from PackageReference
foreach ($proj in $csprojs) {
    $content = Get-Content $proj.FullName -Raw
    $content = $content -replace '(<PackageReference\s+Include="[^"]+")(\s+Version="[^"]+")', '$1'
    $content | Set-Content $proj.FullName -NoNewline
}

# Step 3: Collect all packages
$allPackages = @{}
foreach ($proj in $csprojs) {
    [xml]$xml = Get-Content $proj.FullName
    $xml.Project.ItemGroup.PackageReference | ForEach-Object {
        if ($_.Include) { $allPackages[$_.Include] = $true }
    }
}
$packageList = $allPackages.Keys | Sort-Object

# Step 4: Update Directory.Packages.props
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
}

[xml]$xml = Get-Content $propsFile.FullName
@($xml.Project.ItemGroup | Where-Object { $_.Condition -match "TargetFramework" }) | ForEach-Object {
    $xml.Project.RemoveChild($_) | Out-Null
}

foreach ($tf in $TargetFrameworks) {
    $itemGroup = $xml.CreateElement("ItemGroup")
    $itemGroup.SetAttribute("Condition", "'`$(TargetFramework)' == '$tf'")
    
    foreach ($packageId in $packageList) {
        $version = Get-BestPackageVersion -PackageId $packageId -TargetFramework $tf -AllowPrerelease $false
        if ($version) {
            $pkgVersion = $xml.CreateElement("PackageVersion")
            $pkgVersion.SetAttribute("Include", $packageId)
            $pkgVersion.SetAttribute("Version", $version)
            $itemGroup.AppendChild($pkgVersion) | Out-Null
        }
    }
    
    $xml.Project.AppendChild($itemGroup) | Out-Null
}

$xml.Save($propsFile.FullName)
Write-Host "Done!"
