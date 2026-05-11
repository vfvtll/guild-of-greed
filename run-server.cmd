@echo off
title GuildOfGreed Server
echo === GuildOfGreed Server ===
echo Press Ctrl+C to stop.
echo.
dotnet run --project server
echo.
echo Server exited. Press any key to close.
pause >nul
