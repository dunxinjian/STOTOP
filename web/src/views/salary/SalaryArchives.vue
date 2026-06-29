<template>
  <div class="salary-archives page-container">
    <a-page-header title="员工薪酬档案" sub-title="员工薪酬档案管理" />

    <a-card :bordered="false">
      <div class="toolbar">
        <a-space>
          <a-input v-model:value="searchEmployee" placeholder="员工姓名/工号" allow-clear style="width: 220px" />
          <a-select v-model:value="searchGradeId" placeholder="档位筛选" allow-clear style="width: 180px" :options="gradeOptions" />
          <a-button type="primary">查询</a-button>
          <a-button>重置</a-button>
        </a-space>
        <a-space>
          <a-button type="primary">
            <template #icon><PlusOutlined /></template>
            新增档案
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
            <a-button type="link" size="small">查看历史</a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import type { SalaryArchive } from '@/types/salary'

const searchEmployee = ref<string>('')
const searchGradeId = ref<number | undefined>(undefined)

const gradeOptions = ref<{ label: string; value: number }[]>([])

const columns = [
  { title: '员工ID', dataIndex: 'f员工ID', key: 'f员工ID', width: 100 },
  { title: '员工姓名', dataIndex: 'f员工姓名', key: 'f员工姓名', width: 120 },
  { title: '档位', dataIndex: 'f档位名称', key: 'f档位名称', width: 140 },
  { title: '基本工资', dataIndex: 'f基本工资', key: 'f基本工资', width: 120 },
  { title: '社保基数', dataIndex: 'f社保基数', key: 'f社保基数', width: 120 },
  { title: '公积金基数', dataIndex: 'f公积金基数', key: 'f公积金基数', width: 120 },
  { title: '生效起期', dataIndex: 'f生效起期', key: 'f生效起期', width: 120 },
  { title: '启用状态', dataIndex: 'f启用状态', key: 'f启用状态', width: 100 },
  { title: '操作', key: 'action', width: 200, fixed: 'right' as const },
]

const dataSource = ref<SalaryArchive[]>([])

// TODO: 调用 /api/salary/archives 加载档案列表
// TODO: 调用 /api/salary/grades 加载档位下拉选项
// TODO: 调用 /api/salary/archives (POST/PUT) 新增/更新档案
</script>

<style scoped>
.salary-archives {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
