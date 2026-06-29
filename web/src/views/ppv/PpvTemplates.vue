<template>
  <div class="ppv-templates page-container">
    <a-page-header title="产值模板" sub-title="PPV 产值项模板管理" />

    <a-card :bordered="false">
      <div class="toolbar">
        <a-space>
          <a-input v-model:value="searchKeyword" placeholder="编码/名称" allow-clear style="width: 220px" />
          <a-select v-model:value="searchPositionId" placeholder="岗位筛选" allow-clear style="width: 180px" :options="positionOptions" />
          <a-select v-model:value="searchEnabledValue" placeholder="启用状态" allow-clear style="width: 120px" :options="enabledOptions" />
          <a-button type="primary">查询</a-button>
          <a-button>重置</a-button>
        </a-space>
        <a-space>
          <a-button type="primary">
            <template #icon><PlusOutlined /></template>
            新增模板
          </a-button>
        </a-space>
      </div>

      <a-table
        :columns="columns"
        :data-source="dataSource"
        :pagination="false"
        row-key="fid"
        bordered
        size="small"
      >
        <template #bodyCell="{ column }">
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small">编辑</a-button>
            <a-button type="link" size="small">启用</a-button>
            <a-button type="link" size="small" danger>禁用</a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import type { PpvTemplate } from '@/types/ppv'

const searchKeyword = ref<string>('')
const searchPositionId = ref<number | undefined>(undefined)
const searchEnabled = ref<boolean | undefined>(undefined)
// AntD Select 的 RawValue 仅支持 string|number，这里用数值代理映射 1/0 ↔ true/false
const searchEnabledValue = computed<number | undefined>({
  get: () => (searchEnabled.value === undefined ? undefined : searchEnabled.value ? 1 : 0),
  set: (val) => {
    searchEnabled.value = val === undefined ? undefined : val === 1
  },
})

const positionOptions = ref<{ label: string; value: number }[]>([])

const enabledOptions = [
  { label: '启用', value: 1 },
  { label: '禁用', value: 0 },
]

const columns = [
  { title: '产值项编码', dataIndex: 'f产值项编码', key: 'f产值项编码', width: 140 },
  { title: '产值项名称', dataIndex: 'f产值项名称', key: 'f产值项名称' },
  { title: '岗位', dataIndex: 'f岗位ID', key: 'f岗位ID', width: 140 },
  { title: '单价', dataIndex: 'f单价', key: 'f单价', width: 100 },
  { title: '计量单位', dataIndex: 'f计量单位', key: 'f计量单位', width: 100 },
  { title: '生效起期', dataIndex: 'f生效起期', key: 'f生效起期', width: 120 },
  { title: '生效止期', dataIndex: 'f生效止期', key: 'f生效止期', width: 120 },
  { title: '启用状态', dataIndex: 'f启用状态', key: 'f启用状态', width: 100 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' as const },
]

const dataSource = ref<PpvTemplate[]>([])

// TODO: 调用 /api/ppv/templates 加载模板列表
// TODO: 调用 /api/positions 加载岗位选项
</script>

<style scoped>
.ppv-templates {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
