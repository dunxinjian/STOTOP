<template>
  <div class="auto-plugin-list">
    <PageHeader title="自动插件管理">
      <template #actions>
      </template>
    </PageHeader>

    <div class="table-card">
      <a-table
        :columns="mainColumns"
        :dataSource="autoPluginTypes"
        :loading="loading"
        rowKey="implementationType"
        bordered
        :pagination="false"
        :expandedRowKeys="expandedKeys"
        @expand="onExpand"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'displayName'">
            <strong>{{ record.displayName }}</strong>
          </template>
          <template v-else-if="column.dataIndex === 'pluginType'">
            <a-tag :color="getAutoPluginTypeColor(record.pluginType)">
              {{ normalizeAutoPluginType(record.pluginType) }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'ruleCount'">
            <a-badge
              :count="getRuleCount(record.implementationType)"
              :showZero="true"
              :numberStyle="{ backgroundColor: getRuleCount(record.implementationType) > 0 ? 'var(--color-info)' : 'var(--border)' }"
            />
          </template>
          <template v-else-if="column.dataIndex === 'description'">
            {{ record.description || '-' }}
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-button
              type="link"
              size="small"
              @click="goToRules(record as AutoPluginMetadata)"
            >独立页面</a-button>
          </template>
        </template>

        <!-- 展开行：规则子表格 -->
        <template #expandedRowRender="{ record }">
          <div class="expanded-content">
            <a-table
              :columns="ruleColumns"
              :dataSource="rulesMap[record.implementationType] ?? []"
              :loading="ruleLoadingMap[record.implementationType]"
              rowKey="id"
              size="small"
              bordered
              :pagination="false"
            >
              <template #bodyCell="{ column, record: rule }">
                <template v-if="column.dataIndex === 'ruleName'">
                  <a @click="handleEditRule(record.implementationType, record, rule as AutoPluginRuleDto)">{{ rule.ruleName }}</a>
                </template>
                <template v-else-if="column.dataIndex === 'status'">
                  <a-tag :color="rule.status === 1 ? 'success' : 'error'">
                    {{ rule.status === 1 ? '启用' : '禁用' }}
                  </a-tag>
                </template>
                <template v-else-if="column.dataIndex === 'referenceCount'">
                  <a-tag :color="rule.referenceCount > 0 ? 'processing' : 'default'">
                    {{ rule.referenceCount ?? 0 }}
                  </a-tag>
                </template>
                <template v-else-if="column.dataIndex === 'description'">
                  {{ rule.description || '-' }}
                </template>
                <template v-else-if="column.dataIndex === 'createTime'">
                  {{ rule.createTime ? rule.createTime.substring(0, 16).replace('T', ' ') : '-' }}
                </template>
                <template v-else-if="column.dataIndex === 'action'">
                  <a-button type="link" size="small" @click="handleEditRule(record.implementationType, record, rule as AutoPluginRuleDto)">编辑</a-button>
                  <a-tooltip v-if="rule.referenceCount > 0" title="已被引用，无法删除">
                    <a-button type="link" size="small" danger disabled>删除</a-button>
                  </a-tooltip>
                  <a-popconfirm
                    v-else
                    title="确定删除该规则？"
                    @confirm="handleDeleteRule(record.implementationType, rule as AutoPluginRuleDto)"
                    okText="删除"
                    cancelText="取消"
                  >
                    <a-button type="link" size="small" danger>删除</a-button>
                  </a-popconfirm>
                </template>
              </template>
            </a-table>
            <div class="add-rule-bar">
              <a-button type="dashed" size="small" @click="handleCreateRule(record.implementationType, record)">
                <PlusOutlined /> 新增规则
              </a-button>
            </div>
          </div>
        </template>
      </a-table>
    </div>

    <!-- 通用规则表单对话框 -->
    <AutoPluginRuleFormDialog
      :open="dialogVisible"
      :rule="editingRule"
      :typeCode="activeTypeCode"
      :configSchema="activeSchema"
      @update:open="dialogVisible = $event"
      @saved="onRuleSaved"
    />

    <!-- Excel 导入规则表单 -->
    <ExcelInputRuleForm
      v-model:open="excelFormOpen"
      :rule="editingRuleForExcel"
      :type-code="activeTypeCode"
      @saved="onRuleSavedForType(activeTypeCode)"
    />

    <!-- 自动凭证规则表单 -->
    <AutoVoucherRuleForm
      v-if="legacyMode && autoVoucherFormOpen"
      v-model:open="autoVoucherFormOpen"
      :rule="editingRuleForAutoVoucher"
      :type-code="activeTypeCode"
      @saved="onRuleSavedForType(activeTypeCode)"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AutoPluginRuleFormDialog from './AutoPluginRuleFormDialog.vue'
import ExcelInputRuleForm from './ExcelInputRuleForm.vue'
import AutoVoucherRuleForm from './AutoVoucherRuleForm.vue'
import {
  getAutoPluginTypes,
  getAutoPluginRulesSilent,
  deleteAutoPluginRule,
} from '@/api/cardflow'
import type { AutoPluginMetadata, AutoPluginRuleDto, ConfigFieldSchema } from '@/api/cardflow'

const router = useRouter()

// ==================== 主表格数据 ====================
const loading = ref(false)
const autoPluginTypes = ref<AutoPluginMetadata[]>([])
const expandedKeys = ref<string[]>([])

// 每个 typeCode 对应的规则列表
const rulesMap = reactive<Record<string, AutoPluginRuleDto[]>>({})
const ruleLoadingMap = reactive<Record<string, boolean>>({})

const mainColumns = [
  { title: '名称', dataIndex: 'displayName', key: 'displayName', width: 200 },
  { title: '类型', dataIndex: 'pluginType', key: 'pluginType', width: 120, align: 'center' as const },
  { title: '规则数', dataIndex: 'ruleCount', key: 'ruleCount', width: 100, align: 'center' as const },
  { title: '说明', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '操作', dataIndex: 'action', key: 'action', width: 100, align: 'center' as const },
]

const ruleColumns = [
  { title: '规则名称', dataIndex: 'ruleName', key: 'ruleName', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
  { title: '引用数', dataIndex: 'referenceCount', key: 'referenceCount', width: 80, align: 'center' as const },
  { title: '说明', dataIndex: 'description', key: 'description', width: 250, ellipsis: true },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 150, align: 'center' as const },
]

function normalizeAutoPluginType(autoPluginType: string | number): string {
  if (autoPluginType === 'Input' || (autoPluginType as any) === 0) return 'Input'
  if (autoPluginType === 'Processing' || (autoPluginType as any) === 1) return 'Processing'
  return String(autoPluginType)
}

function getAutoPluginTypeColor(autoPluginType: string | number): string {
  const t = normalizeAutoPluginType(autoPluginType)
  return t === 'Input' ? 'blue' : 'orange'
}

function getRuleCount(typeCode: string): number {
  return rulesMap[typeCode]?.length ?? 0
}

// ==================== 数据加载 ====================
async function fetchTypes() {
  loading.value = true
  try {
    const res: any = await getAutoPluginTypes({ silent: true })
    autoPluginTypes.value = res ?? []
  } catch {
    message.error('获取自动插件类型列表失败')
  } finally {
    loading.value = false
  }
}

async function fetchRulesForType(typeCode: string) {
  ruleLoadingMap[typeCode] = true
  try {
    const res: any = await getAutoPluginRulesSilent(typeCode)
    rulesMap[typeCode] = res ?? []
  } catch {
    // 静默处理：后台预加载失败不弹窗打扰用户，表格展示为空即可
    rulesMap[typeCode] = []
  } finally {
    ruleLoadingMap[typeCode] = false
  }
}

async function fetchAllRules() {
  const types = autoPluginTypes.value
  await Promise.all(types.map(t => fetchRulesForType(t.implementationType)))
}

async function refreshAll() {
  await fetchTypes()
  await fetchAllRules()
}

function onExpand(expanded: boolean, record: AutoPluginMetadata) {
  if (expanded) {
    expandedKeys.value = [...expandedKeys.value, record.implementationType]
    // 如果尚未加载过，则加载
    if (!rulesMap[record.implementationType]) {
      fetchRulesForType(record.implementationType)
    }
  } else {
    expandedKeys.value = expandedKeys.value.filter(k => k !== record.implementationType)
  }
}

function goToRules(autoPlugin: AutoPluginMetadata) {
  router.push(`/cardflow/auto-plugin/${autoPlugin.implementationType}/rules`)
}

// ==================== 规则 CRUD ====================
const activeTypeCode = ref('')
const activeSchema = ref<ConfigFieldSchema[]>([])

const dialogVisible = ref(false)
const editingRule = ref<AutoPluginRuleDto | null>(null)

const excelFormOpen = ref(false)
const editingRuleForExcel = ref<AutoPluginRuleDto | null>(null)

const autoVoucherFormOpen = ref(false)
const editingRuleForAutoVoucher = ref<AutoPluginRuleDto | null>(null)
const legacyMode = ref(false)

function openRuleDialog(typeCode: string, autoPluginMeta: AutoPluginMetadata, rule: AutoPluginRuleDto | null) {
  activeTypeCode.value = typeCode
  activeSchema.value = autoPluginMeta.configSchema ?? []

  if (typeCode === 'ExcelInput') {
    editingRuleForExcel.value = rule
    excelFormOpen.value = true
  } else if (typeCode === 'AutoVoucher') {
    if (rule) {
      router.push({ name: 'AutoVoucherWizard', params: { id: rule.id } })
    } else {
      router.push({ name: 'AutoVoucherWizard' })
    }
  } else {
    editingRule.value = rule
    dialogVisible.value = true
  }
}

function handleCreateRule(typeCode: string, autoPluginMeta: AutoPluginMetadata) {
  openRuleDialog(typeCode, autoPluginMeta, null)
}

function handleEditRule(typeCode: string, autoPluginMeta: AutoPluginMetadata, rule: AutoPluginRuleDto) {
  openRuleDialog(typeCode, autoPluginMeta, rule)
}

async function handleDeleteRule(typeCode: string, rule: AutoPluginRuleDto) {
  try {
    await deleteAutoPluginRule(rule.id)
    message.success('删除成功')
    fetchRulesForType(typeCode)
  } catch {
    message.error('删除失败')
  }
}

function onRuleSaved() {
  dialogVisible.value = false
  fetchRulesForType(activeTypeCode.value)
}

function onRuleSavedForType(typeCode: string) {
  fetchRulesForType(typeCode)
}

// ==================== 初始化 ====================
onMounted(async () => {
  await fetchTypes()
  await fetchAllRules()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.auto-plugin-list {
  padding: $page-padding;
  display: flex;
  flex-direction: column;
  gap: $section-gap;
}

.table-card {
  background: var(--bg-card);
  border-radius: 8px;
  box-shadow: var(--shadow-sm);

  :deep(.ant-table-wrapper) {
    .ant-table {
      border-radius: 8px;
    }
  }
}

.expanded-content {
  padding: 4px 0;

  .add-rule-bar {
    margin-top: 8px;
    text-align: left;
  }
}
</style>
