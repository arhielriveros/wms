[CmdletBinding()]
# TEST-SEC-0002: real Keycloak tokens, tenant IDOR, privilege escalation, tampering and revocation.
param(
    [string]$EnvFile = ".env.example",
    [ValidatePattern("^[a-z0-9][a-z0-9_-]+$")]
    [string]$ProjectName = "wms-security",
    [int]$KeycloakPort = 18083,
    [int]$ApiPort = 18084,
    [int]$TimeoutSeconds = 240,
    [string]$OutputDirectory = ".backups",
    [switch]$KeepEnvironment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

function Read-EnvFile([string]$Path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith("#")) { continue }
        $pair = $trimmed.Split("=", 2)
        if ($pair.Count -eq 2) { $values[$pair[0]] = $pair[1] }
    }
    return $values
}

function New-RandomSecret([int]$Bytes = 32) {
    $buffer = New-Object byte[] $Bytes
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($buffer) } finally { $generator.Dispose() }
    return [Convert]::ToBase64String($buffer)
}

function Invoke-KeycloakAdmin([string]$Method, [string]$Path, [object]$Body = $null) {
    $parameters = @{
        Method = $Method
        Uri = "$script:keycloakBase/admin/realms/wms-dev$Path"
        Headers = @{ Authorization = "Bearer $script:adminToken"; Host = $script:issuerHost }
        UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = ConvertTo-Json -InputObject $Body -Depth 20 -Compress
    }
    try {
        return Invoke-RestMethod @parameters
    }
    catch {
        throw "Keycloak admin request $Method $Path failed: $($_.Exception.Message)"
    }
}

function New-TestUser([string]$Username, [string]$Password, [string]$TenantId, [string]$WarehouseId) {
    $user = @{
        username = $Username
        enabled = $true
        emailVerified = $true
        attributes = @{ tenant_id = @($TenantId); warehouse_ids = @($WarehouseId) }
        credentials = @(@{ type = "password"; value = $Password; temporary = $false })
    }
    Invoke-KeycloakAdmin -Method Post -Path "/users" -Body $user | Out-Null
    $matches = @(Invoke-KeycloakAdmin -Method Get -Path "/users?username=$([Uri]::EscapeDataString($Username))&exact=true")
    if ($matches.Count -ne 1) { throw "Could not resolve Keycloak user $Username." }
    return $matches[0].id
}

function Grant-ClientRoles([string]$UserId, [string]$ClientUuid, [string[]]$RoleNames) {
    $roles = @($RoleNames | ForEach-Object {
        Invoke-KeycloakAdmin -Method Get -Path "/clients/$ClientUuid/roles/$([Uri]::EscapeDataString($_))"
    })
    Invoke-KeycloakAdmin -Method Post -Path "/users/$UserId/role-mappings/clients/$ClientUuid" -Body $roles | Out-Null
}

function Set-WmsUserProfile {
    $profile = Invoke-KeycloakAdmin -Method Get -Path "/users/profile"
    $operationalAttributes = @(
        @{ name = "tenant_id"; displayName = "Tenant ID"; multivalued = $false; permissions = @{ view = @("admin"); edit = @("admin") }; validations = @{ length = @{ max = 36 } } },
        @{ name = "warehouse_ids"; displayName = "Warehouse IDs"; multivalued = $true; permissions = @{ view = @("admin"); edit = @("admin") }; validations = @{ length = @{ max = 36 } } },
        @{ name = "owner_ids"; displayName = "Owner IDs"; multivalued = $true; permissions = @{ view = @("admin"); edit = @("admin") }; validations = @{ length = @{ max = 36 } } },
        @{ name = "zone_ids"; displayName = "Zone IDs"; multivalued = $true; permissions = @{ view = @("admin"); edit = @("admin") }; validations = @{ length = @{ max = 36 } } }
    )
    $operationalNames = @($operationalAttributes | ForEach-Object { $_.name })
    $existing = @($profile.attributes | Where-Object { $_.name -notin $operationalNames })
    $profile.attributes = @($existing + $operationalAttributes)
    Invoke-KeycloakAdmin -Method Put -Path "/users/profile" -Body $profile | Out-Null
}

function Get-PasswordToken([string]$Username, [string]$Password) {
    return Invoke-RestMethod -Method Post -Uri "$script:keycloakBase/realms/wms-dev/protocol/openid-connect/token" `
        -Headers @{ Host = $script:issuerHost } -ContentType "application/x-www-form-urlencoded" -UseBasicParsing `
        -Body @{ grant_type = "password"; client_id = $script:clientId; client_secret = $script:clientSecret; username = $Username; password = $Password }
}

function Invoke-Api([string]$Path, [string]$Token = "", [hashtable]$Headers = @{}) {
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, "$script:apiBase$Path")
    $response = $null
    if ($Token) { $request.Headers.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $Token) }
    foreach ($entry in $Headers.GetEnumerator()) { $request.Headers.TryAddWithoutValidation($entry.Key, [string]$entry.Value) | Out-Null }
    try {
        $response = $script:httpClient.SendAsync($request).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Body = $body }
    }
    finally {
        if ($response) { $response.Dispose() }
        $request.Dispose()
    }
}

function Tamper-TenantClaim([string]$Token, [string]$SourceTenant, [string]$TargetTenant) {
    $parts = $Token.Split('.')
    if ($parts.Count -ne 3) { throw "Access token is not a JWT." }
    $payload = $parts[1].Replace('-', '+').Replace('_', '/')
    while ($payload.Length % 4) { $payload += "=" }
    $json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload))
    if (-not $json.Contains($SourceTenant)) { throw "JWT does not contain the expected tenant claim." }
    $tampered = $json.Replace($SourceTenant, $TargetTenant)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($tampered)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    return "$($parts[0]).$encoded.$($parts[2])"
}

function Assert-Status([string]$Name, [int]$Expected, [object]$Response) {
    if ($Response.Status -ne $Expected) { throw "$Name expected HTTP $Expected but received $($Response.Status). Body: $($Response.Body)" }
    $script:checks[$Name] = $Response.Status
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$envPath = if ([IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $root $EnvFile }
$composePath = Join-Path $root "docker-compose.security.yml"
$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
foreach ($requiredPath in @($envPath, $composePath)) { if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Required security drill file not found: $requiredPath" } }
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
$envMap = Read-EnvFile $envPath
foreach ($key in @("KEYCLOAK_ADMIN", "KEYCLOAK_ADMIN_PASSWORD")) { if (-not $envMap.ContainsKey($key)) { throw "$key is required in $envPath" } }

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$manifest = Join-Path $outputPath "wms-keycloak-security-$stamp.json"
$tenantA = "11111111-1111-1111-1111-111111111111"
$tenantB = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
$warehouseA = "22222222-2222-2222-2222-222222222222"
$warehouseB = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
$script:clientId = "wms-security-drill"
$script:clientSecret = New-RandomSecret
$userPassword = New-RandomSecret 24
$script:issuerHost = "host.docker.internal:$KeycloakPort"
$script:keycloakBase = "http://127.0.0.1:$KeycloakPort"
$script:apiBase = "http://127.0.0.1:$ApiPort"
$script:checks = [ordered]@{}
$script:httpClient = [Net.Http.HttpClient]::new()
$script:httpClient.Timeout = [TimeSpan]::FromSeconds(10)
$script:adminToken = ""

$environmentValues = [ordered]@{
    WMS_SECURITY_CLIENT_SECRET = $script:clientSecret
    SECURITY_KEYCLOAK_PORT = $KeycloakPort.ToString()
    SECURITY_API_PORT = $ApiPort.ToString()
}
$savedEnvironment = @{}
foreach ($entry in $environmentValues.GetEnumerator()) {
    $savedEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, "Process")
    [Environment]::SetEnvironmentVariable($entry.Key, [string]$entry.Value, "Process")
}
$composeArguments = @("compose", "--env-file", $envPath, "--project-name", $ProjectName, "--file", $composePath)

Push-Location $root
try {
    $nativeErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & docker @composeArguments down --volumes --remove-orphans 2>$null | Out-Null
    $ErrorActionPreference = $nativeErrorAction

    & docker @composeArguments up --detach --wait --wait-timeout $TimeoutSeconds postgres keycloak
    if ($LASTEXITCODE -ne 0) { throw "Could not start PostgreSQL and Keycloak for the security drill." }

    $admin = Invoke-RestMethod -Method Post -Uri "$script:keycloakBase/realms/master/protocol/openid-connect/token" `
        -Headers @{ Host = $script:issuerHost } -ContentType "application/x-www-form-urlencoded" -UseBasicParsing `
        -Body @{ grant_type = "password"; client_id = "admin-cli"; username = $envMap.KEYCLOAK_ADMIN; password = $envMap.KEYCLOAK_ADMIN_PASSWORD }
    $script:adminToken = $admin.access_token
    Set-WmsUserProfile

    $testClient = @{
        clientId = $script:clientId
        name = "Ephemeral security drill"
        enabled = $true
        protocol = "openid-connect"
        publicClient = $false
        secret = $script:clientSecret
        standardFlowEnabled = $false
        directAccessGrantsEnabled = $true
        serviceAccountsEnabled = $false
        fullScopeAllowed = $true
        defaultClientScopes = @("wms-context")
        protocolMappers = @(
            @{
                name = "wms-api-audience"
                protocol = "openid-connect"
                protocolMapper = "oidc-audience-mapper"
                config = @{ "included.client.audience" = "wms-api"; "access.token.claim" = "true"; "id.token.claim" = "false"; "introspection.token.claim" = "true" }
            },
            @{
                name = "wms-api-client-roles"
                protocol = "openid-connect"
                protocolMapper = "oidc-usermodel-client-role-mapper"
                config = @{ "claim.name" = "resource_access.`${client_id}.roles"; "multivalued" = "true"; "jsonType.label" = "String"; "access.token.claim" = "true"; "id.token.claim" = "false"; "introspection.token.claim" = "true" }
            },
            @{
                name = "tenant-id"
                protocol = "openid-connect"
                protocolMapper = "oidc-usermodel-attribute-mapper"
                config = @{ "user.attribute" = "tenant_id"; "claim.name" = "tenant_id"; "jsonType.label" = "String"; "access.token.claim" = "true"; "id.token.claim" = "false"; "introspection.token.claim" = "true" }
            },
            @{
                name = "warehouse-ids"
                protocol = "openid-connect"
                protocolMapper = "oidc-usermodel-attribute-mapper"
                config = @{ "user.attribute" = "warehouse_ids"; "claim.name" = "warehouse_ids"; "jsonType.label" = "String"; "multivalued" = "true"; "access.token.claim" = "true"; "id.token.claim" = "false"; "introspection.token.claim" = "true" }
            }
        )
    }
    Invoke-KeycloakAdmin -Method Post -Path "/clients" -Body $testClient | Out-Null
    $clientMatches = @(Invoke-KeycloakAdmin -Method Get -Path "/clients?clientId=$script:clientId")
    $apiMatches = @(Invoke-KeycloakAdmin -Method Get -Path "/clients?clientId=wms-api")
    if ($clientMatches.Count -ne 1 -or $apiMatches.Count -ne 1) { throw "Could not resolve Keycloak clients for the security drill." }
    $wmsApiUuid = $apiMatches[0].id

    $supervisorAId = New-TestUser "security-supervisor-a" $userPassword $tenantA $warehouseA
    $supervisorBId = New-TestUser "security-supervisor-b" $userPassword $tenantB $warehouseB
    $limitedAId = New-TestUser "security-limited-a" $userPassword $tenantA $warehouseA
    $supervisorRoles = @("wms.inventory.read", "wms.supervisor.read", "wms.outbound.read")
    Grant-ClientRoles $supervisorAId $wmsApiUuid $supervisorRoles
    Grant-ClientRoles $supervisorBId $wmsApiUuid $supervisorRoles
    Grant-ClientRoles $limitedAId $wmsApiUuid @("wms.task.read_assigned")

    & docker @composeArguments up --detach --build --wait --wait-timeout $TimeoutSeconds api
    if ($LASTEXITCODE -ne 0) { throw "Could not start the security-test API." }

    $tokenA = Get-PasswordToken "security-supervisor-a" $userPassword
    $tokenB = Get-PasswordToken "security-supervisor-b" $userPassword
    $limited = Get-PasswordToken "security-limited-a" $userPassword

    Assert-Status "unauthenticated_rejected" 401 (Invoke-Api "/api/v1/inventory/stock")
    Assert-Status "supervisor_authorized" 200 (Invoke-Api "/api/v1/inventory/stock" $tokenA.access_token)
    Assert-Status "privilege_escalation_rejected" 403 (Invoke-Api "/api/v1/inventory/stock" $limited.access_token @{ "X-Scopes" = "wms.inventory.read"; "X-Tenant-Id" = $tenantB })
    Assert-Status "tenant_a_cannot_read_tenant_b" 404 (Invoke-Api "/api/v1/outbound/orders/by-external/SEC-ORDER-B" $tokenA.access_token @{ "X-Tenant-Id" = $tenantB })
    Assert-Status "tenant_b_reads_own_resource" 200 (Invoke-Api "/api/v1/outbound/orders/by-external/SEC-ORDER-B" $tokenB.access_token)
    $tampered = Tamper-TenantClaim $tokenA.access_token $tenantA $tenantB
    Assert-Status "tampered_claim_rejected" 401 (Invoke-Api "/api/v1/inventory/stock" $tampered)
    Assert-Status "token_active_before_logout" 200 (Invoke-Api "/api/v1/inventory/stock" $tokenA.access_token)

    Invoke-RestMethod -Method Post -Uri "$script:keycloakBase/realms/wms-dev/protocol/openid-connect/logout" `
        -Headers @{ Host = $script:issuerHost } -ContentType "application/x-www-form-urlencoded" -UseBasicParsing `
        -Body @{ client_id = $script:clientId; client_secret = $script:clientSecret; refresh_token = $tokenA.refresh_token } | Out-Null
    $revoked = $null
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    do {
        $revoked = Invoke-Api "/api/v1/inventory/stock" $tokenA.access_token
        if ($revoked.Status -ne 401) { Start-Sleep -Milliseconds 500 }
    } while ($revoked.Status -ne 401 -and [DateTimeOffset]::UtcNow -lt $deadline)
    Assert-Status "revoked_token_rejected" 401 $revoked

    $result = [ordered]@{
        status = "PASS"
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        testId = "TEST-SEC-0002"
        provider = "Keycloak 26.1"
        tokenValidation = "RS256 + issuer + audience + lifetime + introspection"
        checks = $script:checks
        secretsPersisted = $false
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifest -Encoding UTF8
    [pscustomobject]$result
}
catch {
    $diagnosticErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & docker @composeArguments logs --no-color --tail 250 api keycloak postgres 2>$null | Write-Warning
    $ErrorActionPreference = $diagnosticErrorAction
    throw
}
finally {
    $script:httpClient.Dispose()
    if (-not $KeepEnvironment) {
        $cleanupErrorAction = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        & docker @composeArguments down --volumes --remove-orphans 2>$null | Out-Null
        $ErrorActionPreference = $cleanupErrorAction
    }
    foreach ($entry in $environmentValues.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $savedEnvironment[$entry.Key], "Process")
    }
    Pop-Location
}
