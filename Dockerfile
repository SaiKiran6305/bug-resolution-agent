FROM node:22-bookworm-slim AS web
WORKDIR /web
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/BugResolution.Api/ src/BugResolution.Api/
RUN dotnet publish src/BugResolution.Api -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
COPY --from=web /web/dist ./wwwroot
COPY samples/checkout ./samples/checkout
RUN mkdir -p /data /workspaces && chown -R $APP_UID:$APP_UID /data /workspaces
ENV ASPNETCORE_URLS=http://+:8080 Services__0__RepositoryPath=/app DataDirectory=/data WorkspaceDirectory=/workspaces
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "BugResolution.Api.dll"]
