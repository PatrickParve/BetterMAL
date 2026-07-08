FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AnimeTracker.Api/AnimeTracker.Api.csproj AnimeTracker.Api/
RUN dotnet restore AnimeTracker.Api/AnimeTracker.Api.csproj
COPY AnimeTracker.Api/ AnimeTracker.Api/
RUN dotnet publish AnimeTracker.Api/AnimeTracker.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 tzdata \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENTRYPOINT ["dotnet", "AnimeTracker.Api.dll"]
