<template>
  <div class="manager-quota-page">
    <PageHeader title="管理层奖扣配额" />

    <!-- 我的配额概览 -->
    <a-row :gutter="16" class="quota-overview">
      <a-col :span="6">
        <a-card :bordered="false" class="quota-card">
          <a-statistic title="本月奖分配额" :value="myQuota?.awardQuota ?? 0" suffix="分" :value-style="{ color: 'var(--color-success)' }" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" class="quota-card">
          <a-statistic title="已用奖分" :value="myQuota?.usedAward ?? 0" suffix="分" :value-style="{ color: 'var(--color-warning)' }" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" class="quota-card">
          <a-statistic title="本月扣分配额" :value="myQuota?.deductQuota ?? 0" suffix="分" :value-style="{ color: 'var(--color-danger)' }" />
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card :bordered="false" class="quota-card">
          <a-statistic title="已用扣分" :value="myQuota?.usedDeduct ?? 0" suffix="分" :value-style="{ color: '#8c8c8c' }" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 剩余配额进度 -->
    <a-card :bordered="false" class="section-card" v-if="myQuota">
      <template #title><span class="section-title">剩余配额</span></template>
      <a-row :gutter="32">
        <a-col :span="12">
          <div class="progress-item">
            <span>奖分剩余：{{ myQuota.remainingAward }} / {{ myQuota.awardQuota }}</span>
            <a-progress
              :percent="myQuota.awardQuota ? Math.round((myQuota.usedAward / myQuota.awardQuota) * 100) : 0"
              status="active"
              stroke-color="var(--color-success)"
            />
          </div>
        </a-col>
        <a-col :span="12">
          <div class="progress-item">
            <span>扣分剩余：{{ myQuota.remainingDeduct }} / {{ myQuota.deductQuota }}</span>
            <a-progress
              :percent="myQuota.deductQuota ? Math.round((myQuota.usedDeduct / myQuota.deductQuota) * 100) : 0"
              status="active"
              stroke-color="var(--color-danger)"
            />
          </div>
        </a-col>
      </a-row>
    </a-card>

    <!-- 奖扣分操作 -->
    <a-card :bordered="false" class="section-card">
      <template #title><span class="section-title">奖扣分操作</span></template>
      <a-form :model="operateForm" layout="inline" class="operate-form">
        <a-form-item label="操作类型">
          <a-radio-group v-model:value="operateForm.type" button-style="solid">
            <a-radio-button value="award">奖分</a-radio-button>
            <a-radio-button value="deduct">扣分</a-radio-button>
          </a-radio-group>
        </a-form-item>
        <a-form-item label="员工ID">
          <a-input-number v-model:value="operateForm.userId" :min="1" placeholder="员工ID" style="width: 120px" />
        </a-form-item>
        <a-form-item label="来源">
          <a-select v-model:value="operateForm.sourceId" placeholder="积分来源" style="width: 150px">
            <a-select-option v-for="src in sourceOptions" :key="src.id" :value="src.id">
              {{ src.sourceName }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="积分值">
          <a-input-number v-model:value="operateForm.pointValue" :min="1" placeholder="积分" style="width: 100px" />
        </a-form-item>
        <a-form-item label="备注">
          <a-input v-model:value="operateForm.remark" placeholder="备注说明" style="width: 200px" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" :loading="operating" @click="handleOperate">
            {{ operateForm.type === 'award' ? '确认奖分' : '确认扣分' }}
          </a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 配额列表（管理员） -->
    <a-card :bordered="false" class="section-card">
      <template #title>
        <div style="display: flex; justify-content: space-between; align-items: center">
          <span class="section-title">配额管理列表</span>
          <a-button type="primary" size="small" @click="showQuotaModal = true">
            <template #icon><PlusOutlined /></template>新增/更新配额
          </a-button>
        </div>
      </template>
      <a-table
        :columns="quotaColumns"
        :data-source="quotaList"
        :loading="quotaLoading"
        :pagination="quotaPaginationConfig"
        row-key="id"
        bordered
        size="middle"
        @change="handleQuotaTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'green' : 'default'">
              {{ record.status === 1 ? '生效' : '未生效' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'createTime'">
            {{ formatTime(record.createTime) }}
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 配额编辑弹窗 -->
    <a-modal
      v-model:open="showQuotaModal"
      title="新增/更新配额"
      @ok="handleSaveQuota"
      :confirmLoading="savingQuota"
    >
      <a-form :model="quotaForm" layout="vertical">
        <a-form-item label="管理者ID" required>
          <a-input-number v-model:value="quotaForm.managerId" :min="1" style="width: 100%" placeholder="管理者用户ID" />
        </a-form-item>
        <a-form-item label="年月" required>
          <a-input v-model:value="quotaForm.yearMonth" placeholder="如：2026-04" />
        </a-form-item>
        <a-form-item label="奖分配额" required>
          <a-input-number v-model:value="quotaForm.awardQuota" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="扣分配额" required>
          <a-input-number v-model:value="quotaForm.deductQuota" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getMyQuota,
  getManagerQuotas,
  saveManagerQuota,
  awardPoints,
  deductPoints,
  getPointSources,
} from '@/api/points'
import type { MyQuotaDto, ManagerQuotaListDto, PointSourceDto } from '@/types/points'

// 我的配额
const myQuota = ref<MyQuotaDto | null>(null)

// 来源
const sourceOptions = ref<PointSourceDto[]>([])

// 操作表单
const operateForm = reactive({
  type: 'award' as 'award' | 'deduct',
  userId: undefined as number | undefined,
  sourceId: undefined as number | undefined,
  pointValue: undefined as number | undefined,
  remark: '',
})
const operating = ref(false)

// 配额列表
const quotaList = ref<ManagerQuotaListDto[]>([])
const quotaLoading = ref(false)
const quotaTotal = ref(0)
const quotaPageIndex = ref(1)
const quotaPageSize = ref(20)

// 配额编辑
const showQuotaModal = ref(false)
const savingQuota = ref(false)
const quotaForm = reactive({
  managerId: undefined as number | undefined,
  yearMonth: '',
  awardQuota: 0,
  deductQuota: 0,
})

const quotaColumns = [
  { title: '管理者', dataIndex: 'managerName', width: 120 },
  { title: '年月', dataIndex: 'yearMonth', width: 100 },
  { title: '奖分配额', dataIndex: 'awardQuota', width: 100 },
  { title: '已用奖分', dataIndex: 'usedAward', width: 100 },
  { title: '扣分配额', dataIndex: 'deductQuota', width: 100 },
  { title: '已用扣分', dataIndex: 'usedDeduct', width: 100 },
  { title: '状态', dataIndex: 'status', width: 80 },
  { title: '创建时间', dataIndex: 'createTime', width: 160 },
]

const quotaPaginationConfig = computed(() => ({
  current: quotaPageIndex.value,
  pageSize: quotaPageSize.value,
  total: quotaTotal.value,
  showTotal: (t: number) => `共 ${t} 条`,
  showSizeChanger: true,
}))

function formatTime(t: string | null) {
  if (!t) return ''
  return t.replace('T', ' ').substring(0, 16)
}

function handleQuotaTableChange(pagination: any) {
  quotaPageIndex.value = pagination.current
  quotaPageSize.value = pagination.pageSize
  loadQuotaList()
}

async function loadMyQuota() {
  try {
    myQuota.value = await getMyQuota()
  } catch {
    // ignore
  }
}

async function loadSources() {
  try {
    sourceOptions.value = await getPointSources()
  } catch {
    // ignore
  }
}

async function loadQuotaList() {
  quotaLoading.value = true
  try {
    const res = await getManagerQuotas({
      pageIndex: quotaPageIndex.value,
      pageSize: quotaPageSize.value,
    })
    quotaList.value = res.items
    quotaTotal.value = res.total
  } catch {
    message.error('加载配额列表失败')
  } finally {
    quotaLoading.value = false
  }
}

async function handleOperate() {
  if (!operateForm.userId) {
    message.warning('请输入员工ID')
    return
  }
  if (!operateForm.sourceId) {
    message.warning('请选择积分来源')
    return
  }
  if (!operateForm.pointValue || operateForm.pointValue <= 0) {
    message.warning('请输入有效积分值')
    return
  }
  if (!operateForm.remark.trim()) {
    message.warning('请填写备注说明')
    return
  }

  operating.value = true
  try {
    const payload = {
      userId: operateForm.userId,
      sourceId: operateForm.sourceId,
      pointValue: operateForm.pointValue,
      remark: operateForm.remark,
    }
    if (operateForm.type === 'award') {
      await awardPoints(payload)
      message.success('奖分成功')
    } else {
      await deductPoints(payload)
      message.success('扣分成功')
    }
    // 刷新配额
    loadMyQuota()
  } catch {
    message.error('操作失败')
  } finally {
    operating.value = false
  }
}

async function handleSaveQuota() {
  if (!quotaForm.managerId) {
    message.warning('请输入管理者ID')
    return
  }
  if (!quotaForm.yearMonth) {
    message.warning('请输入年月')
    return
  }
  savingQuota.value = true
  try {
    await saveManagerQuota({
      managerId: quotaForm.managerId,
      yearMonth: quotaForm.yearMonth,
      awardQuota: quotaForm.awardQuota,
      deductQuota: quotaForm.deductQuota,
    })
    message.success('配额保存成功')
    showQuotaModal.value = false
    loadQuotaList()
  } catch {
    message.error('保存失败')
  } finally {
    savingQuota.value = false
  }
}

onMounted(() => {
  loadMyQuota()
  loadSources()
  loadQuotaList()
})
</script>

<style scoped lang="scss">
.manager-quota-page {
  padding: 0 4px;
}

.quota-overview {
  margin-bottom: 16px;
}

.quota-card {
  border-radius: 8px;
  text-align: center;
}

.section-card {
  border-radius: 8px;
  margin-bottom: 16px;
}

.section-title {
  font-size: 15px;
  font-weight: 600;
}

.progress-item {
  margin-bottom: 8px;

  span {
    font-size: 13px;
    color: #595959;
    display: block;
    margin-bottom: 4px;
  }
}

.operate-form {
  :deep(.ant-form-item) {
    margin-bottom: 12px;
  }
}
</style>
