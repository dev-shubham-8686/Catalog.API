<#
Lists every repository:tag stored in the local registry (catalog_registry), by querying its
own HTTP API — `docker images` only shows your local cache, not what's actually on the registry.
#>
param(
    [string]$RegistryHost = "localhost:5001",
    [string]$Username = "registry-user",
    [string]$Password = "RegistryP@ss1"
)

$credentialBytes = [Text.Encoding]::ASCII.GetBytes("$($Username):$($Password)")
$headers = @{ Authorization = "Basic " + [Convert]::ToBase64String($credentialBytes) }

$catalog = Invoke-RestMethod -Uri "http://$RegistryHost/v2/_catalog" -Headers $headers

if (-not $catalog.repositories -or $catalog.repositories.Count -eq 0) {
    Write-Host "No repositories found on $RegistryHost."
    return
}

foreach ($repo in $catalog.repositories) {
    $tags = Invoke-RestMethod -Uri "http://$RegistryHost/v2/$repo/tags/list" -Headers $headers
    foreach ($tag in $tags.tags) {
        Write-Host "$RegistryHost/$repo`:$tag"
    }
}
