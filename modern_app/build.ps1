[CmdletBinding()]
param(
    [switch]$SkipWindows,
    [switch]$SkipAndroid
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$projectRoot = $PSScriptRoot
$repoRoot = Split-Path $projectRoot -Parent
$dotnetRoot = Join-Path $env:USERPROFILE '.dotnet-maui'
$dotnet = Join-Path $dotnetRoot 'dotnet.exe'
$androidSdk = Join-Path $env:USERPROFILE '.android-sdk'
$javaSdk = Join-Path $env:USERPROFILE 'Java\graalvm-jdk-21.0.11+9.1'
$keytool = Join-Path $javaSdk 'bin\keytool.exe'

foreach ($required in @($dotnet, $androidSdk, $javaSdk)) {
    if (-not (Test-Path $required)) {
        throw "Не найден компонент сборки: $required"
    }
}

$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"

function Reset-OutputDirectory([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $releaseRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'releases'))
    if (-not $fullPath.StartsWith($releaseRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Отказ очистить каталог вне releases: $fullPath"
    }
    if (Test-Path $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $fullPath | Out-Null
}

Write-Host 'Проверка игровых правил...'
& $dotnet run --project (Join-Path $projectRoot 'Checks\Checks.csproj') -c Release

if (-not $SkipWindows) {
    $windowsRelease = Join-Path $projectRoot 'releases\windows'
    Reset-OutputDirectory $windowsRelease
    Write-Host 'Сборка Windows x64...'
    & $dotnet publish (Join-Path $projectRoot 'OverwatchRandomizer.Modern.csproj') `
        -f net10.0-windows10.0.19041.0 -c Release -o $windowsRelease
}

if (-not $SkipAndroid) {
    if (-not (Test-Path $keytool)) {
        throw "Не найден keytool: $keytool"
    }

    $signingDir = Join-Path $projectRoot 'signing'
    $keystore = Join-Path $signingDir 'overwatch-randomizer.keystore'
    $passwordFile = Join-Path $signingDir 'password.txt'
    New-Item -ItemType Directory -Path $signingDir -Force | Out-Null

    if (-not (Test-Path $keystore)) {
        $password = -join ((1..32) | ForEach-Object { '{0:x}' -f (Get-Random -Maximum 16) })
        Set-Content -LiteralPath $passwordFile -Value $password -NoNewline
        & $keytool -genkeypair -v -keystore $keystore -alias overwatch-randomizer `
            -keyalg RSA -keysize 2048 -validity 10000 `
            -dname 'CN=Overwatch Randomizer, O=Local Release, C=RU' `
            -storepass $password -keypass $password
    }
    elseif (-not (Test-Path $passwordFile)) {
        throw "Для существующего ключа отсутствует $passwordFile"
    }

    $androidRelease = Join-Path $projectRoot 'releases\android'
    Reset-OutputDirectory $androidRelease

    # Android aapt не принимает кириллицу в полном пути к проекту.
    $drive = 'O:'
    $mappedRoot = "$drive\"
    $mappingCreated = $false
    $existingMapping = (& subst.exe) | Where-Object { $_ -match '^O:\\:' }
    if (-not $existingMapping) {
        & subst.exe $drive $repoRoot
        $mappingCreated = $true
    }

    try {
        $mappedProject = "$mappedRoot`modern_app"
        Push-Location $mappedProject
        try {
            $mappedKeystore = "$mappedProject\signing\overwatch-randomizer.keystore"
            $env:OR_SIGNING_PASSWORD = Get-Content -Raw "$mappedProject\signing\password.txt"
            & $dotnet clean '.\OverwatchRandomizer.Modern.csproj' -f net10.0-android -c Release `
                -p:AndroidSdkDirectory=$androidSdk -p:JavaSdkDirectory=$javaSdk
            Write-Host 'Сборка подписанного Android arm64 APK...'
            & $dotnet publish '.\OverwatchRandomizer.Modern.csproj' -f net10.0-android -c Release `
                -p:AndroidSdkDirectory=$androidSdk -p:JavaSdkDirectory=$javaSdk `
                -p:AndroidKeyStore=true `
                -p:AndroidSigningKeyStore=$mappedKeystore `
                -p:AndroidSigningKeyAlias=overwatch-randomizer `
                -p:AndroidSigningKeyPass=env:OR_SIGNING_PASSWORD `
                -p:AndroidSigningStorePass=env:OR_SIGNING_PASSWORD

            $apk = Get-ChildItem -Path '.\bin\Release\net10.0-android\android-arm64\publish' `
                -Filter '*Signed.apk' | Select-Object -First 1
            if (-not $apk) {
                throw 'Подписанный APK не найден после publish.'
            }
            Copy-Item -LiteralPath $apk.FullName `
                -Destination (Join-Path $androidRelease 'OverwatchRandomizer-arm64-v8a.apk') -Force
        }
        finally {
            Remove-Item Env:OR_SIGNING_PASSWORD -ErrorAction SilentlyContinue
            Pop-Location
        }
    }
    finally {
        if ($mappingCreated) {
            & subst.exe $drive /D
        }
    }
}

Write-Host 'Готово. Релизы находятся в modern_app\releases.'
