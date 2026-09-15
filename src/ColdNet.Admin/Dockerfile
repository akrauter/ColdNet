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
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ColdNet.Admin.dll"]
