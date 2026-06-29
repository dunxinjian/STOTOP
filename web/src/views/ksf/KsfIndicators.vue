<template>
  <div class="ksf-indicators page-container">
    <a-page-header title="指标库" sub-title="KSF 指标库管理" />

    <a-card :bordered="false">
      <div class="toolbar">
        <a-space>
          <a-input v-model:value="searchKeyword" placeholder="编码/名称" allow-clear style="width: 220px" />
          <a-select v-model:value="searchEnabledValue" placeholder="启用状态" allow-clear style="width: 120px" :options="enabledOptions" />
          <a-button type="primary">查询</a-button>
          <a-button>重置</a-button>
        </a-space>
        <a-space>
          <a-button type="primary">
            <template #icon><PlusOutlined /></template>
            新增指标
          </a-button>
        </a-space>
      </div>

      <a-table
        :columns="columns"
        :data-source="dataSource"
        :pagination="false"
        row-key="FID"
        bordered
        size="small"
      >
        <template #bodyCell="{ column }">
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small">编辑</a-button>
            <a-button type="link" size="small" danger>删除</a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import type { KsfIndicator } from '@/types/ksf'

const searchKeyword = ref<string>('')
const searchEnabled = ref<boolean | undefined>(undefined)
// AntD Select 的 RawValue 仅支持 string|number，这里用数值代理映射 1/0 ↔ true/false
const searchEnabledValue = computed<number | undefined>({
  get: () => (searchEnabled.value === undefined ? undefined : searchEnabled.value ? 1 : 0),
  set: (val) => {
    searchEnabled.value = val === undefined ? undefined : val === 1
  },
})

const enabledOptions = [
  { label: '启用', value: 1 },
  { label: '禁用', value: 0 },
]

const columns = [
  { title: '编码', dataIndex: 'F编码', key: 'F编码', width: 140 },
  { title: '名称', dataIndex: 'F名称', key: 'F名称' },
  { title: '计量单位', dataIndex: 'F计量单位', key: 'F计量单位', width: 100 },
  { title: '取数类型', dataIndex: 'F取数类型', key: 'F取数类型', width: 100 },
  { title: '方向', dataIndex: 'F方向', key: 'F方向', width: 100 },
  { title: '启用状态', dataIndex: 'F是否启用', key: 'F是否启用', width: 100 },
  { title: '操作', key: 'action', width: 160, fixed: 'right' as const },
]

const dataSource = ref<KsfIndicator[]>([])
</script>

<style scoped>
.ksf-indicators {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
