<template>
  <div class="page-submit">
    <van-nav-bar
      title="提交卡片"
      left-arrow
      @click-left="$router.back()"
    />

    <!-- 加载中 -->
    <div v-if="loading" class="loading-wrap">
      <van-loading size="24">加载表单...</van-loading>
    </div>

    <!-- 加载失败 -->
    <van-empty v-else-if="loadError" description="表单加载失败" class="error-wrap">
      <van-button size="small" type="primary" @click="loadSchema">重试</van-button>
    </van-empty>

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
  showToast,
  showDialog,
} from 'vant'
import { get, post } from '@/api/request'
import MobileCardForm from '../components/MobileCardForm.vue'
import type { FieldSchema } from '../components/MobileCardForm.vue'
import { useAuthStore } from '../stores/auth'

defineOptions({ name: 'MobileSubmit' })

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const defId = computed(() => route.params.defId as string)
const userId = computed(() => authStore.user?.id || 0)

// 状态
const loading = ref(false)
const loadError = ref(false)
const submitting = ref(false)
const formSchema = ref<FieldSchema[]>([])
const formData = ref<Record<string, any>>({})
const cardFormRef = ref<InstanceType<typeof MobileCardForm> | null>(null)

// --- 草稿管理 ---
const DRAFT_PREFIX = 'stotop_draft_'
const MAX_DRAFTS = 3

function getDraftKey() {
  return `${DRAFT_PREFIX}${defId.value}_${userId.value}`
}

function saveDraft() {
  if (!formSchema.value.length) return
  const key = getDraftKey()
  const draftData = {
    formData: formData.value,
    savedAt: Date.now(),
    defId: defId.value,
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

// --- 加载 Schema ---
async function loadSchema() {
  loading.value = true
  loadError.value = false
  try {
    const res = await get<{ fields: FieldSchema[] }>(`/cardflow/definitions/${defId.value}/schema`)
    formSchema.value = res.fields || res as any
    // 初始化空表单数据
    const initData: Record<string, any> = {}
    for (const field of formSchema.value) {
      if (field.type === 'checkbox') {
        initData[field.name] = []
      } else if (field.type === 'image') {
        initData[field.name] = []
      } else if (field.type === 'table') {
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
    await post('/cardflow/cards', {
      definitionId: defId.value,
      formData: formData.value,
    })
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
  loadSchema()
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
