// 讀出 IFC 的樓層與空間（GlobalId、Name、LongName、所屬樓層），輸出 JSON 到 stdout
// 用法：node scripts/dump-ifc.mjs public/models/AC20-Institute-Var-2.ifc
import { readFileSync } from 'node:fs'
import * as WebIFC from 'web-ifc'

const api = new WebIFC.IfcAPI()
await api.Init()
const id = api.OpenModel(new Uint8Array(readFileSync(process.argv[2])))
const val = (x) => (x && x.value !== undefined ? x.value : null)

const storeys = new Map()
for (const eid of api.GetLineIDsWithType(id, WebIFC.IFCBUILDINGSTOREY)) {
  const l = api.GetLine(id, eid)
  storeys.set(eid, { expressId: eid, globalId: val(l.GlobalId), name: val(l.Name), longName: val(l.LongName), elevation: val(l.Elevation) })
}
// 空間 → 樓層：IfcRelAggregates（樓層聚合空間）
const spaceStorey = new Map()
for (const eid of api.GetLineIDsWithType(id, WebIFC.IFCRELAGGREGATES)) {
  const r = api.GetLine(id, eid)
  if (storeys.has(val(r.RelatingObject))) for (const o of r.RelatedObjects) spaceStorey.set(val(o), val(r.RelatingObject))
}
const spaces = []
for (const eid of api.GetLineIDsWithType(id, WebIFC.IFCSPACE)) {
  const l = api.GetLine(id, eid)
  spaces.push({ expressId: eid, globalId: val(l.GlobalId), name: val(l.Name), longName: val(l.LongName), storey: storeys.get(spaceStorey.get(eid))?.name ?? null })
}
console.log(JSON.stringify({ storeys: [...storeys.values()], spaces }, null, 1))
