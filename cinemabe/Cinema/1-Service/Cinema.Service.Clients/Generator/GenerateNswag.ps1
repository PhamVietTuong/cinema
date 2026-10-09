# ============================================================
# Cinema NSwag Client Generator
# Run this script from its own directory to:
#   1. Publish Cinema.Service.WebApiHost (Release)
#   2. Generate cinema-http.service.ts  -> Angular/
#   3. Generate CinemaClient.cs         -> Cinema/
#   4. Copy cinema-http.service.ts      -> cinema-lib/src/lib/api/
# ============================================================

$SystemName = "Cinema"

$controllers = @()
$controllers += @{
    ApiName            = 'Cinema'
    CopyToLib          = $true
    WebApiPort         = "5102"
}
$controllers += @{
    ApiName            = 'Payment'
    CopyToLib          = $true
    WebApiPort         = "5102"
}
$controllers += @{
    ApiName            = 'Identity'
    CopyToLib          = $true
    WebApiPort         = "5102"
}

# Resolve master generator script
$scriptPath = "$PSScriptRoot\..\..\..\..\Tools\Generator\GenerateNswag.ps1"
if (-not (Test-Path $scriptPath)) {
    Write-Host "Master generator script not found: $scriptPath" -ForegroundColor Red
    Write-Host "Expected location: Tools\Generator\GenerateNswag.ps1"
    Exit 1
}

& $scriptPath -SystemName $SystemName -Controllers $controllers
