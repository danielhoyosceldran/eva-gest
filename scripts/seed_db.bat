@echo off
rem Runs seed_db.py from any working directory (paths are relative to this file).
rem Optional: pass --db-path "C:\path\to\barberia.db" to seed another database.
python "%~dp0seed_db.py" %*
if errorlevel 1 (
    echo.
    echo Seeding failed. Is Python installed and on PATH?
)
pause
