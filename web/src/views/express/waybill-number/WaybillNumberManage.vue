<template>
  <div class="page-container">
    <PageHeader title="运单编号管理" />

    <a-card :bordered="false">
      <a-tabs v-model:activeKey="activeTab">
        <!-- 号段管理 -->
        <a-tab-pane key="pool" tab="号段管理">
          <div style="margin-bottom: 12px; text-align: right">
            <a-button type="primary" @click="handleAddPool">
              <template #icon><PlusOutlined /></template>新增号段
            </a-button>
          </div>
          <a-table
            :columns="poolColumns"
            :data-source="poolData"
            :loading="poolLoading"
            :pagination="false"
            row-key="id"
            bordered
            :scroll="{ x: 900 }"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'remaining'">
                <a-progress
                  :percent="record.totalCount ? Math.round(((record.totalCount - (record.remaining ?? 0)) / record.totalCount) * 100) : 0"
                  :size="'small'"
                  :status="(record.remaining ?? 0) <= 0 ? 'exception' : 'normal'"
                />
                <span style="font-size: 12px; color: #999; margin-left: 4px">{{ record.remaining ?? 0 }} / {{ record.totalCount ?? 0 }}</span>
              </template>
              <template v-if="column.dataIndex === 'createdTime'">
                {{ record.createdTime?.slice(0, 19)?.replace('T', ' ') }}
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <!-- 分配/回收 -->
        <a-tab-pane key="allocate" tab="分配记录">
          <div style="margin-bottom: 12px; display: flex; gap: 8px; justify-content: flex-end">
            <a-button type="primary" @click="allocateDialogVisible = true">
              <template #icon><SwapOutlined /></template>分配
            </a-button>
            <a-button danger @click="returnDialogVisible = true">
              <template #icon><RollbackOutlined /></template>回收
            </a-button>
          </div>
          <a-empty description="分配/回收记录通过上方操作完成" />
        </a-tab-pane>

        <!-- 客户余额 -->
        <a-tab-pane key="balance" tab="客户余额">
          <a-row :gutter="16" style="margin-bottom: 12px">
            <a-col :span="6">
              <a-input-number v-model:value="balanceClientId" placeholder="客户ID" style="width: 100%" />
            </a-col>
            <a-col :span="6">
              <a-input v-model:value="balanceBrandCode" placeholder="品牌编码" style="width: 100%" />
            </a-col>
            <a-col :span="4">
              <a-button type="primary" @click="fetchClientBalance" :loading="balanceLoading">查询</a-button>
            </a-col>
          </a-row>
          <a-descriptions v-if="clientBalance" bordered :column="3" size="small">
            <a-descriptions-item label="可用数量">
              <span style="color: var(--color-success); font-weight: 600">{{ clientBalance.available }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="已使用">{{ clientBalance.used }}</a-descriptions-item>
            <a-descriptions-item label="累计分配">{{ clientBalance.totalAllocated }}</a-descriptions-item>
            <a-descriptions-item label="累计回收">{{ clientBalance.totalReturned }}</a-descriptions-item>
          </a-descriptions>
          <a-empty v-else description="请输入客户ID和品牌编码查询余额" />
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <!-- 新增号段弹窗 -->
    <a-modal v-model:open="poolDialogVisible" title="新增号段" width="500px" :destroy-on-close="true" @cancel="poolDialogVisible = false">
      <a-form ref="poolFormRef" :model="poolForm" :rules="poolRules" :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="品牌编码" name="brandCode">
          <a-input v-model:value="poolForm.brandCode" style="width: 100%" placeholder="请输入品牌编码" />
        </a-form-item>
        <a-form-item label="前缀" name="prefix">
          <a-input v-model:value="poolForm.prefix" placeholder="运单编号前缀" :maxlength="20" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="起始号" name="startNo">
              <a-input v-model:value="poolForm.startNo" placeholder="起始号" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="结束号" name="endNo">
              <a-input v-model:value="poolForm.endNo" placeholder="结束号" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="总数量">
          <a-input-number v-model:value="poolForm.totalCount" :min="1" style="width: 100%" placeholder="总数量（可选，自动计算）" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="poolDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="poolSubmitLoading" @click="handlePoolSubmit">确定</a-button>
      </template>
    </a-modal>

    <!-- 分配弹窗 -->
    <a-modal v-model:open="allocateDialogVisible" title="分配运单编号" width="500px" :destroy-on-close="true" @cancel="allocateDialogVisible = false">
      <a-form ref="allocateFormRef" :model="allocateForm" :rules="allocateRules" :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="号段ID" name="poolId">
          <a-input-number v-model:value="allocateForm.poolId" style="width: 100%" placeholder="请输入号段ID" />
        </a-form-item>
        <a-form-item label="客户ID" name="clientId">
          <a-input-number v-model:value="allocateForm.clientId" style="width: 100%" placeholder="请输入客户ID" />
        </a-form-item>
        <a-form-item label="品牌编码" name="brandCode">
          <a-input v-model:value="allocateForm.brandCode" style="width: 100%" placeholder="请输入品牌编码" />
        </a-form-item>
        <a-form-item label="数量" name="quantity">
          <a-input-number v-model:value="allocateForm.quantity" :min="1" style="width: 100%" placeholder="分配数量" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="allocateDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="allocateLoading" @click="handleAllocate">分配</a-button>
      </template>
    </a-modal>

    <!-- 回收弹窗 -->
    <a-modal v-model:open="returnDialogVisible" title="回收运单编号" width="500px" :destroy-on-close="true" @cancel="returnDialogVisible = false">
      <a-form ref="returnFormRef" :model="returnForm" :rules="returnRules" :label-col="{ style: { width: '80px' } }" style="padding: 10px 20px">
        <a-form-item label="客户ID" name="clientId">
          <a-input-number v-model:value="returnForm.clientId" style="width: 100%" placeholder="请输入客户ID" />
        </a-form-item>
        <a-form-item label="品牌编码" name="brandCode">
          <a-input v-model:value="returnForm.brandCode" style="width: 100%" placeholder="请输入品牌编码" />
        </a-form-item>
        <a-form-item label="数量" name="quantity">
          <a-input-number v-model:value="returnForm.quantity" :min="1" style="width: 100%" placeholder="回收数量" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="returnDialogVisible = false">取消</a-button>
        <a-button type="primary" danger :loading="returnLoading" @click="handleReturn">回收</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, SwapOutlined, RollbackOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getWaybillNumberPools,
  createWaybillNumberPool,
  allocateWaybillNumber,
  returnWaybillNumber,
  getClientWaybillBalance,
  type WaybillNumberPoolDto,
  type ClientWaybillBalanceDto,
} from '@/api/express'

const activeTab = ref('pool')

// ===== 号段管理 =====
const poolLoading = ref(false)
const poolData = ref<WaybillNumberPoolDto[]>([])

const poolColumns = [
  { title: '品牌', dataIndex: 'brandName', width: 100 },
  { title: '前缀', dataIndex: 'prefix', width: 100 },
  { title: '起始号', dataIndex: 'startNo', width: 140 },
  { title: '结束号', dataIndex: 'endNo', width: 140 },
  { title: '使用情况', dataIndex: 'remaining', width: 200 },
  { title: '创建时间', dataIndex: 'createdTime', width: 170 },
]

async function fetchPools() {
  poolLoading.value = true
  try {
    poolData.value = await getWaybillNumberPools()
  } catch {
    message.error('获取号段列表失败')
  } finally {
    poolLoading.value = false
  }
}

// 新增号段
const poolDialogVisible = ref(false)
const poolFormRef = ref<FormInstance>()
const poolSubmitLoading = ref(false)
const poolForm = reactive({
  brandCode: undefined as string | undefined,
  prefix: '',
  startNo: '',
  endNo: '',
  totalCount: undefined as number | undefined,
})
const poolRules: Record<string, Rule[]> = {
  brandCode: [{ required: true, message: '请输入品牌编码', trigger: 'blur' }],
  startNo: [{ required: true, message: '请输入起始号', trigger: 'blur' }],
  endNo: [{ required: true, message: '请输入结束号', trigger: 'blur' }],
}

function handleAddPool() {
  poolForm.brandCode = undefined
  poolForm.prefix = ''
  poolForm.startNo = ''
  poolForm.endNo = ''
  poolForm.totalCount = undefined
  poolDialogVisible.value = true
}

async function handlePoolSubmit() {
  if (!poolFormRef.value) return
  try { await poolFormRef.value.validate() } catch { return }
  poolSubmitLoading.value = true
  try {
    await createWaybillNumberPool({
      brandCode: poolForm.brandCode!,
      prefix: poolForm.prefix || undefined,
      startNo: poolForm.startNo,
      endNo: poolForm.endNo,
      totalCount: poolForm.totalCount,
    })
    message.success('号段创建成功')
    poolDialogVisible.value = false
    fetchPools()
  } catch { /* handled */ } finally {
    poolSubmitLoading.value = false
  }
}

// ===== 分配 =====
const allocateDialogVisible = ref(false)
const allocateFormRef = ref<FormInstance>()
const allocateLoading = ref(false)
const allocateForm = reactive({
  poolId: undefined as number | undefined,
  clientId: undefined as number | undefined,
  brandCode: undefined as string | undefined,
  quantity: 1,
})
const allocateRules: Record<string, Rule[]> = {
  poolId: [{ required: true, message: '请输入号段ID', trigger: 'blur' }],
  clientId: [{ required: true, message: '请输入客户ID', trigger: 'blur' }],
  brandCode: [{ required: true, message: '请输入品牌编码', trigger: 'blur' }],
  quantity: [{ required: true, message: '请输入数量', trigger: 'blur' }],
}

async function handleAllocate() {
  if (!allocateFormRef.value) return
  try { await allocateFormRef.value.validate() } catch { return }
  allocateLoading.value = true
  try {
    await allocateWaybillNumber({
      poolId: allocateForm.poolId!,
      clientId: String(allocateForm.clientId),
      brandCode: allocateForm.brandCode!,
      quantity: allocateForm.quantity,
    })
    message.success('分配成功')
    allocateDialogVisible.value = false
    fetchPools()
  } catch { /* handled */ } finally {
    allocateLoading.value = false
  }
}

// ===== 回收 =====
const returnDialogVisible = ref(false)
const returnFormRef = ref<FormInstance>()
const returnLoading = ref(false)
const returnForm = reactive({
  clientId: undefined as number | undefined,
  brandCode: undefined as string | undefined,
  quantity: 1,
})
const returnRules: Record<string, Rule[]> = {
  clientId: [{ required: true, message: '请输入客户ID', trigger: 'blur' }],
  brandCode: [{ required: true, message: '请输入品牌编码', trigger: 'blur' }],
  quantity: [{ required: true, message: '请输入数量', trigger: 'blur' }],
}

async function handleReturn() {
  if (!returnFormRef.value) return
  try { await returnFormRef.value.validate() } catch { return }
  returnLoading.value = true
  try {
    await returnWaybillNumber({
      clientId: String(returnForm.clientId),
      brandCode: returnForm.brandCode!,
      quantity: returnForm.quantity,
    })
    message.success('回收成功')
    returnDialogVisible.value = false
    fetchPools()
  } catch { /* handled */ } finally {
    returnLoading.value = false
  }
}

// ===== 客户余额 =====
const balanceClientId = ref<number | undefined>(undefined)
const balanceBrandCode = ref<string | undefined>(undefined)
const balanceLoading = ref(false)
const clientBalance = ref<ClientWaybillBalanceDto | null>(null)

async function fetchClientBalance() {
  if (!balanceClientId.value || !balanceBrandCode.value) {
    message.warning('请输入客户ID和品牌编码')
    return
  }
  balanceLoading.value = true
  try {
    clientBalance.value = await getClientWaybillBalance(String(balanceClientId.value), balanceBrandCode.value)
  } catch {
    message.error('查询余额失败')
  } finally {
    balanceLoading.value = false
  }
}

onMounted(() => fetchPools())
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
