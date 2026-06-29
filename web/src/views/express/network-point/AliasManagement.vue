<template>
  <div class="page-container">
    <PageHeader title="网点名称映射">
      <template #actions>
        <a-input-search
          v-model:value="filters.keyword"
          placeholder="搜索名称/网点编号"
          allow-clear
          style="width: 280px"
          @search="handleFilterChange"
          @pressEnter="handleFilterChange"
        />
        <a-button @click="handleReset">重置</a-button>
        <a-divider type="vertical" style="height: 24px; margin: 0 8px;" />
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增映射
        </a-button>
      </template>
    </PageHeader>

    <!-- 数据表格 -->
    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :data-source="dataSource"
        :loading="loading"
        :pagination="paginationConfig"
        row-key="id"
        bordered
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'createdTime'">
            {{ record.createdTime?.slice(0, 16)?.replace('T', ' ') }}
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small" danger @click="handleDelete(record as NetworkPointAliasDto)">删除</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 新增弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      title="新增网点名称映射"
      width="500px"
      :destroy-on-close="true"
      @cancel="dialogVisible = false"
    >
      <a-form
        ref="formRef"
        :model="form"
        :rules="formRules"
        :label-col="{ style: { width: '100px' } }"
        style="padding: 16px 20px"
      >
        <a-form-item label="名称" name="name">
          <a-input v-model:value="form.name" placeholder="请输入别名" :maxlength="100" />
        </a-form-item>
        <a-form-item label="关联网点" name="networkPointCode">
          <a-select
            v-model:value="form.networkPointCode"
            placeholder="请搜索并选择网点"
            show-search
            :filter-option="false"
            :not-found-content="networkPointSearching ? undefined : null"
            @search="handleNetworkPointSearch"
          >
            <template #notFoundContent>
              <a-spin size="small" />
            </template>
            <a-select-option
              v-for="np in networkPointOptions"
              :key="np.code"
              :value="np.code"
            >
              {{ np.code }} - {{ np.manager || '未命名' }}
            </a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitting" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getNetworkPointAliasList,
  createNetworkPointAlias,
  deleteNetworkPointAlias,
  type NetworkPointAliasDto,
} from '@/api/networkPointAlias'
import { getExpNetworkPointOptions, type NetworkPointDto } from '@/api/express'

const columns = [
  { title: '名称', dataIndex: 'name', width: 200 },
  { title: '关联网点编号', dataIndex: 'networkPointCode', width: 150 },
  { title: '关联网点名称', dataIndex: 'networkPointName', width: 200 },
  { title: '创建时间', dataIndex: 'createdTime', width: 170 },
  { title: '操作', key: 'action', width: 100, fixed: 'right' as const, align: 'center' as const },
]

// ===== 列表查询 =====
const loading = ref(false)
const dataSource = ref<NetworkPointAliasDto[]>([])
const pagination = reactive({ current: 1, pageSize: 20, total: 0 })

const paginationConfig = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  total: pagination.total,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (t: number) => `共 ${t} 条`,
}))

const filters = reactive({
  keyword: '',
})

async function fetchList() {
  loading.value = true
  try {
    const res = await getNetworkPointAliasList({
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
      keyword: filters.keyword || undefined,
    })
    dataSource.value = res.items
    pagination.total = (res as any).totalCount ?? res.total
  } catch {
    message.error('查询失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: any) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

function handleFilterChange() {
  pagination.current = 1
  fetchList()
}

function handleReset() {
  filters.keyword = ''
  pagination.current = 1
  fetchList()
}

// ===== 新增弹窗 =====
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()
const submitting = ref(false)

const form = reactive({
  name: '',
  networkPointCode: undefined as string | undefined,
})

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  networkPointCode: [{ required: true, message: '请选择关联网点', trigger: 'change' }],
}

// 网点远程搜索
const networkPointOptions = ref<NetworkPointDto[]>([])
const networkPointSearching = ref(false)
let searchTimer: ReturnType<typeof setTimeout> | null = null

function handleNetworkPointSearch(val: string) {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(async () => {
    if (!val) {
      networkPointOptions.value = []
      return
    }
    networkPointSearching.value = true
    try {
      const res = await getExpNetworkPointOptions({ keyword: val, pageSize: 30 })
      networkPointOptions.value = res.items
    } catch {
      networkPointOptions.value = []
    } finally {
      networkPointSearching.value = false
    }
  }, 300)
}

function handleAdd() {
  form.name = ''
  form.networkPointCode = undefined
  networkPointOptions.value = []
  dialogVisible.value = true
}

async function handleSubmit() {
  try { await formRef.value?.validate() } catch { return }
  submitting.value = true
  try {
    await createNetworkPointAlias({
      name: form.name,
      networkPointCode: form.networkPointCode!,
    })
    message.success('新增成功')
    dialogVisible.value = false
    fetchList()
  } catch {
    message.error('新增失败')
  } finally {
    submitting.value = false
  }
}

// ===== 删除 =====
function handleDelete(record: NetworkPointAliasDto) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除映射"${record.name}"吗？`,
    okType: 'danger',
    async onOk() {
      await deleteNetworkPointAlias(record.id)
      message.success('删除成功')
      fetchList()
    },
  })
}

onMounted(() => fetchList())
</script>

<style scoped lang="scss">
</style>
