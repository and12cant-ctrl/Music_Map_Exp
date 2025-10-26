@echo off
setlocal

REM この bat のディレクトリ（= Tools\micromamba）を基準に相対パスを解決
set MAMBA_DIR=%~dp0
set MAMBA_EXE=%MAMBA_DIR%micromamba.exe
set MAMBA_ROOT_PREFIX=%MAMBA_DIR%mamba_root
set ENV_YAML=%MAMBA_DIR%env.yaml
set ENV_NAME=unitypy

echo [bootstrap] micromamba: %MAMBA_EXE%
echo [bootstrap] root      : %MAMBA_ROOT_PREFIX%
echo [bootstrap] env yaml  : %ENV_YAML%

if not exist "%MAMBA_EXE%" (
  echo [bootstrap] ERROR: micromamba.exe が見つかりません
  exit /b 1
)
if not exist "%ENV_YAML%" (
  echo [bootstrap] ERROR: env.yaml が見つかりません
  exit /b 1
)

REM 環境作成/更新（-r で root を指定。-n で env 名を指定）
"%MAMBA_EXE%" create -y -r "%MAMBA_ROOT_PREFIX%" -n "%ENV_NAME%" -f "%ENV_YAML%"
if errorlevel 1 (
  echo [bootstrap] ERROR: 環境作成/更新に失敗しました
  exit /b 1
)

echo [bootstrap] DONE
exit /b 0
