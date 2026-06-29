<template>
  <div class="page-container account-manage">
    <PageHeader title="科目管理">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #right>
        <a-button v-if="currentCategory !== 'initial' && accountSetStore.hasAccountSetPermission(AccountSetPermissions.SubjectEdit)" type="primary" @click="handleAdd">
          <PlusOutlined />新增
        </a-button>
        <a-button v-if="currentCategory !== 'initial' && accountSetStore.hasAccountSetPermission(AccountSetPermissions.SubjectEdit)" @click="handleBatchDelete" :disabled="selectedRows.length === 0">
          <DeleteOutlined />删除
        </a-button>
        <a-dropdown v-if="currentCategory !== 'initial'" :trigger="['click']">
          <a-button>
            更多<DownOutlined />
          </a-button>
          <template #overlay>
            <a-menu @click="handleMoreCommand">
              <a-menu-item key="import">导入</a-menu-item>
              <a-menu-item key="export">导出</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </template>
      <template #toolbar>
        <div class="toolbar-wrapper">
          <!-- 胶囊式Tab筛选 -->
          <div class="category-capsules">
            <button
              v-for="cat in categories"
              :key="cat.value"
              :class="['capsule-btn', { active: currentCategory === cat.value }]"
              @click="handleCategoryChange(cat.value)"
            >
              {{ cat.label }}
            </button>
          </div>
        </div>
      </template>
    </PageHeader>

    <!-- 科目树表格 -->
    <a-card v-show="currentCategory !== 'initial'" :bordered="false" class="table-card tree-table-card">
      <a-table
        ref="tableRef"
        :columns="accountColumns"
        :dataSource="flattenAccounts"
        rowKey="id"
        :bordered="false"
        :loading="loading"
        :pagination="false"
        :scroll="{ x: 'max-content' }"
        :rowSelection="{ selectedRowKeys: selectedRowKeys, onChange: onSelectionChange }"
        :rowClassName="(record: any) => 'tree-row' + (record._hasChildren ? ' has-children' : '')"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'action'">
            <div v-if="canEdit" class="row-actions">
              <a-tooltip title="编辑">
                <span class="action-icon" @click="handleEdit(record)">
                  <EditOutlined />
                </span>
              </a-tooltip>
              <a-tooltip title="新增子科目">
                <span class="action-icon" @click="handleAddChild(record)">
                  <PlusOutlined />
                </span>
              </a-tooltip>
              <a-tooltip title="删除">
                <span class="action-icon danger" @click="handleDeleteSingle(record)">
                  <DeleteOutlined />
                </span>
              </a-tooltip>
            </div>
          </template>
          <template v-if="column.dataIndex === 'code'">
            <div class="code-cell" :style="{ paddingLeft: ((record._level || 1) - 1) * 24 + 'px' }">
              <span
                v-if="record._hasChildren"
                class="expand-icon"
                @click.stop="toggleExpand(record)"
              >
                <RightOutlined v-if="!isExpanded(record)" />
                <DownOutlined v-else />
              </span>
              <span v-else class="expand-placeholder"></span>
              <span class="code-text">{{ record.code }}</span>
            </div>
          </template>
          <template v-if="column.dataIndex === 'name'">
            <a v-if="canEdit" class="account-name-link" @click="handleEdit(record)">{{ record.name }}</a>
            <span v-else>{{ record.name }}</span>
          </template>
          <template v-if="column.dataIndex === 'balanceDirection'">
            {{ record.balanceDirection === '借' ? '借' : '贷' }}
          </template>
          <template v-if="column.dataIndex === 'auxiliary'">
            <span>{{ formatAuxiliary(record.auxiliary) }}</span>
            <a class="aux-set-link" @click="router.push({ name: 'AuxiliarySetting' })">设置</a>
          </template>
          <template v-if="column.dataIndex === 'unit'">
            <span v-if="record.unit">{{ record.unit }}</span>
            <span v-else>-</span>
          </template>
          <template v-if="column.dataIndex === 'enableStatus'">
            <a-switch
              size="small"
              :checked="record.enableStatus"
              :disabled="!canEdit"
              @change="(val: any) => handleStatusChange(record, !!val)"
            />
          </template>
        </template>
        <template #emptyText><EmptyState /></template>
      </a-table>
    </a-card>

    <!-- 期初值表格 -->
    <a-card v-show="currentCategory === 'initial'" :bordered="false" class="table-card initial-table-card">
      <!-- 工具栏 -->
      <div class="initial-toolbar">
        <a-button type="primary" @click="handleSaveInitialBalances" :loading="savingInitial" :disabled="!isInitialBalanced">
          <SaveOutlined />保存
        </a-button>
        <span class="initial-tip">
          <InfoCircleOutlined />提示：仅末级科目可录入期初余额，按余额方向只允许编辑对应列；损益类科目不录期初
        </span>
      </div>
      <a-table
        :columns="initialColumns"
        :dataSource="initialBalances"
        rowKey="accountId"
        bordered
        :loading="loading"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'accountCode'">
            <span :style="{ paddingLeft: ((record.level || 1) - 1) * 16 + 'px' }">{{ record.accountCode }}</span>
          </template>
          <template v-if="column.dataIndex === 'balanceDirection'">
            <a-tag :color="record.balanceDirection === '借' ? 'processing' : 'success'" >
              {{ record.balanceDirection === '借' ? '借' : '贷' }}
            </a-tag>
          </template>
          <template v-if="column.dataIndex === 'debitBalance'">
            <a-input-number
              v-if="record.isLeaf"
              v-model:value="record.debitBalance"
              :precision="2"
              :controls="false"
              :disabled="record.balanceDirection !== '借' || record.accountCode?.startsWith('5')"
              style="width: 100%"
              @change="() => { if (record.balanceDirection !== '借' || record.accountCode?.startsWith('5')) record.debitBalance = 0 }"
            />
            <span v-else class="non-leaf-amount">{{ formatAmount(record.debitBalance) }}</span>
          </template>
          <template v-if="column.dataIndex === 'creditBalance'">
            <a-input-number
              v-if="record.isLeaf"
              v-model:value="record.creditBalance"
              :precision="2"
              :controls="false"
              :disabled="record.balanceDirection !== '贷' || record.accountCode?.startsWith('5')"
              style="width: 100%"
              @change="() => { if (record.balanceDirection !== '贷' || record.accountCode?.startsWith('5')) record.creditBalance = 0 }"
            />
            <span v-else class="non-leaf-amount">{{ formatAmount(record.creditBalance) }}</span>
          </template>
        </template>
        <template #summary v-if="initialBalances.length > 0">
          <a-table-summary fixed>
            <a-table-summary-row>
              <a-table-summary-cell :index="0">合计</a-table-summary-cell>
              <a-table-summary-cell :index="1" />
              <a-table-summary-cell :index="2" />
              <a-table-summary-cell :index="3" :align="'right'">
                {{ formatAmount(initialDebitTotal) }}
              </a-table-summary-cell>
              <a-table-summary-cell :index="4" :align="'right'">
                {{ formatAmount(initialCreditTotal) }}
              </a-table-summary-cell>
            </a-table-summary-row>
            <a-table-summary-row>
              <a-table-summary-cell :index="0">借贷差额</a-table-summary-cell>
              <a-table-summary-cell :index="1" />
              <a-table-summary-cell :index="2" />
              <a-table-summary-cell :index="3" :align="'right'">
                <span :class="{ 'initial-diff-unbalanced': !isInitialBalanced }">{{ formatAmount(initialDifference) }}</span>
              </a-table-summary-cell>
              <a-table-summary-cell :index="4" />
            </a-table-summary-row>
          </a-table-summary>
        </template>
      </a-table>
    </a-card>

    <!-- 新增/编辑科目弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="isEdit ? '编辑科目' : '新增科目'"
      :width="560"
      :destroyOnClose="true"
      centered
      :bodyStyle="{ maxHeight: '60vh', overflowY: 'auto', padding: '24px' }"
      class="account-dialog"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        layout="vertical"
        class="custom-form"
      >
        <a-form-item label="科目编码" name="code">
          <a-input
            v-model:value="formData.code"
            :disabled="isEdit"
            placeholder="科目编码长度分别为4/6/8/10位数字"
          />
        </a-form-item>
    
        <!-- 提示信息1 -->
        <div class="info-tip">
          <InfoCircleOutlined />
          科目编码支持最长四级，一级科目必须以1（资产）/2（负债）/3（权益）/4（成本）/5（损益）开头，科目编码长度分别为4/6/8/10位数字；子级科目不可超过99个
        </div>
    
        <a-form-item label="科目名称" name="name">
          <a-input
            v-model:value="formData.name"
            placeholder="包含中文最长32个字符，否则最长64个"
          />
        </a-form-item>
    
        <a-form-item label="上级科目">
          <a-input
            :value="parentAccountName"
            disabled
            placeholder="无"
          />
        </a-form-item>
    
        <a-form-item label="科目类别" name="category">
          <a-select
            v-model:value="formData.category"
            placeholder="请选择类别"
            :disabled="isEdit || !!formData.parentId"
            :options="categoryOptions.map(c => ({ label: c.label, value: c.value }))"
          />
        </a-form-item>
    
        <!-- 提示信息2 -->
        <div class="info-tip">
          <InfoCircleOutlined />
          科目类别已根据国家小企业会计准则预置，不支持自定义类别；新增子科目时，子科目将自动继承上级科目的余额方向、类别。
        </div>
    
        <a-form-item label="余额方向">
          <div class="balance-direction-control">
            <a-switch v-model:checked="balanceDirectionSwitch" :disabled="isEdit || !!formData.parentId" />
            <span class="balance-direction-label">{{ balanceDirectionSwitch ? '借' : '贷' }}</span>
          </div>
        </a-form-item>
    
        <a-form-item>
          <div class="quantity-control">
            <a-checkbox v-model:checked="formData.enableQuantity">数量核算</a-checkbox>
            <span class="unit-label">计算单位</span>
            <a-input
              v-model:value="formData.unit"
              :disabled="!formData.enableQuantity"
              placeholder="请输入"
              class="unit-input"
            />
          </div>
        </a-form-item>
      </a-form>
      <template #footer>
        <div class="dialog-footer">
          <a-button @click="dialogVisible = false">取消</a-button>
          <a-button v-if="!isEdit" @click="handleSubmit">保存</a-button>
          <a-button
            v-if="!isEdit"
            type="primary"
            @click="handleSaveAndAdd"
          >
            保存并新增
          </a-button>
          <a-button
            v-if="isEdit"
            type="primary"
            @click="handleSubmit"
          >
            保存
          </a-button>
        </div>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed, watch } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined, DownOutlined, UpOutlined, RightOutlined, EditOutlined, DeleteOutlined, SaveOutlined, InfoCircleOutlined } from '@ant-design/icons-vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getAccountTree,
  getAccountById,
  createAccount,
  updateAccount,
  deleteAccount,
  toggleAccountStatus,
  getInitialBalances,
  saveInitialBalances,
} from '@/api/finance'
import { useAccountSetStore } from '@/stores/accountSet'
import { AccountSetPermissions } from '@/constants/accountSetPermissions'
import { formatAuxiliaryCodes } from '@/constants/auxTypes'
import { useRouter } from 'vue-router'

const router = useRouter()
const accountSetStore = useAccountSetStore()

// 是否有科目编辑权限（控制头部按钮、行内操作、启停开关）
const canEdit = computed(() => accountSetStore.hasAccountSetPermission(AccountSetPermissions.SubjectEdit))

// 分类选项
const categories = [
  { label: '资产', value: '资产' },
  { label: '负债', value: '负债' },
  { label: '权益', value: '权益' },
  { label: '成本', value: '成本' },
  { label: '损益', value: '损益' },
  { label: '期初值', value: 'initial' },
]

// 当前选中的分类
const currentCategory = ref('资产')

// 科目表格 columns
// 注意：编码列不提供排序——数据是扁平化的树，排序会拆散父子层级
const accountColumns = [
  { title: '操作', dataIndex: 'action', key: 'action', width: 80, align: 'center' as const, fixed: 'left' as const },
  { title: '编码', dataIndex: 'code', key: 'code', width: 150 },
  { title: '名称', dataIndex: 'name', key: 'name', width: 200 },
  { title: '类别', dataIndex: 'category', key: 'category', width: 120 },
  { title: '余额方向', dataIndex: 'balanceDirection', key: 'balanceDirection', width: 100, align: 'center' as const },
  { title: '辅助核算', dataIndex: 'auxiliary', key: 'auxiliary' },
  { title: '计算单位', dataIndex: 'unit', key: 'unit', width: 90, align: 'center' as const },
  { title: '启/停用', dataIndex: 'enableStatus', key: 'enableStatus', width: 100, align: 'center' as const, fixed: 'right' as const },
]

// 期初值表格 columns
const initialColumns = [
  { title: '科目编码', dataIndex: 'accountCode', key: 'accountCode', width: 140 },
  { title: '科目名称', dataIndex: 'accountName', key: 'accountName', minWidth: 200 },
  { title: '余额方向', dataIndex: 'balanceDirection', key: 'balanceDirection', width: 90, align: 'center' as const },
  { title: '期初借方余额', dataIndex: 'debitBalance', key: 'debitBalance', width: 160, align: 'right' as const },
  { title: '期初贷方余额', dataIndex: 'creditBalance', key: 'creditBalance', width: 160, align: 'right' as const },
]

// 表格数据
const accountTree = ref<any[]>([])
const initialBalances = ref<any[]>([])

// 期初借贷合计与试算平衡（仅末级参与）
const initialDebitTotal = computed(() =>
  initialBalances.value.filter((r: any) => r.isLeaf).reduce((sum: number, r: any) => sum + (r.debitBalance ?? 0), 0))
const initialCreditTotal = computed(() =>
  initialBalances.value.filter((r: any) => r.isLeaf).reduce((sum: number, r: any) => sum + (r.creditBalance ?? 0), 0))
const initialDifference = computed(() => initialDebitTotal.value - initialCreditTotal.value)
const isInitialBalanced = computed(() => Math.abs(initialDifference.value) < 0.005)

const loading = ref(false)
const savingInitial = ref(false)
const selectedRows = ref<any[]>([])
const selectedRowKeys = ref<number[]>([])

// 扁平化后的科目数据
interface AccountNode {
  id: number
  code: string
  name: string
  category: string
  categoryName: string
  balanceDirection: string
  enableStatus: boolean
  parentId: number | null
  auxiliaryAccounting: any
  unit: string
  level: number
  children?: AccountNode[]
  _expanded?: boolean
  _level?: number
}

// 展开状态存储
const expandedIds = ref<Set<number>>(new Set())

// 切换展开/折叠
const toggleExpand = (row: any) => {
  const id = row.id
  if (expandedIds.value.has(id)) {
    expandedIds.value.delete(id)
  } else {
    expandedIds.value.add(id)
  }
  expandedIds.value = new Set(expandedIds.value)
}

// 检查是否展开
const isExpanded = (row: any) => expandedIds.value.has(row.id)

// 扁平化树形数据
const flattenAccounts = computed(() => {
  const result: AccountNode[] = []
  const flatten = (nodes: any[], level: number) => {
    for (const node of nodes) {
      const { children, ...rest } = node
      const flatNode = {
        ...rest,
        _level: level,
        _hasChildren: children?.length > 0
      }
      result.push(flatNode as any)
      if (children?.length && expandedIds.value.has(node.id)) {
        flatten(children, level + 1)
      }
    }
  }
  flatten(accountTree.value || [], 1)
  return result
})

// 弹窗相关
const dialogVisible = ref(false)
const isEdit = ref(false)
const formRef = ref<FormInstance>()
const formData = reactive({
  id: 0,
  code: '',
  name: '',
  category: '',
  balanceDirection: '借',
  parentId: null as number | null,
  // 后端存储为逗号分隔的编码字符串（如 "customer,supplier"），表单不编辑、仅透传，避免更新时被清空
  auxiliary: null as string | null,
  unit: '',
  enableQuantity: false,
})

// 余额方向开关
const balanceDirectionSwitch = computed({
  get: () => formData.balanceDirection === '借',
  set: (val: boolean) => {
    formData.balanceDirection = val ? '借' : '贷'
  }
})

// 科目类别选项映射
const categoryOptionsMap: Record<string, { label: string; value: string }[]> = {
  '资产': [
    { label: '流动资产', value: '流动资产' },
    { label: '非流动资产', value: '非流动资产' }
  ],
  '负债': [
    { label: '流动负债', value: '流动负债' },
    { label: '非流动负债', value: '非流动负债' }
  ],
  '权益': [
    { label: '所有者权益', value: '所有者权益' }
  ],
  '成本': [
    { label: '成本', value: '成本' }
  ],
  // 与后端 GetSubCategories 的损益子类别保持一致，否则新建科目在列表中查不到
  '损益': [
    { label: '营业收入', value: '营业收入' },
    { label: '营业成本', value: '营业成本' },
    { label: '营业税金及附加', value: '营业税金及附加' },
    { label: '期间费用', value: '期间费用' },
    { label: '其他收益', value: '其他收益' },
    { label: '其他损失', value: '其他损失' },
    { label: '所得税费用', value: '所得税费用' },
    { label: '以前年度损益调整', value: '以前年度损益调整' }
  ]
}

// 当前可选的科目类别
const categoryOptions = computed(() => {
  const mainCategory = getMainCategory(formData.category)
  return categoryOptionsMap[mainCategory] || categoryOptionsMap['资产']
})

// 获取科目所属大类
function getMainCategory(category: string): string {
  for (const [mainCat, subCats] of Object.entries(categoryOptionsMap)) {
    if (subCats.some(c => c.value === category)) {
      return mainCat
    }
  }
  return currentCategory.value !== 'initial' ? currentCategory.value : '资产'
}

// 上级科目名称
const parentAccountName = computed(() => {
  if (!formData.parentId) return '无'
  const parent = findAccountById(accountTree.value, formData.parentId)
  return parent ? `${parent.code} ${parent.name}` : '无'
})

// 根据ID查找科目
function findAccountById(tree: any[], id: number): any | null {
  for (const node of tree) {
    if (node.id === id) return node
    if (node.children) {
      const found = findAccountById(node.children, id)
      if (found) return found
    }
  }
  return null
}

// 把 2 位顺序号拼成完整子科目编码；非法或超 99（同级上限）返回空串
function buildChildCode(parentCode: string, suffix: number): string {
  if (!Number.isInteger(suffix) || suffix < 1 || suffix > 99) return ''
  return parentCode + String(suffix).padStart(2, '0')
}

// 计算"上级科目下最大子编码 + 1"；无有效子科目时返回首个子编码（…01）
function computeNextChildCode(parentCode: string, children: any[]): string {
  let maxSuffix = 0
  for (const child of children || []) {
    const childCode = String(child?.code ?? '')
    if (!childCode.startsWith(parentCode)) continue
    const tail = childCode.slice(parentCode.length)
    if (!/^\d{2}$/.test(tail)) continue
    const n = parseInt(tail, 10)
    if (n > maxSuffix) maxSuffix = n
  }
  return buildChildCode(parentCode, maxSuffix + 1)
}

const formRules: Record<string, any[]> = {
  code: [
    { required: true, message: '请输入编码', trigger: 'blur' },
    { pattern: /^\d{4}(\d{2}){0,3}$/, message: '科目编码为4/6/8/10位数字', trigger: 'blur' },
  ],
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  category: [{ required: true, message: '请选择类别', trigger: 'change' }],
  balanceDirection: [{ required: true, message: '请选择余额方向', trigger: 'change' }],
}

// 格式化辅助核算显示：后端返回逗号分隔的编码字符串，按编码映射为中文名称
// 映射统一收敛到 @/constants/auxTypes（单一真源）
function formatAuxiliary(aux: any): string {
  return formatAuxiliaryCodes(aux)
}

// 加载数据
async function loadData() {
  const accountSetId = accountSetStore.currentAccountSetId

  // 账套ID无效时不发起请求，直接清空数据
  if (!accountSetId || accountSetId <= 0) {
    accountTree.value = []
    initialBalances.value = []
    loading.value = false
    return
  }

  loading.value = true
  try {
    if (currentCategory.value === 'initial') {
      const res = await getInitialBalances(accountSetId) as any
      initialBalances.value = res || []
    } else {
      const res = await getAccountTree(currentCategory.value, accountSetId) as any
      accountTree.value = res || []
    }
  } catch (error) {
    console.error('加载数据失败:', error)
  } finally {
    loading.value = false
  }
}

// 切换分类
function handleCategoryChange(category: string) {
  currentCategory.value = category
  loadData()
}

// 选择变化
function onSelectionChange(keys: (string | number)[], rows: any[]) {
  selectedRowKeys.value = keys as number[]
  selectedRows.value = rows
}

// 新增科目
function handleAdd() {
  isEdit.value = false
  resetForm()
  // 只预填合法的类别选项；大类名本身不是合法类别（如"资产"），预填会生成查询不到的脏数据
  const mainCategory = currentCategory.value !== 'initial' ? currentCategory.value : '资产'
  const options = categoryOptionsMap[mainCategory] || []
  formData.category = options.length === 1 ? options[0].value : ''
  dialogVisible.value = true
}

// 新增子科目
function handleAddChild(parent: any) {
  isEdit.value = false
  resetForm()
  formData.parentId = parent.id
  formData.category = parent.category
  formData.balanceDirection = parent.balanceDirection
  // 默认编码 = 上级下最大子编码 + 1（扁平行已剥离 children，需回树取真实节点）
  const parentNode = findAccountById(accountTree.value, parent.id)
  formData.code = computeNextChildCode(parent.code, parentNode?.children ?? [])
  dialogVisible.value = true
}

// 编辑科目
async function handleEdit(row: any) {
  isEdit.value = true
  try {
    const data = await getAccountById(row.id) as any
    resetForm()
    Object.assign(formData, {
      id: data.id,
      code: data.code,
      name: data.name,
      category: data.category,
      balanceDirection: data.balanceDirection,
      parentId: data.parentId || null,
      auxiliary: data.auxiliary ?? null,
      unit: data.unit || '',
      enableQuantity: !!data.unit,
    })
    dialogVisible.value = true
  } catch (error) {
    console.error('获取科目详情失败:', error)
  }
}

// 组装提交载荷：字段名与后端 Create/UpdateAccountRequest 对齐
function buildPayload() {
  return {
    code: formData.code,
    name: formData.name,
    category: formData.category,
    balanceDirection: formData.balanceDirection,
    parentId: formData.parentId ?? 0,
    auxiliary: formData.auxiliary,
    unit: formData.enableQuantity ? formData.unit : null,
  }
}

// 提交表单（失败时拦截器已弹出服务器返回的具体原因，这里不再重复提示）
async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  const accountSetId = accountSetStore.currentAccountSetId
  if (!accountSetId || accountSetId <= 0) {
    message.warning('请先选择账套')
    return
  }

  try {
    if (isEdit.value) {
      await updateAccount(formData.id, buildPayload())
      message.success('更新成功')
    } else {
      await createAccount(buildPayload(), accountSetId)
      message.success('创建成功')
    }
    dialogVisible.value = false
    loadData()
  } catch (error) {
    console.error(isEdit.value ? '更新失败:' : '创建失败:', error)
  }
}

// 保存并新增
async function handleSaveAndAdd() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  const accountSetId = accountSetStore.currentAccountSetId
  if (!accountSetId || accountSetId <= 0) {
    message.warning('请先选择账套')
    return
  }

  try {
    await createAccount(buildPayload(), accountSetId)
    message.success('创建成功')
    loadData()
    const savedParentId = formData.parentId
    const savedCategory = formData.category
    const savedBalanceDirection = formData.balanceDirection
    const savedCode = formData.code
    resetForm()
    formData.parentId = savedParentId
    formData.category = savedCategory
    formData.balanceDirection = savedBalanceDirection
    // 续填下一条默认编码 = 刚保存编码顺序号 + 1（基于已存编码递增，不等异步刷新的树）
    // 仅子科目续填；顶级新增（无上级）保持留空，与既有行为一致
    if (savedParentId && savedCode.length >= 2) {
      formData.code = buildChildCode(savedCode.slice(0, -2), parseInt(savedCode.slice(-2), 10) + 1)
    }
  } catch (error) {
    console.error('创建失败:', error)
  }
}

// 删除单个科目
function handleDeleteSingle(row: any) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除科目「${row.name}」吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await deleteAccount(row.id)
        message.success('删除成功')
        selectedRowKeys.value = selectedRowKeys.value.filter(k => k !== row.id)
        selectedRows.value = selectedRows.value.filter(r => r.id !== row.id)
        loadData()
      } catch (error) {
        console.error('删除失败:', error)
      }
    },
  })
}

// 批量删除：先删深层级（子科目），逐个执行并汇总结果，避免半途中断
function handleBatchDelete() {
  if (selectedRows.value.length === 0) {
    message.warning('请选择要删除的科目')
    return
  }
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除选中的 ${selectedRows.value.length} 个科目吗？`,
    okType: 'danger',
    async onOk() {
      const rows = [...selectedRows.value].sort((a, b) => (b._level || 1) - (a._level || 1))
      let successCount = 0
      const failedNames: string[] = []
      for (const row of rows) {
        try {
          await deleteAccount(row.id)
          successCount++
        } catch (error) {
          failedNames.push(row.name)
        }
      }
      selectedRowKeys.value = []
      selectedRows.value = []
      loadData()
      if (failedNames.length === 0) {
        message.success(`已删除 ${successCount} 个科目`)
      } else {
        message.warning(`已删除 ${successCount} 个，${failedNames.length} 个删除失败：${failedNames.join('、')}`)
      }
    },
  })
}

// 启用/停用切换
async function handleStatusChange(row: any, val: boolean) {
  // 停用前二次确认（启用直接）；被凭证引用/有启用下级/有余额的科目后端会拒绝
  if (!val) {
    Modal.confirm({
      title: '确认停用',
      content: `确定要停用科目「${row.name}」吗？被凭证引用、有启用下级或有余额的科目将无法停用。`,
      okType: 'danger',
      onOk: () => doToggleStatus(row, val),
    })
    return
  }
  doToggleStatus(row, val)
}

async function doToggleStatus(row: any, val: boolean) {
  try {
    await toggleAccountStatus(row.id)
    message.success(val ? '已启用' : '已停用')
  } catch (error) {
    // 失败原因由请求拦截器统一弹后端消息（如"该科目已被凭证引用，无法停用"）
    console.error('状态切换失败:', error)
  } finally {
    loadData() // 成功失败都刷新，保证开关视觉与后端真实状态一致
  }
}

// 更多操作
function handleMoreCommand({ key }: { key: string | number }) {
  switch (key) {
    case 'import':
      message.info('导入功能开发中')
      break
    case 'export':
      message.info('导出功能开发中')
      break
  }
}

// 保存期初余额
async function handleSaveInitialBalances() {
  try {
    savingInitial.value = true
    const accountSetId = accountSetStore.currentAccountSetId
    const items = initialBalances.value
      .filter((row: any) => row.isLeaf)
      .map((row: any) => ({
        accountId: row.accountId,
        debitBalance: row.debitBalance ?? 0,
        creditBalance: row.creditBalance ?? 0,
      }))
    await saveInitialBalances({ accountSetId, items })
    message.success('保存成功')
  } catch (error) {
    // 失败原因由请求拦截器统一弹出服务器返回的具体消息（如"期初借贷不平衡，借方合计…差额…"），此处不重复提示
    console.error('保存期初余额失败:', error)
  } finally {
    savingInitial.value = false
  }
}

// 格式化金额
function formatAmount(val: number | undefined | null): string {
  if (val === undefined || val === null || val === 0) return '-'
  return val.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 重置表单
function resetForm() {
  formData.id = 0
  formData.code = ''
  formData.name = ''
  formData.category = ''
  formData.balanceDirection = '借'
  formData.parentId = null
  formData.auxiliary = null
  formData.unit = ''
  formData.enableQuantity = false
  formRef.value?.resetFields()
}

onMounted(() => {
  loadData()
})

// 监听账套切换
watch(() => accountSetStore.currentAccountSetId, () => {
  loadData()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.account-manage {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

// ===== 工具栏区域 =====
.toolbar-wrapper {
  display: flex;
  align-items: center;
  width: 100%;
}

// 下划线式Tab筛选
.category-capsules {
  display: inline-flex;
  gap: 0;
  border-bottom: 1px solid $border-color-lighter;
}

.capsule-btn {
  padding: 8px 16px;
  border: none;
  border-radius: 0;
  background: transparent;
  color: rgba(0, 0, 0, 0.65);
  font-size: 14px;
  cursor: pointer;
  transition: color 0.2s;
  white-space: nowrap;
  position: relative;

  &:hover {
    color: var(--color-primary);
  }

  &.active {
    color: var(--text-1);
    font-weight: 600;

    &::after {
      content: '';
      position: absolute;
      bottom: 0;
      left: 50%;
      transform: translateX(-50%);
      width: 70%;
      height: 2px;
      background: var(--color-primary);
      border-radius: 1px;
    }
  }
}

// ===== 树形表格卡片 =====
.tree-table-card {
  flex: 1;
  overflow: hidden;
  min-height: 0;
  display: flex;
  flex-direction: column;
  border-radius: 0;
  box-shadow: $shadow-card;

  :deep(.ant-card-body) {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    min-height: 0;
    padding: $spacing-md;
  }

  :deep(.ant-table-wrapper) {
    flex: 1;
    overflow: hidden;
    min-height: 0;
  }

  :deep(.ant-table) {
    height: 100%;
  }

  :deep(.ant-table-container) {
    height: 100%;
    display: flex;
    flex-direction: column;
  }

  :deep(.ant-table-body),
  :deep(.ant-table-content) {
    flex: 1;
    overflow-y: auto !important;
    min-height: 0;
  }

  // 表头样式
  :deep(.ant-table-thead > tr > th) {
    background: $bg-page;
    border-bottom: none;
    font-weight: 600;
    color: $text-primary;
    padding: 12px 16px;

    &::before {
      display: none;
    }
  }

  // 树形行样式
  :deep(.tree-row) {
    height: 40px;
    transition: $transition-fast;

    &:hover {
      background: rgba($color-primary, 0.04);

      .row-actions {
        opacity: 1;
        visibility: visible;
      }
    }

    > td {
      border-bottom: 1px solid $border-color-lighter;
      padding: 0 16px;
    }
  }
}

// 编码列样式
.code-cell {
  display: flex;
  align-items: center;
  gap: 8px;

  .expand-placeholder {
    width: 16px;
    height: 16px;
  }

  .expand-icon {
    cursor: pointer;
    color: $text-secondary;
    font-size: 12px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 20px;
    height: 20px;
    border-radius: 4px;
    transition: $transition-fast;

    &:hover {
      color: $color-primary;
      background: rgba($color-primary, 0.08);
    }
  }

  .code-text {
    font-size: 14px;
    color: $text-primary;
    font-family: 'SF Mono', Consolas, monospace;
  }
}

// 行快捷操作
.row-actions {
  display: flex;
  gap: 4px;
  opacity: 0;
  visibility: hidden;
  transition: $transition-fast;

  .action-icon {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 28px;
    border-radius: 6px;
    cursor: pointer;
    color: $text-secondary;
    transition: $transition-fast;

    &:hover {
      background: rgba($color-primary, 0.08);
      color: $color-primary;
    }

    &.danger:hover {
      background: rgba($color-danger, 0.08);
      color: $color-danger;
    }
  }
}

// 科目名称链接
.account-name-link {
  color: $color-primary;
  cursor: pointer;
  text-decoration: none;
  font-weight: 500;
  transition: $transition-fast;

  &:hover {
    text-decoration: underline;
  }
}

.aux-set-link {
  margin-left: 8px;
  font-size: 12px;
  color: $color-primary;
  cursor: pointer;

  &:hover {
    text-decoration: underline;
  }
}

// ===== 期初值表格 =====
.initial-table-card {
  @extend .tree-table-card;

  .initial-toolbar {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 0 0 $spacing-md 0;
    margin-bottom: $spacing-md;
    border-bottom: 1px solid $border-color-lighter;

    .initial-tip {
      font-size: 13px;
      color: $text-secondary;
      margin-left: auto;
      display: flex;
      align-items: center;
      gap: 6px;

      .anticon {
        color: $color-primary;
      }
    }
  }

  :deep(.ant-table-thead > tr > th) {
    background: $bg-page;
  }
}

.non-leaf-amount {
  font-size: 13px;
  color: $text-regular;
  font-style: italic;
}

.initial-diff-unbalanced {
  color: $color-danger;
  font-weight: 600;
}

// ===== 弹窗样式 =====
.account-dialog {
  :deep(.ant-modal-content) {
    border-radius: $border-radius-lg;
    overflow: visible;
  }

  :deep(.ant-modal-header) {
    border-bottom: 1px solid $border-color-lighter;
    padding: 16px 24px;
  }

  :deep(.ant-modal-body) {
    padding: 24px;
  }

  :deep(.ant-modal-footer) {
    border-top: 1px solid $border-color-lighter;
    padding: 16px 24px;
  }
}

.custom-form {
  :deep(.ant-form-item) {
    margin-bottom: 20px;

    .ant-form-item-label > label {
      color: $text-primary;
      font-weight: 500;
    }
  }

  :deep(.ant-input),
  :deep(.ant-select-selector),
  :deep(.ant-input-number) {
    height: 40px;
    border-radius: 8px;
  }

  :deep(.ant-input) {
    padding: 0 12px;

    &:hover,
    &:focus {
      border-color: $color-primary;
      box-shadow: 0 0 0 2px rgba($color-primary, 0.08);
    }
  }

  :deep(.ant-select-selector) {
    padding: 0 12px !important;
  }

  .info-tip {
    background: $color-primary-light;
    border-radius: 8px;
    padding: 12px 16px;
    margin-bottom: 20px;
    font-size: 13px;
    color: $text-regular;
    line-height: 1.6;
    display: flex;
    align-items: flex-start;
    gap: 8px;

    .anticon {
      color: $color-primary;
      margin-top: 2px;
    }
  }

  .balance-direction-control {
    display: flex;
    align-items: center;
    gap: 12px;

    .balance-direction-label {
      font-size: 16px;
      font-weight: 600;
      color: $text-primary;
    }
  }

  .quantity-control {
    display: flex;
    align-items: center;
    gap: 20px;
    padding: 12px 16px;
    background: $bg-page;
    border-radius: 8px;

    .unit-label {
      color: $text-regular;
      font-size: 14px;
    }

    .unit-input {
      width: 120px;
    }
  }
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
