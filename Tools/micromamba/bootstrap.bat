@echo off
setlocal

set MAMBA_DIR=%~dp0
set MAMBA_EXE=%MAMBA_DIR%micromamba.exe
set MAMBA_ROOT_PREFIX=%MAMBA_DIR%mamba_root
set ENV_YAML=%MAMBA_DIR%env.yaml
set ENV_NAME=unitypy

echo [bootstrap] micromamba: %MAMBA_EXE%
echo [bootstrap] root      : %MAMBA_ROOT_PREFIX%
echo [bootstrap] env yaml  : %ENV_YAML%

if not exist "%MAMBA_EXE%" (
  echo [bootstrap] ERROR: micromamba.exe can not found
  exit /b 1
)
if not exist "%ENV_YAML%" (
  echo [bootstrap] ERROR: env.yaml can not found
  exit /b 1
)

"%MAMBA_EXE%" create -y -r "%MAMBA_ROOT_PREFIX%" -n "%ENV_NAME%" -f "%ENV_YAML%"
if errorlevel 1 (
  echo [bootstrap] ERROR: Kankyou Sakusei / kousin / failed
  exit /b 1
)

echo [bootstrap] DONE
exit /b 0
