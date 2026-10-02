param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Remove-Item publish -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=false -p:Version=$Version -o publish
if ($LASTEXITCODE) { throw "publish failed" }

$iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
& $iscc "/DAppVersion=$Version" installer.iss
if ($LASTEXITCODE) { throw "installer build failed" }
