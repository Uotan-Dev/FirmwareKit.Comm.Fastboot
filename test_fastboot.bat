@echo off
setlocal enabledelayedexpansion

:: 设置可执行文件路径
set FASTBOOT_EXE=.\FastbootCLI\bin\Release\net10.0\win-x86\publish\fastboot.exe

:: 检查文件是否存在
if not exist "%FASTBOOT_EXE%" (
    echo [ERROR] Fastboot executable not found at %FASTBOOT_EXE%
    echo Please run 'dotnet publish FastbootCLI\FastbootCLI.csproj -c Release -r win-x86' first.
    exit /b 1
)

echo ============================================================
echo   Fastboot CLI Read-Only Command Test Script
echo ============================================================
echo.

:: 1. 测试 devices 命令
echo [TEST] Testing 'devices' command...
"%FASTBOOT_EXE%" devices
if %ERRORLEVEL% neq 0 (
    echo [FAIL] 'devices' command failed.
    exit /b 1
)
echo [PASS] 'devices' command executed.
echo.

:: 获取第一个设备的序列号（可选测试）
:: 这里我们假设后续命令会自动选择唯一连接的设备

:: 2. 测试 getvar all
echo [TEST] Testing 'getvar all'...
"%FASTBOOT_EXE%" getvar all --debug
if %ERRORLEVEL% neq 0 (
    echo [FAIL] 'getvar all' command failed.
    exit /b 1
)
echo [PASS] 'getvar all' command executed.
echo.

:: 3. 测试 getvar 特定变量 (如 product, version)
echo [TEST] Testing 'getvar product'...
"%FASTBOOT_EXE%" getvar product
if %ERRORLEVEL% neq 0 (
    echo [FAIL] 'getvar product' command failed.
) else (
    echo [PASS] 'getvar product' command executed.
)
echo.

echo [TEST] Testing 'getvar version-bootloader'...
"%FASTBOOT_EXE%" getvar version-bootloader
if %ERRORLEVEL% neq 0 (
    echo [FAIL] 'getvar version-bootloader' command failed.
) else (
    echo [PASS] 'getvar version-bootloader' command executed.
)
echo.

echo [TEST] Testing 'getvar has-slot:boot'...
"%FASTBOOT_EXE%" getvar has-slot:boot
if %ERRORLEVEL% neq 0 (
    echo [FAIL] 'getvar has-slot:boot' command failed.
) else (
    echo [PASS] 'getvar has-slot:boot' command executed.
)
echo.

:: 4. 测试未实现或无效命令的优雅报错
echo [TEST] Testing invalid command handling...
"%FASTBOOT_EXE%" invalid_command_test
if %ERRORLEVEL% equ 0 (
    echo [FAIL] Tool should have failed for invalid command.
) else (
    echo [PASS] Tool correctly reported error for invalid command.
)
echo.

:: 5. 最后执行重启命令 (非写入性但具有破坏性的流程性指令)
echo [TEST] Finalizing with 'reboot bootloader'...
echo [INFO] Device will reboot now if connected.
"%FASTBOOT_EXE%" reboot bootloader --debug
if %ERRORLEVEL% neq 0 (
    echo [FAIL] 'reboot bootloader' command failed.
    exit /b 1
)
echo [PASS] 'reboot bootloader' command sent successfully.
echo.

echo ============================================================
echo   All read-only tests completed successfully.
echo ============================================================
pause
