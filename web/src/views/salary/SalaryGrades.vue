<template>
  <div class="salary-grades page-container">
    <a-page-header title="薪酬档位" sub-title="薪酬档位 CRUD 管理" />

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
            新增档位
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
import type { SalaryGrade } from '@/types/salary'

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
  { title: '编码', dataIndex: 'f编码', key: 'f编码', width: 140 },
  { title: '名称', dataIndex: 'f名称', key: 'f名称' },
  { title: '级别', dataIndex: 'f级别', key: 'f级别', width: 100 },
  { title: '基本工资', dataIndex: 'f基本工资', key: 'f基本工资', width: 120 },
  { title: '生效起期', dataIndex: 'f生效起期', key: 'f生效起期', width: 120 },
  { title: '生效止期', dataIndex: 'f生效止期', key: 'f生效止期', width: 120 },
  { title: '启用状态', dataIndex: 'f启用状态', key: 'f启用状态', width: 100 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' as const },
]

const dataSource = ref<SalaryGrade[]>([])

// TODO: 调用 /api/salary/grades 加载档位列表
// TODO: 调用 /api/salary/grades (POST/PUT) 新增/更新档位
// TODO: 调用 /api/salary/grades/{id}/enable|disable 启用/禁用
</script>

<style scoped>
.salary-grades {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
