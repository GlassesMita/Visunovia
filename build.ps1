param([switch]$EditorOnly, [switch]$PlayerOnly)
$ErrorActionPreference = "Stop"
$SolutionDir = Split-Path -Parent $PSScriptRoot
$EditorPath = Join-Path $SolutionDir "Visunovia"
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

    $nodeModules = Join-Path $PlayerNWPath "node_modules"
    if (-not (Test-Path $nodeModules)) {
        Write-Host "正在安装 npm 依赖..." -ForegroundColor Yellow
        Push-Location $PlayerNWPath
        npm install
        if ($LASTEXITCODE -ne 0) {
            Write-Host "npm 依赖安装失败" -ForegroundColor Red
            Pop-Location
            return $false
        }
        Pop-Location
    }

    if (-not (Get-Command nw -ErrorAction SilentlyContinue)) {
        Write-Host "警告: nwjs-builder 未安装或未配置 PATH" -ForegroundColor Yellow
        Write-Host "尝试使用 npx 调用..." -ForegroundColor Yellow
        $nwbuildCmd = "npx nwbuild"
    } else {
        $nwbuildCmd = "nwbuild"
    }

    $tempBuildDir = Join-Path $SolutionDir "temp_nw_build"
    $webSource = Join-Path $PlayerNWPath "Web"
    $vfsSource = Join-Path $PlayerNWPath "VFS"
    $mainJs = Join-Path $PlayerNWPath "node-main.js"
    $preloadJs = Join-Path $PlayerNWPath "preload.js"
    $packageJson = Join-Path $PlayerNWPath "package.json"

    if (Test-Path $tempBuildDir) {
        Remove-Item -Recurse -Force $tempBuildDir
    }
    New-Item -ItemType Directory -Path $tempBuildDir | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $tempBuildDir "Web") | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $tempBuildDir "VFS") | Out-Null

    Copy-Item $webSource -Destination (Join-Path $tempBuildDir "Web") -Recurse
    Copy-Item $vfsSource -Destination (Join-Path $tempBuildDir "VFS") -Recurse
    Copy-Item $mainJs -Destination $tempBuildDir
    Copy-Item $preloadJs -Destination $tempBuildDir
    Copy-Item $packageJson -Destination $tempBuildDir

    $tempPackageJson = Join-Path $tempBuildDir "package.json"
    $tempPackage = Get-Content $tempPackageJson | ConvertFrom-Json
    $tempPackage.main = "node-main.js"
    $tempPackage | ConvertTo-Json -Depth 10 | Set-Content $tempPackageJson

    Write-Host "正在打包 NW.js 应用..." -ForegroundColor Yellow
    Push-Location $tempBuildDir
    try {
        $outputDirArg = "-o `"$PlayerOutput`""
        $nwArgs = "-p win64 $outputDirArg ."

        if ($nwbuildCmd -eq "npx nwbuild") {
            $nwArgs = "nwbuild -p win64 -o `"$PlayerOutput`" ."
        }

        Write-Host "执行: $nwbuildCmd $nwArgs" -ForegroundColor Gray
        Invoke-Expression "$nwbuildCmd $nwArgs"

        if ($LASTEXITCODE -ne 0) {
            Write-Host "NW.js 打包失败" -ForegroundColor Red
            Pop-Location
            return $false
        }
    } catch {
        Write-Host "打包过程中出错: $_" -ForegroundColor Red
        Pop-Location
        return $false
    }
    Pop-Location

    Remove-Item -Recurse -Force $tempBuildDir

    Write-Host "播放器编译成功: $PlayerOutput" -ForegroundColor Green
    return $true
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
