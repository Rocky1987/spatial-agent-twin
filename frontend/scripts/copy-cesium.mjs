import { cp } from 'node:fs/promises'

// 同步版 cpSync 在 Node 22 / Windows 會當掉，改用非同步的 cp
const src = 'node_modules/cesium/Build/Cesium'
for (const dir of ['ThirdParty', 'Workers', 'Assets', 'Widgets']) {
  await cp(`${src}/${dir}`, `public/cesiumStatic/${dir}`, { recursive: true })
}
