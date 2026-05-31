# Vite 开发服务器启动脚本（隐藏窗口模式）
# 该脚本用于在后台静默启动 Vite 开发服务器，避免显示终端窗口

$ErrorActionPreference = 'Stop'

# 获取脚本所在目录
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

# 设置 Node.js 环境变量（解决 431 错误）
$env:NODE_OPTIONS = "--max-http-header-size=32768"

# 启动 Vite 开发服务器
# 使用 Start-Process 的 -WindowStyle Hidden 参数隐藏窗口
$viteProcess = Start-Process -FilePath "npx" `
    -ArgumentList "vite", "--port", "32423", "--strictPort" `
    -WindowStyle Hidden `
    -PassThru `
    -WorkingDirectory $scriptDir

# 输出进程 ID，便于后续管理
Write-Output $viteProcess.Id
