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
# The hand-edited id mapping (backend/custom/README.md), read from /app/custom. It is copied in rather than
# bind-mounted: a host folder needs Docker Desktop to be allowed into it, macOS refuses that for ~/Documents until
# it is granted, and the backend then fails to start at all. So an edit needs `docker compose up -d --build`.
COPY custom/ /app/custom/
USER $APP_UID
ENTRYPOINT ["dotnet", "AnimeTracker.Api.dll"]
