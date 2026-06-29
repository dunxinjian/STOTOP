<template>
  <a-modal
    v-model:open="dialogOpen"
    :title="rule ? '编辑规则 - ' + rule.ruleName : '新增 AutoVoucher 规则'"
    width="1500px"
    :destroyOnClose="true"
    centered
    class="auto-voucher-rule-dialog"
    :bodyStyle="{ height: '75vh', overflowY: 'auto', padding: '16px 20px' }"
  >
    <a-spin :spinning="pageLoading" :tip="submitting ? '正在保存...' : '正在加载规则配置...'" size="large">
    <!-- 顶部基本信息 -->
    <a-form :model="formData" layout="inline" style="margin-bottom: 12px;">
      <a-form-item label="规则名称" required>
        <a-input v-model:value="formData.ruleName" placeholder="请输入规则名称" style="width: 220px;" />
      </a-form-item>
      <a-form-item label="说明">
        <a-input v-model:value="formData.description" placeholder="规则说明" style="width: 200px;" />
      </a-form-item>
      <a-form-item v-if="rule" label="状态">
        <a-switch
          v-model:checked="formData.status"
          :checkedValue="1"
          :unCheckedValue="0"
          checkedChildren="启用"
          unCheckedChildren="禁用"
        />
      </a-form-item>
    </a-form>

    <!-- 工具栏 -->
    <div class="toolbar">
      <span class="label">暂存表</span>
      <a-select
        v-model:value="formData.stagingTable"
        placeholder="选择暂存表"
        style="width: 240px;"
        :loading="stagingTablesLoading"
        show-search
        allow-clear
        @change="onStagingTableChange"
      >
        <a-select-option v-for="t in stagingTables" :key="t.tableName" :value="t.tableName">
          {{ t.tableName }}
        </a-select-option>
      </a-select>

      <a-divider type="vertical" />

      <span class="label">账套</span>
      <a-select
        v-model:value="formData.accountSetId"
        placeholder="选择账套"
        style="width: 240px;"
        :loading="accountSetsLoading"
      >
        <a-select-option v-for="item in accountSets" :key="item.id" :value="item.id">
          {{ item.fName }}
          <a-tag v-if="item.fIsDefault" color="orange" style="margin-left: 4px; font-size: 11px;">默认</a-tag>
        </a-select-option>
      </a-select>

      <a-divider type="vertical" />

      <span class="label">分组字段</span>
      <a-auto-complete
        v-model:value="formData.groupBy"
        :options="columnOptions"
        placeholder="不分组"
        allow-clear
        style="width: 180px;"
        :filter-option="filterOption"
      />

      <a-divider type="vertical" />

      <span class="label">凭证字</span>
      <a-input v-model:value="formData.voucherWord" style="width: 80px;" />

      <a-divider type="vertical" />

      <span class="label">日期字段</span>
      <a-auto-complete
        v-model:value="formData.dateField"
        :options="columnOptions"
        placeholder="F业务日期"
        allow-clear
        style="width: 160px;"
        :filter-option="filterOption"
      />

      <a-divider type="vertical" />

      <span class="label">去重键</span>
      <a-select
        v-model:value="formData.keyFields"
        mode="tags"
        placeholder="选择或输入去重键字段"
        style="width: 260px;"
        :options="columnOptions"
        :filter-option="filterOption"
        allow-clear
      />
    </div>

    <!-- 规则组列表 -->
    <div class="rule-groups">
      <div v-for="(group, gIdx) in ruleGroups" :key="gIdx" class="rule-group-card">
        <div class="group-header">
          <div class="group-header-left">
            <a-tag color="blue">组 {{ gIdx + 1 }}</a-tag>
            <a-input v-model:value="group.name" placeholder="规则组名称" style="width: 240px;" size="small" />
            <a-tag>{{ group.lines.length }} 行</a-tag>
          </div>
          <div class="group-header-right">
            <a-button size="small" @click="addLine(group)"><PlusOutlined /> 添加分录行</a-button>
            <a-popconfirm title="确定删除整个规则组？" @confirm="ruleGroups.splice(gIdx, 1)">
              <a-button size="small" danger><DeleteOutlined /> 删除组</a-button>
            </a-popconfirm>
          </div>
        </div>

        <!-- 分录行表格 -->
        <div class="entry-lines" v-if="group.lines.length > 0">
          <div class="entry-header">
            <span class="col-action">操作</span>
            <span class="col-no">行</span>
            <span class="col-dir">方向</span>
            <span class="col-account">科目</span>
            <span class="col-amount">金额字段</span>
            <span class="col-agg">聚合</span>
            <span class="col-summary">摘要模板</span>
            <span class="col-condition">生成条件</span>
            <span class="col-aux-btn">辅助核算</span>
          </div>
          <div v-for="(line, lIdx) in group.lines" :key="lIdx" class="entry-row">
            <div class="col-action">
              <a-popconfirm title="删除此行？" @confirm="group.lines.splice(lIdx, 1)">
                <a-button type="link" danger size="small"><DeleteOutlined /></a-button>
              </a-popconfirm>
            </div>
            <span class="col-no">{{ lIdx + 1 }}</span>
            <div class="col-dir">
              <a-select v-model:value="line.direction" size="small" style="width: 100%;">
                <a-select-option value="借">借</a-select-option>
                <a-select-option value="贷">贷</a-select-option>
              </a-select>
            </div>
            <div class="col-account">
              <div class="account-inline">
                <a-radio-group v-model:value="line.accountMode" size="small" button-style="solid">
                  <a-radio-button value="fixed">固定</a-radio-button>
                  <a-radio-button value="dynamic">动态</a-radio-button>
                </a-radio-group>
                <template v-if="line.accountMode === 'fixed'">
                  <a-tree-select
                    v-model:value="line.accountId"
                    placeholder="选择科目"
                    size="small"
                    style="flex: 1; min-width: 0;"
                    :tree-data="accountTreeData"
                    :fieldNames="{ label: 'title', value: 'id', children: 'children' }"
                    show-search
                    tree-node-filter-prop="title"
                    allow-clear
                  />
                </template>
                <template v-else>
                  <a-select
                    :value="line.accountMatchField ?? undefined"
                    placeholder="匹配字段"
                    size="small"
                    style="width: 100px;"
                    allow-clear
                    @update:value="(v: SelectValue) => line.accountMatchField = (v as string | undefined) ?? null"
                  >
                    <a-select-option v-for="f in stagingFields" :key="f" :value="f">{{ f }}</a-select-option>
                  </a-select>
                  <a-button size="small" type="dashed" @click="openMatchRuleEditor(line)" style="flex: 1; padding: 0 6px;">
                    映射 ({{ getMatchRuleCount(line) }})
                  </a-button>
                </template>
              </div>
            </div>
            <div class="col-amount">
              <a-select v-model:value="line.amountField" size="small" style="width: 105px;" allow-clear show-search>
                <a-select-option v-for="f in stagingFields" :key="f" :value="f">{{ f }}</a-select-option>
              </a-select>
            </div>
            <div class="col-agg">
              <a-select v-model:value="line.amountAggregation" size="small" style="width: 100%;">
                <a-select-option value="SUM">SUM</a-select-option>
                <a-select-option value="ROW">ROW</a-select-option>
              </a-select>
            </div>
            <div class="col-summary">
              <a-input v-model:value="line.summaryTemplate" size="small" placeholder="&#123;字段名&#125; 变量" />
            </div>
            <div class="col-condition">
              <div class="condition-inline">
                <a-select
                  :value="line.conditionField ?? undefined"
                  placeholder="条件字段"
                  size="small"
                  style="width: 120px; flex-shrink: 0;"
                  allow-clear
                  @update:value="(v: SelectValue) => line.conditionField = (v as string | undefined) ?? null"
                >
                  <a-select-option v-for="f in stagingFields" :key="f" :value="f">{{ f }}</a-select-option>
                </a-select>
                <a-select
                  v-if="line.conditionField"
                  v-model:value="line.conditionValues"
                  mode="tags"
                  size="small"
                  placeholder="输入值后回车"
                  style="flex: 1; min-width: 0;"
                />
              </div>
            </div>
            <div class="col-aux-btn">
              <a-button size="small" type="dashed" @click="openAuxEditor(line)" style="padding: 0 6px;">
                辅助核算<template v-if="line.auxiliaryConfigs.length > 0"> ({{ line.auxiliaryConfigs.length }})</template>
              </a-button>
            </div>
          </div>
        </div>
        <a-empty v-else description="暂无分录行" :imageStyle="{ height: '32px' }" />
      </div>
    </div>

    <a-button type="dashed" block style="margin-top: 12px;" @click="addGroup">
      <PlusOutlined /> 添加规则组
    </a-button>
    </a-spin>

    <!-- 辅助核算编辑弹窗 -->
    <a-modal
      v-model:open="auxEditorVisible"
      title="辅助核算设置"
      width="660px"
      okText="确定"
      cancelText="取消"
      :destroyOnClose="false"
      @ok="saveAuxConfigs"
    >
      <template v-if="editingAuxConfigs">
        <div v-for="(aux, aIdx) in editingAuxConfigs" :key="aIdx" class="aux-modal-item">
          <a-auto-complete
            v-model:value="aux.name"
            :options="auxNameOptions"
            placeholder="辅助类型"
            size="small"
            style="width: 100px;"
            @select="(val: SelectValue) => onAuxNameSelect(aux, val == null ? '' : String(val))"
          />
          <a-radio-group v-model:value="aux.type" size="small" class="aux-type-switch" button-style="solid" @change="() => onAuxTypeChange(aux)">
            <a-radio-button value="fixed">固定值</a-radio-button>
            <a-radio-button value="field">字段</a-radio-button>
          </a-radio-group>
          <a-select
            v-if="aux.type === 'fixed'"
            v-model:value="aux.value"
            placeholder="选择辅助核算项目"
            size="small"
            style="width: 180px;"
            show-search
            :filter-option="(input: string, option: any) => option.label?.toLowerCase().includes(input.toLowerCase())"
            :options="auxItemsCache[aux.name?.trim()] || []"
            :loading="auxItemsLoading"
            @focus="loadAuxItems(aux.name)"
            allow-clear
          />
          <a-select
            v-else
            v-model:value="aux.field"
            placeholder="选择字段"
            size="small"
            style="width: 140px;"
            show-search
            allow-clear
          >
            <a-select-option v-for="f in stagingFields" :key="f" :value="f">{{ f }}</a-select-option>
          </a-select>
          <a-button type="link" danger size="small" @click="editingAuxConfigs.splice(aIdx, 1)">
            <DeleteOutlined />
          </a-button>
        </div>
        <a-empty v-if="editingAuxConfigs.length === 0" description="暂无辅助核算项" :imageStyle="{ height: '32px' }" />
        <a-button type="dashed" block size="small" style="margin-top: 8px;" @click="editingAuxConfigs.push({ name: '', type: 'fixed', value: '', field: '' })">
          <PlusOutlined /> 添加核算项
        </a-button>
      </template>
    </a-modal>

    <!-- 动态匹配映射编辑弹窗 -->
    <a-modal
      v-model:open="matchEditorVisible"
      title="科目动态匹配映射"
      width="600px"
      @ok="saveMatchRules"
      :destroyOnClose="true"
    >
      <p style="color: var(--text-3); font-size: 12px; margin-bottom: 12px;">
        当暂存表字段「{{ editingLine?.accountMatchField || '?' }}」的值匹配下列规则时，使用对应的科目
      </p>
      <div v-for="(mr, mrIdx) in editingMatchRules" :key="mrIdx"
        style="display: flex; gap: 8px; margin-bottom: 8px; align-items: center;">
        <a-input v-model:value="mr.value" placeholder="字段值" style="flex: 1;" size="small" />
        <RightOutlined />
        <a-tree-select
          v-model:value="mr.accountId"
          placeholder="选择科目"
          size="small"
          style="flex: 1;"
          :tree-data="accountTreeData"
          :fieldNames="{ label: 'title', value: 'id', children: 'children' }"
          show-search
          tree-node-filter-prop="title"
          allow-clear
        />
        <a-button size="small" danger @click="editingMatchRules.splice(mrIdx, 1)"><DeleteOutlined /></a-button>
      </div>
      <a-button type="dashed" block size="small" @click="editingMatchRules.push({ value: '', accountId: undefined })">
        <PlusOutlined /> 添加映射
      </a-button>
    </a-modal>

    <template #footer>
      <div style="display: flex; justify-content: flex-end; gap: 8px;">
        <a-button @click="emit('update:open', false)">取消</a-button>
        <a-button type="primary" @click="handleSave" :loading="submitting">
          {{ rule ? '保存' : '创建' }}
        </a-button>
      </div>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted } from 'vue'
import type { SelectValue } from 'ant-design-vue/es/select'
import { message } from 'ant-design-vue'
import {
  PlusOutlined, DeleteOutlined, RightOutlined,
} from '@ant-design/icons-vue'
import { getAccountTree, getAccountSets, getAuxiliaryItemsByAccountSet } from '@/api/finance'
import type { AccountSetDto } from '@/api/finance'
import { createAutoPluginRule, updateAutoPluginRule, getStagingTables, getStagingTableColumns } from '@/api/cardflow'
import type { AutoPluginRuleDto, StagingTableInfo } from '@/api/cardflow'

// ==================== 接口类型 ====================
interface AuxiliaryConfig {
  name: string
  type: 'fixed' | 'field'
  value?: string
  field?: string
}

interface EntryLine {
  direction: string
  accountMode: 'fixed' | 'dynamic'
  accountId?: number | null
  accountMatchField?: string | null
  accountMatchRules: Array<{ value: string; accountId?: number }>
  amountField: string
  amountAggregation: string
  summaryTemplate: string
  conditionField?: string | null
  conditionValues: string[]
  auxiliaryConfigs: AuxiliaryConfig[]
  status: number
}

interface RuleGroup {
  name: string
  lines: EntryLine[]
}

// ==================== Props & Emits ====================
const props = defineProps<{
  open: boolean
  rule: AutoPluginRuleDto | null
  typeCode: string
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved'): void
}>()

const dialogOpen = computed({
  get: () => props.open,
  set: (val: boolean) => emit('update:open', val),
})

const submitting = ref(false)
const pageLoading = ref(false)

const formData = reactive({
  ruleName: '',
  description: '',
  status: 1 as number,
  stagingTable: '' as string,
  accountSetId: undefined as number | undefined,
  groupBy: '',
  voucherWord: '记',
  dateField: 'F业务日期',
  keyFields: [] as string[],
})

// ==================== 暂存表 ====================
const stagingTables = ref<StagingTableInfo[]>([])
const stagingTablesLoading = ref(false)
const stagingFields = ref<string[]>([])

const columnOptions = computed(() =>
  stagingFields.value.map(f => ({ label: f, value: f }))
)

function filterOption(input: string, option: any) {
  return option.value?.toLowerCase().includes(input.toLowerCase())
}

async function fetchStagingTables() {
  stagingTablesLoading.value = true
  try {
    const res: any = await getStagingTables()
    stagingTables.value = res.data ?? res ?? []
  } catch { /* silent */ }
  finally { stagingTablesLoading.value = false }
}

async function loadStagingColumns(tableName: string) {
  if (!tableName) { stagingFields.value = []; return }
  try {
    const res: any = await getStagingTableColumns(tableName)
    const cols: Array<{ columnName: string }> = res.data ?? res ?? []
    const systemCols = new Set(['FId', 'FBatchId', 'FStatus', 'FErrorMessage',
      'FCreateTime', 'FUpdateTime', 'FImportBatchNo', 'FOtherColumnsData'])
    stagingFields.value = cols.map(c => c.columnName).filter(n => !systemCols.has(n))
  } catch { stagingFields.value = [] }
}

async function onStagingTableChange(val: any) {
  stagingFields.value = []
  if (val) await loadStagingColumns(val)
}

// ==================== 账套 ====================
const accountSets = ref<AccountSetDto[]>([])
const accountSetsLoading = ref(false)

async function fetchAccountSets() {
  accountSetsLoading.value = true
  try {
    const res = await getAccountSets() as any
    accountSets.value = res.data ?? res ?? []
    if (accountSets.value.length > 0 && !formData.accountSetId) {
      const def = accountSets.value.find(a => a.fIsDefault)
      formData.accountSetId = def?.id ?? accountSets.value[0].id
    }
  } catch { /* silent */ }
  finally { accountSetsLoading.value = false }
}

// ==================== 科目树 ====================
const accountTreeData = ref<any[]>([])

async function fetchAccountTree() {
  if (!formData.accountSetId) return
  try {
    const res = await getAccountTree(undefined, formData.accountSetId) as any
    accountTreeData.value = buildTreeData(res.data ?? res ?? [])
  } catch { accountTreeData.value = [] }
}

function buildTreeData(items: any[]): any[] {
  return items.map(item => ({
    id: item.id ?? item.fid,
    title: `${item.code ?? item.fCode ?? ''} ${item.name ?? item.fName ?? ''}`.trim(),
    children: item.children ? buildTreeData(item.children) : undefined,
    selectable: !item.children || item.children.length === 0,
  }))
}

watch(() => formData.accountSetId, () => { fetchAccountTree() })

// ==================== 规则组 ====================
const ruleGroups = ref<RuleGroup[]>([])

function addGroup() {
  ruleGroups.value.push({ name: `规则组${ruleGroups.value.length + 1}`, lines: [] })
}

function addLine(group: RuleGroup) {
  group.lines.push({
    direction: '借',
    accountMode: 'fixed',
    accountId: undefined,
    accountMatchField: null,
    accountMatchRules: [],
    amountField: '',
    amountAggregation: 'ROW',
    summaryTemplate: '',
    conditionField: null,
    conditionValues: [],
    auxiliaryConfigs: [],
    status: 1,
  })
}

// ==================== 辅助核算弹窗 ====================
const auxEditorVisible = ref(false)
const editingAuxLine = ref<EntryLine | null>(null)
const editingAuxConfigs = ref<AuxiliaryConfig[]>([])

function openAuxEditor(line: EntryLine) {
  editingAuxLine.value = line
  editingAuxConfigs.value = JSON.parse(JSON.stringify(line.auxiliaryConfigs))
  auxEditorVisible.value = true
  for (const aux of editingAuxConfigs.value) {
    if (aux.name && aux.type === 'fixed') loadAuxItems(aux.name)
  }
}

function saveAuxConfigs() {
  if (editingAuxLine.value) {
    editingAuxLine.value.auxiliaryConfigs = editingAuxConfigs.value
  }
  auxEditorVisible.value = false
}

// ==================== 动态匹配编辑器 ====================
const matchEditorVisible = ref(false)
const editingLine = ref<EntryLine | null>(null)
const editingMatchRules = ref<Array<{ value: string; accountId?: number }>>([])

function openMatchRuleEditor(line: EntryLine) {
  editingLine.value = line
  editingMatchRules.value = JSON.parse(JSON.stringify(line.accountMatchRules))
  if (editingMatchRules.value.length === 0) {
    editingMatchRules.value.push({ value: '', accountId: undefined })
  }
  matchEditorVisible.value = true
}

function saveMatchRules() {
  if (editingLine.value) {
    editingLine.value.accountMatchRules = editingMatchRules.value.filter(r => r.value)
  }
  matchEditorVisible.value = false
}

function getMatchRuleCount(line: EntryLine): number {
  return line.accountMatchRules.filter(r => r.value).length
}

// ==================== 辅助核算选项 ====================
const auxNameOptions = [
  { value: '部门' }, { value: '客户' }, { value: '供应商' },
  { value: '项目' }, { value: '员工' }, { value: '经营单元' }, { value: '快递品牌' },
]

const auxTypeMap: Record<string, string> = {
  '客户': 'customer', '供应商': 'supplier', '员工': 'employee',
  '部门': 'department', '项目': 'project', '经营单元': 'business_unit', '快递品牌': 'express_brand',
}

const auxItemsCache = ref<Record<string, Array<{ value: string; label: string }>>>({})
const auxItemsLoading = ref(false)

async function loadAuxItems(auxName: string) {
  const trimmed = auxName?.trim()
  if (!trimmed) return
  const auxType = auxTypeMap[trimmed]
  if (!auxType) return
  if (auxItemsCache.value[trimmed]) return
  if (!formData.accountSetId) return

  auxItemsLoading.value = true
  try {
    const res = await getAuxiliaryItemsByAccountSet({ accountSetId: formData.accountSetId, auxType })
    const items = ((res as any).data || res || []).map((item: any) => ({
      value: `${item.code || item.id}`,
      label: `${item.code ? item.code + ' ' : ''}${item.name}`,
    }))
    auxItemsCache.value[trimmed] = items
  } catch { auxItemsCache.value[trimmed] = [] }
  finally { auxItemsLoading.value = false }
}

function onAuxNameSelect(aux: AuxiliaryConfig, name: string) {
  aux.name = name
  aux.value = ''
  if (aux.type === 'fixed') loadAuxItems(name)
}

function onAuxTypeChange(aux: AuxiliaryConfig) {
  if (aux.type === 'fixed' && aux.name) loadAuxItems(aux.name)
}

// ==================== 保存 ====================
async function handleSave() {
  if (!formData.ruleName.trim()) { message.warning('请输入规则名称'); return }
  if (!formData.stagingTable) { message.warning('请选择暂存表'); return }
  if (!formData.accountSetId) { message.warning('请选择账套'); return }

  submitting.value = true
  pageLoading.value = true
  try {
    const configJson = JSON.stringify({
      mode: 'rulesBased',
      stagingTable: formData.stagingTable,
      accountSetId: formData.accountSetId,
      groupBy: formData.groupBy || undefined,
      voucherWord: formData.voucherWord || '记',
      dateField: formData.dateField || 'F业务日期',
      keyFields: formData.keyFields || [],
      ruleGroups: ruleGroups.value.map(g => ({
        name: g.name,
        lines: g.lines.map((l, i) => ({
          lineNo: i + 1,
          direction: l.direction,
          accountMode: l.accountMode,
          accountId: l.accountMode === 'fixed' ? l.accountId : null,
          accountMatchField: l.accountMode === 'dynamic' ? l.accountMatchField : null,
          accountMatchRules: l.accountMode === 'dynamic' ? l.accountMatchRules : [],
          amountField: l.amountField,
          amountAggregation: l.amountAggregation,
          summaryTemplate: l.summaryTemplate || null,
          conditionField: l.conditionField || null,
          conditionValues: l.conditionValues?.length ? l.conditionValues : [],
          auxiliaryConfigs: l.auxiliaryConfigs?.length ? l.auxiliaryConfigs : [],
          priority: i + 1,
          status: l.status ?? 1,
        }))
      }))
    })

    if (props.rule) {
      await updateAutoPluginRule(props.rule.id, {
        typeCode: props.typeCode,
        ruleName: formData.ruleName,
        description: formData.description,
        status: formData.status,
        configJson,
      })
      message.success('规则更新成功')
    } else {
      await createAutoPluginRule({
        typeCode: props.typeCode,
        ruleName: formData.ruleName,
        description: formData.description,
        configJson,
      })
      message.success('规则创建成功')
    }
    emit('saved')
    emit('update:open', false)
  } catch {
    message.error(props.rule ? '更新失败' : '创建失败')
  } finally {
    submitting.value = false
    pageLoading.value = false
  }
}

// ==================== 数据加载（编辑时） ====================
function resetAll() {
  formData.ruleName = ''
  formData.description = ''
  formData.status = 1
  formData.stagingTable = ''
  formData.accountSetId = undefined
  formData.groupBy = ''
  formData.voucherWord = '记'
  formData.dateField = 'F业务日期'
  formData.keyFields = []
  ruleGroups.value = []
  auxItemsCache.value = {}
}

async function loadFromRule(r: AutoPluginRuleDto) {
  formData.ruleName = r.ruleName
  formData.description = r.description ?? ''
  formData.status = r.status

  if (r.configJson) {
    try {
      const config = JSON.parse(r.configJson)
      formData.stagingTable = config.stagingTable ?? ''
      formData.accountSetId = config.accountSetId
      formData.groupBy = config.groupBy ?? ''
      formData.voucherWord = config.voucherWord || '记'
      formData.dateField = config.dateField || 'F业务日期'
      formData.keyFields = config.keyFields || []
      ruleGroups.value = (config.ruleGroups || []).map((g: any) => ({
        name: g.name ?? '默认组',
        lines: (g.lines || []).map((l: any) => ({
          direction: l.direction ?? '借',
          accountMode: l.accountMode ?? 'fixed',
          accountId: l.accountId,
          accountMatchField: l.accountMatchField,
          accountMatchRules: l.accountMatchRules || [],
          amountField: l.amountField ?? '',
          amountAggregation: l.amountAggregation ?? 'ROW',
          summaryTemplate: l.summaryTemplate ?? '',
          conditionField: l.conditionField,
          conditionValues: l.conditionValues || [],
          auxiliaryConfigs: l.auxiliaryConfigs || [],
          status: l.status ?? 1,
        })),
      }))
      const loadTasks: Promise<void>[] = []
      if (config.stagingTable) loadTasks.push(loadStagingColumns(config.stagingTable))
      if (config.accountSetId) {
        formData.accountSetId = config.accountSetId
        loadTasks.push(fetchAccountTree())
      }
      await Promise.all(loadTasks)
    } catch { /* ignore */ }
  }
}

watch(
  () => props.open,
  async (val) => {
    if (!val) return
    pageLoading.value = true
    try {
      if (props.rule) {
        // 先加载下拉框数据源，再加载规则配置
        await Promise.all([fetchStagingTables(), fetchAccountSets()])
        await loadFromRule(props.rule)
      } else {
        resetAll()
        await Promise.all([fetchStagingTables(), fetchAccountSets()])
        // 设置默认账套
        if (accountSets.value.length > 0 && !formData.accountSetId) {
          const def = accountSets.value.find(a => a.fIsDefault)
          formData.accountSetId = def?.id ?? accountSets.value[0].id
        }
      }
    } finally {
      pageLoading.value = false
    }
  },
  { immediate: true },
)

// ==================== 初始化 ====================
onMounted(async () => {
  await Promise.all([fetchStagingTables(), fetchAccountSets()])
})
</script>

<style lang="scss" scoped>
.toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: var(--bg-muted);
  border-radius: 8px;
  border: 1px solid var(--border);
  flex-wrap: wrap;

  .label {
    font-weight: 600;
    color: var(--text-1);
    white-space: nowrap;
  }
}

.rule-group-card {
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 14px;
  margin-bottom: 12px;
  background: var(--bg-card);
  transition: box-shadow 0.2s;
  &:hover { box-shadow: var(--shadow-sm); }
}

.group-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.group-header-left,
.group-header-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.entry-lines {
  border: 1px solid var(--border);
  border-radius: 6px;
  overflow-x: auto;
}

.entry-header,
.entry-row {
  display: flex;
  align-items: stretch;
  gap: 0;
  padding: 0;
  min-width: 1100px;

  > span,
  > div {
    padding: 8px 6px;
    display: flex;
    align-items: center;
  }
}

.entry-header {
  background: var(--bg-muted);
  font-weight: 600;
  font-size: 12px;
  color: var(--text-1);
  border-bottom: 1px solid var(--border-strong);
  > span { text-align: center; justify-content: center; }
}

.entry-row {
  border-bottom: 1px solid var(--border);
  &:last-child { border-bottom: none; }
}

.col-action { width: 42px; flex-shrink: 0; text-align: center; justify-content: center; border-right: 1px solid var(--border-strong); }
.col-no { width: 36px; text-align: center; flex-shrink: 0; justify-content: center; border-right: 1px solid var(--border-strong); }
.col-dir { width: 56px; flex-shrink: 0; border-right: 1px solid var(--border-strong); }
.col-account { width: 320px; flex-shrink: 0; border-right: 1px solid var(--border-strong); }
.col-amount { width: 105px; flex-shrink: 0; border-right: 1px solid var(--border-strong); }
.col-agg { width: 80px; flex-shrink: 0; border-right: 1px solid var(--border-strong); }
.col-summary { width: 170px; flex-shrink: 0; border-right: 1px solid var(--border-strong); }
.col-condition { width: 280px; flex-shrink: 0; border-right: 1px solid var(--border-strong); }
.col-aux-btn { width: 110px; flex-shrink: 0; text-align: center; justify-content: center; }

.account-inline {
  display: flex;
  align-items: center;
  gap: 4px;
  width: 100%;
}

.condition-inline {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
}

.aux-modal-item {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.aux-type-switch {
  :deep(.ant-radio-button-wrapper-checked) {
    background-color: var(--bg-muted) !important;
    border-color: var(--border-strong) !important;
    font-weight: 500;
    color: var(--text-1) !important;
  }
}
</style>
