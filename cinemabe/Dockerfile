# Multi-stage build for the Cinema Web API.
# Build context must be the cinemabe/ directory: `docker build -t cinema-api ./cinemabe`
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Cinema.sln
RUN dotnet publish Cinema/1-Service/Cinema.Service.WebApiHost -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
# JWT:Secret and ConnectionStrings:CinemaDatabase MUST be provided at runtime
# (env vars JWT__Secret / ConnectionStrings__CinemaDatabase) — the app refuses
# to start with an empty or default signing key.
ENTRYPOINT ["dotnet", "Cinema.Service.WebApiHost.dll"]
