# 後端：建置內容為整個 repo（需要前端的 space-map.json），所以 compose 的 context 是專案根目錄
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY backend/ backend/
RUN dotnet publish backend/SpatialTwinGuard.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out .
COPY frontend/src/data/space-map.json ./space-map.json
ENV SpaceMapPath=/app/space-map.json
ENTRYPOINT ["dotnet", "SpatialTwinGuard.dll"]
