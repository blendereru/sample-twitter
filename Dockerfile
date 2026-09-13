# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching on restore
COPY sample-twitter.slnx ./
COPY SampleTwitter.API/SampleTwitter.API.csproj SampleTwitter.API/
RUN dotnet restore SampleTwitter.API/SampleTwitter.API.csproj

# Copy source and publish
COPY SampleTwitter.API/ SampleTwitter.API/
RUN dotnet publish SampleTwitter.API/SampleTwitter.API.csproj \
    -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER $APP_UID

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "SampleTwitter.API.dll"]