<template>
  <div class="page-submit">
    <van-nav-bar
      title="提交卡片"
      left-arrow
      @click-left="$router.back()"
    />

    <!-- 加载中 -->
    <div v-if="loading" class="loading-wrap">
      <van-loading size="24">{{ selectedFlowId ? '加载表单...' : '加载流程...' }}</van-loading>
    </div>

    <!-- 加载失败 -->
    <van-empty v-else-if="loadError" description="加载失败" class="error-wrap">
      <van-button size="small" type="primary" @click="retry">重试</van-button>
    </van-empty>

    <!-- 流程选择（未指定流程时先选） -->
    <template v-else-if="!selectedFlowId">
      <van-cell-group v-if="flows.length > 0" inset class="flow-picker">
        <van-cell
          v-for="f in flows"
          :key="f.id"
          :title="f.flowName"
          :label="f.description || f.flowCode"
          is-link
          @click="chooseFlow(f.id)"
        />
      </van-cell-group>
      <van-empty v-else description="当前组织暂无可发起的流程" />
    </template>

    <!-- 表单内容 -->
    <template v-else>
      <div class="form-content">
        <MobileCardForm
          ref="cardFormRef"
          :schema="formSchema"
          v-model="formData"
          :readonly="submitting"
        />
      </div>

      <!-- 底部提交按钮 -->
      <div class="submit-footer">
        <van-button
          type="primary"
          block
          round
          :loading="submitting"
          loading-text="提交中..."
          @click="handleSubmit"
        >
          提交
        </van-button>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  NavBar as VanNavBar,
  Loading as VanLoading,
  Empty as VanEmpty,
  Button as VanButton,
  CellGroup as VanCellGroup,
  Cell as VanCell,
  showToast,
  showDialog,
} from 'vant'
import { get, post, put } from '@/api/request'
import { parseCardSchemaFields } from '@/utils/cardflowSchema'
import type {
  AvailableFlowDto,
  FlowVersionDto,
  FlowVersionDetailDto,
  CardDetailDto,
  CardOperationResult,
  SchemaFieldDefinition,
} from '@/types/cardflow'
import MobileCardForm from '../components/MobileCardForm.vue'
import type { FieldSchema } from '../components/MobileCardForm.vue'
import { useAuthStore } from '../stores/auth'

defineOptions({ name: 'MobileSubmit' })

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const userId = computed(() => authStore.user?.id || 0)
const orgId = computed(() => authStore.currentOrgId || authStore.currentOrg?.id || 0)

// 状态
const loading = ref(false)
const loadError = ref(false)
const submitting = ref(false)
// 路由带有效 defId 时直入表单，否则先展示可发起流程列表
const selectedFlowId = ref<number>(Number(route.params.defId) || 0)
const flows = ref<AvailableFlowDto[]>([])
// 提交时先建草稿卡；提交失败后重试复用同一草稿，避免堆积
const draftCardId = ref<number | null>(null)
const formSchema = ref<FieldSchema[]>([])
const formData = ref<Record<string, any>>({})
const cardFormRef = ref<InstanceType<typeof MobileCardForm> | null>(null)

// --- 草稿管理 ---
const DRAFT_PREFIX = 'stotop_draft_'
const MAX_DRAFTS = 3

function getDraftKey() {
  return `${DRAFT_PREFIX}${selectedFlowId.value}_${userId.value}`
}

function saveDraft() {
  if (!formSchema.value.length || !selectedFlowId.value) return
  const key = getDraftKey()
  const draftData = {
    formData: formData.value,
    savedAt: Date.now(),
    defId: selectedFlowId.value,
  }
  localStorage.setItem(key, JSON.stringify(draftData))
  trimDrafts()
}

function trimDrafts() {
  // 收集所有草稿 key
  const draftKeys: Array<{ key: string; savedAt: number }> = []
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i)
    if (key && key.startsWith(DRAFT_PREFIX)) {
      try {
        const data = JSON.parse(localStorage.getItem(key) || '{}')
        draftKeys.push({ key, savedAt: data.savedAt || 0 })
      } catch { /* ignore */ }
    }
  }

  // 超过上限删除最旧的
  if (draftKeys.length > MAX_DRAFTS) {
    draftKeys.sort((a, b) => a.savedAt - b.savedAt)
    const toDelete = draftKeys.slice(0, draftKeys.length - MAX_DRAFTS)
    toDelete.forEach(d => localStorage.removeItem(d.key))
  }
}

function loadDraft(): Record<string, any> | null {
  const key = getDraftKey()
  const raw = localStorage.getItem(key)
  if (!raw) return null
  try {
    const data = JSON.parse(raw)
    return data.formData || null
  } catch {
    return null
  }
}

function clearDraft() {
  localStorage.removeItem(getDraftKey())
}

// --- 自动保存定时器 ---
let autoSaveTimer: ReturnType<typeof setInterval> | null = null

function startAutoSave() {
  stopAutoSave()
  autoSaveTimer = setInterval(() => {
    saveDraft()
  }, 10000) // 每 10 秒自动保存
}

function stopAutoSave() {
  if (autoSaveTimer) {
    clearInterval(autoSaveTimer)
    autoSaveTimer = null
  }
}

// --- Schema 转换 ---

// file/cardRef/voucherRef 等复杂控件本页不支持渲染，直接跳过；其余降级为可输入类型
function toFieldSchema(fields: SchemaFieldDefinition[]): FieldSchema[] {
  return fields
    .filter(f => !['file', 'cardRef', 'voucherRef'].includes(f.type))
    .map(f => ({
      name: f.key,
      label: f.label,
      type: f.type === 'money' ? 'number' as const
        : f.type === 'enum' ? 'select' as const
        : f.type === 'date' ? 'date' as const
        : 'text' as const,
      required: f.required,
      placeholder: f.placeholder,
      options: (f.options || []).map(o => ({ label: o, value: o })),
    }))
}

// --- 加载可发起流程列表 ---
async function loadFlows() {
  loading.value = true
  loadError.value = false
  try {
    flows.value = (await get<AvailableFlowDto[]>('/cardflow/cards/available-flows', { orgId: orgId.value })) || []
  } catch (e) {
    console.error('[Submit] loadFlows failed:', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

function chooseFlow(id: number) {
  selectedFlowId.value = id
  loadSchema()
}

// --- 加载 Schema ---
// 后端无 definitions/{id}/schema 端点：经 versions 列表定位当前版本，再取版本详情里的 cardSchemaJson
async function loadSchema() {
  loading.value = true
  loadError.value = false
  try {
    const versions = (await get<FlowVersionDto[]>(`/cardflow/definitions/${selectedFlowId.value}/versions`)) || []
    const current = versions.find(v => v.isCurrentVersion) || versions.find(v => v.status === 'published')
    if (!current) throw new Error('该流程尚无已发布版本')
    const detail = await get<FlowVersionDetailDto>(
      `/cardflow/definitions/${selectedFlowId.value}/versions/${current.id}`
    )
    formSchema.value = toFieldSchema(parseCardSchemaFields(detail.cardSchemaJson))

    // 初始化空表单数据
    const initData: Record<string, any> = {}
    for (const field of formSchema.value) {
      if (field.type === 'checkbox' || field.type === 'image' || field.type === 'table') {
        initData[field.name] = []
      } else {
        initData[field.name] = ''
      }
    }
    formData.value = initData

    // 检查草稿恢复
    const draft = loadDraft()
    if (draft) {
      try {
        await showDialog({
          title: '提示',
          message: '检测到上次未提交的草稿，是否恢复？',
          confirmButtonText: '恢复',
          cancelButtonText: '放弃',
          showCancelButton: true,
        })
        // 确认恢复
        formData.value = { ...initData, ...draft }
      } catch {
        // 放弃草稿
        clearDraft()
      }
    }

    // 启动自动保存
    startAutoSave()
  } catch (e) {
    console.error('[Submit] loadSchema failed:', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

function retry() {
  if (selectedFlowId.value > 0) loadSchema()
  else loadFlows()
}

// --- 提交 ---
async function handleSubmit() {
  try {
    // 表单校验
    await cardFormRef.value?.validate()
  } catch {
    showToast('请完善表单必填项')
    return
  }

  submitting.value = true
  try {
    const dataJson = JSON.stringify(formData.value)
    if (!draftCardId.value) {
      const created = await post<CardDetailDto>('/cardflow/cards', {
        flowDefinitionId: selectedFlowId.value,
        orgId: orgId.value,
        dataJson,
      })
      draftCardId.value = created.id
    } else {
      // 上次提交失败留下的草稿：更新内容后重试
      await put<CardDetailDto>(`/cardflow/cards/${draftCardId.value}`, { dataJson })
    }

    const result = await post<CardOperationResult>(`/cardflow/cards/${draftCardId.value}/submit`)
    if (result && result.success === false) {
      showToast({ message: result.message || '提交失败', type: 'fail' })
      return
    }
    showToast({ message: '提交成功', type: 'success' })
    clearDraft()
    stopAutoSave()
    // 跳回首页
    router.replace({ name: 'MobileHome' })
  } catch (e: any) {
    showToast(e?.message || '提交失败，请重试')
  } finally {
    submitting.value = false
  }
}

// --- 生命周期 ---
onMounted(() => {
  if (selectedFlowId.value > 0) loadSchema()
  else loadFlows()
})

onUnmounted(() => {
  stopAutoSave()
  // 离开前保存一次草稿
  saveDraft()
})
</script>

<style scoped>
.page-submit {
  min-height: 100vh;
  background: #f5f5f5;
  display: flex;
  flex-direction: column;
}

.loading-wrap {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 60px 0;
}

.error-wrap {
  flex: 1;
  padding-top: 60px;
}

.flow-picker {
  margin: 12px;
}

.form-content {
  flex: 1;
  padding: 12px 0;
  padding-bottom: 80px;
  background: #fff;
  margin: 12px;
  border-radius: 8px;
}

.submit-footer {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 12px 16px;
  padding-bottom: calc(12px + env(safe-area-inset-bottom));
  background: #fff;
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.06);
}
</style>
