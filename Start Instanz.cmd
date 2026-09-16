@echo off
echo Starte ColdNet (Backend + Frontend)...
start dotnet run --project src/ColdNet.Admin/ColdNet.Admin.csproj    # http://localhost:5202
start dotnet run --project src/ColdNet.Worker/ColdNet.Worker.csproj  # background processing loop
pause