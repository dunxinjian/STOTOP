<template>
  <div class="asset-settings-page">
    <!-- 顶部 Header -->
    <div class="page-header">
      <div class="header-left">
        <span class="hamburger-icon">≡</span>
        <AccountSetSelector style="width: 200px;" />
        <div class="tab-buttons">
          <button
            class="tab-btn"
            :class="{ active: activeTab === 'category' }"
            @click="activeTab = 'category'"
          >资产类别</button>
          <button
            class="tab-btn"
            :class="{ active: activeTab === 'card' }"
            @click="activeTab = 'card'"
          >资产卡片</button>
        </div>
      </div>
      <div class="header-right">
        <!-- 资产类别工具栏 -->
        <template v-if="activeTab === 'category'">
          <a-upload
            :showUploadList="false"
            accept=".xlsx,.xls"
            :beforeUpload="handleXiaofanCategoryImport"
          >
            <a-button size="small" :loading="categoryImporting"><ImportOutlined />导入(小番财务)</a-button>
          </a-upload>
          <a-button size="small" danger :disabled="!categorySelectedKeys.length" @click="handleBatchDeleteCategory">
            <DeleteOutlined />删除
          </a-button>
          <a-dropdown>
            <a-button size="small"><EllipsisOutlined />更多</a-button>
            <template #overlay>
              <a-menu>
                <a-menu-item key="expandAll" @click="categoryExpandAll">展开全部</a-menu-item>
                <a-menu-item key="collapseAll" @click="categoryCollapseAll">折叠全部</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </template>
        <!-- 资产卡片工具栏 -->
        <template v-if="activeTab === 'card'">
          <a-button size="small" type="primary" @click="openCardAddDialog"><PlusOutlined />新增</a-button>
          <a-upload
            :showUploadList="false"
            accept=".xlsx,.xls"
            :beforeUpload="handleXiaofanImport"
          >
            <a-button size="small" :loading="cardImporting"><ImportOutlined />导入(小番财务)</a-button>
          </a-upload>
          <a-button size="small" danger :disabled="!cardSelectedKeys.length" @click="handleBatchDeleteCard">
            <DeleteOutlined />删除
          </a-button>
          <a-dropdown>
            <a-button size="small"><EllipsisOutlined />更多</a-button>
            <template #overlay>
              <a-menu>
                <a-menu-item key="export">导出</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </template>
      </div>
    </div>

    <!-- 内容区 -->
    <div class="page-content">
      <!-- ========== 资产类别 Tab ========== -->
      <div v-show="activeTab === 'category'" class="tab-panel">
        <a-table
          :columns="categoryColumns"
          :dataSource="categoryTreeData"
          :loading="categoryLoading"
          bordered
          size="small"
          rowKey="id"
          :pagination="false"
          :expandedRowKeys="categoryExpandedKeys"
          @expandedRowsChange="(keys: any) => categoryExpandedKeys = keys"
          :rowSelection="{ selectedRowKeys: categorySelectedKeys, onChange: (keys: any) => categorySelectedKeys = keys }"
          :scroll="{ y: 'calc(100vh - 140px)' }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'action'">
              <a-tooltip title="新增子分类">
                <a-button type="link" size="small" @click="openCategoryAddChild(record)"><PlusOutlined /></a-button>
              </a-tooltip>
            </template>
            <template v-if="column.dataIndex === 'name'">
              <a class="link-text" @click="openCategoryEditDialog(record)">{{ record.name }}</a>
            </template>
            <template v-if="column.dataIndex === 'depreciationMethod'">
              {{ record.depreciationMethod || '-' }}
            </template>
            <template v-if="column.dataIndex === 'usefulLife'">
              {{ record.usefulLife ? record.usefulLife * 12 + '月' : '-' }}
            </template>
            <template v-if="column.dataIndex === 'residualRate'">
              {{ record.residualRate != null ? record.residualRate + '%' : '-' }}
            </template>
            <template v-if="column.dataIndex === 'depreciationAccountName'">
              {{ record.depreciationAccountName || '-' }}
            </template>
            <template v-if="column.dataIndex === 'debitAccountName'">
              {{ record.debitAccountName || '-' }}
            </template>
            <template v-if="column.dataIndex === 'creditAccountName'">
              {{ record.creditAccountName || '-' }}
            </template>
            <template v-if="column.dataIndex === 'remark'">
              {{ record.remark || '' }}
            </template>
          </template>
        </a-table>
      </div>

      <!-- ========== 资产卡片 Tab ========== -->
      <div v-show="activeTab === 'card'" class="tab-panel card-tab-panel">
        <div class="card-main-area">
          <a-table
            :columns="cardColumns"
            :dataSource="filteredCardList"
            :loading="cardLoading"
            bordered
            size="small"
            rowKey="id"
            :pagination="false"
            :rowSelection="{ selectedRowKeys: cardSelectedKeys, onChange: (keys: any) => cardSelectedKeys = keys }"
            :scroll="{ y: 'calc(100vh - 140px)' }"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'name'">
                <a class="link-text" @click="openCardEditDialog(record)">{{ record.name }}</a>
              </template>
              <template v-if="column.dataIndex === 'originalValue'">
                <span class="amount-value">{{ formatAmount(record.originalValue) }}</span>
              </template>
              <template v-if="column.dataIndex === 'residualRate'">
                {{ record.residualRate != null ? record.residualRate + '%' : '-' }}
              </template>
              <template v-if="column.dataIndex === 'entryDate'">
                {{ formatDate(record.entryDate) }}
              </template>
              <template v-if="column.dataIndex === 'entryPeriod'">
                {{ record.entryPeriod || '-' }}
              </template>
              <template v-if="column.dataIndex === 'debitAccountName'">
                {{ record.debitAccountName || '-' }}
              </template>
              <template v-if="column.dataIndex === 'creditAccountName'">
                {{ record.creditAccountName || '-' }}
              </template>
              <template v-if="column.dataIndex === 'monthlyDepreciation'">
                <span class="amount-value">{{ formatAmount(record.monthlyDepreciation) }}</span>
              </template>
              <template v-if="column.dataIndex === 'status'">
                <a-tag :color="statusColorMap[record.status] || 'default'">{{ statusTextMap[record.status] || '未知' }}</a-tag>
              </template>
            </template>
          </a-table>
        </div>
        <!-- 右侧类别筛选面板 -->
        <div class="card-side-panel">
          <div class="side-panel-header">
            <a-input
              v-model:value="categoryFilterKeyword"
              placeholder="搜索类别"
              size="small"
              allowClear
            >
              <template #prefix><SearchOutlined /></template>
            </a-input>
          </div>
          <div class="side-panel-tree">
            <div
              class="tree-item"
              :class="{ active: !cardFilterCategoryId }"
              @click="cardFilterCategoryId = undefined; loadCards()"
            >
              全部类别
            </div>
            <template v-for="cat in filteredCategoryList" :key="cat.id">
              <div
                class="tree-item"
                :class="{ active: cardFilterCategoryId === cat.id }"
                @click="cardFilterCategoryId = cat.id; loadCards()"
              >
                {{ cat.code }}_{{ cat.name }}
              </div>
            </template>
          </div>
        </div>
      </div>
    </div>

    <!-- ========== 资产类别弹窗 ========== -->
    <a-modal
      v-model:open="categoryDialogVisible"
      :title="categoryEditingId ? '编辑资产类别' : '新增资产类别'"
      :width="480"
      :destroyOnClose="true"
    >
      <a-form
        ref="categoryFormRef"
        :model="categoryForm"
        :rules="categoryRules"
        :labelCol="{ span: 6 }"
        :wrapperCol="{ span: 16 }"
      >
        <a-form-item label="类别编码" name="code">
          <a-input v-model:value="categoryForm.code" placeholder="请输入类别编码" :disabled="!!categoryEditingId" />
        </a-form-item>
        <a-form-item label="类别名称" name="name">
          <a-input v-model:value="categoryForm.name" placeholder="请输入类别名称" />
        </a-form-item>
        <a-form-item label="折旧方法" name="depreciationMethod">
          <a-select
            v-model:value="categoryForm.depreciationMethod"
            placeholder="请选择折旧方法"
            style="width: 100%"
            :options="depreciationMethodOptions"
          />
        </a-form-item>
        <a-form-item label="使用年限" name="usefulLife">
          <a-input-number v-model:value="categoryForm.usefulLife" :min="1" style="width: 100%" placeholder="请输入使用年限" />
        </a-form-item>
        <a-form-item label="残值率" name="residualRate">
          <a-input-number v-model:value="categoryForm.residualRate" :min="0" :max="100" :precision="2" style="width: 100%" placeholder="请输入残值率">
            <template #addonAfter>%</template>
          </a-input-number>
        </a-form-item>
        <a-form-item label="折旧科目ID" name="depreciationAccountId">
          <a-input-number v-model:value="categoryForm.depreciationAccountId" style="width: 100%" placeholder="请输入折旧科目ID（可选）" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="categoryDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="categorySaving" @click="handleCategorySave">保存</a-button>
      </template>
    </a-modal>

    <!-- ========== 资产卡片弹窗 ========== -->
    <a-modal
      v-model:open="cardDialogVisible"
      :title="cardIsEdit ? '编辑资产卡片' : '新增资产卡片'"
      :width="520"
      :destroyOnClose="true"
    >
      <a-form
        ref="cardFormRef"
        :model="cardForm"
        :rules="cardCurrentRules"
        :labelCol="{ span: 6 }"
        :wrapperCol="{ span: 16 }"
      >
        <template v-if="!cardIsEdit">
          <a-form-item label="资产编码" name="code">
            <a-input v-model:value="cardForm.code" placeholder="请输入资产编码" />
          </a-form-item>
          <a-form-item label="资产名称" name="name">
            <a-input v-model:value="cardForm.name" placeholder="请输入资产名称" />
          </a-form-item>
          <a-form-item label="所属类别" name="categoryId">
            <a-select
              v-model:value="cardForm.categoryId"
              placeholder="请选择类别"
              style="width: 100%"
              :options="categoryList.map((c: any) => ({ label: c.name, value: c.id }))"
            />
          </a-form-item>
          <a-form-item label="部门ID" name="departmentId">
            <a-input-number v-model:value="cardForm.departmentId" placeholder="可选" style="width: 100%" />
          </a-form-item>
          <a-form-item label="原值" name="originalValue">
            <a-input-number v-model:value="cardForm.originalValue" :precision="2" :min="0" placeholder="请输入原值" style="width: 100%" />
          </a-form-item>
          <a-form-item label="入账日期" name="entryDate">
            <a-date-picker v-model:value="cardForm.entryDate" valueFormat="YYYY-MM-DD" placeholder="请选择入账日期" style="width: 100%" />
          </a-form-item>
          <a-form-item label="开始折旧日期" name="depreciationStartDate">
            <a-date-picker v-model:value="cardForm.depreciationStartDate" valueFormat="YYYY-MM-DD" placeholder="可选" style="width: 100%" />
          </a-form-item>
          <a-form-item label="使用年限" name="usefulLifeYears">
            <a-input-number v-model:value="cardForm.usefulLifeYears" :min="0" placeholder="留空则使用类别默认值" style="width: 100%" />
          </a-form-item>
          <a-form-item label="残值率(%)" name="residualRate">
            <a-input-number v-model:value="cardForm.residualRate" :min="0" :max="100" :precision="2" placeholder="留空则使用类别默认值" style="width: 100%" />
          </a-form-item>
          <a-form-item label="备注" name="remark">
            <a-textarea v-model:value="cardForm.remark" placeholder="可选" :rows="3" />
          </a-form-item>
        </template>
        <template v-else>
          <a-form-item label="资产名称" name="name">
            <a-input v-model:value="cardForm.name" placeholder="请输入资产名称" />
          </a-form-item>
          <a-form-item label="部门ID" name="departmentId">
            <a-input-number v-model:value="cardForm.departmentId" placeholder="可选" style="width: 100%" />
          </a-form-item>
          <a-form-item label="备注" name="remark">
            <a-textarea v-model:value="cardForm.remark" placeholder="可选" :rows="3" />
          </a-form-item>
        </template>
      </a-form>
      <template #footer>
        <a-button @click="cardDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="cardSaving" @click="handleCardSave">保存</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch, h } from 'vue'
import { message, Modal } from 'ant-design-vue'
import {
  PlusOutlined,
  DeleteOutlined,
  EllipsisOutlined,
  SearchOutlined,
  ImportOutlined,
} from '@ant-design/icons-vue'
import type { FormInstance } from 'ant-design-vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import { useAccountSetStore } from '@/stores/accountSet'
import {
  getAssetCategories,
  createAssetCategory,
  updateAssetCategory,
  deleteAssetCategory,
  getAssetCards,
  createAssetCard,
  updateAssetCard,
  deleteAssetCard,
  importAssetCardsFromXiaofan,
  importAssetCategoriesFromXiaofan,
} from '@/api/finance'

const accountSetStore = useAccountSetStore()

// ==================== 通用 ====================
const activeTab = ref<'category' | 'card'>('category')

// 状态映射
const statusColorMap: Record<number, string> = { 1: 'green', 0: 'default', 2: 'red' }
const statusTextMap: Record<number, string> = { 1: '使用中', 0: '停用', 2: '已处置' }

// 折旧方法选项
const depreciationMethodOptions = [
  { label: '直线法', value: '直线法' },
  { label: '双倍余额递减法', value: '双倍余额递减法' },
  { label: '年数总和法', value: '年数总和法' },
]

// ==================== 资产类别 ====================
const categoryList = ref<any[]>([])
const categoryTreeData = ref<any[]>([])
const categoryLoading = ref(false)
const categorySelectedKeys = ref<any[]>([])
const categoryExpandedKeys = ref<any[]>([])

const categoryColumns = [
  { title: '操作', dataIndex: 'action', key: 'action', width: 60, align: 'center' as const },
  { title: '编码', dataIndex: 'code', key: 'code', width: 100 },
  { title: '名称', dataIndex: 'name', key: 'name', width: 160 },
  { title: '折旧/摊销方法', dataIndex: 'depreciationMethod', key: 'depreciationMethod', width: 130 },
  { title: '默认总期限', dataIndex: 'usefulLife', key: 'usefulLife', width: 100, align: 'center' as const },
  { title: '默认残值率', dataIndex: 'residualRate', key: 'residualRate', width: 100, align: 'center' as const },
  { title: '默认资产科目', dataIndex: 'depreciationAccountName', key: 'depreciationAccountName', width: 140 },
  { title: '借方科目', dataIndex: 'debitAccountName', key: 'debitAccountName', width: 140 },
  { title: '贷方科目', dataIndex: 'creditAccountName', key: 'creditAccountName', width: 140 },
  { title: '备注', dataIndex: 'remark', key: 'remark', width: 120 },
]

// 类别弹窗
const categoryDialogVisible = ref(false)
const categorySaving = ref(false)
const categoryEditingId = ref<number | null>(null)
const categoryParentId = ref<number | null>(null)
const categoryFormRef = ref<FormInstance>()

const categoryForm = reactive({
  code: '',
  name: '',
  depreciationMethod: undefined as string | undefined,
  usefulLife: 5,
  residualRate: 5,
  depreciationAccountId: undefined as number | undefined,
})

const categoryRules: Record<string, any[]> = {
  code: [{ required: true, message: '请输入类别编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入类别名称', trigger: 'blur' }],
  depreciationMethod: [{ required: true, message: '请选择折旧方法', trigger: 'change' }],
  usefulLife: [{ required: true, message: '请输入使用年限', trigger: 'blur' }],
  residualRate: [{ required: true, message: '请输入残值率', trigger: 'blur' }],
}

async function loadCategories() {
  const asId = accountSetStore.getCurrentAccountSetId()
  if (!asId) return
  categoryLoading.value = true
  try {
    const data = await getAssetCategories(asId)
    categoryList.value = data
    categoryTreeData.value = buildCategoryTree(data)
  } catch (e) {
    message.error('加载资产类别数据失败')
  } finally {
    categoryLoading.value = false
  }
}

function buildCategoryTree(list: any[]): any[] {
  // If data already has children structure, return directly
  if (list.some(item => item.children)) return list
  // Otherwise flat list as root items
  return list.map(item => ({ ...item }))
}

function categoryExpandAll() {
  const keys: any[] = []
  const collect = (list: any[]) => {
    list.forEach(item => {
      if (item.children && item.children.length) {
        keys.push(item.id)
        collect(item.children)
      }
    })
  }
  collect(categoryTreeData.value)
  categoryExpandedKeys.value = keys
}

function categoryCollapseAll() {
  categoryExpandedKeys.value = []
}

function openCategoryAddChild(parent: any) {
  categoryEditingId.value = null
  categoryParentId.value = parent.id
  Object.assign(categoryForm, {
    code: parent.code,
    name: '',
    depreciationMethod: parent.depreciationMethod || undefined,
    usefulLife: parent.usefulLife || 5,
    residualRate: parent.residualRate ?? 5,
    depreciationAccountId: parent.depreciationAccountId || undefined,
  })
  categoryDialogVisible.value = true
}

function openCategoryEditDialog(row: any) {
  categoryEditingId.value = row.id
  categoryParentId.value = null
  Object.assign(categoryForm, {
    code: row.code,
    name: row.name,
    depreciationMethod: row.depreciationMethod,
    usefulLife: row.usefulLife,
    residualRate: row.residualRate,
    depreciationAccountId: row.depreciationAccountId || undefined,
  })
  categoryDialogVisible.value = true
}

async function handleCategorySave() {
  try {
    await categoryFormRef.value?.validate()
  } catch {
    return
  }
  const asId = accountSetStore.getCurrentAccountSetId()
  if (!asId) { message.warning('请先选择账套'); return }
  categorySaving.value = true
  try {
    const payload: any = {
      code: categoryForm.code,
      name: categoryForm.name,
      depreciationMethod: categoryForm.depreciationMethod!,
      usefulLife: categoryForm.usefulLife,
      residualRate: categoryForm.residualRate,
      depreciationAccountId: categoryForm.depreciationAccountId || null,
    }
    if (categoryParentId.value) {
      payload.parentId = categoryParentId.value
    }
    if (categoryEditingId.value) {
      await updateAssetCategory(categoryEditingId.value, payload, asId)
    } else {
      await createAssetCategory(payload, asId)
    }
    message.success('保存成功')
    categoryDialogVisible.value = false
    await loadCategories()
  } catch (e: any) {
    message.error(e?.message || '保存失败')
  } finally {
    categorySaving.value = false
  }
}

function handleBatchDeleteCategory() {
  if (!categorySelectedKeys.value.length) return
  Modal.confirm({
    title: '确认删除',
    content: `确定删除选中的 ${categorySelectedKeys.value.length} 项资产类别？`,
    onOk: async () => {
      try {
        for (const id of categorySelectedKeys.value) {
          await deleteAssetCategory(id)
        }
        message.success('删除成功')
        categorySelectedKeys.value = []
        await loadCategories()
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    },
  })
}

// ==================== 资产卡片 ====================
const cardList = ref<any[]>([])
const cardLoading = ref(false)
const cardSelectedKeys = ref<any[]>([])
const cardFilterCategoryId = ref<number | undefined>(undefined)
const categoryFilterKeyword = ref('')

const cardColumns = [
  { title: '编码', dataIndex: 'code', key: 'code', width: 100 },
  { title: '名称', dataIndex: 'name', key: 'name', width: 150 },
  { title: '原值', dataIndex: 'originalValue', key: 'originalValue', width: 120, align: 'right' as const },
  { title: '残值率', dataIndex: 'residualRate', key: 'residualRate', width: 80, align: 'center' as const },
  { title: '启用日期', dataIndex: 'entryDate', key: 'entryDate', width: 100, align: 'center' as const },
  { title: '录入期间', dataIndex: 'entryPeriod', key: 'entryPeriod', width: 100, align: 'center' as const },
  { title: '借方科目', dataIndex: 'debitAccountName', key: 'debitAccountName', width: 130 },
  { title: '贷方科目', dataIndex: 'creditAccountName', key: 'creditAccountName', width: 130 },
  { title: '月折旧/摊销', dataIndex: 'monthlyDepreciation', key: 'monthlyDepreciation', width: 120, align: 'right' as const },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80, align: 'center' as const },
]

// 右侧类别筛选
const filteredCategoryList = computed(() => {
  if (!categoryFilterKeyword.value.trim()) return categoryList.value
  const kw = categoryFilterKeyword.value.trim().toLowerCase()
  return categoryList.value.filter(
    (c: any) => c.name.toLowerCase().includes(kw) || (c.code && c.code.toLowerCase().includes(kw))
  )
})

const filteredCardList = computed(() => {
  return cardList.value
})

async function loadCards() {
  const asId = accountSetStore.getCurrentAccountSetId()
  if (!asId) return
  cardLoading.value = true
  try {
    const params: any = { accountSetId: asId }
    if (cardFilterCategoryId.value) params.categoryId = cardFilterCategoryId.value
    cardList.value = await getAssetCards(params)
  } catch (e) {
    message.error('加载资产卡片数据失败')
  } finally {
    cardLoading.value = false
  }
}

// 卡片弹窗
const cardDialogVisible = ref(false)
const cardSaving = ref(false)
const cardIsEdit = ref(false)
const cardEditingId = ref<number | null>(null)
const cardFormRef = ref<FormInstance>()

const cardForm = reactive({
  code: '',
  name: '',
  categoryId: undefined as number | undefined,
  departmentId: undefined as number | undefined,
  originalValue: undefined as number | undefined,
  entryDate: '',
  depreciationStartDate: '',
  usefulLifeYears: undefined as number | undefined,
  residualRate: undefined as number | undefined,
  remark: '',
})

const cardAddRules: Record<string, any[]> = {
  code: [{ required: true, message: '请输入资产编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入资产名称', trigger: 'blur' }],
  categoryId: [{ required: true, message: '请选择所属类别', trigger: 'change' }],
  originalValue: [{ required: true, message: '请输入原值', trigger: 'blur' }],
  entryDate: [{ required: true, message: '请选择入账日期', trigger: 'change' }],
}

const cardEditRules: Record<string, any[]> = {}

const cardCurrentRules = computed(() => cardIsEdit.value ? cardEditRules : cardAddRules)

function openCardAddDialog() {
  cardIsEdit.value = false
  cardEditingId.value = null
  Object.assign(cardForm, {
    code: '',
    name: '',
    categoryId: undefined,
    departmentId: undefined,
    originalValue: undefined,
    entryDate: '',
    depreciationStartDate: '',
    usefulLifeYears: undefined,
    residualRate: undefined,
    remark: '',
  })
  cardDialogVisible.value = true
}

function openCardEditDialog(row: any) {
  cardIsEdit.value = true
  cardEditingId.value = row.id
  Object.assign(cardForm, {
    name: row.name || '',
    departmentId: row.departmentId || undefined,
    remark: row.remark || '',
  })
  cardDialogVisible.value = true
}

async function handleCardSave() {
  try {
    await cardFormRef.value?.validate()
  } catch {
    return
  }
  const asId = accountSetStore.getCurrentAccountSetId()
  if (!asId) { message.warning('请先选择账套'); return }
  cardSaving.value = true
  try {
    if (cardIsEdit.value && cardEditingId.value) {
      await updateAssetCard(cardEditingId.value, {
        name: cardForm.name,
        departmentId: cardForm.departmentId || undefined,
        remark: cardForm.remark || undefined,
      }, asId)
    } else {
      await createAssetCard({
        code: cardForm.code,
        name: cardForm.name,
        categoryId: cardForm.categoryId,
        departmentId: cardForm.departmentId || undefined,
        originalValue: cardForm.originalValue,
        entryDate: cardForm.entryDate,
        depreciationStartDate: cardForm.depreciationStartDate || undefined,
        usefulLifeYears: cardForm.usefulLifeYears || undefined,
        residualRate: cardForm.residualRate != null ? cardForm.residualRate : undefined,
        remark: cardForm.remark || undefined,
      }, asId)
    }
    message.success('保存成功')
    cardDialogVisible.value = false
    await loadCards()
  } catch (e: any) {
    message.error(e?.message || '保存失败')
  } finally {
    cardSaving.value = false
  }
}

function handleBatchDeleteCard() {
  if (!cardSelectedKeys.value.length) return
  Modal.confirm({
    title: '确认删除',
    content: `确定删除选中的 ${cardSelectedKeys.value.length} 项资产卡片？`,
    onOk: async () => {
      try {
        for (const id of cardSelectedKeys.value) {
          await deleteAssetCard(id)
        }
        message.success('删除成功')
        cardSelectedKeys.value = []
        await loadCards()
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    },
  })
}

// ==================== 小番财务 Excel 导入 ====================
const cardImporting = ref(false)
const categoryImporting = ref(false)

async function handleXiaofanCategoryImport(file: File): Promise<boolean> {
  const asId = accountSetStore.getCurrentAccountSetId()
  if (!asId) { message.warning('请先选择账套'); return false }
  const ext = file.name.toLowerCase()
  if (!ext.endsWith('.xlsx') && !ext.endsWith('.xls')) {
    message.error('仅支持 .xlsx 或 .xls 格式')
    return false
  }
  categoryImporting.value = true
  try {
    const fd = new FormData()
    fd.append('file', file)
    const res = await importAssetCategoriesFromXiaofan(fd, asId)
    const { totalRows, importedCount, skippedCount, errors } = res
    if (errors && errors.length) {
      Modal.warning({
        title: `导入完成：共 ${totalRows} 行，成功 ${importedCount}，跳过 ${skippedCount}`,
        width: 640,
        content: () =>
          h(
            'div',
            { style: 'max-height: 360px; overflow: auto;' },
            errors.slice(0, 200).map((e) =>
              h('div', { style: 'padding: 2px 0; color: var(--color-danger-text); font-size: 12px;' },
                `第 ${e.rowNumber} 行：${e.message}`),
            ),
          ),
      })
    } else {
      message.success(`导入成功 ${importedCount} 项资产类别`)
    }
    await loadCategories()
  } catch (e: any) {
    message.error(e?.message || '导入失败')
  } finally {
    categoryImporting.value = false
  }
  return false
}

async function handleXiaofanImport(file: File): Promise<boolean> {
  const asId = accountSetStore.getCurrentAccountSetId()
  if (!asId) { message.warning('请先选择账套'); return false }
  const ext = file.name.toLowerCase()
  if (!ext.endsWith('.xlsx') && !ext.endsWith('.xls')) {
    message.error('仅支持 .xlsx 或 .xls 格式')
    return false
  }
  cardImporting.value = true
  try {
    const fd = new FormData()
    fd.append('file', file)
    const res = await importAssetCardsFromXiaofan(fd, asId)
    const { totalRows, importedCount, skippedCount, errors } = res
    if (errors && errors.length) {
      Modal.warning({
        title: `导入完成：共 ${totalRows} 行，成功 ${importedCount}，跳过 ${skippedCount}`,
        width: 640,
        content: () =>
          h(
            'div',
            { style: 'max-height: 360px; overflow: auto;' },
            errors.slice(0, 200).map((e) =>
              h('div', { style: 'padding: 2px 0; color: var(--color-danger-text); font-size: 12px;' },
                `第 ${e.rowNumber} 行：${e.message}`),
            ),
          ),
      })
    } else {
      message.success(`导入成功 ${importedCount} 项资产卡片`)
    }
    await loadCards()
  } catch (e: any) {
    message.error(e?.message || '导入失败')
  } finally {
    cardImporting.value = false
  }
  return false // 阻止 a-upload 自动上传
}

// ==================== 工具函数 ====================
function formatAmount(val: number | undefined) {
  if (val === undefined || val === null) return '0.00'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function formatDate(dateStr: string) {
  if (!dateStr) return ''
  return dateStr.substring(0, 10)
}

// ==================== 监听账套切换 ====================
watch(() => accountSetStore.currentAccountSetId, () => {
  categorySelectedKeys.value = []
  cardSelectedKeys.value = []
  loadCategories()
  loadCards()
})

// ==================== 初始化 ====================
onMounted(async () => {
  await loadCategories()
  await loadCards()
})
</script>

<style scoped lang="scss">
.asset-settings-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
  border-bottom: 1px solid #e8e8e8;
  background: #fff;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.hamburger-icon {
  font-size: 20px;
  cursor: pointer;
  color: #666;
  user-select: none;
}

.tab-buttons {
  display: flex;
  gap: 4px;
}

.tab-btn {
  padding: 4px 14px;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  background: #fff;
  cursor: pointer;
  font-size: 13px;
  color: #333;
  transition: all 0.2s;

  &:hover {
    border-color: var(--color-primary);
    color: var(--color-primary);
  }

  &.active {
    background: var(--bg-card);
    border-color: var(--border-strong);
    color: var(--text-1);
    font-weight: 600;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
  }
}

.header-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-content {
  flex: 1;
  overflow: hidden;
  min-height: 0;
}

.tab-panel {
  height: 100%;
  overflow: hidden;
}

.card-tab-panel {
  display: flex;
}

.card-main-area {
  flex: 1;
  overflow: hidden;
  min-width: 0;
}

.card-side-panel {
  width: 250px;
  flex-shrink: 0;
  border-left: 1px solid #e8e8e8;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.side-panel-header {
  padding: 8px;
  border-bottom: 1px solid #f0f0f0;
}

.side-panel-tree {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.tree-item {
  padding: 6px 12px;
  cursor: pointer;
  font-size: 13px;
  color: #333;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  border-left: 3px solid transparent;

  &:hover {
    background: #f5f5f5;
  }

  &.active {
    background: var(--bg-muted);
    color: var(--text-1);
    border-left-color: var(--color-primary);
    font-weight: 500;
  }
}

.link-text {
  color: var(--text-1);
  cursor: pointer;
  &:hover {
    color: var(--color-primary);
    text-decoration: underline;
  }
}

.amount-value {
  font-family: 'Courier New', monospace;
  font-size: 13px;
  color: var(--color-info);
  font-weight: 600;
}

// 表格紧凑
:deep(.ant-table) {
  font-size: 13px;
}

:deep(.ant-table-thead > tr > th) {
  padding: 6px 8px !important;
}

:deep(.ant-table-tbody > tr > td) {
  padding: 4px 8px !important;
}
</style>
