@echo off
setlocal
cd /d "%~dp0"
echo Starting Nodal Workflow Lab...
dotnet run --project NodalWorkflowLab.csproj
if errorlevel 1 (
  echo.
  echo Start failed. Ensure the .NET 10 SDK is installed.
  pause
)
