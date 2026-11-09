# Build stage: restore dependencies and publish the API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so Docker can cache the restore layer.
COPY ["src/AnalysisHistory.Api/AnalysisHistory.Api.csproj", "src/AnalysisHistory.Api/"]
COPY ["src/Shared.Contracts/Shared.Contracts.csproj", "src/Shared.Contracts/"]
RUN dotnet restore "src/AnalysisHistory.Api/AnalysisHistory.Api.csproj"

# Copy the remaining source code and publish in Release mode.
COPY . .
WORKDIR "/src/src/AnalysisHistory.Api"
RUN dotnet publish "AnalysisHistory.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage: use the smaller ASP.NET runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AnalysisHistory.Api.dll"]
