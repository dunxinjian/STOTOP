<template>
  <div class="page-container">
    <PageHeader title="大区与省区管理" />

    <a-card :bordered="false">
      <a-tabs v-model:activeKey="activeTab">
        <!-- ========== 大区管理 ========== -->
        <a-tab-pane key="region" tab="大区管理">
          <div style="margin-bottom: 12px; text-align: right">
            <a-button type="primary" size="small" @click="handleAddRegion">
              <template #icon><PlusOutlined /></template>新增大区
            </a-button>
          </div>
          <a-table
            :columns="regionColumns"
            :data-source="regionTableData"
            :loading="regionLoading"
            :pagination="false"
            row-key="name"
            bordered
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'action'">
                <a-button type="link" size="small" @click="handleRenameRegion(record as any)">重命名</a-button>
              </template>
            </template>
          </a-table>

          <!-- 新增大区弹窗 -->
          <a-modal v-model:open="addRegionVisible" title="新增大区" :width="400" @ok="submitAddRegion" :confirmLoading="addRegionSubmitting">
            <a-form :label-col="{ style: { width: '80px' } }" style="padding: 12px 0">
              <a-form-item label="大区名称" required>
                <a-input v-model:value="newRegionName" placeholder="请输入大区名称" :maxlength="20" />
              </a-form-item>
            </a-form>
          </a-modal>

          <!-- 重命名大区弹窗 -->
          <a-modal v-model:open="renameVisible" title="重命名大区" :width="400" @ok="submitRenameRegion" :confirmLoading="renameSubmitting">
            <a-form :label-col="{ style: { width: '80px' } }" style="padding: 12px 0">
              <a-form-item label="原名称">
                <a-input :value="renameOldName" disabled />
              </a-form-item>
              <a-form-item label="新名称" required>
                <a-input v-model:value="renameNewName" placeholder="请输入新名称" :maxlength="20" />
              </a-form-item>
            </a-form>
          </a-modal>
        </a-tab-pane>

        <!-- ========== 省区管理 ========== -->
        <a-tab-pane key="province" tab="省区管理">
          <!-- 筛选 -->
          <a-row :gutter="16" style="margin-bottom: 12px" align="middle">
            <a-col :span="4">
              <a-select v-model:value="filters.region" placeholder="大区" allow-clear style="width: 100%" @change="handleFilterChange">
                <a-select-option v-for="r in regionOptions" :key="r" :value="r">{{ r }}</a-select-option>
              </a-select>
            </a-col>
            <a-col :span="4">
              <a-select v-model:value="(filters as any).isRemote" placeholder="偏远标记" allow-clear style="width: 100%" @change="handleFilterChange">
                <a-select-option :value="true">偏远</a-select-option>
                <a-select-option :value="false">非偏远</a-select-option>
              </a-select>
            </a-col>
            <a-col :span="6">
              <a-input-search v-model:value="filters.keyword" placeholder="搜索编码/名称/简称" allow-clear @search="handleFilterChange" @pressEnter="handleFilterChange" />
            </a-col>
            <a-col :flex="1" style="text-align: right">
              <a-button type="primary" @click="handleAddProvince">
                <template #icon><PlusOutlined /></template>新增省区
              </a-button>
            </a-col>
          </a-row>

          <a-table
            :columns="provinceColumns"
            :data-source="filteredProvinces"
            :loading="provinceLoading"
            :pagination="false"
            row-key="id"
            bordered
            :scroll="{ x: 800 }"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'isRemote'">
                <a-tag :color="record.isRemote ? 'warning' : 'default'">
                  {{ record.isRemote ? '偏远' : '否' }}
                </a-tag>
              </template>
              <template v-if="column.key === 'action'">
                <a-button type="link" size="small" @click="handleEditProvince(record as any)">编辑</a-button>
                <a-popconfirm title="确定删除该省区？" ok-text="确定" cancel-text="取消" @confirm="handleDeleteProvince(record as any)">
                  <a-button type="link" size="small" danger>删除</a-button>
                </a-popconfirm>
              </template>
            </template>
          </a-table>
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <!-- 新增/编辑省区弹窗 -->
    <a-modal v-model:open="provinceDialogVisible" :title="editingId ? '编辑省区' : '新增省区'" :width="520" :destroy-on-close="true" @cancel="provinceDialogVisible = false">
      <a-form ref="formRef" :model="form" :rules="formRules" :label-col="{ style: { width: '80px' } }" style="padding: 12px 0">
        <a-form-item label="编码" name="code">
          <a-input v-model:value="form.code" placeholder="如: 11" :maxlength="10" />
        </a-form-item>
        <a-form-item label="名称" name="name">
          <a-input v-model:value="form.name" placeholder="如: 北京市" :maxlength="20" />
        </a-form-item>
        <a-form-item label="简称" name="shortName">
          <a-input v-model:value="form.shortName" placeholder="如: 北京" :maxlength="10" />
        </a-form-item>
        <a-form-item label="大区" name="region">
          <a-select v-model:value="form.region" placeholder="选择大区" allow-clear style="width: 100%">
            <a-select-option v-for="r in regionOptions" :key="r" :value="r">{{ r }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="偏远" name="isRemote">
          <a-switch v-model:checked="form.isRemote" checked-children="是" un-checked-children="否" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="provinceDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitting" @click="handleSubmitProvince">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getProvinceList,
  getRegionList,
  createProvince,
  updateProvince,
  deleteProvince,
  renameRegion,
  type ProvinceDto,
  type CreateProvinceRequest,
} from '@/api/express'

const activeTab = ref('region')

// ==================== 大区管理 ====================

const regionLoading = ref(false)
const regions = ref<string[]>([])
const allProvinces = ref<ProvinceDto[]>([])

const regionColumns = [
  { title: '大区名称', dataIndex: 'name', width: 200 },
  { title: '省区数量', dataIndex: 'count', width: 120, align: 'center' as const },
  { title: '包含省区', dataIndex: 'provinces', ellipsis: true },
  { title: '操作', key: 'action', width: 120, align: 'center' as const },
]

const regionTableData = computed(() => {
  return regions.value.map(r => {
    const provs = allProvinces.value.filter(p => p.region === r)
    return {
      name: r,
      count: provs.length,
      provinces: provs.map(p => p.shortName || p.name).join('、'),
    }
  })
})

// 新增大区
const addRegionVisible = ref(false)
const addRegionSubmitting = ref(false)
const newRegionName = ref('')

function handleAddRegion() {
  newRegionName.value = ''
  addRegionVisible.value = true
}

async function submitAddRegion() {
  const name = newRegionName.value.trim()
  if (!name) {
    message.warning('请输入大区名称')
    return
  }
  if (regions.value.includes(name)) {
    message.warning('该大区已存在')
    return
  }
  // 新增大区时需要至少创建一个占位省区，或者直接添加到列表
  // 由于大区是从省份的 region 字段聚合而来，新增空大区需要在前端暂存
  // 这里直接加入本地列表，用户后续新增省区时选择该大区即可
  regions.value.push(name)
  addRegionVisible.value = false
  message.success('大区已添加，请在省区管理中为其添加省区')
}

// 重命名大区
const renameVisible = ref(false)
const renameSubmitting = ref(false)
const renameOldName = ref('')
const renameNewName = ref('')

function handleRenameRegion(record: { name: string }) {
  renameOldName.value = record.name
  renameNewName.value = record.name
  renameVisible.value = true
}

async function submitRenameRegion() {
  const newName = renameNewName.value.trim()
  if (!newName) {
    message.warning('请输入新名称')
    return
  }
  if (newName === renameOldName.value) {
    renameVisible.value = false
    return
  }
  renameSubmitting.value = true
  try {
    await renameRegion(renameOldName.value, newName)
    message.success('重命名成功')
    renameVisible.value = false
    await fetchAll()
  } catch {
    message.error('重命名失败')
  } finally {
    renameSubmitting.value = false
  }
}

// ==================== 省区管理 ====================

const provinceLoading = ref(false)
const regionOptions = computed(() => regions.value)

const provinceColumns = [
  { title: '编码', dataIndex: 'code', width: 80, align: 'center' as const },
  { title: '名称', dataIndex: 'name', width: 160 },
  { title: '简称', dataIndex: 'shortName', width: 100 },
  { title: '大区', dataIndex: 'region', width: 100, align: 'center' as const },
  { title: '偏远', dataIndex: 'isRemote', width: 80, align: 'center' as const },
  { title: '操作', key: 'action', width: 140, fixed: 'right' as const, align: 'center' as const },
]

const filters = reactive({
  region: undefined as string | undefined,
  isRemote: undefined as boolean | undefined,
  keyword: '',
})

const filteredProvinces = computed(() => {
  let list = allProvinces.value
  if (filters.region) {
    list = list.filter(p => p.region === filters.region)
  }
  if (filters.isRemote !== undefined) {
    list = list.filter(p => p.isRemote === filters.isRemote)
  }
  if (filters.keyword) {
    const kw = filters.keyword.trim().toLowerCase()
    list = list.filter(p =>
      p.code.toLowerCase().includes(kw) ||
      p.name.toLowerCase().includes(kw) ||
      p.shortName.toLowerCase().includes(kw)
    )
  }
  return list
})

function handleFilterChange() {
  // 前端筛选，无需重新请求
}

// 弹窗
const provinceDialogVisible = ref(false)
const editingId = ref<number | undefined>()
const submitting = ref(false)
const formRef = ref<FormInstance>()

const form = reactive({
  code: '',
  name: '',
  shortName: '',
  region: undefined as string | undefined,
  isRemote: false,
})

const formRules = {
  code: [{ required: true, message: '请输入编码' }],
  name: [{ required: true, message: '请输入名称' }],
  shortName: [{ required: true, message: '请输入简称' }],
}

function resetForm() {
  form.code = ''
  form.name = ''
  form.shortName = ''
  form.region = undefined
  form.isRemote = false
}

function handleAddProvince() {
  editingId.value = undefined
  resetForm()
  provinceDialogVisible.value = true
}

function handleEditProvince(record: ProvinceDto) {
  editingId.value = record.id
  Object.assign(form, {
    code: record.code,
    name: record.name,
    shortName: record.shortName,
    region: record.region || undefined,
    isRemote: record.isRemote,
  })
  provinceDialogVisible.value = true
}

async function handleSubmitProvince() {
  try { await formRef.value?.validate() } catch { return }
  submitting.value = true
  try {
    const data: CreateProvinceRequest = {
      code: form.code,
      name: form.name,
      shortName: form.shortName,
      region: form.region || undefined,
      isRemote: form.isRemote,
    }
    if (editingId.value) {
      await updateProvince(editingId.value, data)
      message.success('更新成功')
    } else {
      await createProvince(data)
      message.success('创建成功')
    }
    provinceDialogVisible.value = false
    await fetchAll()
  } catch {
    message.error('保存失败')
  } finally {
    submitting.value = false
  }
}

async function handleDeleteProvince(record: ProvinceDto) {
  try {
    await deleteProvince(record.id)
    message.success('删除成功')
    await fetchAll()
  } catch {
    message.error('删除失败')
  }
}

// ==================== 数据加载 ====================

async function fetchAll() {
  regionLoading.value = true
  provinceLoading.value = true
  try {
    const [provinceData, regionData] = await Promise.all([
      getProvinceList(),
      getRegionList(),
    ])
    allProvinces.value = provinceData
    regions.value = regionData
  } catch {
    message.error('加载数据失败')
  } finally {
    regionLoading.value = false
    provinceLoading.value = false
  }
}

onMounted(fetchAll)
</script>

<style scoped>
.page-container {
  padding: 0;
}
</style>
