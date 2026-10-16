# spatial-twin-guard 實作進度（極簡 MVP）

依據：[spatial-twin-guard 可行性評估報告.md](spatial-twin-guard%20可行性評估報告.md)，再依 Gemini 審查建議瘦身成極簡 MVP · 2026-10-15

> 目標：用 5 個 Stage 做出完整、可以展示的畫面。做完就勾選；每過一個 Stage，在總覽表填實際人日與狀態（未開始、進行中、過關、降級過關）。
>
> 原本的完整規劃（B0a、B0b、V0–V6）保留在 [spatial-twin-guard 實作進度（完整版）.md](spatial-twin-guard%20實作進度（完整版）.md)，MVP 做完要擴充時再從那裡挑。

## 範圍鐵律

- **只有三個輕量服務：** `frontend`（Vue 3 + TypeScript + Vite）、`backend`（ASP.NET Core 10 Web API）、`nginx`。不建 `src/base` 母體，不用 PostgreSQL / pgvector，也不用 MSW。
- **用 System Prompt 取代資料庫查詢：** 重點樓層與空間（約 20–30 筆）寫成一份靜態 JSON 對照表，注入 LLM 的 System Prompt。
- **宏觀到微觀只做視窗切換：** Cesium 飛到目標大樓上空後，在右側展開 That Open 的獨立 BIM 窗格（雙視窗，地圖留在左邊；原案是 CSS 淡入疊畫面，2026-10-16 改）。不轉 `.glb`，不用 Blender 改模型，也不做座標錨定。
- **不寫自動化評測：** 用對話視窗手動驗證 5 個展示情境（見[文末](#5-個展示情境)）。

照這幾條，還可以再省下這些：

- **Stage 1–4 不用 Docker。** 平常用 `npm run dev` 和 `dotnet run` 開發，由 Vite 的 proxy 把 `/api` 轉給後端。Docker Compose 到 Stage 5 打包時才加。
- **後端不串流，也不用 SignalR。** `POST /api/chat` 一次回傳模型的回覆與工具呼叫，由前端依序執行。IoT 模擬數據在前端產生。
- **前端不需要 mock 框架。** Stage 1–3 用畫面上的除錯按鈕直接呼叫函式；Stage 4 改由 AI 呼叫同一組函式。

## 進度總覽

| Stage | 內容 | 估計（人日） | 限時 | 實際 | 狀態 |
| --- | --- | --- | --- | --- | --- |
| 1 | 前端雙畫面：Cesium 台灣底圖 ↔ That Open 載入 IFC | 1–2 | 2 | 1 | 過關（Institute 載入秒數待補） |
| 2 | 宏觀→微觀轉場：目標大樓高亮、飛行、開啟 BIM 窗格（已改雙視窗） | 0.5–1 | 1 | 約 1 | 過關（預設目標建物待寫死） |
| 3 | BIM 互動與對照表：剖切樓層、隔離空間、高亮 | 1.5–2.5 | 2.5 | | 未開始 |
| 4 | AI 指令：後端 LLM + System Prompt + 工具呼叫 | 1–2 | 2 | | 未開始 |
| 5 | IoT 告警模擬、5 個情境驗收、Docker Compose 打包 | 1 | 1.5 | | 未開始 |
| | **合計** | **5–8.5** | **9** | | |

- **Stage 1–3 是畫面部分，合計 3–5.5 人日，跟「3–5 天看到完整視覺成果」大致吻合。** 5 個 Stage 全部在 3–5 天內做完，前提是 Stage 1 的套件設定沒有卡關。
- **最大的未知數在 Stage 1：** Cesium 與 That Open 各自的建置設定。That Open 每次改版 API 都變動很大，網路上的舊教學常常對不上新版。
- **超過限時，就照該 Stage 的「不過關時」降一階，** 記入文末的「降級紀錄」，不回頭。

## 開工前準備（約半天，不計入工時）

- [ ] 安裝 Node.js LTS、.NET 10 SDK；Docker Desktop 到 Stage 5 才會用到
- [ ] 申請 Cesium ion 帳號，另建一支只開 `assets:read` 權限的 token（OSM Buildings 需要）
- [ ] 申請 LLM API 金鑰（Anthropic 或 Google），在主控台設每月用量上限；Stage 4 才會用到
- [ ] 下載 [`AC20-FZK-Haus.ifc`](https://www.ifcwiki.org/images/e/e3/AC20-FZK-Haus.ifc) 與 [`AC20-Institute-Var-2.ifc`](https://www.ifcwiki.org/images/9/98/AC20-Institute-Var-2.ifc)，放進 `frontend/public/models/`。來源是 KIT，可以自由使用，註明出處 IAI / KIT 即可

不需要 Blender、Bonsai 或 IfcOpenShell。

## 專案結構

```text
spatial-twin-guard/
├─ frontend/             Vue 3 + TypeScript + Vite
│  └─ public/models/     IFC 模型
├─ backend/              ASP.NET Core 10 Web API
│  └─ space-map.json     樓層與空間對照表（Stage 3 產出）
├─ nginx/nginx.conf      Stage 5
└─ docker-compose.yml    Stage 5
```

## Stage 1 前端雙畫面（1–2 人日，限時 2）

同一頁有兩個畫面，一個是 Cesium 台灣底圖，一個是 That Open 載入的 IFC 模型，用按鈕切換。

- [x] **1-1 建立專案：** `npm create vite@latest frontend -- --template vue-ts`
- [x] **1-2 Cesium 台灣底圖**
  - [x] 照 Cesium 官方的 Vite 寫法：用 `vite-plugin-static-copy` 複製 Workers、ThirdParty、Assets、Widgets，以 `define` 設定 `CESIUM_BASE_URL`，並引入 `widgets.css`
  - [x] Viewer 放在非響應式變數或 `shallowRef`。Viewer 被 Vue 的深層響應式包住會變得很慢，還可能報錯
  - [x] 國土測繪 WMTS 底圖（`WebMapTileServiceImageryProvider`）。圖層從 GetCapabilities 挑，例如臺灣通用電子地圖；畫面上要註明圖資來源
  - [x] 初始相機設在台灣上空
  - [x] ion token 放在 `frontend/.env` 的 `VITE_CESIUM_ION_TOKEN`，並把 `.env` 加進 `.gitignore`
- [x] **1-3 That Open 載入 IFC**
  - [x] 安裝 `@thatopen/components`、`@thatopen/components-front`、`@thatopen/fragments`、`web-ifc`、`three`，照官方範例的版本組合一起鎖定
  - [x] 設定 web-ifc 的 `.wasm` 路徑；鎖定的版本若需要 Fragments 的 worker 檔，一併設定路徑
  - [x] 先載入 FZK-Haus（2.6 MB），確認可以旋轉、縮放
  - [ ] 再試載一次 Institute（10.9 MB），記下載入秒數：＿＿
- [x] **1-4 切換與淡入**
  - [x] 兩個畫面疊在同一個容器裡，都保持掛載。切換時只改 CSS 的 `opacity` 與 `pointer-events`，轉場約 0.6–0.8 秒
  - [x] 不要用 `v-if` 或 `display: none`。用 `v-if` 的話，每次切換都會重建 Viewer、重新載入模型；用 `display: none` 會讓畫布尺寸變成 0，切回來時可能空白，或比例跑掉
  - [x] 被隱藏的那一邊要暫停重繪：Cesium 開 `scene.requestRenderMode`，That Open 停掉渲染迴圈。兩個 WebGL 畫面同時全速重繪，會拖慢效能

**過關檢查**

- [x] 首頁顯示國土測繪底圖的台灣
- [x] 按下切換鈕，淡入 IFC 模型，可以旋轉、縮放
- [x] 來回切換 10 次：模型不重新載入、沒有黑畫面，主控台也沒有錯誤

**不過關時：** That Open 卡超過 1 天，就照官方範例 repo 的版本組合重建，先求能載出 FZK-Haus。國土測繪底圖載不出來，就先改用 OSM 底圖，畫面照常往下做。

## Stage 2 宏觀→微觀轉場（0.5–1 人日，限時 1）

> **設計調整（2026-10-16）：** 原本是兩個畫面疊在一起、用淡入切換。改成**雙視窗**：地圖固定在左邊，BIM 窗格從右邊展開，使用者不必離開地圖。轉場函式的介面不變（仍是回傳 Promise 的 `flyToLocation`），Stage 4 的工具串接不受影響。

- [x] 在台灣選一棟 OSM 建物，當作研究所大樓的替身。模型本身在德國，也沒有地理參考，所以不做座標錨定。點選建物（`scene.pick`），記下它的經緯度與 `elementId`
  - 點選後存在 `localStorage`（`stg.target`）；目前選定的建物：`elementId=298062721`，經緯度 121.56508, 25.05013
  - [ ] 把這棟寫成程式裡的預設值（目前預設是台北 101 一帶的座標，沒有 `elementId`；全新 clone 或 Docker 使用者看不到高亮）
- [x] 載入 Cesium OSM Buildings（`createOsmBuildingsAsync`），用 `Cesium3DTileStyle` 讓目標大樓以醒目的顏色顯示（紅色）
- [x] `flyToLocation('institute')`：飛到大樓上空斜視（`flyToBoundingSphere`，3 秒），飛行完成時展開 BIM 窗格
- [x] `flyToLocation('taiwan')`：收合 BIM 窗格，相機拉回台灣全景（`flyTo`，2.5 秒）
- [x] 每個函式都回傳 Promise，等轉場完成才結束，Stage 4 才能把工具依序串起來
- [x] 加兩個除錯按鈕：「飛往大樓」、「回到台灣」

**雙視窗與地圖資訊（超出原計畫，使用者要求）**

- [x] 右緣箭頭按鈕開啟／收合 BIM 窗格；BIM 窗格右上角 ✕ 關閉
- [x] 兩個窗格中間有分隔條，可用滑鼠拖曳調整寬度（20–80%）。用原生 `pointerdown` / `setPointerCapture` 實作，沒有引入套件
- [x] 窗格尺寸改變時，BIM 的渲染器與相機比例（`ResizeObserver` → `renderer.resize()`、`camera.updateAspect()`）、Cesium 的重繪（`requestRender()`）會同步更新
- [x] 左側地圖下方：圖形比例尺（取畫面底部 100px 對應的地面距離，再取整成 1／2／5／10 × 10ⁿ）
- [x] 底部資訊列：圖資來源、比例尺分母（以 OGC 0.28 mm 像素換算）、滑鼠所在位置的 TWD97（EPSG:3826）坐標與經緯度，隨滑鼠移動更新。換算寫在 `frontend/src/utils/twd97.ts`，已用兩個已知點驗算（121°E, 23.7°N → 250000, 2621799.6）
  - [ ] 尚未與官方控制點比對精度
- [x] 啟用 Cesium World Terrain（`Terrain.fromWorldTerrain()`），目標標記點貼地（`CLAMP_TO_GROUND`）。比例尺與座標是用橢球面算的，不含地形高度，山區傾斜視角會有小誤差

**過關檢查**

- [x] 按下「飛往大樓」：飛到大樓上空後展開 BIM 窗格，全程 ≤ 5 秒，沒有黑畫面（使用者實測，2026-10-16）
- [x] 按下「回到台灣」，可以反向切回

**不過關時：** 目標大樓的高亮做不出來，就在大樓位置放一個標記點（Cesium Entity）代替。（實際沒有用到後備方案，但標記點仍一併保留）

## 專案設定與部署（Stage 2 期間補上）

- [x] **GitHub：** https://github.com/Rocky1987/spatial-agent-twin ，主分支 `main`
- [x] **CI：** `.github/workflows/ci.yml`。push 到 `main` 與 pull request 時，在 `frontend/` 跑 Node 22、`npm ci`、`npm run build`（含 Cesium 靜態檔複製與 `vue-tsc` 型別檢查）。第一次執行 28 秒通過。目前只涵蓋前端，後端到 Stage 4 再補
- [x] **Docker（提前到 Stage 2）：** `frontend/Dockerfile`（Node 22 打包、nginx 1.27 提供靜態檔）、`frontend/nginx.conf`（`.wasm` 為 `application/wasm`、SPA 回退）、`frontend/.dockerignore`、根目錄 `README.md`。clone 後 `docker build` 再 `docker run -p 8080:80` 即可，已在本機驗證。Docker Compose 仍留到後端出現時再做
  - Cesium ion token 以 `--build-arg VITE_CESIUM_ION_TOKEN=...` 傳入，會打包進前端 JS（瀏覽器端本來就看得到）；沒給 token 也能跑，只是沒有 OSM 3D 建物與地形
- [x] **Cesium ion token：** 另建專用 token，只開 `assets:read`、`assets:limited-list`，Resources 限定 OSM Buildings（96188）與 Cesium World Terrain（1），Allowed URLs 為 `http://localhost:5173`、`:5174`（Vite）、`:8080`（Docker）。放在 `frontend/.env`，已被 `.gitignore` 與 `.dockerignore` 排除
  - 踩到的坑：Regenerate token 後，`.env` 沒更新會一直 401（`INVALID_TOKEN`）；舊 dev server 佔著 5173 時，新的會跑在 5174，必須也在白名單內

## Stage 3 BIM 互動與對照表（1.5–2.5 人日，限時 2.5）

主模型換成 Institute。這裡做出來的函式，就是 Stage 4 交給 AI 呼叫的工具。

- [ ] **3-1 互動函式**（先用除錯按鈕呼叫）
  - [ ] `sectionStorey(storey)`：隱藏上方樓層，剖切面設在該層標高上方約 1.2 公尺（平面圖慣例）
  - [ ] `isolateSpaces(spaces[])`：只顯示這些空間所在的樓層，空間以半透明色塊標示，相機對準
  - [ ] `highlightSpaces(spaces[], color)`：Stage 5 的告警會用到
  - [ ] `resetView()`：全部顯示，並移除剖切
  - [ ] 點選構件時顯示名稱與類別（除錯用）
- [ ] **3-2 靜態對照表 `space-map.json`**
  - [ ] 收錄 5 個樓層，加上 20–30 個重點空間
  - [ ] 每一筆的欄位：
    - `key`：短代號，例如 `KG-Technikraum-I`
    - `globalId`
    - `name`：房號
    - `longName`：德文房名
    - `zh`：中文名稱與常見說法
    - `storey`：所屬樓層的 `key`
  - [ ] 樓層換算寫進表裡：Keller = 地下室、Erdgeschoss = 1 樓、1. Obergeschoss = 2 樓、2. Obergeschoss = 3 樓、Dachgeschoss = 4 樓（頂樓）
  - [ ] 產生方式：在 BIM 畫面讀出所有 `IfcBuildingStorey` 與 `IfcSpace` 的屬性，輸出成 JSON，再手動補上 `zh` 欄位
  - [ ] 建議收錄以下 26 間（依 2026-10-15 的模型清點結果）：
    - 地下室：技術機房 I–IV（Technikraum）、會議室 I–II（Besprechungsraum）、研討室（Seminarraum）、實驗室 K1–K5（Labor）
    - 地下室到 3 樓，每層各有男女廁（WC Herren、WC Damen），共 8 間
    - 1 樓：實驗室 I–II、管理員室（Buero Hausmeister）、學生辦公室 I–III（Buero Studenten）
  - [ ] 注意模型命名的陷阱：頂樓的樓梯間走道叫 `Flur 3.OG Treppe`，跟樓層名稱 Dachgeschoss 對不上。走道不收進對照表，就不會混淆

**過關檢查**

- [ ] 5 個樓層都能剖切，結果和模型的樓層標高一致
- [ ] 對照表裡每一筆的 `globalId`，都能用 `isolateSpaces` 找到並對準
- [ ] Institute 載入 ≤ 15 秒。超過的話，改成預先轉成 Fragments 檔（That Open 自己的格式）再載入

**不過關時：** 剖切面做不好，就只隱藏上方樓層，降級成「只顯示單一樓層」。空間色塊顯示不出來，就改成隔離該樓層，並在房間位置放文字標籤。

## Stage 4 AI 指令（1–2 人日，限時 2）

- [ ] **4-1 後端**
  - [ ] `dotnet new webapi -o backend`（.NET 10），只開一支 `POST /api/chat`
  - [ ] 請求內容是對話紀錄，加上目前的告警清單（Stage 5 才有）
  - [ ] 回應格式是 `{ reply, toolCalls: [{ name, args }] }`，不串流
  - [ ] LLM 金鑰用 `dotnet user-secrets` 或環境變數保管，不放進前端
  - [ ] 在前端的 `vite.config.ts` 設 `server.proxy`，把 `/api` 轉到本機後端，開發時不用處理 CORS
- [ ] **4-2 System Prompt 與工具定義**
  - [ ] System Prompt 包含三部分：角色與規則、`space-map.json` 全文、樓層換算說明（例如台灣的 2 樓是 1. Obergeschoss）
  - [ ] 工具對應 Stage 2、3 的前端函式：`flyToLocation`、`sectionStorey`、`isolateSpaces`、`showAlerts`、`resetView`
  - [ ] 工具參數用對照表的 `key`，並在工具的 JSON Schema 裡列成 `enum`。這樣模型只能從清單裡選，不會編出不存在的 ID，也不必照抄 22 字元的 `globalId`
  - [ ] 後端把 `key` 換成 `globalId`，再回傳給前端
  - [ ] 只做單輪：前端執行完工具後，不必把結果送回模型。一句複合指令，由模型一次回傳多個工具呼叫
- [ ] **4-3 前端對話窗**
  - [ ] 輸入框與訊息列表。每則回覆下方列出呼叫了哪些工具、帶了哪些參數，方便除錯
  - [ ] 依序執行 `toolCalls`，前一個完成才執行下一個（例如飛行完成才剖切）

**過關檢查**

- [ ] 情境 1、2、3、5 都能從對話窗觸發成功（情境 4 要等 Stage 5）
- [ ] 每個情境另外換 2–3 種說法，手動試過也都成功

**不過關時：** 模型常選錯，就先在對照表的 `zh` 欄位補同義詞。仍然不穩，就在後端加關鍵字比對：遇到固定句型，直接回傳預先寫好的工具呼叫，不經過 LLM。

## Stage 5 IoT 告警與打包（1 人日，限時 1.5）

- [ ] **5-1 IoT 模擬（前端）**
  - [ ] 約 10 個感測器掛在對照表的空間上，每 1–2 秒產生一次溫度
  - [ ] 溫度超過門檻，就用 `highlightSpaces` 標紅，並在畫面角落列出告警清單
  - [ ] 送出對話時附上目前的告警清單，模型才答得出「哪裡有告警」
- [ ] **5-2 Docker Compose**
  - [ ] `frontend`：用 multi-stage Dockerfile，以 Node 打包出靜態檔
  - [ ] `backend`：ASP.NET Core 執行映像，LLM 金鑰從環境變數或 `.env` 讀取
  - [ ] `nginx`：唯一對外入口。`/` 提供前端靜態檔（SPA 回退用 `try_files $uri $uri/ /index.html`），`/api/` 轉給 backend
  - [ ] 確認 `.wasm` 回應的 Content-Type 是 `application/wasm`
  - [ ] `docker compose up -d` 一次全部啟動，從 Nginx 的網址跑完 5 個情境
- [ ] **5-3 錄影：** 5 個情境各錄一段

前端的打包產物也可以直接複製進 nginx 映像。這樣實際只跑 nginx 與 backend 兩個容器，設定更少。

**過關檢查**

- [ ] 在 Compose 版本上，5 個展示情境全部成功
- [ ] 前端打包產物裡搜不到 LLM 金鑰（Cesium ion token 本來就放在前端，不在此限）

## 5 個展示情境

| # | 對話輸入（例） | 預期畫面 | 工具 |
| --- | --- | --- | --- |
| 1 | 帶我去研究所大樓 | 飛到大樓上空，淡入 BIM 畫面 | `flyToLocation` |
| 2 | 剖切到 2 樓 | 顯示 1. Obergeschoss 的剖切平面 | `sectionStorey` |
| 3 | 幫我隔離 3 樓的廁所 | 只顯示 2. Obergeschoss 的兩間廁所，相機對準 | `isolateSpaces` |
| 4 | 現在哪裡有告警？ | 告警的房間標紅，模型說出房間名稱 | `showAlerts` |
| 5 | 請帶我看技術機房，並幫我剖切到該樓層 | 一句話完成：飛到大樓、剖切地下室、隔離 4 間技術機房 | 3 個工具依序執行 |

情境 2、3 驗證樓層換算；情境 3、5 驗證中文對德文房名的對應。

## 降級紀錄

每降一階記一行，錄影與 README 說明時會用到。

| 日期 | Stage | 降級內容 | 原因 |
| --- | --- | --- | --- |
| 2026-10-16 | 2 | 「疊在一起淡入切換」改成雙視窗（地圖在左、BIM 窗格在右，可拖曳、可收合） | 使用者覺得切換到另一個畫面看 BIM 不方便，地圖應該留在原頁面；函式介面不變，不算降級 |

## MVP 之後（暫緩）

需要時再從[完整版清單](spatial-twin-guard%20實作進度（完整版）.md)挑：

- BIM 窗格開啟時，Cesium 與 That Open 是否都要全速重繪：目前兩邊都開著；若效能不佳，再考慮降低其中一邊的更新率
- 同畫面無縫穿透：IFC 放進 Cesium，並做座標錨定（建議先限時 1 天試轉 glTF 再決定）
- 換成台灣或公共工程題材的 BIM 模型：2026-10-16 查過，政府資料開放平臺、建研所、營建署與 GitHub 都沒有可下載的台灣 IFC；MVP 沿用 KIT 模型。之後可自己用 Bonsai 建一棟，或向事務所／機關詢問；公共工程題材則需改 Stage 3 的樓層函式與對照表
- SignalR 即時推播，IoT 模擬改由後端產生
- PostgreSQL + pgvector，構件索引進資料庫
- 30 題自動化評測
- 共用母體抽象（`src/base`、範本 repo）
