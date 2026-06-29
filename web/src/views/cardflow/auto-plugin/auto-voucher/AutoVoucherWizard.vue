<!-- AutoVoucher 规则配置向导 — 主页面 -->
<template>
  <div class="auto-voucher-wizard">
    <!-- 规则不存在时显示友好空状态 -->
    <div v-if="wizard.ruleNotFound.value" class="rule-not-found">
      <a-empty description="">
        <template #description>
          <div class="not-found-text">
            <p>当前规则不存在或已被删除</p>
            <p class="sub-text">可能是规则已被移除，或不属于当前组织</p>
          </div>
        </template>
        <a-button type="primary" @click="goBack">返回规则列表</a-button>
      </a-empty>
    </div>

    <!-- 正常向导内容 -->
    <template v-else>
    <!-- 头部：返回 + 选择器 + 操作按钮 -->
    <div class="wizard-header">
      <div class="header-left">
        <a-button type="text" @click="goBack">
          <ArrowLeftOutlined /> 返回
        </a-button>
        <!-- 规则名称 -->
        <a-input
          v-model:value="wizard.store.formData.ruleName"
          placeholder="规则名称"
          style="width: 220px;"
        />
      </div>
      <div class="header-right">
        <!-- 批次选择器 -->
        <a-select
          v-model:value="wizard.currentBatchId.value"
          placeholder="选择批次"
          style="min-width: 260px; max-width: 400px; flex: 1;"
          show-search
          :filter-option="filterBatchOption"
          :loading="loadingBatches"
          @change="onBatchChange"
        >
          <a-select-option v-for="b in batchList" :key="b.id" :value="b.id">
            #{{ b.id }} - {{ b.fileName || b.batchNo }} ({{ b.totalRows }}行)
          </a-select-option>
        </a-select>
        <!-- 启用/禁用（仅编辑模式） -->
        <a-switch
          v-if="wizard.currentRuleId.value"
          :checked="wizard.store.formData.status === 1"
          @change="(checked: string | number | boolean) => wizard.store.formData.status = checked ? 1 : 0"
          checked-children="启用"
          un-checked-children="禁用"
        />
        <a-tag v-if="wizard.store.isDirty" color="orange">未保存</a-tag>
        <a-button @click="handleDryRun">
          <ThunderboltOutlined /> 预演
        </a-button>
        <a-button @click="settingsDrawerOpen = true">
          <SettingOutlined /> 全局设置
        </a-button>
        <!-- 更多操作下拉菜单 -->
        <a-dropdown>
          <a-button>
            <EllipsisOutlined />
          </a-button>
          <template #overlay>
            <a-menu>
              <a-menu-item key="json" @click="handleExportJson">
                <CodeOutlined /> JSON
              </a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
        <a-button type="primary" :loading="saving" @click="handleSave">
          <SaveOutlined /> 保存
        </a-button>
      </div>
    </div>

    <!-- 三栏并存主体 -->
    <div class="main-content">
      <!-- 左栏：规则组树（始终可见） -->
      <div class="left-panel">
        <RuleGroupTree v-if="wizard.currentRuleId.value" />
        <a-empty v-else description="请先选择规则" />
      </div>

      <!-- 中栏：字段覆盖率 -->
      <div class="center-panel">
        <FieldValuesPanel
          v-if="wizard.currentBatchId.value"
          :analysis="wizard.fieldAnalysis.value"
          :loading="wizard.fieldAnalysisLoading.value"
          :existing-groups="existingGroupsForMenu"
          :staging-table="wizard.store.formData.stagingTable"
          :batch-id="wizard.currentBatchId.value"
          @add-rule-for-value="onAddRuleForValue"
          @select-matched="onSelectMatched"
        />
        <div v-else-if="wizard.currentRuleId.value" class="center-guide">
          <a-empty description="选择批次后查看字段覆盖率分析" />
        </div>
        <div v-else class="center-guide">
          <a-empty description="请先选择规则和批次" />
        </div>
      </div>

      <!-- 右栏：规则配置面板（始终可见） -->
      <div class="right-panel">
        <RuleConfigPanel
          v-if="wizard.currentRuleId.value"
          :fields="stagingFields"
          :batch-values="batchValuesForHints"
          :sample-row="sampleRow"
          @dry-run="handleDryRun"
        />
        <a-empty v-else description="请先选择规则" />
      </div>
    </div>

    <!-- 底部统计条 -->
    <div class="kpi-bar">
      <div class="kpi-item">
        <span class="kpi-value">{{ ruleGroupCount }}</span>
        <span class="kpi-label">规则组数</span>
      </div>
      <a-divider type="vertical" />
      <div class="kpi-item">
        <span class="kpi-value">{{ entryLineCount }}</span>
        <span class="kpi-label">分录行数</span>
      </div>
      <a-divider type="vertical" />
      <div class="kpi-item">
        <span class="kpi-value" :class="coverageClass">{{ wizard.coverageStats.value.totalCoverage }}%</span>
        <span class="kpi-label">总覆盖率</span>
      </div>
    </div>

    <!-- 全局配置抽屉 -->
    <GlobalSettingsDrawer
      v-model:open="settingsDrawerOpen"
      :fields="stagingFields"
      :account-sets="wizard.store.accountSets"
    />

    <!-- DryRun 抽屉 -->
    <DryRunPanel
      v-model:open="dryRunDrawerOpen"
      :rule-id="wizard.currentRuleId.value ?? undefined"
    />

    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import { useRouter, useRoute } from 'vue-router'
import { ArrowLeftOutlined, SettingOutlined, ThunderboltOutlined, CodeOutlined, SaveOutlined, EllipsisOutlined } from '@ant-design/icons-vue'
import { message, Modal } from 'ant-design-vue'
import { getImportBatches } from '@/api/cardflow'
import { useAutoVoucherWizard } from './composables/useAutoVoucherWizard'
import FieldValuesPanel from './components/FieldValuesPanel.vue'
import RuleGroupTree from './components/RuleGroupTree.vue'
import RuleConfigPanel from './components/RuleConfigPanel.vue'
import GlobalSettingsDrawer from './components/GlobalSettingsDrawer.vue'
import DryRunPanel from './components/DryRunPanel.vue'
import { downloadBlob } from '@/utils/download'

const router = useRouter()
const route = useRoute()
const wizard = useAutoVoucherWizard()

// ==================== 批次列表 ====================
const batchList = ref<any[]>([])
const loadingBatches = ref(false)

async function loadBatchList() {
  const stagingTable = wizard.store.formData.stagingTable
  loadingBatches.value = true
  try {
    const params: any = {
      pageSize: 10,
      page: 1,
      status: 2,
      noDateFilter: true,
    }
    // 有 stagingTable 时按表过滤，否则加载所有已完成批次
    if (stagingTable) {
      params.targetTable = stagingTable
    }
    const res: any = await getImportBatches(params)
    batchList.value = res?.items || res || []
  } catch {
    batchList.value = []
  } finally {
    loadingBatches.value = false
  }
}

function filterBatchOption(input: string, option: any) {
  const text = `#${option.value} - ${option.children}`
  return text.toLowerCase().includes(input.toLowerCase())
}

// ==================== 暂存表字段 ====================
const stagingFields = computed(() => wizard.store.currentStagingFields)

// ==================== 批次中存在的值（用于智能提示） ====================
const batchValuesForHints = computed(() => {
  const analysis = wizard.fieldAnalysis.value
  if (!analysis) return []
  // 收集所有层的未匹配值作为提示
  const values: string[] = []
  for (const layer of analysis.layers) {
    for (const v of layer.unmatchedValues) {
      values.push(v.value)
    }
  }
  return [...new Set(values)]
})

// ==================== 预览用第一行数据 ====================
const sampleRow = computed(() => {
  const analysis = wizard.fieldAnalysis.value
  if (!analysis?.unmatchedRowSample?.length) return null
  return analysis.unmatchedRowSample[0]
})

// ==================== 统计指标 ====================
const ruleGroupCount = computed(() => wizard.store.formData.ruleGroups?.length ?? 0)
const entryLineCount = computed(() => {
  const groups = wizard.store.formData.ruleGroups ?? []
  return groups.reduce((sum, g) => sum + (g.lines?.length ?? 0), 0)
})

const coverageClass = computed(() => {
  const pct = wizard.coverageStats.value.totalCoverage
  if (pct >= 90) return 'good'
  if (pct >= 60) return 'medium'
  return 'low'
})

// ==================== 事件处理 ====================
const settingsDrawerOpen = ref(false)
const dryRunDrawerOpen = ref(false)
const saving = ref(false)

// ==================== 现有规则组列表（供未匹配值菜单使用） ====================
const existingGroupsForMenu = computed(() =>
  (wizard.store.formData.ruleGroups ?? []).map(g => ({ id: g.id, name: g.name })),
)

// 响应式监听 stagingTable 变化，自动加载批次列表（安全网：处理非规则切换场景）
watch(() => wizard.store.formData.stagingTable, (newTable) => {
  if (newTable) {
    loadBatchList()
  } else {
    batchList.value = []
  }
}, { immediate: true })

async function onBatchChange(value: SelectValue) {
  const batchId = value as number | undefined
  if (batchId) {
    await wizard.loadFieldAnalysis(batchId, wizard.currentRuleId.value ?? undefined)
  }
}

function onAddRuleForValue(payload: { layerName: string; value: string; count: number; target: string }) {
  const { layerName, value, count, target } = payload
  const labelMap: Record<string, string> = {
    Layer1: '精确编码',
    Layer2: '费用类别',
    Layer3: '摘要关键词',
  }
  const fieldLabel = labelMap[layerName] ?? layerName
  if (target === 'new') {
    const newGroup = wizard.store.addGroup(value)
    wizard.store.addMatchValueToGroup(newGroup.id, layerName, value)
    wizard.store.selectedGroupId = newGroup.id
    wizard.store.selectedLineId = null
    message.success(`已新建规则组「${value}」，${fieldLabel} 匹配已加入（${count}行）`)
    return
  }
  const groupId = target
  const group = wizard.store.formData.ruleGroups.find(g => g.id === groupId)
  if (!group) {
    message.error('目标规则组不存在')
    return
  }
  const ok = wizard.store.addMatchValueToGroup(groupId, layerName, value)
  wizard.store.selectedGroupId = groupId
  wizard.store.selectedLineId = null
  if (ok) {
    message.success(`已将「${value}」加入「${group.name}」的 ${fieldLabel} 匹配（${count}行）`)
  } else {
    message.info(`「${value}」已在「${group.name}」的 ${fieldLabel} 匹配列表中`)
  }
}

function onSelectMatched(payload: { layerName: string; value: string }) {
  // 1. 找到匹配的规则组
  const analysis = wizard.fieldAnalysis.value
  if (!analysis) return
  const layer = analysis.layers.find((l: any) => l.layerName === payload.layerName)
  if (!layer) return
  const matchedItem = layer.matchedValues.find((v: any) => v.value === payload.value)
  const matchedGroupName = matchedItem?.matchedGroup
  if (!matchedGroupName) return

  // 2. 切换到对应规则组
  const group = wizard.store.formData.ruleGroups.find(g => g.name === matchedGroupName)
  if (group) {
    wizard.store.selectedGroupId = group.id
    wizard.store.selectedLineId = null
    // 3. 如果有 matchedLineId 信息，自动展开对应分录行
    if (matchedItem?.matchedLineId) {
      wizard.store.selectedLineId = matchedItem.matchedLineId
    }
  }
}

async function handleSave() {
  // 防止空配置写入：检查关键数据是否已加载
  if (!wizard.store.formData.ruleGroups?.length) {
    message.warning('规则组数据未加载完成或为空，无法保存')
    return
  }
  if (!wizard.store.formData.stagingTable) {
    message.warning('请先配置暂存表')
    return
  }
  saving.value = true
  try {
    await wizard.saveRule()
  } catch {
    // 错误已在 composable 中处理
  } finally {
    saving.value = false
  }
}

function handleCancel() {
  if (wizard.store.isDirty) {
    Modal.confirm({
      title: '有未保存的修改',
      content: '确认放弃修改？',
      okText: '确认放弃',
      cancelText: '继续编辑',
      onOk: () => wizard.$reset(),
    })
  } else {
    goBack()
  }
}

function handleDryRun() {
  dryRunDrawerOpen.value = true
}

function handleExportJson() {
  const json = JSON.stringify(wizard.store.formData, null, 2)
  const blob = new Blob([json], { type: 'application/json' })
  downloadBlob(blob, `rule_${wizard.currentRuleId.value ?? 'new'}.json`)
}

function goBack() {
  router.push('/cardflow/auto-plugin/AutoVoucher/rules')
}

// ==================== 未保存变更提示 ====================
function beforeUnloadHandler(e: BeforeUnloadEvent) {
  if (wizard.store.isDirty) {
    e.preventDefault()
    e.returnValue = ''
  }
}

// ==================== 初始化 ====================
onMounted(async () => {
  window.addEventListener('beforeunload', beforeUnloadHandler)
  await Promise.all([
    wizard.store.loadStagingTables(),
    wizard.store.loadAccountSets(),
  ])
  // 路由参数：必须有 id
  const routeRuleId = route.params.id ? Number(route.params.id) : null
  if (!routeRuleId || isNaN(routeRuleId)) {
    message.warning('缺少规则 ID')
    goBack()
    return
  }
  wizard.currentRuleId.value = routeRuleId
  await wizard.loadRule(routeRuleId)
  // 规则不存在时不继续加载批次
  if (!wizard.ruleNotFound.value) {
    await loadBatchList()
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('beforeunload', beforeUnloadHandler)
  wizard.$reset()
})
</script>

<style lang="scss" scoped>
.auto-voucher-wizard {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 16px;
  gap: 12px;
  overflow: hidden;
}

.rule-not-found {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  background: var(--bg-card);
  border-radius: 8px;
  border: 1px solid var(--border);

  .not-found-text {
    p {
      margin: 0;
      font-size: 15px;
      color: var(--text-2);
    }
    .sub-text {
      font-size: 13px;
      color: var(--text-3);
      margin-top: 4px;
    }
  }
}

.wizard-header {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;
  width: 100%;
  min-width: 0;
  box-sizing: border-box;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.header-right {
  margin-left: auto;
  display: flex;
  gap: 8px;
  align-items: center;
  flex: 1;
  min-width: 0;
  overflow-x: auto;
  overflow-y: hidden;
  white-space: nowrap;

  > * {
    flex-shrink: 0;
  }
}

.main-content {
  display: flex;
  gap: 12px;
  flex: 1;
  min-height: 0;
}

.left-panel {
  width: 360px;
  flex-shrink: 0;
  display: flex;
}

.center-panel {
  width: 320px;
  flex-shrink: 0;
  display: flex;
}

.right-panel {
  flex: 1;
  min-width: 0;
  display: flex;
}

.center-guide {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  background: var(--bg-card);
  border-radius: 8px;
  border: 1px solid var(--border);
}

.kpi-bar {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 10px 20px;
  background: var(--bg-card);
  border-radius: 8px;
  border: 1px solid var(--border);
  flex-shrink: 0;
}

.kpi-item {
  display: flex;
  align-items: center;
  gap: 6px;
}

.kpi-label {
  font-size: 12px;
  color: var(--text-3);
}

.kpi-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-1);

  &.good { color: var(--color-success-text); }
  &.medium { color: var(--color-warning-text); }
  &.low { color: var(--color-danger-text); }
}
</style>
