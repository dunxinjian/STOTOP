<template>
  <div class="page-container">
    <PageHeader title="积分规则管理" description="管理各来源下的积分规则">
      <template #actions>
        <a-button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>新增规则
        </a-button>
      </template>
      <template #toolbar>
        <div style="display: flex; align-items: center; justify-content: flex-end; width: 100%; gap: 8px;">
          <a-select v-model:value="searchForm.sourceId" size="small" placeholder="全部来源" style="width: 200px" allowClear :options="sourceOptions" @change="handleSearch" />
          <a-input v-model:value="searchForm.keyword" size="small" placeholder="规则名称/编码" style="width: 200px" allowClear @pressEnter="handleSearch" />
          <a-select v-model:value="isEnabledFilter" size="small" placeholder="全部状态" style="width: 120px" allowClear :options="[{ label: '启用', value: 1 }, { label: '禁用', value: 0 }]" @change="handleSearch" />
          <a-button type="primary" size="small" @click="handleSearch">查询</a-button>
          <a-button size="small" @click="handleReset">重置</a-button>
        </div>
      </template>
    </PageHeader>

    <a-card :bordered="false">
      <a-spin :spinning="loading">
        <!-- 按来源分组折叠面板 -->
        <a-collapse v-if="groupedRules.length > 0" v-model:activeKey="activeKeys" :bordered="false">
          <a-collapse-panel
            v-for="group in groupedRules"
            :key="group.sourceId"
            :header="`${group.sourceName}（${group.rules.length} 条规则）`"
          >
            <a-table
              :columns="columns"
              :dataSource="group.rules"
              rowKey="id"
              bordered
              size="small"
              :pagination="false"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'pointValue'">
                  <a-tag :color="record.pointValue > 0 ? 'success' : 'error'">
                    {{ record.pointValue > 0 ? '+' : '' }}{{ record.pointValue }}
                  </a-tag>
                </template>
                <template v-if="column.dataIndex === 'cycleLimit'">
                  {{ record.cycleLimit === 0 ? '无限' : record.cycleLimit }}
                </template>
                <template v-if="column.dataIndex === 'requireApproval'">
                  <a-tag :color="record.requireApproval ? 'warning' : 'default'">
                    {{ record.requireApproval ? '需审批' : '免审批' }}
                  </a-tag>
                </template>
                <template v-if="column.dataIndex === 'isEnabled'">
                  <a-switch
                    :checked="record.isEnabled"
                    checked-children="启用"
                    un-checked-children="禁用"
                    :loading="record._toggling"
                    @change="handleToggle(record as RuleWithToggle)"
                  />
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <a-button type="link" size="small" @click="handleEdit(record as PointRuleListDto)">
                    <EditOutlined />编辑
                  </a-button>
                  <a-popconfirm
                    title="确定删除该规则吗？"
                    okText="确定"
                    cancelText="取消"
                    @confirm="handleDelete(record as PointRuleListDto)"
                  >
                    <a-button type="link" size="small" danger>
                      <DeleteOutlined />删除
                    </a-button>
                  </a-popconfirm>
                </template>
              </template>
            </a-table>
          </a-collapse-panel>
        </a-collapse>
        <EmptyState v-else-if="!loading" description="暂无规则数据" />
      </a-spin>

      <!-- 分页 -->
      <div v-if="pagination.total > 0" style="display: flex; justify-content: flex-end; margin-top: 16px">
        <a-pagination
          :current="pagination.pageIndex"
          :pageSize="pagination.pageSize"
          :total="pagination.total"
          show-size-changer
          :show-total="(t: number) => `共 ${t} 条`"
          :pageSizeOptions="['20', '50', '100', '200']"
          @change="handlePageChange"
          @showSizeChange="(_c: number, s: number) => handleSizeChange(s)"
        />
      </div>
    </a-card>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="dialogVisible"
      :title="dialogType === 'add' ? '新增规则' : '编辑规则'"
      :width="680"
      :destroyOnClose="true"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        :label-col="{ style: { width: '110px' } }"
        style="padding: 10px 20px"
      >
        <!-- 基本信息 -->
        <a-divider orientation="left" style="margin-top: 0">基本信息</a-divider>

        <a-form-item label="规则名称" name="ruleName">
          <a-input v-model:value="formData.ruleName" placeholder="请输入规则名称" :maxlength="100" />
        </a-form-item>
        <a-form-item label="规则编码" name="ruleCode">
          <a-input
            v-model:value="formData.ruleCode"
            placeholder="请输入规则编码（如 TASK_COMPLETE）"
            :maxlength="50"
            :disabled="dialogType === 'edit'"
          />
        </a-form-item>
        <a-form-item label="关联来源" name="sourceId">
          <a-select
            v-model:value="formData.sourceId"
            placeholder="请选择来源分类"
            :options="sourceOptions"
          />
        </a-form-item>
        <a-form-item label="事件类型" name="eventType">
          <a-select
            v-model:value="formData.eventType"
            placeholder="请选择或输入事件类型"
            :options="eventTypeOptions"
            show-search
            :filter-option="false"
            @search="handleEventTypeSearch"
          >
            <template #notFoundContent>
              <div style="padding: 4px 8px; color: #999">输入自定义事件类型后回车选择</div>
            </template>
          </a-select>
        </a-form-item>

        <!-- 积分设置 -->
        <a-divider orientation="left">积分设置</a-divider>

        <a-form-item label="积分值" name="pointValue" extra="正数=奖分，负数=扣分">
          <a-input-number
            v-model:value="formData.pointValue"
            :min="-9999"
            :max="9999"
            style="width: 100%"
            placeholder="正数为奖分，负数为扣分"
          />
        </a-form-item>
        <a-form-item label="周期上限" name="cycleLimit" extra="0 表示无限制">
          <a-input-number v-model:value="formData.cycleLimit" :min="0" :max="99999" style="width: 100%" />
        </a-form-item>
        <a-form-item label="账户类型" name="accountType" extra="A 分 ：终身资本；B 分：可清算可兑换">
          <a-select v-model:value="formData.accountType" :options="accountTypeOptions" />
        </a-form-item>
        <a-form-item label="清算策略" name="resetStrategy" extra="仅对 B 分生效">
          <a-select v-model:value="formData.resetStrategy" :options="resetStrategyOptions" />
        </a-form-item>
        <a-form-item label="转换比例" name="convertRatio" extra="业务计量值 → 积分值的乘数，默认 1.0">
          <a-input-number v-model:value="formData.convertRatio" :min="0" :max="1000" :step="0.1" :precision="2" style="width: 100%" />
        </a-form-item>
        <a-form-item label="需要审批" name="requireApproval">
          <a-switch v-model:checked="formData.requireApproval" checked-children="是" un-checked-children="否" />
        </a-form-item>

        <!-- 高级设置 -->
        <a-divider orientation="left">高级设置</a-divider>

        <a-form-item label="条件说明" name="conditionDescription">
          <a-input v-model:value="formData.conditionDescription" placeholder="条件的简要说明" :maxlength="200" />
        </a-form-item>
        <a-form-item label="条件表达式" name="conditionExpression" extra="JSON 格式，用于事件匹配条件">
          <a-textarea
            v-model:value="formData.conditionExpression"
            :rows="3"
            placeholder='例如：{"minScore": 90, "taskType": "daily"}'
          />
        </a-form-item>

        <!-- 倍率规则 -->
        <a-form-item label="倍率规则" extra="键值对形式，键为条件，值为倍率">
          <div v-for="(item, index) in multiplierItems" :key="item._uid" style="display: flex; gap: 8px; margin-bottom: 8px">
            <a-input v-model:value="item.key" placeholder="条件（如 excellent）" style="flex: 1" />
            <a-input-number v-model:value="item.value" placeholder="倍率" :min="0" :max="100" :step="0.1" style="width: 120px" />
            <a-button type="text" danger @click="removeMultiplierItem(index)">
              <DeleteOutlined />
            </a-button>
          </div>
          <a-button type="dashed" block @click="addMultiplierItem">
            <PlusOutlined />添加倍率
          </a-button>
        </a-form-item>

        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="formData.sortOrder" :min="0" :max="9999" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="dialogType === 'edit'" label="状态" name="isEnabled">
          <a-switch v-model:checked="formData.isEnabled" checked-children="启用" un-checked-children="禁用" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-button @click="dialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">确定</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import {
  getPointRules,
  getPointRule,
  createPointRule,
  updatePointRule,
  deletePointRule,
  togglePointRule,
  getPointSources,
} from '@/api/points'
import type { PointRuleListDto, PointSourceDto } from '@/types/points'
import { genTempId } from '@/utils/tempId'

type RuleWithToggle = PointRuleListDto & { _toggling?: boolean }

const columns = [
  { title: '规则名称', dataIndex: 'ruleName', key: 'ruleName', width: 160 },
  { title: '编码', dataIndex: 'ruleCode', key: 'ruleCode', width: 150 },
  { title: '事件类型', dataIndex: 'eventType', key: 'eventType', width: 130 },
  { title: '积分值', dataIndex: 'pointValue', key: 'pointValue', width: 90, align: 'center' as const },
  { title: '条件说明', dataIndex: 'conditionDescription', key: 'conditionDescription', ellipsis: true },
  { title: '周期上限', dataIndex: 'cycleLimit', key: 'cycleLimit', width: 90, align: 'center' as const },
  { title: '审批', dataIndex: 'requireApproval', key: 'requireApproval', width: 90, align: 'center' as const },
  { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 100, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 130, align: 'center' as const, fixed: 'right' as const },
]

// 常用事件类型
const defaultEventTypes = [
  { label: '任务完成', value: 'TASK_COMPLETE' },
  { label: '任务超期', value: 'TASK_OVERDUE' },
  { label: '质量评优', value: 'QUALITY_EXCELLENT' },
  { label: '质量不合格', value: 'QUALITY_FAIL' },
  { label: '培训通过', value: 'TRAINING_PASS' },
  { label: '考试优秀', value: 'EXAM_EXCELLENT' },
  { label: '文化表彰', value: 'CULTURE_AWARD' },
  { label: '创新提案', value: 'INNOVATION_PROPOSAL' },
  { label: '特别奖励', value: 'SPECIAL_AWARD' },
  { label: '特别处罚', value: 'SPECIAL_PENALTY' },
]
const eventTypeSearchValue = ref('')
const eventTypeOptions = computed(() => {
  const base = [...defaultEventTypes]
  if (eventTypeSearchValue.value && !base.some(o => o.value === eventTypeSearchValue.value)) {
    base.push({ label: eventTypeSearchValue.value, value: eventTypeSearchValue.value })
  }
  return base
})
function handleEventTypeSearch(val: string) {
  eventTypeSearchValue.value = val
}

// 来源列表
const sources = ref<PointSourceDto[]>([])
const sourceOptions = computed(() =>
  sources.value.filter(s => s.isEnabled).map(s => ({ label: s.sourceName, value: s.id }))
)

// 搜索
const searchForm = reactive({
  keyword: '',
  sourceId: undefined as number | undefined,
  isEnabled: undefined as boolean | undefined,
})

// 启用状态下拉代理（boolean ↔ 1/0），AntD SelectValue 仅支持 string|number
const isEnabledFilter = computed<number | undefined>({
  get: () => (searchForm.isEnabled == null ? undefined : searchForm.isEnabled ? 1 : 0),
  set: (val) => { searchForm.isEnabled = val == null ? undefined : val === 1 },
})

// 表格
const loading = ref(false)
const allRules = ref<RuleWithToggle[]>([])
const pagination = reactive({ pageIndex: 1, pageSize: 50, total: 0 })
const activeKeys = ref<number[]>([])

// 按来源分组
const groupedRules = computed(() => {
  const map = new Map<number, { sourceId: number; sourceName: string; rules: RuleWithToggle[] }>()
  for (const rule of allRules.value) {
    if (!map.has(rule.sourceId)) {
      map.set(rule.sourceId, {
        sourceId: rule.sourceId,
        sourceName: rule.sourceName || '未知来源',
        rules: [],
      })
    }
    map.get(rule.sourceId)!.rules.push(rule)
  }
  return Array.from(map.values())
})

// 弹窗
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const formRef = ref<FormInstance>()
const submitLoading = ref(false)
const currentId = ref<number | null>(null)

const formData = reactive({
  ruleName: '',
  ruleCode: '',
  sourceId: undefined as number | undefined,
  eventType: '',
  pointValue: 0,
  conditionExpression: '' as string | undefined,
  conditionDescription: '' as string | undefined,
  multiplierRule: '' as string | null,
  cycleLimit: 0,
  requireApproval: false,
  sortOrder: 0,
  isEnabled: true,
  accountType: 2,
  resetStrategy: 0,
  convertRatio: 1,
})

// 账户类型 / 清算策略 选项
const accountTypeOptions = [
  { label: 'A 分（终身资本）', value: 1 },
  { label: 'B 分（可清算）', value: 2 },
]
const resetStrategyOptions = [
  { label: '不清算', value: 0 },
  { label: '月清', value: 1 },
  { label: '年清', value: 2 },
]

// 倍率规则动态键值对
const multiplierItems = ref<{ _uid: string; key: string; value: number }[]>([])

function addMultiplierItem() {
  multiplierItems.value.push({ _uid: genTempId(), key: '', value: 1 })
}

function removeMultiplierItem(index: number) {
  multiplierItems.value.splice(index, 1)
}

function multiplierItemsToJson(): string | null {
  const items = multiplierItems.value.filter(i => i.key.trim())
  if (items.length === 0) return null
  const obj: Record<string, number> = {}
  for (const item of items) { obj[item.key.trim()] = item.value }
  return JSON.stringify(obj)
}

function jsonToMultiplierItems(json: string | null) {
  multiplierItems.value = []
  if (!json) return
  try {
    const obj = JSON.parse(json)
    for (const [k, v] of Object.entries(obj)) {
      multiplierItems.value.push({ _uid: genTempId(), key: k, value: Number(v) || 1 })
    }
  } catch { /* ignore */ }
}

const formRules: Record<string, any[]> = {
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' }],
  ruleCode: [{ required: true, message: '请输入规则编码', trigger: 'blur' }],
  sourceId: [{ required: true, message: '请选择关联来源', trigger: 'change' }],
  eventType: [{ required: true, message: '请选择事件类型', trigger: 'change' }],
  pointValue: [{ required: true, message: '请输入积分值', trigger: 'blur' }],
  sortOrder: [{ required: true, message: '请输入排序', trigger: 'blur' }],
}

async function fetchSources() {
  try {
    const res = await getPointSources() as any
    sources.value = res || []
  } catch { /* ignore */ }
}

async function fetchRules() {
  loading.value = true
  try {
    const params: any = {
      pageIndex: pagination.pageIndex,
      pageSize: pagination.pageSize,
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.sourceId != null) params.sourceId = searchForm.sourceId
    if (searchForm.isEnabled != null) params.isEnabled = searchForm.isEnabled

    const res = await getPointRules(params) as any
    if (res) {
      allRules.value = res?.items || res || []
      pagination.total = res?.total || allRules.value.length

      // 自动展开所有分组
      activeKeys.value = groupedRules.value.map(g => g.sourceId)
    }
  } finally {
    loading.value = false
  }
}

function handleSearch() { pagination.pageIndex = 1; fetchRules() }
function handleReset() {
  searchForm.keyword = ''
  searchForm.sourceId = undefined
  searchForm.isEnabled = undefined
  pagination.pageIndex = 1
  fetchRules()
}
function handlePageChange(page: number, pageSize: number) {
  pagination.pageIndex = page
  pagination.pageSize = pageSize
  fetchRules()
}
function handleSizeChange(val: number) { pagination.pageSize = val; fetchRules() }

async function handleToggle(record: RuleWithToggle) {
  record._toggling = true
  try {
    await togglePointRule(record.id)
    record.isEnabled = !record.isEnabled
    message.success(record.isEnabled ? '已启用' : '已禁用')
  } finally {
    record._toggling = false
  }
}

async function handleDelete(record: PointRuleListDto) {
  try {
    await deletePointRule(record.id)
    message.success('删除成功')
    fetchRules()
  } catch (error) {
    console.error('删除失败:', error)
  }
}

function handleAdd() {
  dialogType.value = 'add'
  currentId.value = null
  resetForm()
  dialogVisible.value = true
}

async function handleEdit(row: PointRuleListDto) {
  dialogType.value = 'edit'
  currentId.value = row.id

  try {
    const detail = await getPointRule(row.id) as any
    if (detail) {
      formData.ruleName = detail.ruleName
      formData.ruleCode = detail.ruleCode
      formData.sourceId = detail.sourceId
      formData.eventType = detail.eventType
      formData.pointValue = detail.pointValue
      formData.conditionExpression = detail.conditionExpression || ''
      formData.conditionDescription = detail.conditionDescription || ''
      formData.cycleLimit = detail.cycleLimit
      formData.requireApproval = detail.requireApproval
      formData.sortOrder = detail.sortOrder
      formData.isEnabled = detail.isEnabled
      formData.accountType = detail.accountType ?? 2
      formData.resetStrategy = detail.resetStrategy ?? 0
      formData.convertRatio = Number(detail.convertRatio ?? 1)
      jsonToMultiplierItems(detail.multiplierRule)
    }
  } catch {
    // fallback to list data
    formData.ruleName = row.ruleName
    formData.ruleCode = row.ruleCode
    formData.sourceId = row.sourceId
    formData.eventType = row.eventType
    formData.pointValue = row.pointValue
    formData.conditionDescription = row.conditionDescription || ''
    formData.conditionExpression = ''
    formData.cycleLimit = row.cycleLimit
    formData.requireApproval = row.requireApproval
    formData.sortOrder = row.sortOrder
    formData.isEnabled = row.isEnabled
    multiplierItems.value = []
  }
  dialogVisible.value = true
}

function resetForm() {
  formData.ruleName = ''
  formData.ruleCode = ''
  formData.sourceId = undefined
  formData.eventType = ''
  formData.pointValue = 0
  formData.conditionExpression = ''
  formData.conditionDescription = ''
  formData.cycleLimit = 0
  formData.requireApproval = false
  formData.sortOrder = 0
  formData.isEnabled = true
  formData.accountType = 2
  formData.resetStrategy = 0
  formData.convertRatio = 1
  multiplierItems.value = []
}

function validateJson(value: string | null | undefined): boolean {
  if (!value || value.trim() === '') return true
  try { JSON.parse(value); return true } catch { return false }
}

async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  // 校验 JSON 格式
  if (!validateJson(formData.conditionExpression)) {
    message.error('条件表达式不是有效的 JSON 格式')
    return
  }

  submitLoading.value = true
  try {
    const multiplierJson = multiplierItemsToJson()

    if (dialogType.value === 'add') {
      await createPointRule({
        sourceId: formData.sourceId!,
        ruleName: formData.ruleName,
        ruleCode: formData.ruleCode,
        eventType: formData.eventType,
        pointValue: formData.pointValue,
        conditionExpression: formData.conditionExpression || null,
        conditionDescription: formData.conditionDescription || null,
        multiplierRule: multiplierJson,
        cycleLimit: formData.cycleLimit,
        requireApproval: formData.requireApproval,
        sortOrder: formData.sortOrder,
        accountType: formData.accountType,
        resetStrategy: formData.resetStrategy,
        convertRatio: formData.convertRatio,
      })
      message.success('新增成功')
    } else {
      await updatePointRule(currentId.value!, {
        sourceId: formData.sourceId!,
        ruleName: formData.ruleName,
        eventType: formData.eventType,
        pointValue: formData.pointValue,
        conditionExpression: formData.conditionExpression || null,
        conditionDescription: formData.conditionDescription || null,
        multiplierRule: multiplierJson,
        cycleLimit: formData.cycleLimit,
        requireApproval: formData.requireApproval,
        sortOrder: formData.sortOrder,
        isEnabled: formData.isEnabled,
        accountType: formData.accountType,
        resetStrategy: formData.resetStrategy,
        convertRatio: formData.convertRatio,
      })
      message.success('更新成功')
    }
    dialogVisible.value = false
    fetchRules()
  } finally {
    submitLoading.value = false
  }
}

onMounted(() => {
  fetchSources()
  fetchRules()
})
</script>

<style scoped lang="scss">
:deep(.ant-collapse-header) {
  font-weight: 600;
  font-size: 14px;
}

:deep(.ant-collapse-content-box) {
  padding: 0 !important;
}
</style>
