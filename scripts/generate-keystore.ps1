# Generate AudioBridge release keystore
# Set these environment variables or edit the defaults
$storePass = $env:AUDIOBRIDGE_STORE_PASS
$keyPass  = $env:AUDIOBRIDGE_KEY_PASS

if (-not $storePass) { $storePass = Read-Host -Prompt "Enter keystore password" -AsSecureString }
if (-not $keyPass)  { $keyPass  = Read-Host -Prompt "Enter key password" -AsSecureString }

$storePassPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($storePass))
$keyPassPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($keyPass))

$keystorePath = Join-Path (Split-Path $PSScriptRoot -Parent) "audiobridge.jks"

keytool -genkey -v `
    -keystore $keystorePath `
    -alias audiobridge `
    -keyalg RSA -keysize 2048 -validity 10000 `
    -storepass $storePassPlain `
    -keypass $keyPassPlain `
    -dname "CN=AudioBridge, OU=Development, O=AudioBridge, L=Unknown, ST=Unknown, C=US"

Write-Host "Keystore created at: $keystorePath"
Write-Host ""
Write-Host "Build APK with:"
Write-Host "  `$env:AUDIOBRIDGE_STORE_PASS = `"<password>`""
Write-Host "  `$env:AUDIOBRIDGE_KEY_PASS = `"<password>`""
Write-Host "  cd android; .\gradlew assembleRelease"
