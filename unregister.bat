@echo off
rem Removes QDSVersionLauncher as the handler for .qsys files and restores
rem whatever was associated with .qsys before (if anything).
"%~dp0QDSVersionLauncher.exe" --unregister-association
