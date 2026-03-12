# AutoRegressionVM Release Script
# Usage: .\scripts\release.ps1 -Version "1.0.0"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [switch]$SkipBuild,
    [switch]$SkipGitHubRelease
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$MSBuild = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
$CsprojPath = Join-Path $ProjectRoot "AutoRegressionVM.csproj"
$ReleaseBinDir = Join-Path $ProjectRoot "bin\Release"
$ReleaseDir = Join-Path $ProjectRoot "release"
$ZipName = "AutoRegressionVM-v$Version.zip"
$ZipPath = Join-Path $ReleaseDir $ZipName

# MSBuild 경로 확인
if (-not (Test-Path $MSBuild)) {
    # 다른 VS 버전 탐색
    $candidates = @(
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )
    $MSBuild = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $MSBuild) {
        Write-Error "MSBuild.exe not found. Install Visual Studio or set path manually."
        exit 1
    }
}

Write-Host "=== AutoRegressionVM Release v$Version ===" -ForegroundColor Cyan
Write-Host "MSBuild: $MSBuild"

# 1. AssemblyInfo.cs 버전 업데이트
Write-Host "`n[1/5] Updating AssemblyInfo.cs version to $Version..." -ForegroundColor Yellow
$assemblyInfoPath = Join-Path $ProjectRoot "Properties\AssemblyInfo.cs"
$assemblyContent = Get-Content $assemblyInfoPath -Raw
$assemblyContent = $assemblyContent -replace 'AssemblyVersion\("[^"]*"\)', "AssemblyVersion(`"$Version.0`")"
$assemblyContent = $assemblyContent -replace 'AssemblyFileVersion\("[^"]*"\)', "AssemblyFileVersion(`"$Version.0`")"
Set-Content -Path $assemblyInfoPath -Value $assemblyContent -NoNewline
Write-Host "  Version set to $Version.0" -ForegroundColor Green

# 2. NuGet Restore + Release 빌드
if (-not $SkipBuild) {
    Write-Host "`n[2/5] Restoring NuGet packages..." -ForegroundColor Yellow
    & $MSBuild $CsprojPath -t:Restore -verbosity:minimal
    if ($LASTEXITCODE -ne 0) { Write-Error "NuGet restore failed."; exit 1 }

    Write-Host "`n[3/5] Building Release configuration..." -ForegroundColor Yellow
    & $MSBuild $CsprojPath -p:Configuration=Release -verbosity:minimal
    if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }
    Write-Host "  Build succeeded." -ForegroundColor Green
} else {
    Write-Host "`n[2/5] Skipping NuGet restore (SkipBuild)" -ForegroundColor DarkGray
    Write-Host "[3/5] Skipping build (SkipBuild)" -ForegroundColor DarkGray
}

# 3. 릴리즈 패키징
Write-Host "`n[4/5] Packaging release..." -ForegroundColor Yellow

if (Test-Path $ReleaseDir) { Remove-Item $ReleaseDir -Recurse -Force }
$stagingDir = Join-Path $ReleaseDir "AutoRegressionVM-v$Version"
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

# 필요한 파일만 복사
$filesToCopy = @(
    @{ Src = "AutoRegressionVM.exe"; Required = $true },
    @{ Src = "AutoRegressionVM.exe.config"; Required = $true },
    @{ Src = "Newtonsoft.Json.dll"; Required = $true }
)

foreach ($file in $filesToCopy) {
    $srcPath = Join-Path $ReleaseBinDir $file.Src
    if (Test-Path $srcPath) {
        Copy-Item $srcPath $stagingDir
        Write-Host "  + $($file.Src)" -ForegroundColor Gray
    } elseif ($file.Required) {
        Write-Error "Required file not found: $srcPath"
        exit 1
    }
}

# 프로젝트 루트에서 복사
Copy-Item (Join-Path $ProjectRoot "settings.example.json") $stagingDir
Copy-Item (Join-Path $ProjectRoot "README.md") $stagingDir
Write-Host "  + settings.example.json" -ForegroundColor Gray
Write-Host "  + README.md" -ForegroundColor Gray

# ZIP 생성
Compress-Archive -Path $stagingDir -DestinationPath $ZipPath -Force
$zipSize = [math]::Round((Get-Item $ZipPath).Length / 1KB, 1)
Write-Host "  Created: $ZipName ($zipSize KB)" -ForegroundColor Green

# 4. GitHub Release 생성
if (-not $SkipGitHubRelease) {
    Write-Host "`n[5/5] Creating GitHub Release..." -ForegroundColor Yellow

    $tagName = "v$Version"

    # 태그가 이미 있는지 확인
    $existingTag = git tag -l $tagName 2>$null
    if ($existingTag) {
        Write-Warning "Tag $tagName already exists. Skipping GitHub release."
    } else {
        git tag $tagName
        git push origin $tagName

        $releaseNotes = @"
## AutoRegressionVM v$Version

### 설치 방법
1. ``AutoRegressionVM-v$Version.zip`` 다운로드
2. 압축 해제
3. ``settings.example.json`` → ``settings.json``으로 복사 후 환경 설정
4. ``AutoRegressionVM.exe`` 실행

### 요구 사항
- Windows 10/11
- .NET Framework 4.7.2 이상
- VMware Workstation Pro
"@

        gh release create $tagName $ZipPath --title "v$Version" --notes $releaseNotes
        Write-Host "  GitHub Release v$Version created!" -ForegroundColor Green
    }
} else {
    Write-Host "`n[5/5] Skipping GitHub Release (SkipGitHubRelease)" -ForegroundColor DarkGray
}

Write-Host "`n=== Release v$Version Complete ===" -ForegroundColor Cyan
Write-Host "Artifact: $ZipPath"
