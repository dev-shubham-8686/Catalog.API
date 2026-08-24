<#
Generates the htpasswd credentials file the local registry service uses for basic auth.
Run this once before `docker compose up -d registry`. Re-run to rotate/add users.
#>
param(
    [string]$Username = "registry-user",
    [string]$Password = "RegistryP@ss1"
)

$authDir = Join-Path $PSScriptRoot "..\containers\registry\auth"
New-Item -ItemType Directory -Force -Path $authDir | Out-Null

$htpasswdPath = Join-Path $authDir "htpasswd"

# httpd's htpasswd tool supports bcrypt (-B), which the registry's htpasswd
# auth backend requires (it will reject MD5/crypt hashes).
docker run --rm --entrypoint htpasswd httpd:2.4-alpine -Bbn $Username $Password |
    Out-File -FilePath $htpasswdPath -Encoding ascii -NoNewline

Write-Host "Wrote credentials for '$Username' to $htpasswdPath"
Write-Host "Login with: docker login localhost:5001 -u $Username -p $Password"
