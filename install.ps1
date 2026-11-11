# Compila My Quick Accent, lo instala en %LOCALAPPDATA%\Programs\MyQuickAccent y lo arranca.
# Para que inicie con Windows: clic derecho en el ícono de la bandeja > "Iniciar con Windows".
$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\MyQuickAccent'

Get-Process MyQuickAccent -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

dotnet publish "$PSScriptRoot\MyQuickAccent.csproj" -c Release -o $dest -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }

Start-Process (Join-Path $dest 'MyQuickAccent.exe')
Write-Host "Instalado en $dest y corriendo (busque el ícono 'á' en la bandeja del sistema)."
