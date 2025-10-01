# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["src/learning-tool.csproj", "./"]
RUN dotnet restore "./learning-tool.csproj"

# Copy everything else and build
COPY src/ ./
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "learning-tool.dll"]