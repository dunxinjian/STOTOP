<template>
  <div class="promotion-reviews page-container">
    <a-page-header title="晋升评审" sub-title="晋升申请审批与历史记录" />

    <a-card :bordered="false">
      <a-tabs v-model:active-key="activeTab">
        <a-tab-pane key="pending" tab="待审列表">
          <div class="toolbar">
            <a-space>
              <a-input v-model:value="searchEmployee" placeholder="员工姓名" allow-clear style="width: 220px" />
              <a-button type="primary">查询</a-button>
              <a-button>重置</a-button>
            </a-space>
            <a-space>
              <a-button type="primary">批量通过</a-button>
              <a-button danger>批量驳回</a-button>
            </a-space>
          </div>
          <a-table
            :columns="pendingColumns"
            :data-source="pendingData"
            :pagination="false"
            row-key="fid"
            bordered
            size="small"
          >
            <template #bodyCell="{ column }">
              <template v-if="column.key === 'action'">
                <a-button type="link" size="small">详情</a-button>
                <a-button type="link" size="small">通过</a-button>
                <a-button type="link" size="small" danger>驳回</a-button>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="history" tab="历史记录">
          <div class="toolbar">
            <a-space>
              <a-input v-model:value="historyEmployee" placeholder="员工姓名" allow-clear style="width: 220px" />
              <a-range-picker v-model:value="historyRange" />
              <a-button type="primary">查询</a-button>
              <a-button>重置</a-button>
            </a-space>
          </div>
          <a-table
            :columns="historyColumns"
            :data-source="historyData"
            :pagination="false"
            row-key="fid"
            bordered
            size="small"
          />
        </a-tab-pane>
      </a-tabs>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { Dayjs } from 'dayjs'
import type { PromotionReview, PromotionHistory } from '@/types/salary'

const activeTab = ref<string>('pending')

// 待审
const searchEmployee = ref<string>('')
const pendingColumns = [
  { title: '员工', dataIndex: 'f员工姓名', key: 'f员工姓名', width: 140 },
  { title: '当前档位', dataIndex: 'f当前档位名称', key: 'f当前档位名称', width: 140 },
  { title: '目标档位', dataIndex: 'f目标档位名称', key: 'f目标档位名称', width: 140 },
  { title: 'A分累计', dataIndex: 'fA分累计', key: 'fA分累计', width: 110 },
  { title: '申请时间', dataIndex: 'f申请时间', key: 'f申请时间', width: 160 },
  { title: '备注', dataIndex: 'f备注', key: 'f备注' },
  { title: '操作', key: 'action', width: 220, fixed: 'right' as const },
]
const pendingData = ref<PromotionReview[]>([])

// 历史
const historyEmployee = ref<string>('')
const historyRange = ref<[Dayjs, Dayjs] | undefined>(undefined)
const historyColumns = [
  { title: '员工', dataIndex: 'f员工姓名', key: 'f员工姓名', width: 140 },
  { title: '原档位', dataIndex: 'f原档位名称', key: 'f原档位名称', width: 140 },
  { title: '新档位', dataIndex: 'f新档位名称', key: 'f新档位名称', width: 140 },
  { title: '生效时间', dataIndex: 'f生效时间', key: 'f生效时间', width: 160 },
  { title: '备注', dataIndex: 'f备注', key: 'f备注' },
]
const historyData = ref<PromotionHistory[]>([])

// TODO: 调用 /api/salary/promotion-reviews?status=0 加载待审列表
// TODO: 调用 /api/salary/promotion-reviews/{id}/review 审批（通过/驳回）
// TODO: 调用 /api/salary/promotion-history 加载历史记录
</script>

<style scoped>
.promotion-reviews {
  padding: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}
</style>
