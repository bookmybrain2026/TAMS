# Build the ASP.NET Core application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TAMS.csproj ./
RUN dotnet restore TAMS.csproj

COPY . .
RUN dotnet publish TAMS.csproj -c Release -o /app/publish --no-restore

# Run the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "TAMS.dll"]
