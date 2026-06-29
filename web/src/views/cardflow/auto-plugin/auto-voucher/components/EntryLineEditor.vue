<!-- 分录行编辑器：折叠分区编辑单条分录行的方向、科目、辅助核算、摘要、条件等 -->
<template>
  <div class="entry-line-editor">
    <a-collapse v-model:activeKey="activeKeys" :bordered="false" class="editor-collapse">
      <!-- ▼ 基本配置：方向 + 金额字段 -->
      <a-collapse-panel key="basic" header="基本配置">
        <a-form layout="vertical" size="small">
          <a-row :gutter="12">
            <a-col :span="8">
              <a-form-item label="方向" :validate-status="fieldStatus('direction')" :help="fieldErrorMsg('direction')">
                <a-radio-group
                  :value="line.direction"
                  @change="(e: any) => updateField('direction', e.target.value)"
                >
                  <a-radio-button value="借">借</a-radio-button>
                  <a-radio-button value="贷">贷</a-radio-button>
                </a-radio-group>
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="金额字段" :validate-status="fieldStatus('amountField')" :help="fieldErrorMsg('amountField')">
                <a-select
                  :value="line.amountField"
                  :status="fieldStatus('amountField')"
                  placeholder="选择金额字段"
                  allow-clear
                  show-search
                  @change="(val: any) => updateField('amountField', val)"
                >
                  <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
                </a-select>
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="聚合方式">
                <a-radio-group
                  :value="line.amountAggregation"
                  @change="(e: any) => updateField('amountAggregation', e.target.value)"
                >
                  <a-radio-button value="ROW">逐行</a-radio-button>
                  <a-radio-button value="SUM">汇总</a-radio-button>
                </a-radio-group>
              </a-form-item>
            </a-col>
          </a-row>
        </a-form>
      </a-collapse-panel>

      <!-- ▼ 科目配置：固定/动态切换 + AccountMatchTable -->
      <a-collapse-panel key="account" header="科目配置">
        <a-form layout="vertical" size="small">
          <a-form-item label="科目模式">
            <a-radio-group
              :value="line.accountMode"
              @change="(e: any) => onAccountModeChange(e.target.value)"
            >
              <a-radio-button value="fixed">固定科目</a-radio-button>
              <a-radio-button value="dynamic">动态科目</a-radio-button>
            </a-radio-group>
          </a-form-item>

          <!-- 固定科目 -->
          <template v-if="line.accountMode === 'fixed'">
            <a-form-item label="科目" :validate-status="fieldStatus('accountId')" :help="fieldErrorMsg('accountId')">
              <a-tree-select
                :value="line.accountId"
                :status="fieldStatus('accountId')"
                placeholder="选择科目"
                :tree-data="accountTreeData"
                :fieldNames="{ label: 'title', value: 'id', children: 'children' }"
                show-search
                tree-node-filter-prop="title"
                allow-clear
                style="width: 100%;"
                @change="(val: any) => updateField('accountId', val)"
              />
            </a-form-item>
          </template>

          <!-- 动态科目 -->
          <template v-else>
            <a-form-item label="匹配字段" :validate-status="fieldStatus('accountMatchField')" :help="fieldErrorMsg('accountMatchField')">
              <a-select
                :value="line.accountMatchField ?? undefined"
                :status="fieldStatus('accountMatchField')"
                placeholder="选择暂存表列"
                allow-clear
                show-search
                @change="(val: any) => updateField('accountMatchField', val)"
              >
                <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item label="动态映射规则" :validate-status="fieldStatus('accountMatchRules')" :help="fieldErrorMsg('accountMatchRules')">
              <AccountMatchTable
                v-model="editingMatchRules"
                :account-tree-data="accountTreeData"
                :batch-values="batchValues"
                @update:model-value="onMatchRulesChange"
              />
            </a-form-item>
          </template>
        </a-form>
      </a-collapse-panel>

      <!-- ▼ 辅助核算：AuxiliaryConfigList -->
      <a-collapse-panel key="auxiliary" header="辅助核算">
        <AuxiliaryConfigList
          v-model="editingAuxConfigs"
          :fields="fields"
          :account-set-id="accountSetId"
          @update:model-value="onAuxConfigsChange"
        />
      </a-collapse-panel>

      <!-- ▼ 摘要模板：SummaryTemplateInput -->
      <a-collapse-panel key="summary" header="摘要模板">
        <a-form layout="vertical" size="small">
          <a-form-item :validate-status="fieldStatus('summaryTemplate')" :help="fieldErrorMsg('summaryTemplate')">
            <SummaryTemplateInput
              :model-value="line.summaryTemplate"
              :fields="fields"
              :sample-row="sampleRow"
              @update:model-value="(val: string) => updateField('summaryTemplate', val)"
            />
          </a-form-item>
        </a-form>
      </a-collapse-panel>

      <!-- ▷ 条件过滤（默认折叠） -->
      <a-collapse-panel key="condition" header="条件过滤" force-render>
        <a-form layout="vertical" size="small">
          <a-form-item label="条件字段">
            <a-select
              :value="line.conditionField ?? undefined"
              placeholder="选择暂存表列"
              allow-clear
              show-search
              @change="(val: any) => updateField('conditionField', val)"
            >
              <a-select-option v-for="f in fields" :key="f" :value="f">{{ f }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item v-if="line.conditionField" label="条件值" :validate-status="fieldStatus('conditionValues')" :help="fieldErrorMsg('conditionValues')">
            <a-select
              :value="line.conditionValues"
              :status="fieldStatus('conditionValues')"
              mode="tags"
              placeholder="输入值后回车"
              @change="(val: any) => updateField('conditionValues', val)"
            />
          </a-form-item>
        </a-form>
      </a-collapse-panel>
    </a-collapse>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useAutoVoucherRuleStore } from '@/stores/autoVoucherRule'
import type { EntryLine, AccountMatchRule, AuxiliaryConfig } from '@/stores/autoVoucherRule'
import AccountMatchTable from './AccountMatchTable.vue'
import AuxiliaryConfigList from './AuxiliaryConfigList.vue'
import SummaryTemplateInput from './SummaryTemplateInput.vue'

const store = useAutoVoucherRuleStore()

const props = defineProps<{
  /** 当前编辑的分录行 */
  line: EntryLine
  /** 可用字段列表（暂存表列名） */
  fields: string[]
  /** 科目树数据 */
  accountTreeData: any[]
  /** 账套 ID */
  accountSetId?: number
  /** 批次中存在的值（用于智能提示） */
  batchValues?: string[]
  /** 预览用的第一行数据 */
  sampleRow?: Record<string, any> | null
}>()

const emit = defineEmits<{
  'update:line': [patch: Partial<EntryLine>]
}>()

// ==================== 字段级校验错误 ====================
const lineErrors = computed(() => store.entryErrors[props.line.id] || {})

function fieldStatus(field: string): '' | 'error' {
  return lineErrors.value[field] ? 'error' : ''
}

function fieldErrorMsg(field: string): string {
  return lineErrors.value[field] || ''
}

// ==================== 折叠面板状态 ====================
const activeKeys = ref(['basic', 'account', 'auxiliary', 'summary'])

// ==================== 动态科目映射的本地编辑副本 ====================
const editingMatchRules = ref<AccountMatchRule[]>(JSON.parse(JSON.stringify(props.line.accountMatchRules || [])))

watch(
  () => props.line.accountMatchRules,
  (newRules) => {
    editingMatchRules.value = JSON.parse(JSON.stringify(newRules || []))
  },
  { immediate: false }
)

// ==================== 辅助核算的本地编辑副本 ====================
const editingAuxConfigs = ref<AuxiliaryConfig[]>(JSON.parse(JSON.stringify(props.line.auxiliaryConfigs || [])))

// ==================== 字段更新 ====================
function updateField(field: string, value: any) {
  emit('update:line', { [field]: value } as Partial<EntryLine>)
  // 清除对应字段的校验错误
  store.clearEntryFieldError(props.line.id, field)
}

// ==================== 科目模式切换联动 ====================
function onAccountModeChange(mode: 'fixed' | 'dynamic') {
  const patch: Partial<EntryLine> = { accountMode: mode }
  if (mode === 'fixed') {
    // 切换到固定：清空动态配置
    patch.accountMatchField = null
    patch.accountMatchRules = []
  } else {
    // 切换到动态：清空固定科目
    patch.accountId = null
  }
  emit('update:line', patch)
}

function onMatchRulesChange(rules: AccountMatchRule[]) {
  emit('update:line', { accountMatchRules: rules })
  store.clearEntryFieldError(props.line.id, 'accountMatchRules')
}

function onAuxConfigsChange(configs: AuxiliaryConfig[]) {
  emit('update:line', { auxiliaryConfigs: configs })
}
</script>

<style lang="scss" scoped>
.entry-line-editor {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-card);
}

.editor-collapse {
  :deep(.ant-collapse-header) {
    padding: 10px 16px !important;
    font-size: 13px;
    font-weight: 500;
  }
  :deep(.ant-collapse-content-box) {
    padding: 12px 16px !important;
  }
}

:deep(.ant-form-item) {
  margin-bottom: 10px;
}
</style>
