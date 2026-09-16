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

# Runtime stage (includes LibreOffice for Office-to-PDF, Ghostscript for PDF-to-PDF/A)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
# "Docker" (not "Production", which appsettings.Production.json owns for the self-contained
# local-execution release - a relative connection string meaningless inside a container) so
# appsettings.Docker.json's ConnectionStrings is what actually takes effect here. Worker uses the
# generic Host (no ASP.NET web host), which reads DOTNET_ENVIRONMENT, not ASPNETCORE_ENVIRONMENT.
ENV DOTNET_ENVIRONMENT=Docker

# libreoffice-nogui: headless office document conversion (OfficeToPdf module)
# ghostscript + icc-profiles-free: PDF/A conversion (PdfToPdfA module) - point the module's
# IccProfilePath setting at /usr/share/color/icc/sRGB.icc, a stable path independent of the
# Ghostscript version (unlike Ghostscript's own bundled profile under /usr/share/ghostscript/<version>/)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       libreoffice-nogui \
       fontconfig \
       fonts-dejavu-core \
       ghostscript \
       icc-profiles-free \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ColdNet.Worker.dll"]
