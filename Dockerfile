# syntax=docker/dockerfile:1

FROM node:20-bookworm-slim AS frontend-build
WORKDIR /src/GrowDiary.React

COPY GrowDiary.React/package*.json ./
RUN npm ci

COPY GrowDiary.React/ ./
RUN mkdir -p ../GrowDiary.Web/wwwroot && npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
WORKDIR /src

COPY GrowDiary.Web/GrowDiary.Web.csproj GrowDiary.Web/
RUN dotnet restore GrowDiary.Web/GrowDiary.Web.csproj

COPY GrowDiary.Web/ GrowDiary.Web/
COPY --from=frontend-build /src/GrowDiary.Web/wwwroot/ GrowDiary.Web/wwwroot/
RUN dotnet publish GrowDiary.Web/GrowDiary.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5076
ENV GROWDIARY_DB_PATH=/data/grow-diary.db

EXPOSE 5076
VOLUME ["/data"]

COPY --from=publish /app/publish ./
ENTRYPOINT ["dotnet", "GrowDiary.Web.dll"]
