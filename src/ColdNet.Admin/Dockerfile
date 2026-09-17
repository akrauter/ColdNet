# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/ColdNet.Core/ColdNet.Core.csproj", "src/ColdNet.Core/"]
COPY ["src/ColdNet.Data/ColdNet.Data.csproj", "src/ColdNet.Data/"]
COPY ["src/ColdNet.EdmVault/ColdNet.EdmVault.csproj", "src/ColdNet.EdmVault/"]
COPY ["src/ColdNet.Modules/ColdNet.Modules.csproj", "src/ColdNet.Modules/"]
COPY ["src/ColdNet.Engine/ColdNet.Engine.csproj", "src/ColdNet.Engine/"]
COPY ["src/ColdNet.Admin/ColdNet.Admin.csproj", "src/ColdNet.Admin/"]
RUN dotnet restore "src/ColdNet.Admin/ColdNet.Admin.csproj"

# Copy remaining source code and publish
COPY src/ src/
WORKDIR "/src/src/ColdNet.Admin"
RUN dotnet publish "ColdNet.Admin.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
# "Docker" (not "Production", which appsettings.Production.json owns for the self-contained
# local-execution release - binds to localhost:5202, meaningless inside a container) so
# appsettings.Docker.json's Urls/ConnectionStrings are what actually take effect here.
ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://+:8080

# Admin can also trigger a scheduling pass itself (the "Run now" button), so it needs the same
# module dependencies as the Worker image: fonts-dejavu-core (TextToPdf), ghostscript +
# icc-profiles-free (PdfToPdfA - point IccProfilePath at /usr/share/color/icc/sRGB.icc),
# tesseract-ocr + language packs (ExtractText's image OCR path - "tesseract" is already on PATH
# once installed, matching the module's default TesseractPath).
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       fontconfig \
       fonts-dejavu-core \
       ghostscript \
       icc-profiles-free \
       tesseract-ocr \
       tesseract-ocr-deu \
       tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ColdNet.Admin.dll"]
