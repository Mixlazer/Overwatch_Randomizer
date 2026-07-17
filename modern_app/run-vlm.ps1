[CmdletBinding()]
param(
    [string]$Server = (Join-Path $PSScriptRoot 'llama-server.exe'),
    [string]$Model = (Join-Path $PSScriptRoot 'models\Qwen3.5-0.8B-Q4_K_M.gguf'),
    [string]$Projector = (Join-Path $PSScriptRoot 'models\mmproj-F32.gguf')
)

$ErrorActionPreference = 'Stop'
foreach ($file in @($Server, $Model, $Projector)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Не найден файл: $file" }
}

& $Server --model $Model --mmproj $Projector --host 127.0.0.1 --port 8080 `
    --ctx-size 4096 --jinja --reasoning off
