# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/ColdNet.Core/ColdNet.Core.csproj", "src/ColdNet.Core/"]
COPY ["src/ColdNet.Data/ColdNet.Data.csproj", "src/ColdNet.Data/"]
COPY ["src/ColdNet.EdmVault/ColdNet.EdmVault.csproj", "src/ColdNet.EdmVault/"]
COPY ["src/ColdNet.Modules/ColdNet.Modules.csproj", "src/ColdNet.Modules/"]
COPY ["src/ColdNet.Engine/ColdNet.Engine.csproj", "src/ColdNet.Engine/"]
COPY ["src/ColdNet.Worker/ColdNet.Worker.csproj", "src/ColdNet.Worker/"]
RUN dotnet restore "src/ColdNet.Worker/ColdNet.Worker.csproj"

# Copy remaining source code and publish
COPY src/ src/
WORKDIR "/src/src/ColdNet.Worker"
RUN dotnet publish "ColdNet.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage (includes LibreOffice for headless Office-to-PDF conversion)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Install LibreOffice for headless office document conversion (OfficeToPdf module)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       libreoffice-nogui \
       fontconfig \
       fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ColdNet.Worker.dll"]
