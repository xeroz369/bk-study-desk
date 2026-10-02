# Đóng gói MSIX bản Microsoft Store từ mã nguồn repo public. Dùng chung cho máy dev (tools-dev\package-store.ps1) và CI (release.yml).
#   pwsh -File store/package-msix.ps1 -Version 1.1.1 [-Out dist/store] [-Dev]
# - Cần Windows SDK (makeappx, makepri); runner windows-latest của GitHub có sẵn.
# - File .msix không cần ký: Store tự ký khi publish. Version phải lớn hơn bản đã nộp trước.
# - Identity mặc định là của app trên Partner Center (Product management → Product identity, Store ID 9NX0KRZRR850).
#   -Dev dùng identity riêng để cài thử trên máy (Developer Mode) mà không đụng bản cài từ Store; file đó không upload được.
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent) 'dist\store'),
    [string]$IdentityName = 'xeroz369.BKStudyDesk',
    [string]$Publisher = 'CN=FF5F6ECE-F76F-4FE9-BCC4-9236BBA4C0DE',
    [string]$PublisherDisplayName = 'xeroz369',
    [switch]$Dev,
    [switch]$SkipUi
)
$ErrorActionPreference = 'Stop'
if ($Dev) { $IdentityName = 'BKStudyDesk.Dev'; $Publisher = 'CN=BKStudyDeskDev' }
$src = Join-Path $Root 'src\SoHocTap'

$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" | Sort-Object FullName | Select-Object -Last 1
if (!$sdk) { throw 'Không tìm thấy makeappx.exe (Windows SDK).' }
$makeappx = $sdk.FullName
$makepri = Join-Path $sdk.DirectoryName 'makepri.exe'

$layout = Join-Path $Out 'layout'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force $layout | Out-Null

# ---------------------------------------------------------------- 1. publish (self-contained, không pdb, ẩn path build)
# Khung Luyện tập build ra ui\ ở gốc repo, csproj chép vào cạnh exe. CI đã build ở bước trước thì truyền -SkipUi.
if (!$SkipUi) {
    Push-Location (Join-Path $Root 'src\ui'); npm ci --no-audit --no-fund | Out-Host; npm run build | Out-Host; $ok = $LASTEXITCODE -eq 0; Pop-Location
    if (!$ok) { throw 'npm run build lỗi' }
}
dotnet publish $src -c Release -r win-x64 --self-contained true -o $layout `
    -p:Store=true -p:Version=$Version -p:DebugType=none -p:DebugSymbols=false -p:ContinuousIntegrationBuild=true -p:Deterministic=true `
    "-p:PathMap=$src=src" -p:SatelliteResourceLanguages=en%3Bvi | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish lỗi' }

# ---------------------------------------------------------------- 2. manifest + icon + resources.pri
Copy-Item (Join-Path $PSScriptRoot 'Assets') (Join-Path $layout 'Assets') -Recurse
$v = ($Version.Split('.') + @('0', '0', '0'))[0..2] -join '.'
$manifest = (Get-Content (Join-Path $PSScriptRoot 'Package.appxmanifest') -Raw -Encoding utf8).
    Replace('$NAME$', $IdentityName).Replace('$PUBLISHER$', $Publisher).
    Replace('$PUBLISHER_DISPLAY$', $PublisherDisplayName).Replace('$VERSION$', "$v.0")
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

$pri = Join-Path $Out 'priconfig.xml'
& $makepri createconfig /cf $pri /dq vi-VN /o | Out-Null
& $makepri new /pr $layout /cf $pri /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri lỗi' }

# ---------------------------------------------------------------- 3. pack
$msix = Join-Path $Out "BKStudyDesk-$v-x64.msix"
& $makeappx pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx lỗi' }
Write-Host "MSIX: $msix ($([math]::Round((Get-Item $msix).Length / 1MB, 1)) MB) · identity $IdentityName"
$msix
