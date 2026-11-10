
# Stage 1: Build TAMS
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project file
COPY ["TAMS/TAMS.csproj", "TAMS/"]

# Restore dependencies
RUN dotnet restore "TAMS/TAMS.csproj"

# Copy remaining files
COPY . .

# Publish application
WORKDIR /src/TAMS
RUN dotnet publish "TAMS.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Run TAMS
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

# Configure Render port
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "TAMS.dll"]
