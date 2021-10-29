@echo off

SET R_HOME=../../../R-sharp/App/net5.0

"%R_HOME%/Rscript.exe" --build /save ./Rnotebook.zip --skip-src-build
"%R_HOME%/R#.exe" --install.packages ./Rnotebook.zip

pause