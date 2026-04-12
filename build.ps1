param([switch]$EditorOnly, [switch]$PlayerOnly)
$ErrorActionPreference = "Stop"
$SolutionDir = $PSScriptRoot
$EditorPath = $SolutionDir
$PlayerNWPath = Join-Path $SolutionDir "Visunovia.Player.NW"
$PlayerOutput = Join-Path $SolutionDir "Editor\Player"
$Arch = "win-x64"

function Write-Step($message) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host $message -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Build-Editor {
    Write-Step "编译 Visunovia 编辑器"
    $editorProject = Join-Path $EditorPath "Visunovia.csproj"
    if (-not (Test-Path $editorProject)) {
        Write-Host "编辑器项目未找到: $editorProject" -ForegroundColor Red
        return $false
    }
    dotnet publish $editorProject -c Release -r $Arch --self-contained true -p:PublishSingleFile=true -p:OutputPath="$EditorPath"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "编辑器编译失败" -ForegroundColor Red
        return $false
    }
    Write-Host "编辑器编译成功: $EditorPath\Visunovia.exe" -ForegroundColor Green
    return $true
}

function Build-NWPlayer {
    Write-Step "编译 Visunovia NW.js 播放器"
    if (-not (Test-Path $PlayerNWPath)) {
        Write-Host "NW.js 播放器项目未找到: $PlayerNWPath" -ForegroundColor Red
        return $false
    }

    Write-Host "正在安装 npm 依赖..." -ForegroundColor Yellow
    Push-Location $PlayerNWPath
    try {
        npm install
        if ($LASTEXITCODE -ne 0) {
            Write-Host "npm 依赖安装失败" -ForegroundColor Red
            Pop-Location
            return $false
        }
    } catch {
        Write-Host "npm 安装出错: $_" -ForegroundColor Red
        Pop-Location
        return $false
    }
    Pop-Location

    Write-Host "正在使用 nw-builder 打包 NW.js 应用..." -ForegroundColor Yellow
    $env:PLATFORM = "win64"
    $env:OUTPUT_DIR = $PlayerOutput
    
    try {
        Push-Location $PlayerNWPath
        node build.js
        if ($LASTEXITCODE -ne 0) {
            Write-Host "NW.js 打包失败" -ForegroundColor Red
            Pop-Location
            return $false
        }
        Pop-Location
    } catch {
        Write-Host "打包过程中出错: $_" -ForegroundColor Red
        Pop-Location
        return $false
    }

    if (Test-Path (Join-Path $PlayerOutput "nw.exe")) {
        Write-Host "播放器编译成功: $PlayerOutput" -ForegroundColor Green
        return $true
    } elseif (Test-Path (Join-Path $PlayerOutput "Visunovia.Player.NW.exe")) {
        Write-Host "播放器编译成功: $PlayerOutput" -ForegroundColor Green
        return $true
    } else {
        Write-Host "警告: 未在输出目录找到 nw.exe 或 *.exe" -ForegroundColor Yellow
        Write-Host "输出目录: $PlayerOutput" -ForegroundColor Yellow
        Get-ChildItem $PlayerOutput -Recurse -File | Select-Object -First 10 FullName
        return $true
    }
}

function Main {
    Write-Host ""
    Write-Host "Visunovia 编译脚本" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""

    if ($PlayerOnly) {
        $result = Build-NWPlayer
    } elseif ($EditorOnly) {
        $result = Build-Editor
    } else {
        $editorResult = Build-Editor
        if (-not $editorResult) { exit 1 }
        $playerResult = Build-NWPlayer
        if (-not $playerResult) { exit 1 }
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "编译完成!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
}

Main
