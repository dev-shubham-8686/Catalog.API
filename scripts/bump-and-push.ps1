<#
Looks up the highest existing semver tag for a repository in the local registry, bumps it
(patch by default), then tags and pushes the given local image under the new version.

Examples:
  ./scripts/bump-and-push.ps1
      # bumps catalog-api's patch version using store-catalog_api:latest

  ./scripts/bump-and-push.ps1 -Bump Minor
  ./scripts/bump-and-push.ps1 -Repo identity-api -Image store-identity_api:latest
#>
param(
    [string]$Repo = "catalog-api",
    [string]$Image = "store-catalog_api:latest",
    [ValidateSet("Major", "Minor", "Patch")]
    [string]$Bump = "Patch",
    [string]$RegistryHost = "localhost:5001",
    [string]$Username = "registry-user",
    [string]$Password = "RegistryP@ss1"
)

$credentialBytes = [Text.Encoding]::ASCII.GetBytes("$($Username):$($Password)")
$headers = @{ Authorization = "Basic " + [Convert]::ToBase64String($credentialBytes) }

function Get-LatestVersion {
    try {
        $response = Invoke-RestMethod -Uri "http://$RegistryHost/v2/$Repo/tags/list" -Headers $headers
    }
    catch {
        # 404 => repository doesn't exist in the registry yet
        return $null
    }

    $semverTags = $response.tags | Where-Object { $_ -match '^\d+\.\d+\.\d+$' } | ForEach-Object { [version]$_ }

    if (-not $semverTags) {
        return $null
    }

    return $semverTags | Sort-Object | Select-Object -Last 1
}

$latest = Get-LatestVersion

if ($null -eq $latest) {
    Write-Host "No existing semver tags found for '$Repo' - starting at 1.0.0"
    $nextVersion = [version]"1.0.0"
}
else {
    switch ($Bump) {
        "Major" { $nextVersion = [version]::new($latest.Major + 1, 0, 0) }
        "Minor" { $nextVersion = [version]::new($latest.Major, $latest.Minor + 1, 0) }
        "Patch" { $nextVersion = [version]::new($latest.Major, $latest.Minor, $latest.Build + 1) }
    }
    Write-Host "Latest tag for '$Repo' is $latest - bumping $Bump -> $nextVersion"
}

$newTag = "$RegistryHost/$Repo`:$nextVersion"

docker tag $Image $newTag
if ($LASTEXITCODE -ne 0) { throw "docker tag failed - does local image '$Image' exist?" }

docker push $newTag
if ($LASTEXITCODE -ne 0) { throw "docker push failed" }

Write-Host "Pushed $newTag"
