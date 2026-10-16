import { computed, onBeforeUnmount, reactive, ref } from 'vue'

// IoT 溫度模擬：前端自己產生數據（individual-dev 的 fake 方案），不接真實感測器
export const THRESHOLD = 30
const TICK_MS = 2000
const BASE = 24

// 約 10 個感測器，各掛在一個空間上（key 對應 space-map.json）
const SENSOR_SPACES = [
  'KG-Technikraum-I', 'KG-Technikraum-II', 'KG-Technikraum-III', 'KG-Technikraum-IV',
  'KG-Labor-K1', 'KG-Labor-K2', 'KG-Labor-K3', 'EG-Labor-I', 'EG-Labor-II', 'KG-Seminarraum',
]

export interface Sensor { spaceKey: string; temp: number; heat: number; since?: string }

const clock = () => new Date().toLocaleTimeString('zh-TW', { hour12: false })

export function useSensors() {
  const sensors = reactive<Sensor[]>(SENSOR_SPACES.map((spaceKey) => ({ spaceKey, temp: BASE + Math.random() * 2, heat: 0 })))
  const tick = ref(0)

  // 超過門檻的感測器；since 是第一次超標的時間
  const alerts = computed(() =>
    sensors.filter((s) => s.temp >= THRESHOLD).map((s) => ({ spaceKey: s.spaceKey, temp: Math.round(s.temp * 10) / 10, time: s.since })),
  )

  // 讓某個感測器開始升溫（heat 是剩餘升溫次數）；不指定就隨機挑一個正常的
  function spike(spaceKey?: string) {
    const cool = sensors.filter((x) => x.temp < THRESHOLD)
    const s = spaceKey ? sensors.find((x) => x.spaceKey === spaceKey) : cool[Math.floor(Math.random() * cool.length)]
    if (s) s.heat = 6
  }

  function step() {
    tick.value++
    for (const s of sensors) {
      if (s.heat > 0) {
        s.temp += 1.5 + Math.random()
        s.heat--
      } else {
        // 沒在升溫就慢慢回到常溫，加一點雜訊
        s.temp += (BASE - s.temp) * 0.25 + (Math.random() - 0.5) * 0.6
      }
      if (s.temp >= THRESHOLD) s.since ??= clock()
      else s.since = undefined
    }
    // 偶爾自己發生一次升溫，畫面不會一直平靜
    if (Math.random() < 0.06 && !sensors.some((s) => s.heat > 0 || s.temp >= THRESHOLD)) spike()
  }

  const timer = setInterval(step, TICK_MS)
  onBeforeUnmount(() => clearInterval(timer))

  return { sensors, alerts, spike }
}
