<script setup lang="ts">
import { nextTick, ref } from 'vue'
import { Bot, Check, SendHorizontal, TriangleAlert, X } from 'lucide-vue-next'
import spaceMap from '@/data/space-map.json'
import Button from './ui/Button.vue'

// App.vue 那一組工具；AI 回傳的 toolCalls 與除錯按鈕走同一組函式
export interface Tools {
  flyToLocation(place: 'institute' | 'taiwan'): Promise<void>
  sectionStorey(key: string): Promise<void>
  isolateSpaces(keys: string[]): Promise<void>
  highlightSpaces(keys: string[], color?: string): Promise<void>
  resetView(): Promise<void>
}
export interface AlertItem { spaceKey: string; temp: number; time?: string }

interface Call { name: string; args: Record<string, unknown>; state: 'wait' | 'run' | 'ok' | 'fail'; error?: string }
interface Msg { role: 'user' | 'assistant'; text: string; calls?: Call[]; error?: boolean }

const props = defineProps<{ tools: Tools; alerts?: AlertItem[] }>()
const emit = defineEmits<{ (e: 'close'): void }>()

const msgs = ref<Msg[]>([])
const input = ref('')
const busy = ref(false)
const listEl = ref<HTMLElement>()

// 畫面狀態快照：每一步（一句指令，或一次手動按鈕）執行前先存一份。
// past 是之前的狀態，future 是被「上一步」退回來的狀態，所以可以一路退回最初，也可以再往前
interface ViewState { place: 'institute' | 'taiwan'; section: string | null; isolate: string[] | null; highlight: string[] | null }
const initial = (): ViewState => ({ place: 'taiwan', section: null, isolate: null, highlight: null })
const copy = (v: ViewState): ViewState => ({ ...v, isolate: v.isolate && [...v.isolate], highlight: v.highlight && [...v.highlight] })
let state = initial()
const past: ViewState[] = []
const future: ViewState[] = []

// 開始新的一步：存快照，並清掉 future（走了新路，原本退回來的就不能再往前）
function begin() {
  past.push(copy(state))
  if (past.length > 30) past.shift()
  future.length = 0
}

// 包一層：每個工具執行後同步記錄目前的畫面狀態
const tracked: Tools = {
  async flyToLocation(place) { await props.tools.flyToLocation(place); state.place = place },
  async sectionStorey(key) { await props.tools.sectionStorey(key); state.place = 'institute'; state.section = key },
  async isolateSpaces(keys) { await props.tools.isolateSpaces(keys); state.place = 'institute'; state.isolate = keys },
  async highlightSpaces(keys, color) { await props.tools.highlightSpaces(keys, color); state.place = 'institute'; state.highlight = keys },
  async resetView() { await props.tools.resetView(); state = { ...initial(), place: 'institute' } },
}

const storeyZh = (k: string) => spaceMap.storeys.find((s) => s.key === k)?.zh ?? k
const spaceZh = (k: string) => spaceMap.spaces.find((s) => s.key === k)?.zh ?? k

// 工具呼叫的白話說明；原始名稱放在滑鼠提示
function label(c: Call): string {
  const a = c.args
  switch (c.name) {
    case 'flyToLocation': return a.place === 'taiwan' ? '回到台灣全景' : '飛往目標大樓並展開 BIM 窗格'
    case 'sectionStorey': return `剖切樓層：${storeyZh(a.key as string)}`
    case 'isolateSpaces': return `隔離空間：${(a.keys as string[]).map(spaceZh).join('、')}`
    case 'highlightSpaces': return `標紅空間：${(a.keys as string[]).map(spaceZh).join('、')}`
    case 'resetView': return '還原 BIM 視圖（清除剖切、隔離與標記）'
    case 'showAlerts': return '在 BIM 中標出目前所有告警的空間'
    case 'undo': return '回到上一步的畫面'
    case 'redo': return '再往前一步'
    default: return c.name
  }
}
const rawName = (c: Call) => `${c.name}(${JSON.stringify(c.args)})`

// 回到快照：先飛到該位置；BIM 狀態先清掉再依序重做
async function restore(t: ViewState) {
  if (t.place !== state.place) await props.tools.flyToLocation(t.place)
  if (t.place === 'institute') {
    await props.tools.resetView()
    if (t.section) await props.tools.sectionStorey(t.section)
    if (t.isolate) await props.tools.isolateSpaces(t.isolate)
    if (t.highlight) await props.tools.highlightSpaces(t.highlight)
    state = copy(t)
  } else {
    state = { ...state, place: 'taiwan' }
  }
}
async function undo() {
  const prev = past.pop()
  if (!prev) throw new Error('已經是最初的畫面')
  future.push(copy(state))
  await restore(prev)
}
async function redo() {
  const next = future.pop()
  if (!next) throw new Error('沒有可以再往前的步驟')
  past.push(copy(state))
  await restore(next)
}

const examples = ['飛到大樓，剖開二樓', '只看三樓的會議室', '把地下室的技術室標紅', '現在哪裡有告警？標出來', '還原並回到台灣', '回到上一步', '再往前一步']

const scrollDown = () => nextTick(() => listEl.value?.scrollTo({ top: listEl.value.scrollHeight, behavior: 'smooth' }))

async function run(c: Call) {
  const a = c.args
  switch (c.name) {
    case 'flyToLocation': return tracked.flyToLocation(a.place as 'institute' | 'taiwan')
    case 'sectionStorey': return tracked.sectionStorey(a.key as string)
    case 'isolateSpaces': return tracked.isolateSpaces(a.keys as string[])
    case 'highlightSpaces': return tracked.highlightSpaces(a.keys as string[])
    case 'resetView': return tracked.resetView()
    case 'showAlerts': {
      const keys = (props.alerts ?? []).map((x) => x.spaceKey)
      if (!keys.length) throw new Error('目前沒有告警')
      // 用隔離：只標紅的話，地下室的房間被樓上蓋住，相機會鑽進牆裡
      return tracked.isolateSpaces(keys)
    }
    case 'undo': return undo()
    case 'redo': return redo()
    default: throw new Error('未知的工具：' + c.name)
  }
}

async function send(text = input.value) {
  text = text.trim()
  if (!text || busy.value) return
  input.value = ''
  busy.value = true
  // 送給後端的歷史：只留文字；沒有文字只有工具的回覆，用一句摘要代替，避免空訊息
  const history = msgs.value
    .filter((m) => !m.error)
    .map((m) => ({
      role: m.role,
      content: m.text || (m.calls?.length ? `（已執行：${m.calls.map((c) => c.name).join('、')}）` : '（無回應）'),
    }))
  msgs.value.push({ role: 'user', text })
  scrollDown()
  try {
    const res = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        messages: [...history, { role: 'user', content: text }],
        alerts: (props.alerts ?? []).map((a) => ({ spaceKey: a.spaceKey, temp: a.temp, time: a.time })),
      }),
    })
    const data = await res.json().catch(() => ({}))
    if (!res.ok) throw new Error(data.error ?? `HTTP ${res.status}`)
    const calls: Call[] = (data.toolCalls ?? []).map((t: { name: string; args: Record<string, unknown> }) => ({ name: t.name, args: t.args ?? {}, state: 'wait' }))
    msgs.value.push({ role: 'assistant', text: data.reply ?? '', calls })
    scrollDown()
    // 這句指令要改動畫面時才算新的一步（純「上一步／下一步」不算）
    if (calls.some((c) => c.name !== 'undo' && c.name !== 'redo')) begin()
    // 依序執行：前一個轉場完成才做下一個
    for (const c of calls) {
      c.state = 'run'
      try {
        await run(c)
        c.state = 'ok'
      } catch (e) {
        c.state = 'fail'
        c.error = e instanceof Error ? e.message : String(e)
        for (const rest of calls) if (rest.state === 'wait') rest.state = 'fail'
        break
      }
    }
  } catch (e) {
    msgs.value.push({ role: 'assistant', text: e instanceof Error ? e.message : String(e), error: true })
  } finally {
    busy.value = false
    scrollDown()
  }
}

function onKey(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
    e.preventDefault()
    send()
  }
}

defineExpose({ tracked, begin })
</script>

<template>
  <div class="ui flex max-h-[min(560px,calc(100%-170px))] w-[340px] max-w-[calc(100%-24px)] flex-col overflow-hidden rounded-xl border border-solid border-[var(--line)] bg-[var(--panel-solid)]/95 text-sm text-white shadow-xl backdrop-blur">
    <div class="flex items-center gap-2 border-0 border-b border-solid border-[var(--line)] px-3 py-2">
      <Bot class="size-4 text-sky-300" />
      <span class="flex-1 font-medium">AI 助理</span>
      <Button variant="ghost" size="icon" class="size-6" title="收合" @click="emit('close')"><X class="size-4" /></Button>
    </div>

    <div ref="listEl" class="flex min-h-[120px] flex-1 flex-col gap-2 overflow-y-auto p-3">
      <p v-if="!msgs.length" class="m-0 text-white/60">用中文下指令，我來操作畫面。點下方的快捷指令試試，或自己輸入。</p>

      <template v-for="(m, i) in msgs" :key="i">
        <div v-if="m.role === 'user'" class="max-w-[85%] self-end whitespace-pre-wrap break-words rounded-xl rounded-br-sm bg-sky-600 px-3 py-1.5">{{ m.text }}</div>
        <div v-else class="flex max-w-[92%] flex-col gap-1.5 self-start">
          <div
            v-if="m.text"
            class="whitespace-pre-wrap break-words rounded-xl rounded-bl-sm px-3 py-1.5"
            :class="m.error ? 'bg-red-500/20 text-red-200' : 'bg-white/10'"
          >
            <TriangleAlert v-if="m.error" class="mr-1 inline size-4 align-text-bottom" />{{ m.text }}
          </div>
          <div v-if="m.calls?.length" class="flex flex-col gap-1">
            <div v-for="(c, j) in m.calls" :key="j" class="flex items-center gap-1.5 rounded-md bg-black/30 px-2 py-1 text-xs" :title="c.error ?? rawName(c)">
              <Check v-if="c.state === 'ok'" class="size-3.5 shrink-0 text-emerald-400" />
              <TriangleAlert v-else-if="c.state === 'fail'" class="size-3.5 shrink-0 text-red-400" />
              <span v-else class="size-3.5 shrink-0 animate-pulse rounded-full" :class="c.state === 'run' ? 'bg-sky-400' : 'bg-white/30'" />
              <span class="break-words">{{ label(c) }}</span>
            </div>
          </div>
        </div>
      </template>

      <div v-if="busy && msgs[msgs.length - 1]?.role === 'user'" class="self-start rounded-xl bg-white/10 px-3 py-1.5 text-white/60">思考中…</div>
    </div>

    <div class="flex flex-wrap gap-1.5 border-0 border-t border-solid border-[var(--line)] px-2 pt-2">
      <button
        v-for="ex in examples"
        :key="ex"
        type="button"
        :disabled="busy"
        class="cursor-pointer rounded-full border border-solid border-[var(--line)] bg-white/5 px-2.5 py-1 text-xs text-white/90 hover:bg-white/15 disabled:pointer-events-none disabled:opacity-50"
        @click="send(ex)"
      >{{ ex }}</button>
    </div>

    <form class="flex items-end gap-1.5 p-2" @submit.prevent="send()">
      <textarea
        v-model="input"
        rows="1"
        placeholder="輸入指令，Enter 送出"
        class="max-h-24 min-h-9 flex-1 resize-none rounded-lg border border-solid border-[var(--line)] bg-black/30 px-2.5 py-1.5 text-sm text-white outline-none placeholder:text-white/40 focus:border-sky-400"
        @keydown="onKey"
      />
      <Button type="submit" size="icon" :disabled="busy || !input.trim()" title="送出"><SendHorizontal class="size-4" /></Button>
    </form>
  </div>
</template>
