# ============================================================
# Master NSwag Generator
# Called by each Service.Clients\Generator\GenerateNswag.ps1
# ============================================================
Param(
    [Parameter(Mandatory)] [string]$SystemName,
    [Parameter(Mandatory)] [Array]$Controllers
)

# $PSScriptRoot = E:\cinema\cinemabe\Tools\Generator
# $repoRoot     = E:\cinema\cinemabe
$repoRoot  = (Resolve-Path "$PSScriptRoot\..\..").Path
$startPath = (Get-Location).ToString()   # caller's dir — used only for nswag file output

Write-Host "`nNSwag generation for $SystemName`n" -ForegroundColor Green

# Resolve output path for generated TypeScript (cinema-lib services folder)
$TSOutputPath = "$repoRoot\..\cinemafe\projects\CinemaLib\src\lib\services\"
if (Test-Path $TSOutputPath) {
    $TSOutputPath = (Resolve-Path -Path $TSOutputPath).Path
} else {
    Write-Host "WARNING: cinema-lib services folder not found at $TSOutputPath" -ForegroundColor Yellow
    Write-Host "         TypeScript output will stay in Angular\ folder only."
    $TSOutputPath = $null
}

# ── 1. Publish WebApiHost ──────────────────────────────────────────────────
$webApiPath = "$repoRoot\Cinema\1-Service\$SystemName.Service.WebApiHost\"
if (-not (Test-Path $webApiPath)) {
    Write-Host "WebApiHost project not found: $webApiPath" -ForegroundColor Red
    Exit 1
}
$webApiPath    = (Resolve-Path -Path $webApiPath).Path
$publishOutput = "$webApiPath\bin\Release\PublishOutput"

Write-Host "Publishing $SystemName.Service.WebApiHost..." -ForegroundColor Blue
dotnet publish --output $publishOutput --configuration Release $webApiPath
Write-Host "Publish complete.`n"

# ── 2. Clear old Swagger JSON files ───────────────────────────────────────
$swaggerDir = "$repoRoot\Swagger"
if (Test-Path $swaggerDir) {
    Remove-Item "$swaggerDir\*.json" -ErrorAction SilentlyContinue
    Write-Host "Old Swagger JSON files removed."
}

# ── 3. Generate for each controller ───────────────────────────────────────
foreach ($ctrl in $Controllers) {
    $ApiName   = $ctrl.ApiName
    $ApiLower  = $ApiName.ToLower()
    $ApiUpper  = $ApiName.ToUpper()

    Write-Host "`n##### $ApiName #####`n" -ForegroundColor Blue

    # Build .nswag config from template
    $nswagFile    = "$startPath\$ApiName`Client.nswag"
    $templateFile = "$PSScriptRoot\TemplateDotNetCore.nswag"
    if (-not (Test-Path $templateFile)) {
        Write-Host "Template not found: $templateFile" -ForegroundColor Red
        Continue
    }

    $localAngularDir = "$repoRoot\Cinema\1-Service\$SystemName.Service.Clients\Angular"
    if (-not (Test-Path $localAngularDir)) {
        New-Item -ItemType Directory -Path $localAngularDir -Force | Out-Null
    }
    $localAngularDir = (Resolve-Path $localAngularDir).Path

    (Get-Content $templateFile) `
        -replace '\[SystemName\]',            $SystemName `
        -replace '\[PascalCaseController\]',  $ApiName `
        -replace '\[LowerCaseController\]',   $ApiLower `
        -replace '\[UpperCaseController\]',   $ApiUpper `
        -replace '\[TSServicesAddress\]',     ($localAngularDir.Replace("\", "/") + "/") `
        -replace '\[WebApiPort\]',            $ctrl.WebApiPort `
        | Set-Content $nswagFile

    Write-Host "NSwag config written: $nswagFile"

    # Delete old TypeScript output
    $tsFile = "$localAngularDir\$ApiLower-http.service.ts"
    if (Test-Path $tsFile) {
        Remove-Item $tsFile -Force
        Write-Host "Removed old TS file: $tsFile"
    }

    # Run NSwag
    Write-Host "Running NSwag..." -ForegroundColor Cyan
    $nswagDll = "${env:ProgramFiles(x86)}\Rico Suter\NSwagStudio14\Net80\dotnet-nswag.dll"
    if (-not (Test-Path $nswagDll)) {
        Write-Host "NSwagStudio not found at: $nswagDll" -ForegroundColor Red
        Write-Host "Please install NSwag Studio from https://github.com/RicoSuter/NSwag/releases" -ForegroundColor Red
        Exit 1
    }
    dotnet $nswagDll run $nswagFile
    Write-Host "NSwag complete.`n"

    # Fix ICollection → List in generated C# client
    $csFile = "$repoRoot\Cinema\1-Service\$SystemName.Service.Clients\$ApiName\$($ApiName)Client.cs"
    if (Test-Path $csFile) {
        $content = [System.IO.File]::ReadAllText($csFile)
        $fixed   = $content `
            -replace 'new System\.Collections\.Generic\.ICollection<', 'new List<' `
            -replace 'System\.Collections\.Generic\.ICollection<',      'IList<'
        if ($content -ne $fixed) {
            $fixed = "using System.Collections.Generic;`r`n" + $fixed
            [System.IO.File]::WriteAllText($csFile, $fixed)
            Write-Host "Fixed ICollection references in $csFile"
        }
    }

    # Post-process TypeScript: add export wrapper & fix imports
    if (Test-Path $tsFile) {
        $tsContent = [System.IO.File]::ReadAllText($tsFile) `
            -replace 'namespace ',      "export class $($ApiName)ServiceAgent { }`nexport namespace " `
            -replace 'HttpClient, HttpHeaders, HttpParams, HttpResponse, HttpResponseBase, HttpErrorResponse', `
                     'HttpClient, HttpHeaders, HttpResponse, HttpResponseBase'
        $tsContent = "/* spellcheck: off */`r`n`r`n" + $tsContent
        [System.IO.File]::WriteAllText($tsFile, $tsContent)
        Write-Host "Post-processed TypeScript: $tsFile"
    }

    # Copy to cinema-lib
    if ($ctrl.CopyToLib -and $TSOutputPath -and (Test-Path $tsFile)) {
        Write-Host "Copying TS to cinema-lib..." -ForegroundColor Green
        Copy-Item -Path $tsFile -Destination $TSOutputPath -Force
        Write-Host "Copied to: $TSOutputPath$ApiLower-http.service.ts"
    }
}

Write-Host "`nGeneration complete." -ForegroundColor Green
