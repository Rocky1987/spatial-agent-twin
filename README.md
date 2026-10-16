# spatial-agent-twin

AI 空間智慧數位雙生戰情室（spatial-twin-guard）：左側 Cesium 城市地圖，右側 BIM 建築內部。

## 用 Docker 啟動（不需安裝 Node）

```bash
# 可選：有 Cesium ion token 才會顯示 OSM 3D 建物
docker build -t spatial-twin-frontend \
  --build-arg VITE_CESIUM_ION_TOKEN=<你的token> frontend
docker run --rm -p 8080:80 spatial-twin-frontend
```

開啟 http://localhost:8080 。沒給 token 也能跑，只是看不到 3D 建物。

## 本機開發

```bash
cd frontend
cp .env.example .env   # 填入 VITE_CESIUM_ION_TOKEN
npm ci
npm run dev
```
