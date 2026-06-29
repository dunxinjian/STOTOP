<template>
  <button
    class="feedback-fab"
    :class="[`side-${side}`, { 'is-dragging': dragging }]"
    :style="fabStyle"
    type="button"
    title="提交反馈（可拖动调整位置）"
    @pointerdown="onPointerDown"
    @pointermove="onPointerMove"
    @pointerup="onPointerUp"
    @pointercancel="onPointerCancel"
    @click="open"
  >
    <MessageOutlined />
    <span>反馈</span>
  </button>

  <a-modal
    v-model:open="visible"
    title="提交反馈"
    width="680px"
    :style="feedbackModalStyle"
    :body-style="feedbackModalBodyStyle"
    :destroy-on-close="true"
  >
    <a-form ref="formRef" :model="form" :rules="rules" layout="vertical">
      <a-form-item label="反馈标题" name="title">
        <a-input v-model:value="form.title" placeholder="一句话说明问题或建议" :maxlength="200" />
      </a-form-item>
      <div class="quick-grid">
        <a-form-item label="类型" name="type">
          <a-select v-model:value="form.type" :options="typeOptions" />
        </a-form-item>
        <a-form-item label="所属模块" name="module">
          <a-select v-model:value="form.module" show-search :options="moduleOptions" />
        </a-form-item>
        <a-form-item label="严重程度" name="severity">
          <a-select v-model:value="form.severity" :options="severityOptions" />
        </a-form-item>
      </div>
      <a-form-item label="问题描述">
        <a-textarea v-model:value="form.description" :rows="3" :maxlength="2000" show-count />
      </a-form-item>
      <a-form-item label="实际结果">
        <a-textarea v-model:value="form.actualResult" :rows="2" :maxlength="1000" />
      </a-form-item>
      <a-form-item label="附件链接">
        <a-textarea v-model:value="form.attachmentLinks" :rows="2" placeholder="截图、录屏或文件链接，可多行填写" />
      </a-form-item>
    </a-form>
    <template #footer>
      <a-button @click="visible = false">取消</a-button>
      <a-button type="primary" :loading="submitting" @click="submit">提交</a-button>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { MessageOutlined } from '@ant-design/icons-vue'
import { createFeedback } from '@/api/feedback'

const typeOptions = [
  { value: 1, label: 'Bug' },
  { value: 2, label: '易用性' },
  { value: 3, label: '需求' },
  { value: 4, label: '数据问题' },
  { value: 5, label: '权限问题' },
]

const severityOptions = [
  { value: 1, label: '阻塞' },
  { value: 2, label: '高' },
  { value: 3, label: '中' },
  { value: 4, label: '低' },
]

const moduleOptions = [
  { value: 'express', label: '快递' },
  { value: 'finance', label: '财务' },
  { value: 'oa', label: 'OA审批' },
  { value: 'cardflow', label: 'CardFlow' },
  { value: 'system', label: '系统管理' },
  { value: 'workhub', label: '工作台' },
  { value: 'crm', label: '客户关系' },
  { value: 'mobile', label: '移动端' },
  { value: 'other', label: '其他' },
]

const FAB_POS_KEY = 'stotop:feedback-fab-pos'
const FAB_HEIGHT = 40
const EDGE_GAP = 8

function clampTop(value: number) {
  return Math.min(Math.max(value, EDGE_GAP), Math.max(EDGE_GAP, window.innerHeight - FAB_HEIGHT - EDGE_GAP))
}

function loadFabPos(): { side: 'left' | 'right'; top: number } | null {
  try {
    const raw = localStorage.getItem(FAB_POS_KEY)
    if (!raw) return null
    const pos = JSON.parse(raw)
    if ((pos.side === 'left' || pos.side === 'right') && typeof pos.top === 'number') return pos
  } catch { /* 存档损坏则回到默认位置 */ }
  return null
}

const savedPos = loadFabPos()
const side = ref<'left' | 'right'>(savedPos?.side ?? 'right')
const fabTop = ref(clampTop(savedPos?.top ?? window.innerHeight - FAB_HEIGHT - 26))
const dragging = ref(false)
const dragPos = reactive({ x: 0, y: 0 })
let pressing = false
let suppressClick = false
let pressX = 0
let pressY = 0
let grabDx = 0
let grabDy = 0
let fabWidth = 0

const fabStyle = computed(() =>
  dragging.value
    ? { left: `${dragPos.x}px`, top: `${dragPos.y}px`, right: 'auto' }
    : { top: `${fabTop.value}px` },
)

function onPointerDown(e: PointerEvent) {
  if (e.pointerType === 'mouse' && e.button !== 0) return
  const el = e.currentTarget as HTMLElement
  const rect = el.getBoundingClientRect()
  pressing = true
  suppressClick = false
  pressX = e.clientX
  pressY = e.clientY
  grabDx = e.clientX - rect.left
  grabDy = e.clientY - rect.top
  fabWidth = rect.width
  el.setPointerCapture(e.pointerId)
}

function onPointerMove(e: PointerEvent) {
  if (!pressing) return
  if (!dragging.value) {
    if (Math.hypot(e.clientX - pressX, e.clientY - pressY) < 5) return
    dragging.value = true
  }
  dragPos.x = e.clientX - grabDx
  dragPos.y = e.clientY - grabDy
}

function onPointerUp() {
  if (!pressing) return
  pressing = false
  if (!dragging.value) return
  dragging.value = false
  suppressClick = true
  side.value = dragPos.x + fabWidth / 2 < window.innerWidth / 2 ? 'left' : 'right'
  fabTop.value = clampTop(dragPos.y)
  try {
    localStorage.setItem(FAB_POS_KEY, JSON.stringify({ side: side.value, top: fabTop.value }))
  } catch { /* 隐私模式等写入失败可忽略 */ }
}

function onPointerCancel() {
  pressing = false
  dragging.value = false
}

const onWindowResize = () => {
  fabTop.value = clampTop(fabTop.value)
}
onMounted(() => window.addEventListener('resize', onWindowResize))
onBeforeUnmount(() => window.removeEventListener('resize', onWindowResize))

const visible = ref(false)
const submitting = ref(false)
const formRef = ref<FormInstance>()
const feedbackModalStyle = { top: '24px' }
const feedbackModalBodyStyle = {
  maxHeight: 'calc(100vh - 190px)',
  overflowY: 'auto',
} as const
const form = reactive({
  title: '',
  type: 1,
  module: 'system',
  severity: 3,
  description: '',
  actualResult: '',
  attachmentLinks: '',
})

const rules: any = {
  title: [{ required: true, message: '请输入反馈标题', trigger: 'blur' }],
  type: [{ required: true, message: '请选择类型', trigger: 'change' }],
  module: [{ required: true, message: '请选择模块', trigger: 'change' }],
  severity: [{ required: true, message: '请选择严重程度', trigger: 'change' }],
}

function open() {
  if (suppressClick) {
    suppressClick = false
    return
  }
  form.title = ''
  form.type = 1
  form.module = inferModule()
  form.severity = 3
  form.description = ''
  form.actualResult = ''
  form.attachmentLinks = ''
  visible.value = true
}

function inferModule() {
  const segment = window.location.pathname.split('/')[1]
  return moduleOptions.some(item => item.value === segment) ? segment : 'system'
}

async function submit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  submitting.value = true
  try {
    await createFeedback({
      ...form,
      pageUrl: window.location.href,
      clientInfo: navigator.userAgent,
    })
    message.success('反馈已提交')
    visible.value = false
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped lang="scss">
.feedback-fab {
  position: fixed;
  z-index: 900;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 40px;
  padding: 0 14px;
  border: 1px solid var(--color-primary);
  color: #fff;
  background: var(--color-primary);
  box-shadow: 0 8px 22px var(--color-primary-border);
  cursor: pointer;
  touch-action: none;
  user-select: none;
  -webkit-user-select: none;
  // 平时收进屏幕边缘只露出图标，避免遮挡分页等内容
  opacity: 0.65;
  transition: transform 0.2s ease, opacity 0.2s ease;

  &.side-right {
    right: 0;
    border-radius: 8px 0 0 8px;
    transform: translateX(calc(100% - 30px));
  }

  &.side-left {
    left: 0;
    border-radius: 0 8px 8px 0;
    transform: translateX(calc(-100% + 30px));
  }

  &:hover,
  &:focus-visible {
    transform: translateX(0);
    opacity: 1;
  }

  &.is-dragging {
    transform: none;
    opacity: 1;
    transition: none;
    border-radius: 8px;
    cursor: grabbing;
  }
}

.quick-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0 12px;
}

@media (max-width: 768px) {
  .quick-grid {
    grid-template-columns: 1fr;
  }
}
</style>
