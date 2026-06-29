<template>
  <div class="promotion-rules page-container">
    <a-page-header title="晋升规则" sub-title="晋升规则配置（档位升迁条件）" />

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
            新增规则
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
import type { PromotionRule } from '@/types/salary'

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
  { title: '当前档位', dataIndex: 'f当前档位名称', key: 'f当前档位名称', width: 140 },
  { title: '目标档位', dataIndex: 'f目标档位名称', key: 'f目标档位名称', width: 140 },
  { title: 'A分阈值', dataIndex: 'fA分阈值', key: 'fA分阈值', width: 110 },
  { title: '连续达标月数', dataIndex: 'f连续达标月数', key: 'f连续达标月数', width: 130 },
  { title: '启用状态', dataIndex: 'f启用状态', key: 'f启用状态', width: 100 },
  { title: '操作', key: 'action', width: 260, fixed: 'right' as const },
]

const dataSource = ref<PromotionRule[]>([])

// TODO: 调用 /api/salary/promotion-rules 加载晋升规则列表
// TODO: 调用 /api/salary/promotion-rules (POST/PUT) 新增/更新规则
// TODO: 调用 /api/salary/promotion-rules/{id}/enable|disable 启用/禁用
</script>

<style scoped>
.promotion-rules {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
