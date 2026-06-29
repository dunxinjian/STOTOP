<template>
  <div class="page-container page-container--flush">
    <PageHeader title="电子签管理" description="管理合同电子签署记录">
      <template #left>
        <a-input v-model:value="searchForm.keyword" size="middle" placeholder="合同号" style="width: 200px" allow-clear @keyup.enter="handleSearch" />
        <a-select v-model:value="searchForm.signStatus" size="middle" placeholder="签署状态" style="width: 140px" allow-clear :options="signStatusOptions" @change="handleSearch" />
        <a-button type="primary" size="middle" @click="handleSearch">查询</a-button>
        <a-button size="middle" @click="handleReset">
          <template #icon><ReloadOutlined /></template>重置
        </a-button>
      </template>
    </PageHeader>

    <a-alert
      type="info"
      message="电子签章功能即将上线，当前使用线下签署模式。签署完成后请手动上传签署文件。"
      show-icon
      style="margin-bottom: 16px"
    />

    <DataTable
      v-model:pagination="pagination"
      :columns="tableColumns"
      :data-source="tableData"
      :loading="loading"
      :scroll="{ x: 1100 }"
      row-key="id"
      empty-text="暂无电子签记录"
      @change="fetchList"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'signStatus'">
          <StatusTag :type="signStatusTagType(record.signStatus)">{{ signStatusText(record.signStatus) }}</StatusTag>
        </template>
        <template v-if="column.dataIndex === 'signedTime'">
          {{ record.signedTime || '-' }}
        </template>
        <template v-if="column.dataIndex === 'action'">
          <a-button
            v-if="has(ContractPermissions.ESignManage) && record.signStatus === 0"
            type="link" size="small" @click="handleMarkSigned(record as ESignRecordDto)"
          >标记已签</a-button>
          <a-button
            v-if="has(ContractPermissions.ESignManage) && record.signStatus === 0"
            type="link" size="small" danger @click="handleReject(record as ESignRecordDto)"
          >拒签</a-button>
        </template>
      </template>
    </DataTable>

    <!-- 标记已签弹窗 -->
    <a-modal
      v-model:open="signDialogVisible"
      title="标记已签"
      width="500px"
      :destroy-on-close="true"
      @cancel="signDialogVisible = false"
    >
      <a-form :label-col="{ style: { width: '100px' } }" style="padding: 10px 20px">
        <a-form-item label="签署时间">
          <a-date-picker
            v-model:value="signForm.signedTime"
            show-time
            format="YYYY-MM-DD HH:mm:ss"
            placeholder="请选择签署时间"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="签署文件">
          <a-upload
            :file-list="signForm.fileList"
            :before-upload="beforeUpload"
            @remove="handleRemoveFile"
          >
            <a-button>
              <UploadOutlined />选择文件
            </a-button>
          </a-upload>
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="signDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmitSign">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { UploadFile } from 'ant-design-vue'
import { UploadOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import dayjs, { type Dayjs } from 'dayjs'
import PageHeader from '@/components/PageHeader.vue'
import DataTable from '@/components/DataTable.vue'
import StatusTag from '@/components/StatusTag.vue'
import { usePermission, ContractPermissions } from '@/utils/permission'
import {
  getESignRecordList,
  completeESign,
  rejectESign,
  type ESignRecordDto,
} from '@/api/contract'

const { has } = usePermission()

const signStatusOptions = [
  { label: '待签', value: 0 },
  { label: '已签', value: 1 },
  { label: '已拒签', value: 2 },
]

function signStatusText(status: number) {
  return ['待签', '已签', '已拒签'][status] || '未知'
}

function signStatusTagType(s: number): 'success' | 'danger' | 'info' | 'default' {
  return (['info', 'success', 'danger'] as const)[s] || 'default'
}

const tableColumns = [
  { title: '合同号', dataIndex: 'contractNo', key: 'contractNo', width: 150 },
  { title: '签署人', dataIndex: 'signer', key: 'signer', width: 120 },
  { title: '签署角色', dataIndex: 'signerRole', key: 'signerRole', width: 100 },
  { title: '签署状态', dataIndex: 'signStatus', key: 'signStatus', width: 100, align: 'center' as const },
  { title: '签署时间', dataIndex: 'signedTime', key: 'signedTime', width: 180 },
  { title: '创建时间', dataIndex: 'createdTime', key: 'createdTime', width: 180 },
  { title: '操作', dataIndex: 'action', key: 'action', width: 160, align: 'center' as const, fixed: 'right' as const },
]

const searchForm = reactive({
  keyword: '',
  signStatus: undefined as number | undefined,
})

const loading = ref(false)
const tableData = ref<ESignRecordDto[]>([])
const pagination = ref({ pageIndex: 1, pageSize: 20, total: 0 })

const signDialogVisible = ref(false)
const submitLoading = ref(false)
const currentRecord = ref<ESignRecordDto | null>(null)

const signForm = reactive({
  signedTime: undefined as Dayjs | undefined,
  fileList: [] as UploadFile[],
})

async function fetchList() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.value.pageIndex,
      pageSize: pagination.value.pageSize,
    }
    if (searchForm.signStatus !== undefined) params.signStatus = searchForm.signStatus
    const res = await getESignRecordList(params) as any
    if (res) {
      tableData.value = res?.items || res || []
      pagination.value.total = res?.total || res?.length || 0
    }
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pagination.value.pageIndex = 1
  fetchList()
}

function handleReset() {
  searchForm.keyword = ''
  searchForm.signStatus = undefined
  pagination.value.pageIndex = 1
  fetchList()
}

function handleMarkSigned(record: ESignRecordDto) {
  currentRecord.value = record
  signForm.signedTime = dayjs()
  signForm.fileList = []
  signDialogVisible.value = true
}

function beforeUpload(file: UploadFile) {
  signForm.fileList = [file]
  return false
}

function handleRemoveFile() {
  signForm.fileList = []
}

async function handleSubmitSign() {
  if (!currentRecord.value) return
  submitLoading.value = true
  try {
    await completeESign(currentRecord.value.id, {
      signedFilePath: signForm.fileList.length > 0 ? (signForm.fileList[0] as any).name : undefined,
    })
    message.success('标记签署成功')
    signDialogVisible.value = false
    fetchList()
  } finally {
    submitLoading.value = false
  }
}

async function handleReject(record: ESignRecordDto) {
  try {
    await rejectESign(record.id)
    message.success('已标记拒签')
    fetchList()
  } catch (error) {
    console.error('操作失败:', error)
  }
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;
</style>
