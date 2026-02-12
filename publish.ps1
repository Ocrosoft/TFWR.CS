<#
.SYNOPSIS
    发布 TFWR.CS 项目：构建转译器和 REF dll，打包为 zip。
.DESCRIPTION
    1. 以 Release 模式发布 TFWR.CS（转译器）和 TFWR.CS.REF 项目
    2. 组装输出目录结构：
       - TFWR.CS/     转译器运行文件
       - cs/   示例 C# 源码 + REF dll/xml + sln
    3. 压缩为 SampleProject.zip
#>

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$refProjectDir = Join-Path $repoRoot "TFWR.CS.REF"
$transpilerProjectDir = Join-Path $repoRoot "TFWR.CS"
$sampleDir = Join-Path $repoRoot "SampleProject"
$sampleCsDir = Join-Path $sampleDir "cs"
$outputZip = Join-Path $repoRoot "SampleProject.zip"

# ── 1. 发布 TFWR.CS.REF ──
Write-Host ">>> 发布 TFWR.CS.REF ($Configuration)..." -ForegroundColor Cyan
dotnet publish "$refProjectDir\TFWR.CS.REF.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "TFWR.CS.REF 发布失败"
    exit 1
}

$refPublishDir = Join-Path $refProjectDir "bin\$Configuration\net9.0\publish"
$dllPath = Join-Path $refPublishDir "TFWR.CS.REF.dll"
$xmlPath = Join-Path $refPublishDir "TFWR.CS.REF.xml"

if (-not (Test-Path $dllPath)) {
    Write-Error "找不到发布产物: $dllPath"
    exit 1
}

# ── 2. 发布 TFWR.CS（转译器）──
Write-Host ">>> 发布 TFWR.CS 转译器 ($Configuration)..." -ForegroundColor Cyan
dotnet publish "$transpilerProjectDir\TFWR.CS.csproj" -c $Configuration -p:SatelliteResourceLanguages=en
if ($LASTEXITCODE -ne 0) {
    Write-Error "TFWR.CS 转译器发布失败"
    exit 1
}

$transpilerPublishDir = Join-Path $transpilerProjectDir "bin\$Configuration\net9.0\publish"

if (-not (Test-Path (Join-Path $transpilerPublishDir "TFWR.CS.exe"))) {
    Write-Error "找不到转译器: $transpilerPublishDir\TFWR.CS.exe"
    exit 1
}

# ── 3. 拷贝 REF dll/xml 到 SampleProject/cs ──
Write-Host ">>> 拷贝 TFWR.CS.REF dll/xml 到 cs/..." -ForegroundColor Cyan
Copy-Item $dllPath -Destination $sampleCsDir -Force
if (Test-Path $xmlPath) {
    Copy-Item $xmlPath -Destination $sampleCsDir -Force
}

Write-Host "  $dllPath -> $sampleCsDir" -ForegroundColor DarkGray
Write-Host "  $xmlPath -> $sampleCsDir" -ForegroundColor DarkGray

# ── 4. 拷贝转译器到 TFWR.CS/ ──
Write-Host ">>> 拷贝转译器到 TFWR.CS/..." -ForegroundColor Cyan
$transpilerDestDir = Join-Path $sampleDir "TFWR.CS"

if (Test-Path $transpilerDestDir) {
    Remove-Item $transpilerDestDir -Recurse -Force
}
New-Item -ItemType Directory -Path $transpilerDestDir | Out-Null

# 只拷贝运行所需的文件（跳过 pdb、本地化资源目录）
Get-ChildItem $transpilerPublishDir -File | Where-Object {
    $_.Extension -notin @('.pdb')
} | ForEach-Object {
    Copy-Item $_.FullName -Destination $transpilerDestDir -Force
    Write-Host "  $($_.Name)" -ForegroundColor DarkGray
}

# ── 5. 在 cs/ 下生成 .sln 文件 ──
Write-Host ">>> 生成 SampleProject.sln..." -ForegroundColor Cyan

$slnPath = Join-Path $sampleCsDir "SampleProject.sln"
if (Test-Path $slnPath) {
    Remove-Item $slnPath -Force
}

Push-Location $sampleCsDir
dotnet new sln --name SampleProject
dotnet sln add SampleProject.csproj
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Error "生成 sln 失败"
    exit 1
}

# ── 6. 清理 SampleProject 中的 bin/obj ──
Write-Host ">>> 清理构建产物..." -ForegroundColor Cyan
foreach ($dir in @("bin", "obj")) {
    $target = Join-Path $sampleCsDir $dir
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
}

# ── 7. 压缩为 zip ──
Write-Host ">>> 打包 SampleProject.zip..." -ForegroundColor Cyan
if (Test-Path $outputZip) {
    Remove-Item $outputZip -Force
}
Compress-Archive -Path "$sampleDir\*" -DestinationPath $outputZip -Force

Write-Host ""
Write-Host ">>> 完成！输出: $outputZip" -ForegroundColor Green
Write-Host "    包含文件:" -ForegroundColor Green

$zipEntries = [System.IO.Compression.ZipFile]::OpenRead($outputZip).Entries | Select-Object -ExpandProperty FullName
$zipEntries | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
