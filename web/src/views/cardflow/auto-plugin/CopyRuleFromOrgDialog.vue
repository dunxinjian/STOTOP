<template>
  <a-modal
    :open="open"
    title="从其他组织复制规则"
    :width="680"
    :confirmLoading="confirmLoading"
    :okButtonProps="{ disabled: !selectedRuleId }"
    okText="复制到当前组织"
    @ok="handleOk"
    @cancel="handleClose"
    :destroyOnClose="true"
  >
    <div class="copy-dialog-body">
      <!-- 选择源组织 -->
      <div class="form-row">
        <span class="form-label">选择组织：</span>
        <a-select
          v-model:value="selectedOrgId"
          placeholder="请选择源组织"
          style="width: 220px;"
          show-search
          option-filter-prop="label"
          :loading="orgLoading"
          @change="onOrgChange"
        >
          <a-select-option
            v-for="org in availableOrgs"
            :key="org.orgId"
            :value="org.orgId"
            :label="org.orgName"
          >
            {{ org.orgName }}
          </a-select-option>
        </a-select>

        <span class="form-label" style="margin-left: 16px;">自动插件类型：</span>
        <a-select
          v-model:value="filterTypeCode"
          placeholder="全部类型"
          style="width: 180px;"
          allow-clear
          @change="onFilterChange"
        >
          <a-select-option
            v-for="t in autoPluginTypes"
            :key="t.implementationType"
            :value="t.implementationType"
          >
            {{ t.displayName }}
          </a-select-option>
        </a-select>
      </div>

      <!-- 规则列表 -->
      <a-table
        v-if="selectedOrgId"
        :columns="columns"
        :dataSource="ruleList"
        :loading="listLoading"
        rowKey="id"
        size="small"
        :pagination="false"
        :rowSelection="{ type: 'radio', selectedRowKeys, onChange: onSelectChange }"
        :scroll="{ y: 320 }"
        class="rule-table"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : 'default'" size="small">
              {{ record.status === 1 ? '启用' : '禁用' }}
            </a-tag>
          </template>
          <template v-else-if="column.dataIndex === 'createTime'">
            {{ record.createTime ? record.createTime.substring(0, 16).replace('T', ' ') : '-' }}
          </template>
        </template>
      </a-table>

      <a-empty
        v-else
        description="请先选择源组织"
        :image="Empty.PRESENTED_IMAGE_SIMPLE"
        class="empty-hint"
      />
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Key } from 'ant-design-vue/es/table/interface'
import { message, Empty } from 'ant-design-vue'
import { useOrgContextStore } from '@/stores/orgContext'
import { getRulesForOrg, copyRule, getAutoPluginTypes } from '@/api/cardflow'
import type { AutoPluginRuleDto, AutoPluginMetadata } from '@/api/cardflow'

const props = defineProps<{
  open: boolean
  typeCode?: string
}>()

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
  (e: 'success'): void
}>()

const orgStore = useOrgContextStore()

const orgLoading = ref(false)
const selectedOrgId = ref<number | undefined>(undefined)
const filterTypeCode = ref<string | undefined>(undefined)
const listLoading = ref(false)
const ruleList = ref<AutoPluginRuleDto[]>([])
const selectedRowKeys = ref<number[]>([])
const confirmLoading = ref(false)
const autoPluginTypes = ref<AutoPluginMetadata[]>([])

const selectedRuleId = computed(() => selectedRowKeys.value[0] ?? null)

// 排除当前组织
const availableOrgs = computed(() => {
  return orgStore.organizations.filter(o => o.orgId !== orgStore.currentOrgId)
})

const columns = [
  { title: '规则名称', dataIndex: 'ruleName', key: 'ruleName', ellipsis: true },
  { title: '类型', dataIndex: 'typeCode', key: 'typeCode', width: 120 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
  { title: '说明', dataIndex: 'description', key: 'description', width: 180, ellipsis: true },
  { title: '创建时间', dataIndex: 'createTime', key: 'createTime', width: 150 },
]

// 打开时初始化
watch(() => props.open, async (val) => {
  if (val) {
    selectedOrgId.value = undefined
    ruleList.value = []
    selectedRowKeys.value = []
    filterTypeCode.value = props.typeCode || undefined
    // 确保组织列表已加载
    if (!orgStore.organizations.length) {
      orgLoading.value = true
      await orgStore.fetchOrganizations()
      orgLoading.value = false
    }
    // 加载自动插件类型列表
    if (!autoPluginTypes.value.length) {
      try {
        const res: any = await getAutoPluginTypes()
        autoPluginTypes.value = res ?? []
      } catch { /* ignore */ }
    }
  }
})

async function loadRules() {
  if (!selectedOrgId.value) return
  selectedRowKeys.value = []
  listLoading.value = true
  try {
    const res: any = await getRulesForOrg(selectedOrgId.value, filterTypeCode.value)
    ruleList.value = res ?? []
  } catch {
    message.error('获取规则列表失败')
    ruleList.value = []
  } finally {
    listLoading.value = false
  }
}

function onOrgChange() {
  if (selectedOrgId.value) {
    loadRules()
  } else {
    ruleList.value = []
    selectedRowKeys.value = []
  }
}

function onFilterChange() {
  if (selectedOrgId.value) {
    loadRules()
  }
}

function onSelectChange(keys: Key[]) {
  selectedRowKeys.value = keys.map(Number)
}

async function handleOk() {
  if (!selectedRuleId.value) return
  confirmLoading.value = true
  try {
    await copyRule(selectedRuleId.value)
    message.success('复制成功')
    emit('success')
    handleClose()
  } catch {
    message.error('复制失败，请重试')
  } finally {
    confirmLoading.value = false
  }
}

function handleClose() {
  emit('update:open', false)
}
</script>

<style scoped>
.copy-dialog-body {
  min-height: 200px;
}

.form-row {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 4px 0;
}

.form-label {
  font-size: 14px;
  color: var(--text-2);
  white-space: nowrap;
  margin-right: 8px;
}

.rule-table {
  margin-top: 4px;
}

.empty-hint {
  padding: 48px 0;
}
</style>
