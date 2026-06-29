<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { CreateCardRequest, SchemaFieldDefinition } from '@/types/cardflow'
import {
  NavBar as VanNavBar,
  Loading as VanLoading,
  ActionBar as VanActionBar,
  ActionBarButton as VanActionBarButton,
  CellGroup as VanCellGroup,
  Cell as VanCell,
} from 'vant'
import { showToast, showConfirmDialog } from 'vant'
import 'vant/es/nav-bar/style'
import 'vant/es/loading/style'
import 'vant/es/action-bar/style'
import 'vant/es/action-bar-button/style'
import 'vant/es/cell-group/style'
import 'vant/es/cell/style'
import 'vant/es/toast/style'
import 'vant/es/dialog/style'
import SchemaRenderer from '@/components/cardflow/SchemaRenderer.vue'
import CardRelationPicker from '@/components/cardflow/CardRelationPicker.vue'
import { getFlowDefinition, getFlowDraftVersion, getFlowVersions, getFlowVersionDetail, createCard, updateCard, submitCard } from '@/api/cardflow'
import { useOrgContextStore } from '@/stores/orgContext'
import { parseCardSchemaFields } from '@/utils/cardflowSchema'

const route = useRoute()
const router = useRouter()
const orgContextStore = useOrgContextStore()
const flowId = computed(() => Number(route.params.flowId))

const loading = ref(true)
const submitting = ref(false)
const flowName = ref('')
const cardNumber = ref('（自动生成）')
const schema = ref<SchemaFieldDefinition[]>([])
const formData = ref<Record<string, any>>({})
const cardId = ref<number | null>(null)
const concurrencyStamp = ref<string | null>(null)
const showRelationPicker = ref(false)

function readQueryString(key: string): string | null {
  const value = route.query[key]
  if (Array.isArray(value)) return value[0] ?? null
  return value ?? null
}

function readQueryNumber(key: string): number | null {
  const value = readQueryString(key)
  if (!value) return null
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

function buildCreateCardPayload(dataJson: string): CreateCardRequest {
  const orgId = orgContextStore.currentOrgId
  if (!orgId) throw new Error('请选择组织后再发起流程')

  return {
    flowDefinitionId: flowId.value,
    dataJson,
    orgId,
    sourceModule: readQueryString('sourceModule'),
    sourceType: readQueryString('sourceType'),
    sourceId: readQueryNumber('sourceId'),
    returnUrl: readQueryString('returnUrl'),
    initialDataJson: readQueryString('initialDataJson'),
    sourceTitle: readQueryString('sourceTitle') || flowName.value || null,
  }
}

async function loadFlowSchema() {
  loading.value = true
  try {
    if (!orgContextStore.currentOrgId) {
      await orgContextStore.fetchCurrentContext()
    }

    const def = await getFlowDefinition(flowId.value)
    flowName.value = def.flowName

    let version: any = null

    // 1) 尝试读取草稿版本
    try {
      version = await getFlowDraftVersion(flowId.value)
    } catch {
      version = null
    }

    // 2) 如无草稿，回退到当前发布版本
    if (!version) {
      const versions = await getFlowVersions(flowId.value)
      const current = versions.find((v: any) => v.isCurrentVersion) || versions[0]
      if (current) {
        version = await getFlowVersionDetail(flowId.value, current.id)
      }
    }

    if (version?.cardSchemaJson) {
      schema.value = parseCardSchemaFields(version.cardSchemaJson)
    } else {
      schema.value = []
    }

    const initialDataJson = readQueryString('initialDataJson')
    if (initialDataJson) {
      try {
        const parsed = JSON.parse(initialDataJson)
        if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
          formData.value = { ...parsed, ...formData.value }
        }
      } catch {
        // 忽略非法初始数据，后端仍会做校验和审计保留。
      }
    }
  } catch {
    showToast({ message: '加载流程失败', type: 'fail' })
  } finally {
    loading.value = false
  }
}

async function handleSaveDraft() {
  submitting.value = true
  try {
    const dataJson = JSON.stringify(formData.value)
    if (cardId.value) {
      const res = await updateCard(cardId.value, { dataJson, concurrencyStamp: concurrencyStamp.value || undefined })
      concurrencyStamp.value = res.concurrencyStamp
      showToast({ message: '草稿已保存', type: 'success' })
    } else {
      const res = await createCard(buildCreateCardPayload(dataJson))
      cardId.value = res.id
      cardNumber.value = res.cardNumber || '（自动生成）'
      concurrencyStamp.value = res.concurrencyStamp
      showToast({ message: '草稿已创建', type: 'success' })
    }
  } catch {
    showToast({ message: '保存失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

async function handleSubmit() {
  try {
    await showConfirmDialog({ title: '确认提交', message: '提交后将进入审批流程，确定继续？' })
  } catch {
    return // 用户取消
  }

  submitting.value = true
  try {
    // 先保存
    const dataJson = JSON.stringify(formData.value)
    if (!cardId.value) {
      const res = await createCard(buildCreateCardPayload(dataJson))
      cardId.value = res.id
      concurrencyStamp.value = res.concurrencyStamp
    } else {
      await updateCard(cardId.value, { dataJson, concurrencyStamp: concurrencyStamp.value || undefined })
    }

    // 提交
    const result = await submitCard(cardId.value!)
    if (result.success) {
      showToast({ message: '提交成功', type: 'success' })
      router.back()
    } else {
      showToast({ message: result.message || '提交失败', type: 'fail' })
    }
  } catch {
    showToast({ message: '提交失败', type: 'fail' })
  } finally {
    submitting.value = false
  }
}

function onClickLeft() {
  router.back()
}

onMounted(() => {
  loadFlowSchema()
})
</script>

<template>
  <div class="card-fill-form">
    <VanNavBar :title="flowName || '填写卡片'" left-arrow @click-left="onClickLeft" fixed placeholder />

    <div v-if="loading" class="loading-wrap">
      <VanLoading size="36px" vertical>加载中...</VanLoading>
    </div>

    <template v-else>
      <!-- 流程信息头 -->
      <VanCellGroup inset class="info-header">
        <VanCell title="流程" :value="flowName" />
        <VanCell title="编号" :value="cardNumber" />
      </VanCellGroup>

      <!-- 动态表单 -->
      <div class="form-section">
        <SchemaRenderer :schema="schema" :model-value="formData" mode="edit" platform="mobile" @update:model-value="formData = $event" />
      </div>

      <!-- 关联卡片 -->
      <VanCellGroup inset class="relation-section" v-if="cardId">
        <VanCell title="关联卡片" is-link @click="showRelationPicker = true" value="选择" />
      </VanCellGroup>

      <!-- 底部安全间距 -->
      <div class="bottom-spacer" />

      <!-- 底部操作栏 -->
      <VanActionBar>
        <VanActionBarButton type="warning" text="保存草稿" :loading="submitting" @click="handleSaveDraft" />
        <VanActionBarButton type="danger" text="提交" :loading="submitting" @click="handleSubmit" />
      </VanActionBar>
    </template>

    <!-- 关联选择器 -->
    <CardRelationPicker
      v-if="cardId"
      :card-id="cardId"
      v-model:show="showRelationPicker"
      @select="() => { /* handle relation select */ }"
    />
  </div>
</template>

<style scoped>
.card-fill-form {
  min-height: 100vh;
  background: #f7f8fa;
  padding-bottom: env(safe-area-inset-bottom);
}
.loading-wrap {
  display: flex;
  justify-content: center;
  padding-top: 30vh;
}
.info-header {
  margin-top: 12px !important;
}
.form-section {
  margin-top: 12px;
}
.relation-section {
  margin-top: 12px !important;
}
.bottom-spacer {
  height: 60px;
}
</style>
