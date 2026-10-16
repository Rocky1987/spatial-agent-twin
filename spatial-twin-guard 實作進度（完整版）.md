# spatial-twin-guard 實作進度（完整版）

依據：[spatial-twin-guard 可行性評估報告.md](spatial-twin-guard%20可行性評估報告.md)（第 2 版）· 建立於 2026-10-15

> **已暫緩：** 實作先改走極簡 MVP，見 [spatial-twin-guard 實作進度.md](spatial-twin-guard%20實作進度.md)。本清單留作 MVP 完成後擴充時的參考。

> 用法：做完就勾選。每過一關，回到「進度總覽」填實際人日與狀態。狀態分四種：未開始、進行中、過關、降級過關。

## 進度總覽

**前端先行。** 先搭最小的架構骨架，接著前端全部用 mock 資料做完；真的需要後端資料時，才接上後端 API 與 AI 模型。

| 階段 | 順序 | 關卡 | 前置關卡 | 估計（人日） | 限時 | 實際 | 狀態 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A 骨架 | 1 | V0 資料與環境 | — | 2–3 | 3 | | 未開始 |
| | 2 | B0a 母體前端骨架 | — | 3.5–6 | 6 | | 未開始 |
| B 前端（mock） | 3 | V1 宏觀尖兵 | B0a、V0 | 3–4 | 4 | | 未開始 |
| | 4 | V2 微觀尖兵 | B0a、V0 | 6.5–9 | 9 | | 未開始 |
| | 5 | V4 整合 | V1、V2 | 5–6 | 6 | | 未開始 |
| | 6 | V5a 前端聯動 | V4 | 4–6 | 6 | | 未開始 |
| C 後端與 AI | 7 | B0b 母體後端核心 | B0a | 6–9 | 9 | | 未開始 |
| | 8 | V3 AI 尖兵 | B0b、V5a | 4–6.5 | 6.5 | | 未開始 |
| | 9 | V5b 真實聯動 | V3 | 1–1.5 | 1.5 | | 未開始 |
| 收尾 | 10 | V6 部署與展示 | V5b（或 V5a） | 2–3 | 3 | | 未開始 |
| — | — | 效能調校緩衝 | 按需動用 | 3–5 | 5 | | — |
| | | **合計（不含 V0）** | | **38–56** | | | |

- **階段 B 不需要後端。** 資料全部用 mock，本機不必裝 PostgreSQL。到了階段 C，PostgreSQL + pgvector 也是用 Docker 容器跑，一樣不用裝在本機。
- **V5a 做完就有一個可以展示的版本：** AI 對話用固定句型（降級階梯第 4 列 ④），IoT 用模擬數據（第 5 列 ②）。階段 C 若卡住，可以直接跳到 V6。
- **代價是 AI 指令準確率（報告第 6 節第 1 點）比較晚才知道。** 好處是 V3 一開始就用 V2 匯出的真實構件命名來測，不必先用假資料測一次、再用真資料重測。
- **母體程式先在本專案裡做，放在獨立資料夾**（例如前端的 `src/base/`），不跟專案程式混在一起。B0b 完成後再抽成範本 repo，避免兩個 repo 來回同步。
- **限時**取估計上限（報告第 3 節）。超過限時或驗收不過，就照降級階梯往下一階，不回頭。V2 實際超過 10 人日就重估全案。
- **工時：** 合計比報告的 38–55 人日多 0.5–1 人日，多的是 B0a 新增的 mock 層。V0 的工時報告沒有單列，這裡另估 2–3 人日，不含在合計內。
- **可能追加：** 降級階梯第 4 列 ③ pgvector 語意比對 +2–3 人日；同畫面無縫穿透加分項，限時 3 天。

## V0 資料與環境（2–3 人日，限時 3）

決定示範資料走降級階梯第 1、2、7 列的哪一階。

**環境與帳號**

- [ ] Docker Desktop（WSL 2）、Node.js LTS；.NET 10 SDK 到階段 C 才用到，可以晚點裝
- [ ] Blender + Bonsai、IfcOpenShell（含 IfcConvert）、任一桌面 IFC 檢視器
- [ ] Cesium ion Community 帳號；另建一支只開 `assets:read` 權限的 token 給前端用
- [ ] 規格書：註明以報告附錄的 11 處修正為準

**IFC 模型（降級階梯第 1 列）**

不限定建築類型或特定構件，能呈現數位雙生效果的建築或設施都可以，IFC2x3、IFC4 皆可。

以下兩個候選模型我在 2026-10-15 下載並清點過內容。來源是 [KIT IFC Examples](https://www.ifcwiki.org/index.php?title=KIT_IFC_Examples)，頁面標明可自由使用，發表時註明出處為 IAI / KIT 即可。兩者都是 ArchiCAD 20 匯出的 IFC4，沒有機電構件，也沒有地理參考（`IfcMapConversion`）。

| 模型 | 大小 | 樓層 | 空間（`IfcSpace`） | 其他構件 | 建議用途 |
| --- | --- | --- | --- | --- | --- |
| [`AC20-Institute-Var-2.ifc`](https://www.ifcwiki.org/images/9/98/AC20-Institute-Var-2.ifc)（研究所大樓） | 10.9 MB | 5 層：Keller（-3 m）、Erdgeschoss、1. Obergeschoss、2. Obergeschoss、Dachgeschoss | 82 間：辦公室、實驗室、技術機房 I–IV、研討室、會議室、各層廁所 | 門 77、窗 206、樓梯 4、家具 253 | 主要示範模型 |
| [`AC20-FZK-Haus.ifc`](https://www.ifcwiki.org/images/e/e3/AC20-FZK-Haus.ifc)（住宅） | 2.6 MB | 2 層：Erdgeschoss、Dachgeschoss（2.7 m） | 7 間：臥室、浴室、書房、客廳、走道、廚房、夾層 | 門 5、窗 11、樓梯 1 | V2 開發初期的輕量測試模型 |

- [ ] ① 選定模型（限時 1 天）。建議主要示範用 Institute，V2 開發初期先用 FZK-Haus。條件：
  - [ ] 2 層以上，`IfcBuildingStorey` 完整（沒有樓層的設施，`sliceFloor` 改成依高度剖切）
  - [ ] 有可以掛感測器的對象：空間（`IfcSpace`）最通用，設備或機電構件也可以
- [ ] ② 條件差一點，就用 Bonsai 補上缺的空間、設備或屬性
- [ ] 在桌面檢視器盤點模型有哪些類別、空間與設備，各在哪一層
- [ ] 記下模型的樓層表（名稱、標高），V2 驗收剖切與 V3 都會用到
- [ ] 依盤點結果選定示範句的目標，填入「請帶我看〈＿＿〉，並幫我剖切到該樓層。」
  - 用 Institute 的話，「技術機房」是現成的選擇，4 間都在 Keller
  - 各層都有的廁所與辦公室，可以拿來測「3 樓的廁所」這類帶樓層的指令

**地點與圖資（降級階梯第 2、7 列）**

- [ ] 選定示範地點。模型不是當地的真實建物，就選一棟輪廓與尺寸相近的 OSM 建物來替換
- [ ] 在 Cesium Sandcastle 的 OSM Buildings 範例飛到示範地點，目視確認建物覆蓋度
- [ ] 量測建築錨點：經緯度、地面高程、方位角（兩個候選模型都沒有地理參考，一定要人工定）
- [ ] 國土測繪 WMTS：從 GetCapabilities 選定底圖圖層，記下引用標示文字

**驗收基準機**

- [ ] 記下規格（CPU、GPU、記憶體、螢幕解析度）：一台獨顯開發機，加一台內顯筆電作為最低標準

**過關檢查**

- [ ] 示範句的目標已選定，且在桌面檢視器查得到它所屬的樓層（或已決定走降級階梯哪一階）
- [ ] 錨點的經緯度與方位角已定
- [ ] 降級階梯第 1、2、7 列的決定已記入「降級紀錄」

## B0a 母體前端骨架（3.5–6 人日，限時 6）

一開始的架構只搭前端需要的部分：Nginx 入口、Vue 外殼、mock 資料層。後端相關的部分留到 B0b。

- [ ] **B0a-1 Docker Compose + Nginx，先只放前端**（1–2，含 B0b 補上的轉發設定）
  - [ ] Multi-stage Dockerfile：用 Node 打包 Vue，再把產物複製進 nginx 映像
  - [ ] Nginx 是唯一對外入口；`/` 設 SPA 路由回退 `try_files $uri $uri/ /index.html`
  - [ ] `gzip_types` 加入 `application/wasm`，並確認 `.wasm` 回傳 `application/wasm`（V2 會驗）
  - [ ] nginx 容器有 healthcheck
  - [ ] `/api/`、`/hub/` 的轉發等到 B0b 再加。api 容器還不存在時就先寫進去，Nginx 會因為找不到 upstream 主機而啟動失敗
  - [ ] 日常開發用 `npm run dev`（Vite 熱更新）；Compose 用來驗證打包後的結果
- [ ] **B0a-2 Vue 3 外殼與戰情室面板框架**（2–3）
  - [ ] Vue 3 + TypeScript + Vite 骨架；母體程式放 `src/base/`，專案程式放別的資料夾
  - [ ] 面板以動態元件註冊，專案只寫面板內容；版面存成設定檔
  - [ ] 一個讀取 mock 範例資料的範例面板
- [ ] **B0a-3 Mock 資料層**（0.5–1，報告未列，新增）
  - [ ] 用 MSW（Mock Service Worker）攔截 `/api/*`。前端照真實 API 的路徑寫，接上後端時只要關掉 MSW
  - [ ] 每支 API 的請求與回應先定成 TypeScript 型別，當作前後端的合約，B0b 照著實作
  - [ ] mock 資料放 JSON 檔
  - [ ] 用環境變數（例如 `VITE_USE_MOCK`）切換，不要綁 `import.meta.env.DEV`；這樣 Compose 打包的版本也能在 Nginx 後面用 mock 展示

**過關檢查**

- [ ] `npm run dev` 開啟首頁，面板顯示 mock 資料，改程式碼後畫面即時更新
- [ ] `docker compose up -d` 後 nginx 為 healthy；經由 Nginx 開首頁，一樣顯示 mock 資料
- [ ] 在子路由重新整理不會 404

**不過關時：** 超過 6 人日就先固定版面，面板框架只留本專案用得到的部分。

## V1 宏觀尖兵（3–4 人日，限時 4）

- [ ] **V1-1 Cesium 整合進母體前端**（1）
  - [ ] 用 Cesium 官方的 Vite 寫法：`vite-plugin-static-copy` 複製 Workers、ThirdParty、Assets、Widgets，再以 `define` 設定 `CESIUM_BASE_URL`（不用 `vite-plugin-cesium`）
  - [ ] Viewer 放在 `shallowRef` 或非響應式變數
  - [ ] 註冊成戰情室面板；ion token 從 `.env` 讀取（例如 `VITE_CESIUM_ION_TOKEN`）
- [ ] **V1-2 台灣底圖與 3D 建物**（2–3）
  - [ ] 國土測繪 WMTS 底圖（`WebMapTileServiceImageryProvider`），畫面上註明圖資來源
  - [ ] 載入 Cesium OSM Buildings，或 V0 決定的替代方案
  - [ ] 以 `Cesium3DTileStyle` 依高度著色（OSM Buildings 的 `cesium#estimatedHeight` 屬性）
  - [ ] 用 `scene.pick` 點選建物，高亮並顯示屬性

**過關檢查**

- [ ] 在驗收基準機上，1080p 平移縮放 ≥ 30 FPS（可開 `scene.debugShowFramesPerSecond` 觀察）
- [ ] 點選後 300 ms 內高亮並顯示屬性

**不過關時：** 走降級階梯第 2 列。

## V2 微觀尖兵（6.5–9 人日，限時 9）

實際超過 10 人日就重估全案（報告第 6 節第 3 點）。

- [ ] **V2-1 Nginx 補 WASM 與靜態圖資設定**（0.5–1）
  - [ ] 經由 Nginx 取得 web-ifc 的 `.wasm` 時，標頭正確
  - [ ] IFC、Fragments、3D Tiles 設快取標頭
- [ ] **V2-2 IFC 載入與點選屬性（That Open）**（3–4）
  - [ ] 鎖定 `@thatopen/components`、`@thatopen/components-front`、`@thatopen/fragments`、`web-ifc` 的版本；`three` 以 That Open 要求的版本為準
  - [ ] 註冊成戰情室面板，並設定 web-ifc 的 WASM 路徑
  - [ ] 先用 FZK-Haus 打通流程，再換成目標模型；模型檔放 `public/models/`
  - [ ] 預轉成 Fragments 檔再載入
  - [ ] 點選構件時顯示 ExpressID、名稱、類別
  - [ ] 記下目標模型的載入時間與記憶體用量（報告第 3 節的未知數）
- [ ] **V2-3 分類隔離、樓層剖切、高亮**（3–4）
  - [ ] 依 IFC 類別隔離與隱藏構件（例如 That Open 的 `Classifier`、`Hider`）
  - [ ] 樓層剖切：剖切面從 `IfcBuildingStorey` 標高推算，可先設在標高上方約 1.2 公尺（平面圖慣例），並隱藏上方樓層
  - [ ] 高亮指定構件（例如 `Highlighter`）
  - [ ] 三個動作包成函式，介面對齊之後的 `isolateElement`、`sliceFloor` 工具，V5a 直接接上
  - [ ] 匯出構件索引（GlobalId、類別、名稱、所屬樓層）成 JSON。空間的 Name 是房號（例如 `001`），房間名稱在 LongName（例如 `Technikraum I`），兩個都要存
  - [ ] 構件索引在階段 B 由 MSW 提供，V3 再匯入 PostgreSQL

**過關檢查**

- [ ] 目標模型首次載入 ≤ 15 秒；預轉 Fragments 後 ≤ 5 秒
- [ ] 點選後 500 ms 內顯示 ExpressID、名稱、類別
- [ ] 每一層的剖切結果與 V0 記下的樓層表一致

**不過關時：** 先動用效能調校緩衝；仍然不過，走降級階梯第 6 列，拆樓層按需載入，最後才換較小的模型。

## V4 整合（5–6 人日，限時 6）

- [ ] **V4-1 座標錨定與方位角校正**（2–3）
  - [ ] ① 讀 IFC 的 `IfcMapConversion`；沒有的話走 ②，用 V0 量測的經緯度與方位角。兩個候選模型都要走 ②
  - [ ] 以 `Transforms.eastNorthUpToFixedFrame` 產生錨定矩陣；要帶方位角可用 `Transforms.headingPitchRollToFixedFrame`
  - [ ] Three.js 端以建築原點為中心計算，不用地心座標，避免 Float32 精度造成抖動
  - [ ] 用 IfcConvert 轉一份 `.glb`，套用錨定矩陣放進 Cesium，與底圖建物對照；同時隱藏同位置的 OSM 建物
- [ ] **V4-2 兩段式宏觀→微觀轉換**（3）
  - [ ] Cesium 飛到建築上空
  - [ ] 把飛行終點的相機姿態換算到建築局部座標，作為 Three.js 的初始視角
  - [ ] 淡入淡出切換，不閃畫面、不黑畫面
  - [ ] 反向：從 BIM 檢視回到 Cesium
  - [ ] 來回切換時不重複建立 Viewer，也不重新載入模型
- [ ] **加分項：同畫面無縫穿透**（限時 3 天，不計入估計）
  - [ ] 到期沒做出來就停下，記入降級紀錄

**過關檢查**

- [ ] IFC 外輪廓與底圖建物偏差 ≤ 2 公尺（放在空地時，此項不適用）
- [ ] 從開始飛行到切換完成 ≤ 5 秒，無黑畫面
- [ ] 來回切換 10 次後，記憶體增加 < 10%

**不過關時：** 轉換效果走降級階梯第 3 列（先改硬切換，再改成兩個獨立頁面）；錨定走第 7 列（放在空地）。

## V5a 前端聯動（4–6 人日，限時 6）

全部在前端完成，AI 與 IoT 都先用 mock。做完這關就有一個可以展示的版本。

- [ ] **V5a-1 串流對話窗**（2–3，屬於母體，程式放 `src/base/`）
  - [ ] `EventSource` 只支援 GET，所以 `POST /api/agent` 要改用 `fetch` 讀取串流
  - [ ] Markdown 顯示
  - [ ] 工具呼叫卡片：顯示模型呼叫了哪個工具、帶了什麼參數，以及執行結果
  - [ ] 用 MSW mock `POST /api/agent`：示範句等固定句型，回傳預先寫好的串流文字與工具呼叫事件。之後若 AI 準確率不夠，這就是降級階梯第 4 列 ④
- [ ] **V5a-2 前端執行器**（1–1.5）
  - [ ] 收到 `flyToLocation`、`isolateElement`、`sliceFloor`，就呼叫 V1、V2、V4 做好的函式，並回報執行結果
  - [ ] 指令來源做成可替換：現在接 mock 串流，V3 改接 SignalR
- [ ] **V5a-3 IoT 前端著色，使用模擬數據**（1–1.5）
  - [ ] 感測器對照表（JSON）：感測器 ID 對應 V0 盤點出的空間或設備的 `GlobalId`，加上數值範圍與告警門檻
  - [ ] 前端模擬資料來源，每秒產生一次數值；介面和之後的 SignalR 用戶端相同，V5b 直接替換
  - [ ] 依數值與告警門檻改變構件顏色；戰情室面板顯示感測器清單與告警

**過關檢查**

- [ ] 對話窗輸入示範句：串流逐字顯示、出現工具呼叫卡片，相機到位並完成剖切
- [ ] 50 個模擬感測器每秒更新一次時，FPS 仍 ≥ 30
- [ ] 錄一段完整的展示畫面，當作階段 C 卡住時的保底版本

**不過關時：** FPS 不足就動用效能調校緩衝，或降低更新頻率。

## B0b 母體後端核心（6–9 人日，限時 9）

需要真實資料與 AI 時才做。PostgreSQL 跑在 Docker 容器裡，本機不用安裝。

- [ ] **B0b-1 PostgreSQL + pgvector（Docker）**（1–2）
  - [ ] Compose 加入 `db` 服務，用 `pgvector/pgvector` 映像，資料放 named volume
  - [ ] 本機用 `dotnet run` 開發時，只啟動資料庫：`docker compose up -d db`，並把 5432 對應到 localhost
  - [ ] EF Core migration 在 api 啟動時自動套用，並在 migration 內啟用 `vector` 擴充
- [ ] **B0b-2 SignalR 基底**（1）
  - [ ] 強型別 Hub（`Hub<T>`）；前端用 `withAutomaticReconnect()`
  - [ ] 一個推送測試訊息的端點，給 15 分鐘驗收第 4 項用
- [ ] **B0b-3 ASP.NET Core：LLM 供應商抽象、Tool 路由、Schema 驗證**（3–4）
  - [ ] 申請 LLM API 金鑰（Anthropic 或 Google），在主控台設每月用量上限
  - [ ] .NET 10 專案骨架與健康檢查端點；照 B0a-3 的 TypeScript 合約實作 API
  - [ ] LLM 供應商介面，至少接一家；模型名稱放設定檔，金鑰放 `.env`
  - [ ] 工具參數用 C# record 定義，以 `JsonSchemaExporter` 產生 Schema，執行前先驗證
  - [ ] 工具分兩類：後端執行；交給前端執行（經 SignalR 派送，等前端回報結果）
  - [ ] 多輪工具迴圈：`POST /api/agent` 以 SSE 串流回傳文字與工具呼叫事件，事件格式和 V5a 的 mock 相同
  - [ ] 一個範例工具，給 15 分鐘驗收第 3 項用
- [ ] **B0b-4 前端接上後端**（含在 B0a-1 的估計內）
  - [ ] Nginx 加 `/api/` 轉發到 api 容器的 8080；SSE 路徑設 `proxy_buffering off`
  - [ ] Nginx 加 `/hub/`，設 `proxy_http_version 1.1` 與 `Upgrade`、`Connection` 標頭
  - [ ] api 容器加 healthcheck，`depends_on` 用 `condition: service_healthy`。aspnet 執行映像不含 curl，要自己裝，或改用別的檢查方式
  - [ ] Vite 開發伺服器設 `server.proxy`，把 `/api`、`/hub` 轉到本機後端（`/hub` 要開 `ws: true`）
  - [ ] 關掉 MSW，面板與對話窗改讀真實 API
- [ ] **B0b-5 抽出母體範本與腳手架**（1–2）
  - [ ] 把 `src/base/`、後端母體專案、Compose 與 Nginx 設定抽成範本 repo，並設為 GitHub template repository
  - [ ] 一支腳本改專案名稱、埠號、資料庫名稱
  - [ ] 附 `.env.example`，`.env` 加入 `.gitignore`

**過關檢查：從範本建立一個空白測試專案，從複製範本開始計時（首次下載映像不計）**

- [ ] `docker compose up -d` 後，所有容器的 healthcheck 為 healthy
- [ ] 首頁戰情室面板顯示範例資料
- [ ] 對話窗送出一句話，看到串流回覆，並觸發一次範例工具呼叫
- [ ] 後端推送一則 SignalR 測試訊息，前端收到
- [ ] migration 完成，`vector` 擴充可用
- [ ] 記下實際分鐘數：＿＿。另記一次含首次 `npm install`、`dotnet restore` 的時間：＿＿（報告第 3 節的未知數）
- [ ] spatial-twin-guard 關掉 mock 後，V5a 的功能仍正常

**不過關時：** 超過 9 人日就砍到剛好夠本專案用，其餘移到第二批。整個階段 C 卡住時，直接用 V5a 的版本進 V6。

## V3 AI 尖兵（4–6.5 人日，限時 6.5）

前端執行器（V5a）與真實構件索引（V2）都已經有了，V3 直接用真實資料測。

- [ ] **V3-1 後端空間工具**（1–1.5）
  - [ ] 把 V2 匯出的構件索引匯入 PostgreSQL
  - [ ] 後端執行（查 PostgreSQL）：`findElements(category, floor?)` 回傳 GlobalId 與所屬樓層；`listFloors()`
  - [ ] 交給前端執行，經 SignalR 送到 V5a 的執行器：`flyToLocation(target)`、`isolateElement(category, elementId?)`、`sliceFloor(floorNumber)`
  - [ ] `flyToLocation` 的 `target` 限定為預先定義的地點清單或 GlobalId，不收任意字串
- [ ] **V3-2 中文語意對應與準確率調校**（3–5）
  - [ ] 測試題庫 30 句以上，每句附預期的工具與參數
  - [ ] 測試腳本：自動跑題庫、比對工具與參數，輸出正確題數與回應時間中位數
  - [ ] 先用 ① 跑一次，只靠工具迴圈：＿＿/30
  - [ ] 未達 27 題就加 ②：在提示裡注入樓層與類別清單，並加同義詞表，把中文說法對到模型裡的 IFC 類別與實際命名，再跑一次：＿＿/30
  - [ ] 用 Institute 模型時，樓層與房名要特別處理：
    - 台灣的「1 樓」是 Erdgeschoss，「2 樓」是 1. Obergeschoss，「地下室」是 Keller
    - 房間名稱是德文，例如「技術機房」要對到 `Technikraum`

**過關檢查**

- [ ] 30 題中，工具與參數全對 ≥ 27 題
- [ ] V0 選定的示範句連跑 10 次全部成功，3D 畫面實際動作
- [ ] 回應時間中位數 ≤ 4 秒

**不過關時：** 限時到時若有 24–26 題，算降級過關，展示影片只用驗證過的句子。低於 24 題就走降級階梯第 4 列 ③，用 pgvector 做語意比對（另加 2–3 人日）；仍然不夠，走 ④，直接沿用 V5a 的固定句型。

## V5b 真實聯動（1–1.5 人日，限時 1.5）

- [ ] **IoT 改接後端**（1–1.5）
  - [ ] `BackgroundService` 產生模擬數據，`IHubContext` 推播；每次把所有感測器合成一則批次訊息推送，不要每個感測器各推一則
  - [ ] 前端的模擬資料來源換成 SignalR 用戶端
  - [ ] （選用）感測歷史寫入 PostgreSQL

**過關檢查**

- [ ] 示範句從送出到相機到位、剖切完成 ≤ 6 秒
- [ ] 50 個感測器每秒經 SignalR 更新一次時，FPS 仍 ≥ 30
- [ ] 後端重啟後 10 秒內自動重連

**不過關時：** 先降低推播頻率；仍然不夠，走降級階梯第 5 列，改回 V5a 的前端模擬數據。

## V6 部署與展示（2–3 人日，限時 3）

階段 C 若跳過，就部署 V5a 的 mock 版本，下方過關檢查中 `/hub/` 與 LLM 金鑰兩項不適用。

- [ ] `/api/agent` 加速率限制（ASP.NET Core 內建的 `AddRateLimiter`）
- [ ] UI 打磨
- [ ] README：架構圖、啟動方式、「已知取捨」（從下方降級紀錄整理）、模型與圖資來源（含 IAI / KIT）
- [ ] 錄影腳本與展示影片，只用 V3（或 V5a）驗證過的句子

**過關檢查**

- [ ] 在乾淨機器上（或刪光映像與 volume 後），`docker compose up -d` 一次啟動
- [ ] `.wasm` 的回應標頭為 `application/wasm`
- [ ] `/hub/` 回應 101 升級
- [ ] 經過 Nginx 後，串流對話仍逐字顯示
- [ ] 瀏覽器主控台 0 個 CORS 錯誤
- [ ] 前端打包產物搜不到 LLM API 金鑰（Cesium ion token 本來就放在前端，不在此限）

**不過關時：** 修正設定即可，不該有結構性問題。

## 效能調校緩衝（3–5 人日，按需動用）

V2、V4、V5a、V5b 的效能標準不過時才動用；用掉多少，記在總覽表的「實際」欄。

- [ ] 拆樓層按需載入
- [ ] LOD、Web Worker
- [ ] IoT 更新的重繪節流

## 每過一關

- [ ] 在總覽表填實際人日與狀態
- [ ] 有降級就記入下方「降級紀錄」
- [ ] 截圖或錄一小段畫面，留作 V6 的展示素材

## 降級紀錄

每降一階記一行，V6 時整理進 README 的「已知取捨」。

| 日期 | 降級階梯列 | 從哪一階到哪一階 | 原因 |
| --- | --- | --- | --- |
| | | | |

## 延後：母體第二批（本專案不做）

降級階梯第 9 列。第一個需要它們的專案開工前再做。

- 權限（JWT、角色）：2–3 人日
- PDF/圖片標註檢視器：3–5 人日
- Python 微服務骨架：1 人日
- 中文全文檢索：1–2 人日
