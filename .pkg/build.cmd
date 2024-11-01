@echo off

SET drive=%~d0
SET R_HOME=%drive%/GCModeller\src\R-sharp\App\net8.0

%R_HOME%/Rscript.exe --build /src ../ /save ./languageserver.zip --skip-src-build 
%R_HOME%/R#.exe --install.packages ./languageserver.zip

pause