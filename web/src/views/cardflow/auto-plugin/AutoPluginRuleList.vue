<template>
  <div class="auto-plugin-rule-list">
    <PageHeader :title="pageTitle">
      <template #right>
        <a-button @click="copyRuleOpen = true">
          <CopyOutlined /> 从其他组织复制
        </a-button>
        <a-button type="primary" @click="handleCreate">
          <PlusOutlined /> 新增规则
        </a-button>
        <a-button @click="router.push('/cardflow/auto-plugin')">
          <RollbackOutlined /> 返回
        </a-button>
      </template>
    </PageHeader>

    <div class="card table-wrap">
      <!-- 空态引导 -->
      <div v-if="!loading && tableData.length === 0" class="rule-empty">
        <a-empty description="当前组织暂无该类型规则配置。可从其他组织复制已有规则，或手动新建。">
          <template #extra>
            <a-space>
              <a-button @click="copyRuleOpen = true">
                <CopyOutlined /> 从其他组织复制
              </a-button>
              <a-button type="primary" @click="handleCreate">
                <PlusOutlined /> 新增规则
              </a-button>
            </a-space>
          </template>
        </a-empty>
      </div>
      <a-table
        v-else
        :columns="tableColumns"
        :dataSource="tableData"
        :loading="loading"
        rowKey="id"
        bordered
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'ruleName'">
            <a @click="handleEdit(record as AutoPluginRuleDto)">{{ record.ruleName }}</a>
          </template>
          <template v-else-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'error'">
              {{ record.status === 1 ? '启用' : '禁用' }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'referenceCount'">
            <a-tag :color="record.referenceCount > 0 ? 'processing' : 'default'">
              {{ record.referenceCount ?? 0 }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'description'">
            {{ record.description || '-' }}
          </template>
          <template v-else-if="column.dataIndex === 'createTime'">
            {{ record.createTime ? record.createTime.substring(0, 16).replace('T', ' ') : '-' }}
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record as AutoPluginRuleDto)">编辑</a-button>
            <a-tooltip v-if="record.referenceCount > 0" title="已被引用，无法删除">
              <a-button type="link" size="small" danger disabled>删除</a-button>
            </a-tooltip>
            <a-popconfirm
              v-else
              title="确定删除该规则？"
              @confirm="handleDelete(record as AutoPluginRuleDto)"
              okText="删除"
              cancelText="取消"
            >
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </div>

    <AutoPluginRuleFormDialog
      :open="dialogVisible"
      :rule="editingRule"
      :typeCode="typeCode"
      :configSchema="currentSchema"
      @update:open="dialogVisible = $event"
      @saved="onRuleSaved"
    />

    <ExcelInputRuleForm
      v-model:open="excelFormOpen"
      :rule="editingRuleForExcel"
      :type-code="typeCode"
      @saved="fetchList"
    />

    <AutoVoucherRuleForm
      v-if="legacyMode && typeCode === 'AutoVoucher'"
      v-model:open="autoVoucherFormOpen"
      :rule="editingRuleForAutoVoucher"
      :type-code="typeCode"
      @saved="fetchList"
    />

    <CreateAutoVoucherRuleDialog
      v-model:open="autoVoucherCreateOpen"
      :type-code="typeCode"
      @created="onAutoVoucherCreated"
    />

    <CopyRuleFromOrgDialog
      :open="copyRuleOpen"
      :type-code="typeCode"
      @update:open="copyRuleOpen = $event"
      @success="fetchList"
    />

  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { PlusOutlined, RollbackOutlined, CopyOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AutoPluginRuleFormDialog from './AutoPluginRuleFormDialog.vue'
import ExcelInputRuleForm from './ExcelInputRuleForm.vue'
import AutoVoucherRuleForm from './AutoVoucherRuleForm.vue'
import CreateAutoVoucherRuleDialog from './CreateAutoVoucherRuleDialog.vue'
import CopyRuleFromOrgDialog from './CopyRuleFromOrgDialog.vue'
import {
  getAutoPluginTypes,
  getAutoPluginRules,
  deleteAutoPluginRule,
} from '@/api/cardflow'
import type { AutoPluginMetadata, AutoPluginRuleDto, ConfigFieldSchema } from '@/api/cardflow'

const route = useRoute()
const router = useRouter()

const typeCode = computed(() => route.params.typeCode as string)

const loading = ref(false)
const tableData = ref<AutoPluginRuleDto[]>([])
const autoPluginMeta = ref<AutoPluginMetadata | null>(null)

const pageTitle = computed(() =>
  autoPluginMeta.value ? `${autoPluginMeta.value.displayName} — 规则管理` : '自动插件规则管理'
)

const currentSchema = computed<ConfigFieldSchema[]>(() =>
  autoPluginMeta.value?.configSchema ?? []
)

const tableColumns = [
  { title: '规则名称', dataIndex: 'ruleName', key: 'ruleName', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
  { title: '引用数', dataIndex: 'referenceCount', key: 'referenceCount', width: 80, align: 'center' as const },
  { title: '说明', dataIndex: 'description', key: 'description', width: 250, ellipsis: true },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 160 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 180, align: 'center' as const, fixed: 'right' as const },
]

async function fetchMeta() {
  try {
    const res: any = await getAutoPluginTypes()
    const types: AutoPluginMetadata[] = res ?? []
    autoPluginMeta.value = types.find(t => t.implementationType === typeCode.value) ?? null
  } catch {
    // ignore
  }
}

async function fetchList() {
  loading.value = true
  try {
    const res: any = await getAutoPluginRules(typeCode.value)
    tableData.value = res ?? []
  } catch {
    message.error('获取规则列表失败')
  } finally {
    loading.value = false
  }
}

// ==================== Dialog ====================
const dialogVisible = ref(false)
const editingRule = ref<AutoPluginRuleDto | null>(null)

const excelFormOpen = ref(false)
const editingRuleForExcel = ref<AutoPluginRuleDto | null>(null)

const autoVoucherFormOpen = ref(false)
const editingRuleForAutoVoucher = ref<AutoPluginRuleDto | null>(null)
const legacyMode = ref(false)
const autoVoucherCreateOpen = ref(false)
const copyRuleOpen = ref(false)

function handleCreate() {
  if (typeCode.value === 'ExcelInput') {
    editingRuleForExcel.value = null
    excelFormOpen.value = true
  } else if (typeCode.value === 'AutoVoucher') {
    autoVoucherCreateOpen.value = true
  } else {
    editingRule.value = null
    dialogVisible.value = true
  }
}

function handleEdit(record: AutoPluginRuleDto) {
  if (typeCode.value === 'ExcelInput') {
    editingRuleForExcel.value = record
    excelFormOpen.value = true
  } else if (typeCode.value === 'AutoVoucher') {
    router.push({ name: 'AutoVoucherWizard', params: { id: record.id } })
  } else {
    editingRule.value = record
    dialogVisible.value = true
  }
}

async function handleDelete(record: AutoPluginRuleDto) {
  try {
    await deleteAutoPluginRule(record.id)
    message.success('删除成功')
    fetchList()
  } catch {
    message.error('删除失败')
  }
}

function onRuleSaved() {
  dialogVisible.value = false
  fetchList()
}

function onAutoVoucherCreated(ruleId: number) {
  autoVoucherCreateOpen.value = false
  router.push({ name: 'AutoVoucherWizard', params: { id: ruleId } })
}


onMounted(() => {
  fetchMeta()
  fetchList()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.auto-plugin-rule-list {
  padding: $page-padding;
  display: flex;
  flex-direction: column;
  gap: $section-gap;
}

.card {
  background: var(--bg-card);
  border-radius: 8px;
  box-shadow: var(--shadow-sm);
}

.table-wrap {
  padding: 0;

  :deep(.ant-table-wrapper) {
    .ant-table {
      border-radius: 8px;
    }
  }
}

.rule-empty {
  padding: 48px 24px;
  text-align: center;
}
</style>
