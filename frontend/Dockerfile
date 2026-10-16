# 前端：建置靜態檔，再由 nginx 提供，並把 /api 轉給後端
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
# Cesium ion token 屬於前端公開設定（會進 bundle）；LLM 金鑰只在後端容器
ARG VITE_CESIUM_ION_TOKEN=""
ENV VITE_CESIUM_ION_TOKEN=$VITE_CESIUM_ION_TOKEN
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
