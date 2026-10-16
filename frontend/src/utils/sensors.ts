import { computed, onBeforeUnmount, reactive, ref } from 'vue'
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'

// IoT 溫度：後端模擬 10 個感測器，每 2 秒透過 SignalR（/hubs/sensors）把全部讀數推過來
export const THRESHOLD = 30

export interface Sensor { spaceKey: string; temp: number; since?: string }

const clock = (iso?: string | null) => (iso ? new Date(iso).toLocaleTimeString('zh-TW', { hour12: false }) : undefined)

export function useSensors() {
  const sensors = reactive<Sensor[]>([])
  const connected = ref(false)

  const conn = new HubConnectionBuilder()
    .withUrl('/hubs/sensors')
    .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()

  conn.on('readings', (list: { spaceKey: string; temp: number; since: string | null }[]) => {
    sensors.splice(0, sensors.length, ...list.map((r) => ({ spaceKey: r.spaceKey, temp: r.temp, since: clock(r.since) })))
  })
  conn.onreconnecting(() => (connected.value = false))
  conn.onreconnected(() => (connected.value = true))
  conn.onclose(() => (connected.value = false))

  // 後端還沒起來時，每 3 秒再試一次
  let stopped = false
  async function start() {
    while (!stopped && conn.state === HubConnectionState.Disconnected) {
      try {
        await conn.start()
        connected.value = true
      } catch {
        await new Promise((r) => setTimeout(r, 3000))
      }
    }
  }
  void start()
  onBeforeUnmount(() => {
    stopped = true
    void conn.stop()
  })

  const alerts = computed(() =>
    sensors.filter((s) => s.temp >= THRESHOLD).map((s) => ({ spaceKey: s.spaceKey, temp: s.temp, time: s.since })),
  )

  // 請後端讓某個感測器開始升溫（錄影與驗收用）
  function spike(spaceKey?: string) {
    if (connected.value) void conn.invoke('Spike', spaceKey ?? null)
  }

  return { sensors, alerts, spike, connected }
}
