@echo off
rem Associates .qsys files with QDSVersionLauncher.exe (current user only,
rem no administrator rights needed). Run this once after placing the EXE
rem wherever you want it to live permanently.
"%~dp0QDSVersionLauncher.exe" --register-association
