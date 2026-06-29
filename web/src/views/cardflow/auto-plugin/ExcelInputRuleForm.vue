<template>
  <a-modal
    v-model:open="dialogOpen"
    :title="rule ? '编辑规则 - ' + rule.ruleName : '新增 ExcelInput 规则'"
    width="1400px"
    :destroyOnClose="true"
    centered
    class="excel-input-rule-dialog"
    :bodyStyle="{ height: '70vh', overflowY: 'auto', padding: '16px 20px' }"
  >
    <a-form
      :model="formData"
      :labelCol="{ style: { width: '100px' } }"
    >
      <!-- 步骤条 -->
      <a-steps :current="activeStep" class="excel-input-steps" size="small"
        :items="[
          { title: '基本信息' },
          { title: '数据分布配置' },
          { title: '列映射' },
          { title: '业务主键与流水号' },
          { title: '合计行检测' },
          { title: '高级选项' },
        ]"
        @change="(cur: number) => activeStep = cur"
      />

      <!-- Step 0: 基本信息 -->
      <div v-show="activeStep === 0" class="step-content">
        <a-form-item label="规则名称" required>
          <a-input v-model:value="formData.ruleName" placeholder="请输入规则名称" />
        </a-form-item>
        <a-form-item label="说明">
          <a-textarea v-model:value="formData.description" :rows="2" placeholder="请输入规则说明" />
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
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="表头行号">
              <a-input-number v-model:value="formData.headerRow" :min="1" :max="100" style="width: 180px" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="数据起始行">
              <a-input-number v-model:value="formData.dataStartRow" :min="1" :max="100" style="width: 180px" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-divider orientation="left">管道匹配配置</a-divider>

        <a-form-item label="全列名标识">
          <div style="display: flex; gap: 8px; align-items: flex-start;">
            <a-textarea
              v-model:value="formData.fullColumnIdentifier"
              :rows="2"
              placeholder="上传Excel自动提取，或手动输入逗号分隔的全部列名"
              style="flex: 1"
            />
            <a-upload :beforeUpload="extractColumnsFromFile" :showUploadList="false" accept=".xlsx,.xls">
              <a-button size="small">
                <template #icon><UploadOutlined /></template>
                从Excel提取
              </a-button>
            </a-upload>
          </div>
        </a-form-item>

        <a-form-item>
          <template #label>
            <span>关键列标识</span>
            <a-tooltip title="用于管道匹配：上传文件的表头包含这些列名时，自动关联到此规则">
              <QuestionCircleOutlined style="margin-left: 4px; color: var(--text-3);" />
            </a-tooltip>
          </template>
          <a-input
            v-model:value="formData.columnIdentifier"
            placeholder="可选，如: 交易日期,交易金额,网点名称"
          />
        </a-form-item>
      </div>

      <!-- Step 2: 列映射 -->
      <div v-show="activeStep === 2" class="step-content">
        <div style="margin-bottom: 16px;">
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
            <span style="font-weight: 600;">列映射配置</span>
            <div style="display: flex; gap: 8px;">
              <a-button size="small" type="primary" ghost @click="autoMatchColumns" :disabled="allExcelColumns.length === 0 || targetTableColumns.length === 0"><ThunderboltOutlined /> 自动匹配</a-button>
              <a-button size="small" type="dashed" @click="addMapping"><PlusOutlined /> 添加映射</a-button>
            </div>
          </div>

          <!-- 列映射表头 -->
          <div v-if="columnMappings.length > 0" style="display: flex; gap: 8px; margin-bottom: 6px; align-items: center; font-size: 13px; color: var(--text-2); font-weight: 500;">
            <span style="flex: 1;">Excel列名</span>
            <span style="width: 14px;"></span>
            <span style="flex: 1;">数据库字段</span>
            <span style="flex: 1; display: flex; align-items: center;">别名
              <a-tooltip>
                <template #title>
                  <div>
                    <p><strong>别名作用：</strong>解决同一字段在不同Excel中列名不一致的问题</p>
                    <p><strong>格式：</strong>多个别名用逗号分隔</p>
                    <p><strong>匹配顺序：</strong></p>
                    <ol style="padding-left: 16px; margin: 4px 0;">
                      <li>先精确匹配Excel列名</li>
                      <li>找不到则依次匹配别名</li>
                    </ol>
                    <p><strong>示例：</strong>Excel列名"收件人"，别名"收货人, 签收人"，则不同快递公司的Excel都能正确映射</p>
                  </div>
                </template>
                <QuestionCircleOutlined style="margin-left: 4px; color: var(--text-3); cursor: help;" />
              </a-tooltip>
            </span>
            <span style="width: 24px;"></span>
          </div>

          <!-- 动态表单行 -->
          <div v-for="(item, index) in columnMappings" :key="item._uid"
               style="display: flex; gap: 8px; margin-bottom: 8px; align-items: center;">
            <a-auto-complete
              v-model:value="item.excelColumn"
              placeholder="Excel列名"
              style="flex: 1;"
              allow-clear
              :options="getAvailableExcelColumns(index).map((col: string) => ({ label: col, value: col }))"
            />
            <RightOutlined />
            <a-auto-complete
              v-model:value="item.dbColumn"
              placeholder="数据库字段名"
              style="flex: 1;"
              allow-clear
              :options="targetTableColumnOptions"
            >
              <template #option="{ value: val, label }">
                <span>{{ label }}</span>
                <span style="float: right; color: var(--text-3); font-size: 12px;">{{ getColumnType(val as string) }}</span>
              </template>
            </a-auto-complete>
            <a-input v-model:value="item.aliasesStr" placeholder="别名 (逗号分隔)" style="flex: 1;" />
            <a-button danger shape="circle" size="small" @click="removeMapping(index)"><DeleteOutlined /></a-button>
          </div>
          <a-empty v-if="columnMappings.length === 0" description="暂无列映射，请添加" :imageStyle="{ height: '40px' }" />
        </div>

        <!-- 小数字段选择 -->
        <a-divider style="margin: 12px 0;" />
        <a-form-item label="数值型字段">
          <a-select
            v-model:value="decimalFields"
            mode="multiple"
            placeholder="选择需要解析为数字的字段"
            style="width: 100%;"
          >
            <a-select-option v-for="m in columnMappings.filter(x => x.dbColumn)" :key="m.dbColumn" :value="m.dbColumn">{{ m.dbColumn }}</a-select-option>
          </a-select>
          <div class="hint-text">标记的字段将被解析为 decimal 类型</div>
        </a-form-item>
      </div>

      <!-- Step 3: 业务主键与流水号 -->
      <div v-show="activeStep === 3" class="step-content">
        <a-form-item label="流水号规则">
          <div style="width: 100%;">
            <div style="display: flex; gap: 8px; align-items: center;">
              <a-input
                v-model:value="formData.serialNumberRule"
                placeholder="如: ST-{记账日期}-{流水号}"
                style="flex: 1;"
              />
              <a-dropdown :trigger="['click']">
                <a-button>插入字段 <DownOutlined /></a-button>
                <template #overlay>
                  <a-menu @click="({ key }: any) => insertSerialField(key)">
                    <a-menu-item
                      v-for="m in columnMappings.filter(x => x.excelColumn)"
                      :key="m.excelColumn"
                      :disabled="!m.excelColumn"
                    >
                      {{ m.excelColumn }}
                    </a-menu-item>
                    <a-menu-item v-if="columnMappings.filter(x => x.excelColumn).length === 0" disabled>暂无列映射</a-menu-item>
                  </a-menu>
                </template>
              </a-dropdown>
            </div>
            <div style="margin-top: 8px; display: flex; gap: 8px; flex-wrap: wrap;">
              <a-button size="small" @click="insertSerialToken('\u007B当年\u007D')">当年</a-button>
              <a-button size="small" @click="insertSerialToken('\u007B当月\u007D')">当月</a-button>
              <a-button size="small" @click="insertSerialToken('\u007B当日\u007D')">当日</a-button>
              <a-button size="small" @click="insertSerialToken('\u007B流水号\u007D')">流水号</a-button>
            </div>
            <div class="hint-text">点击按钮插入占位符，示例：ST-&#123;当年&#125;&#123;当月&#125;&#123;当日&#125;-&#123;流水号&#125;</div>
            <div class="hint-text">
              预览: <span style="color: var(--text-2);">{{ serialRulePreview }}</span>
            </div>
          </div>
        </a-form-item>

        <a-divider style="margin: 12px 0;" />

        <a-form-item>
          <template #label>
            <span>业务主键字段</span>
            <a-tooltip title="用于数据去重：相同主键的数据行在同批次内仅保留一条">
              <QuestionCircleOutlined style="margin-left: 4px; color: var(--text-3);" />
            </a-tooltip>
          </template>
          <a-select
            v-model:value="keyFields"
            mode="multiple"
            placeholder="选择参与SHA256计算的字段"
            style="width: 100%;"
          >
            <a-select-option v-for="m in columnMappings.filter(x => x.excelColumn)" :key="m.excelColumn" :value="m.excelColumn">{{ m.excelColumn }}</a-select-option>
          </a-select>
          <div class="hint-text">选中字段的值拼接后计算SHA256，作为每行数据的唯一标识</div>
        </a-form-item>

      </div>

      <!-- Step 1: 数据分布配置 -->
      <div v-show="activeStep === 1" class="step-content">
        <a-form-item label="输出模式" required>
          <a-select v-model:value="formData.outputMode" style="width: 260px;">
            <a-select-option value="stg">写入暂存表（默认）</a-select-option>
            <a-select-option value="batchRow">写入批次明细</a-select-option>
            <a-select-option value="both">两者都写</a-select-option>
          </a-select>
          <div class="hint-text">stg — 写入 STG 暂存表；batchRow — 写入批次明细行；both — 同时写入两者</div>
        </a-form-item>

        <a-divider style="margin: 8px 0 16px;" />

        <div style="margin-bottom: 16px;">
          <div style="display: flex; align-items: center; margin-bottom: 8px;">
            <span style="font-weight: 600;" :style="formData.outputMode === 'batchRow' ? { color: 'var(--text-3)' } : {}">暂存表</span>
            <span v-if="formData.outputMode !== 'batchRow'" style="color: var(--color-danger); margin-left: 4px; font-size: 14px;">*</span>
          </div>
          <template v-if="formData.outputMode === 'batchRow'">
            <a-input disabled placeholder="batchRow 模式无需配置暂存表" style="color: var(--text-3); background: var(--bg-muted);" />
          </template>
          <template v-else>
            <a-select
              v-model:value="formData.targetTable"
              placeholder="请选择目标暂存表"
              style="width: 100%;"
              showSearch
              allowClear
              :loading="stagingTablesLoading"
              @change="onTargetTableChange"
            >
              <a-select-option
                v-for="t in stagingTables"
                :key="t.tableName"
                :value="t.tableName"
              >{{ t.tableName }}</a-select-option>
            </a-select>
          </template>
          <div class="hint-text">
            <template v-if="formData.outputMode === 'batchRow'">batchRow 模式下数据写入批次明细，无需配置暂存表</template>
            <template v-else>选择数据导入的目标暂存表（STG 前缀表）</template>
          </div>
        </div>
      </div>

      <!-- Step 4: 合计行检测 -->
      <div v-show="activeStep === 4" class="step-content">
        <a-form-item label="启用合计行过滤">
          <a-switch v-model:checked="totalRowConfig.enabled" />
          <span style="margin-left: 8px; font-size: 12px; color: var(--text-3);">开启后，匹配的合计行将在写入暂存表时被自动忽略</span>
        </a-form-item>

        <template v-if="totalRowConfig.enabled">
          <a-form-item label="空字段判断">
            <a-select
              v-model:value="totalRowConfig.emptyFields"
              mode="multiple"
              placeholder="选择字段，当这些字段全部为空时识别为合计行"
              style="width: 100%;"
            >
              <a-select-option v-for="m in columnMappings.filter(x => x.excelColumn)" :key="m.excelColumn" :value="m.excelColumn">
                {{ m.excelColumn }}
              </a-select-option>
            </a-select>
            <div class="hint-text">当所选字段全部为空/null时，该行被判定为合计行</div>
          </a-form-item>

          <a-form-item label="包含字符">
            <a-select
              v-model:value="totalRowConfig.containsKeywords"
              mode="tags"
              placeholder="输入关键词后按回车添加"
              style="width: 100%;"
            >
            </a-select>
            <div class="hint-text">任意字段值包含这些关键词时，该行被判定为合计行</div>
          </a-form-item>
        </template>
      </div>

      <!-- Step 5: 高级选项 -->
      <div v-show="activeStep === 5" class="step-content">
        <div style="margin-bottom: 8px; display: flex; justify-content: space-between; align-items: center;">
          <span style="font-weight: 600;">数据转换规则</span>
          <a-button size="small" type="primary" @click="addTransformRule">添加规则</a-button>
        </div>

        <a-empty v-if="transformRules.length === 0" description="暂无转换规则" :imageStyle="{ height: '40px' }" />

        <div v-for="(rule, rIdx) in transformRules" :key="rIdx" class="transform-rule-card">
          <div class="rule-header">
            <span class="rule-index">#{{ rIdx + 1 }}</span>
            <div class="rule-header-actions">
              <a-radio-group v-model:value="rule.mode" size="small" button-style="solid">
                <a-radio-button value="visual">可视化</a-radio-button>
                <a-radio-button value="expression">表达式</a-radio-button>
              </a-radio-group>
              <a-button shape="circle" size="small" :disabled="rIdx === 0" @click="moveTransformRule(rIdx, -1)"><UpOutlined /></a-button>
              <a-button shape="circle" size="small" :disabled="rIdx === transformRules.length - 1" @click="moveTransformRule(rIdx, 1)"><DownOutlined /></a-button>
              <a-button danger shape="circle" size="small" @click="transformRules.splice(rIdx, 1)"><DeleteOutlined /></a-button>
            </div>
          </div>

          <!-- 目标列 & 源列 -->
          <div style="display: flex; gap: 8px; margin-bottom: 8px;">
            <a-select v-model:value="rule.targetColumn" placeholder="目标列" style="flex: 1;" showSearch :options="dbColumnOptions" />
            <a-select v-model:value="rule.sourceColumns" mode="multiple" placeholder="源列（可多选）" style="flex: 2;" showSearch :options="excelColumnOptions" />
          </div>

          <!-- 可视化模式 -->
          <div v-if="rule.mode === 'visual'">
            <div v-for="(step, sIdx) in rule.transforms" :key="sIdx"
                 style="display: flex; gap: 8px; margin-bottom: 6px; align-items: center; padding-left: 12px;">
              <a-tag color="default">步骤 {{ sIdx + 1 }}</a-tag>
              <a-select v-model:value="step.type" placeholder="操作" style="width: 140px;" @change="onStepTypeChange(step)">
                <a-select-option v-for="op in transformTypeOptions" :key="op.value" :value="op.value">{{ op.label }}</a-select-option>
              </a-select>
              <!-- 参数区域 -->
              <template v-if="step.type === 'left' || step.type === 'right'">
                <a-input-number v-model:value="step.params.length" :min="1" placeholder="长度" size="small" style="width: 120px;" />
              </template>
              <template v-else-if="step.type === 'replace'">
                <a-input v-model:value="step.params.search" placeholder="查找" size="small" style="width: 100px;" />
                <a-input v-model:value="step.params.replace" placeholder="替换" size="small" style="width: 100px;" />
              </template>
              <template v-else-if="step.type === 'condition'">
                <a-select v-model:value="step.params.field" placeholder="字段" size="small" style="width: 100px;" showSearch :options="excelColumnOptions" />
                <a-select v-model:value="step.params.op" placeholder="运算" size="small" style="width: 70px;">
                  <a-select-option v-for="o in ['==', '!=', '>', '<', '>=', '<=', 'contains']" :key="o" :value="o">{{ o }}</a-select-option>
                </a-select>
                <a-input v-model:value="step.params.value" placeholder="值" size="small" style="width: 70px;" />
                <a-input v-model:value="step.params.then" placeholder="满足" size="small" style="width: 70px;" />
                <a-input v-model:value="step.params.else" placeholder="不满足" size="small" style="width: 70px;" />
              </template>
              <template v-else-if="step.type === 'concat'">
                <a-input v-model:value="step.params.separator" placeholder="分隔符" size="small" style="width: 80px;" />
                <a-input v-model:value="step.params.fields" placeholder="字段名(逗号分隔)" size="small" style="flex: 1;" />
              </template>
              <template v-else-if="step.type === 'round'">
                <a-input-number v-model:value="step.params.decimals" :min="0" placeholder="小数位" size="small" style="width: 120px;" />
              </template>
              <template v-else-if="step.type === 'dateFormat'">
                <a-input v-model:value="step.params.format" placeholder="格式(如 yyyy-MM-dd)" size="small" style="width: 180px;" />
              </template>
              <template v-else-if="step.type === 'lookup'">
                <a-input v-model:value="step.params.table" placeholder="映射表名" size="small" style="width: 100px;" />
                <a-input v-model:value="step.params.key" placeholder="键列" size="small" style="width: 80px;" />
                <a-input v-model:value="step.params.value" placeholder="值列" size="small" style="width: 80px;" />
              </template>
              <template v-else-if="step.type === 'default'">
                <a-input v-model:value="step.params.value" placeholder="默认值" size="small" style="width: 150px;" />
              </template>
              <template v-else-if="step.type === 'regex'">
                <a-input v-model:value="step.params.pattern" placeholder="正则表达式" size="small" style="width: 160px;" />
                <a-input-number v-model:value="step.params.group" :min="0" placeholder="捕获组" size="small" style="width: 100px;" />
              </template>
              <!-- trim / abs 无参数 -->
              <a-button danger shape="circle" size="small" @click="rule.transforms.splice(sIdx, 1)"><DeleteOutlined /></a-button>
            </div>
            <a-button size="small" @click="addTransformStep(rule)" style="margin-left: 12px;">+ 添加步骤</a-button>
          </div>

          <!-- 表达式模式 -->
          <div v-else>
            <div style="font-size: 12px; color: var(--text-3); margin-bottom: 4px;">可用变量: <code>row['列名']</code> 访问当前行数据</div>
            <a-textarea
              v-model:value="rule.expression"
              :rows="3"
              placeholder="输入 JavaScript 表达式，如: row['记账日期'].trim()"
              style="font-family: monospace;"
            />
          </div>
        </div>

        <a-button v-if="transformRules.length > 0" type="dashed" block @click="addTransformRule" style="margin-top: 8px;">
          <PlusOutlined /> 添加转换规则
        </a-button>
      </div>
    </a-form>

    <template #footer>
      <div class="step-footer">
        <div>
          <a-button v-if="activeStep > 0" @click="activeStep--">上一步</a-button>
        </div>
        <div>
          <a-button v-if="activeStep < 5" @click="activeStep++">下一步</a-button>
          <a-button type="primary" @click="handleSubmit" :loading="submitting">
            {{ rule ? '保存' : '创建' }}
          </a-button>
          <a-button @click="emit('update:open', false)">取消</a-button>
        </div>
      </div>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { message } from 'ant-design-vue'
import {
  PlusOutlined, RightOutlined, DeleteOutlined,
  DownOutlined, UpOutlined, QuestionCircleOutlined, UploadOutlined, ThunderboltOutlined,
} from '@ant-design/icons-vue'
import { createAutoPluginRule, updateAutoPluginRule, getAutoPluginRule, getStagingTables, getStagingTableColumns, extractColumnsFromExcel } from '@/api/cardflow'
import type { AutoPluginRuleDto, StagingTableInfo, StagingColumnInfo } from '@/api/cardflow'
import { genTempId } from '@/utils/tempId'

interface MappingRow {
  _uid: string
  excelColumn: string
  dbColumn: string
  aliasesStr: string
}

interface TransformStep {
  type: string
  params: Record<string, any>
}

interface TransformRuleItem {
  targetColumn: string
  sourceColumns: string[]
  mode: 'visual' | 'expression'
  transforms: TransformStep[]
  expression: string
}

const props = defineProps<{
  open: boolean
  rule: AutoPluginRuleDto | null
  typeCode: string
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved'): void
}>()

// ==================== 对话框状态 ====================
const dialogOpen = computed({
  get: () => props.open,
  set: (val: boolean) => emit('update:open', val),
})

const activeStep = ref(0)
const submitting = ref(false)

const formData = reactive({
  ruleName: '',
  description: '',
  status: 1 as number,
  outputMode: 'stg' as 'stg' | 'batchRow' | 'both',
  targetTable: '' as string,
  headerRow: 1,
  dataStartRow: 2,
  serialNumberRule: '',
  fullColumnIdentifier: '',
  columnIdentifier: '',
})

const columnMappings = reactive<MappingRow[]>([])
const decimalFields = ref<string[]>([])
const keyFields = ref<string[]>([])

// 暂存表相关变量（已废弃路由模式，仅保留单表）
const stagingMode = ref<'fixed'>('fixed')

// 合计行
const totalRowConfig = reactive({
  enabled: false,
  emptyFields: [] as string[],
  containsKeywords: [] as string[],
})

// 批次拆分
const batchSplit = reactive({
  enabled: false,
  splitField: '',
  targetTableTemplate: '',
})

const transformRules = reactive<TransformRuleItem[]>([])

const transformTypeOptions = [
  { value: 'trim', label: '去除首尾空格' },
  { value: 'left', label: '截取前N位' },
  { value: 'right', label: '截取后N位' },
  { value: 'replace', label: '查找替换' },
  { value: 'condition', label: '条件判断' },
  { value: 'concat', label: '合并多字段' },
  { value: 'abs', label: '取绝对值' },
  { value: 'round', label: '四舍五入' },
  { value: 'dateFormat', label: '日期格式化' },
  { value: 'lookup', label: '查表映射' },
  { value: 'default', label: '空值填充' },
  { value: 'regex', label: '正则提取' },
]

// 从 fullColumnIdentifier 解析完整列名列表
const allExcelColumns = computed(() => {
  if (!formData.fullColumnIdentifier) return []
  return formData.fullColumnIdentifier
    .split(',')
    .map(col => col.trim())
    .filter(Boolean)
})

// 从列映射中提取可选列
const excelColumnOptions = computed(() =>
  columnMappings.map(m => ({ value: m.excelColumn, label: m.excelColumn })).filter(o => o.value)
)

const dbColumnOptions = computed(() =>
  columnMappings.map(m => ({ value: m.dbColumn, label: m.dbColumn })).filter(o => o.value)
)

function addTransformRule() {
  transformRules.push({ targetColumn: '', sourceColumns: [], transforms: [], expression: '', mode: 'visual' })
}

function addTransformStep(rule: TransformRuleItem) {
  rule.transforms.push({ type: 'trim', params: {} })
}

function onStepTypeChange(step: TransformStep) {
  step.params = {}
}

function moveTransformRule(index: number, direction: number) {
  const target = index + direction
  if (target < 0 || target >= transformRules.length) return
  const temp = transformRules[index]
  transformRules[index] = transformRules[target]
  transformRules[target] = temp
}

function generateExpression(rule: TransformRuleItem): string {
  const mainCol = rule.sourceColumns[0] || ''
  let expr = `row['${mainCol}']`
  for (const step of (rule.transforms || [])) {
    switch (step.type) {
      case 'trim': expr = `(${expr} || '').trim()`; break
      case 'left': expr = `(${expr} || '').substring(0, ${step.params.length || 0})`; break
      case 'right': { const len = step.params.length || 0; expr = `(function(v){ return v.substring(v.length - ${len}); })(${expr} || '')`; break }
      case 'replace': expr = `(${expr} || '').replace('${step.params.search || ''}', '${step.params.replace || ''}')`; break
      case 'condition': { const f = step.params.field ?? mainCol; expr = `parseFloat(row['${f}']) ${step.params.op ?? '=='} ${step.params.value ?? ''} ? '${step.params.then ?? ''}' : '${step.params.else ?? ''}'`; break }
      case 'concat': { const sep = step.params.separator || ''; const fields = (step.params.fields || '').split(',').map((f: string) => f.trim()).filter(Boolean); const parts = fields.map((f: string) => `(row['${f}'] || '')`).join(` + '${sep}' + `); expr = parts || expr; break }
      case 'abs': expr = `Math.abs(parseFloat(${expr}) || 0)`; break
      case 'round': expr = `parseFloat(parseFloat(${expr} || 0).toFixed(${step.params.decimals || 0}))`; break
      case 'dateFormat': expr = `formatDate(${expr}, '${step.params.format || 'yyyy-MM-dd'}')`; break
      case 'lookup': expr = `lookup('${step.params.table || ''}', ${expr}, '${step.params.value || ''}')`; break
      case 'default': expr = `(${expr}) || '${step.params.value || ''}'`; break
      case 'regex': expr = `((${expr} || '').match(/${step.params.pattern ?? ''}/)||[])[${step.params.group ?? 0}]||''`; break
    }
  }
  return expr
}

// ==================== 暂存表相关 ====================
const stagingTables = ref<StagingTableInfo[]>([])
const stagingTablesLoading = ref(false)
const targetTableColumns = ref<StagingColumnInfo[]>([])

const targetTableColumnOptions = computed(() =>
  targetTableColumns.value.map(col => ({ label: col.columnName, value: col.columnName }))
)

function getColumnType(colName: string) {
  const col = targetTableColumns.value.find(c => c.columnName === colName)
  return col?.dataType ?? ''
}

async function fetchStagingTables() {
  stagingTablesLoading.value = true
  try {
    const res: any = await getStagingTables()
    stagingTables.value = res.data ?? res ?? []
  } catch { /* silent */ } finally {
    stagingTablesLoading.value = false
  }
}

async function loadTargetTableColumns(tableName: string) {
  if (!tableName) { targetTableColumns.value = []; return }
  try {
    const res: any = await getStagingTableColumns(tableName)
    targetTableColumns.value = res.data ?? res ?? []
  } catch { targetTableColumns.value = [] }
}

async function onTargetTableChange(val: any) {
  await loadTargetTableColumns(val)
  if (targetTableColumns.value.length > 0 && columnMappings.length > 0) {
    for (const mapping of columnMappings) {
      if (!mapping.excelColumn) continue
      const matched = targetTableColumns.value.find(
        col => col.columnName.replace(/^F/, '') === mapping.excelColumn
      )
      if (matched) mapping.dbColumn = matched.columnName
    }
  }
}

// Excel列可用选项（排除已选）
function getAvailableExcelColumns(currentIndex: number): string[] {
  const usedColumns = new Set(
    columnMappings
      .filter((_, i) => i !== currentIndex)
      .map(m => m.excelColumn)
      .filter(Boolean)
  )
  // 优先使用从fullColumnIdentifier解析的完整列名
  const source = allExcelColumns.value.length > 0
    ? allExcelColumns.value
    : columnMappings.filter(m => m.excelColumn).map(m => m.excelColumn)
  return [...new Set(source)].filter(col => !usedColumns.has(col))
}

// ==================== 列映射操作 ====================
function addMapping() {
  columnMappings.push({ _uid: genTempId(), excelColumn: '', dbColumn: '', aliasesStr: '' })
}

function autoMatchColumns() {
  if (allExcelColumns.value.length === 0) {
    message.warning('请先在基本信息中提取或输入Excel列名')
    return
  }
  if (targetTableColumns.value.length === 0) {
    message.warning('请先在数据分布配置中选择暂存表')
    return
  }

  // 收集已映射的Excel列名，避免重复
  const alreadyMapped = new Set(columnMappings.map(m => m.excelColumn).filter(Boolean))

  let matchedCount = 0
  let addedCount = 0

  for (const excelCol of allExcelColumns.value) {
    if (alreadyMapped.has(excelCol)) continue

    // 在暂存表字段中查找匹配
    const dbCol = findMatchingDbColumn(excelCol)

    // 添加映射行（无论是否匹配到DB字段）
    columnMappings.push({
      _uid: genTempId(),
      excelColumn: excelCol,
      dbColumn: dbCol || '',
      aliasesStr: '',
    })
    addedCount++
    if (dbCol) matchedCount++
  }

  if (addedCount === 0) {
    message.info('所有Excel列名已有映射，无需添加')
  } else {
    message.success(`已添加 ${addedCount} 个映射，其中 ${matchedCount} 个自动匹配到数据库字段`)
  }
}

function findMatchingDbColumn(excelCol: string): string | null {
  const cols = targetTableColumns.value
  // 需要排除的系统字段（不应被自动匹配）
  const systemFields = new Set(['FID', 'F批次ID', 'F原始行号', 'F业务主键', 'F流水号', 'F其他列数据', 'F处理状态', 'F错误信息', 'F关联凭证ID', 'F创建时间'])

  const candidates = cols.filter(c => !systemFields.has(c.columnName))

  // 策略1：精确匹配（去掉F前缀后完全相同）
  const exact = candidates.find(c => c.columnName.replace(/^F/, '') === excelCol)
  if (exact) return exact.columnName

  // 策略2：Excel列名包含在DB字段名中（去F前缀后）
  const dbContains = candidates.find(c => {
    const dbName = c.columnName.replace(/^F/, '')
    return dbName.length > 1 && excelCol.length > 1 && dbName.includes(excelCol)
  })
  if (dbContains) return dbContains.columnName

  // 策略3：DB字段名包含在Excel列名中（去F前缀后）
  const excelContains = candidates.find(c => {
    const dbName = c.columnName.replace(/^F/, '')
    return dbName.length > 1 && excelCol.length > 1 && excelCol.includes(dbName)
  })
  if (excelContains) return excelContains.columnName

  return null
}

function removeMapping(index: number) {
  columnMappings.splice(index, 1)
}

// ==================== 流水号规则 ====================
function insertSerialField(fieldName: string) {
  formData.serialNumberRule = (formData.serialNumberRule || '') + `{${fieldName}}`
}

function insertSerialToken(token: string) {
  formData.serialNumberRule = (formData.serialNumberRule || '') + token
}

const serialRulePreview = computed(() => {
  if (!formData.serialNumberRule) return '(请输入规则)'
  const sampleData: Record<string, string> = {
    '记账日期': '2026-04-18', '流水号': '00123', '金额': '99.50',
    '当年': '2026', '当月': '04', '当日': '18',
  }
  for (const m of columnMappings) {
    if (m.excelColumn && !sampleData[m.excelColumn]) sampleData[m.excelColumn] = `[${m.excelColumn}]`
  }
  return formData.serialNumberRule.replace(/\{([^}]+)\}/g, (_, key) => sampleData[key] ?? `[${key}]`)
})

// ==================== watch ====================
watch(() => formData.targetTable, (newTableName) => {
  if (newTableName) loadTargetTableColumns(newTableName)
  else targetTableColumns.value = []
})

// ==================== 初始化/重置 ====================
function resetAll() {
  formData.ruleName = ''
  formData.description = ''
  formData.status = 1
  formData.outputMode = 'stg'
  formData.targetTable = ''
  formData.headerRow = 1
  formData.dataStartRow = 2
  formData.serialNumberRule = ''
  formData.fullColumnIdentifier = ''
  formData.columnIdentifier = ''

  columnMappings.splice(0, columnMappings.length)
  decimalFields.value = []
  keyFields.value = []

  stagingMode.value = 'fixed'

  totalRowConfig.enabled = false
  totalRowConfig.emptyFields = []
  totalRowConfig.containsKeywords = []

  batchSplit.enabled = false
  batchSplit.splitField = ''
  batchSplit.targetTableTemplate = ''

  transformRules.splice(0, transformRules.length)
  activeStep.value = 0
}

function loadFromRule(rule: AutoPluginRuleDto) {
  formData.ruleName = rule.ruleName
  formData.description = rule.description ?? ''
  formData.status = rule.status

  let config: Record<string, any> = {}
  if (rule.configJson) {
    try { config = JSON.parse(rule.configJson) } catch { /* ignore */ }
  }

  formData.outputMode = (config.outputMode as any) ?? 'stg'
  formData.targetTable = config.targetTable ?? ''
  formData.headerRow = config.headerRow ?? 1
  formData.dataStartRow = config.dataStartRow ?? 2
  formData.serialNumberRule = config.serialNumberRule ?? ''
  formData.fullColumnIdentifier = config.fullColumnIdentifier ?? ''
  formData.columnIdentifier = config.columnIdentifier ?? ''

  // 列映射
  columnMappings.splice(0, columnMappings.length)
  if (Array.isArray(config.columnMapping)) {
    for (const m of config.columnMapping) {
      columnMappings.push({
        _uid: genTempId(),
        excelColumn: m.excelColumn ?? '',
        dbColumn: m.dbColumn ?? '',
        aliasesStr: Array.isArray(m.aliases) ? m.aliases.join(',') : '',
      })
    }
  }

  decimalFields.value = Array.isArray(config.decimalFields) ? [...config.decimalFields] : []
  keyFields.value = Array.isArray(config.keyFields) ? [...config.keyFields] : []

  // 路由配置已废弃，忽略 routeRules
  stagingMode.value = 'fixed'

  // 合计行检测
  if (config.totalRowDetection?.enabled) {
    totalRowConfig.enabled = true
    totalRowConfig.emptyFields = Array.isArray(config.totalRowDetection.emptyFields) ? [...config.totalRowDetection.emptyFields] : []
    totalRowConfig.containsKeywords = Array.isArray(config.totalRowDetection.containsKeywords) ? [...config.totalRowDetection.containsKeywords] : []
  } else {
    totalRowConfig.enabled = false
    totalRowConfig.emptyFields = []
    totalRowConfig.containsKeywords = []
  }

  // 转换规则
  if (config.transformRules && Array.isArray(config.transformRules)) {
    transformRules.splice(0, transformRules.length, ...config.transformRules.map((r: any) => ({
      targetColumn: r.targetColumn || '',
      sourceColumns: r.sourceColumns || [],
      mode: r.mode || 'visual',
      transforms: (r.transforms || []).map((t: any) => ({ type: t.type || '', params: t.params || {} })),
      expression: r.expression || '',
    })))
  } else {
    transformRules.splice(0, transformRules.length)
  }

  // 加载目标表列信息
  if (formData.targetTable) {
    loadTargetTableColumns(formData.targetTable)
  }

  activeStep.value = 0
}

watch(
  () => props.open,
  async (val) => {
    if (!val) return
    // 懒加载暂存表（首次打开时加载）
    if (stagingTables.value.length === 0) {
      fetchStagingTables()
    }
    if (props.rule) {
      let ruleToLoad = props.rule
      // 编辑时列表API不返回configJson，需补充加载详情
      if (!ruleToLoad.configJson) {
        try {
          ruleToLoad = await getAutoPluginRule(ruleToLoad.id)
        } catch (error) {
          console.error('加载规则详情失败', error)
          return
        }
      }
      loadFromRule(ruleToLoad)
    } else {
      resetAll()
    }
  },
)

// ==================== 提交 ====================
async function handleSubmit() {
  if (!formData.ruleName.trim()) {
    message.warning('请输入规则名称')
    return
  }
  if (formData.outputMode !== 'batchRow' && !formData.targetTable) {
    message.warning('请选择目标暂存表')
    return
  }

  // 检查列映射中的DB字段是否重复
  const dbColumns = columnMappings
    .map(m => m.dbColumn)
    .filter(Boolean)
  if (new Set(dbColumns).size !== dbColumns.length) {
    const duplicates = dbColumns.filter((col, idx) => dbColumns.indexOf(col) !== idx)
    const uniqueDuplicates = [...new Set(duplicates)]
    message.warning(`数据库字段映射重复：${uniqueDuplicates.join('、')}，每个暂存表字段只能映射一次`)
    return
  }

  submitting.value = true
  try {
    const config: Record<string, any> = {
      outputMode: formData.outputMode,
      targetTable: formData.targetTable || undefined,
      headerRow: formData.headerRow,
      dataStartRow: formData.dataStartRow,
      columnMapping: columnMappings.map(m => ({
        excelColumn: m.excelColumn,
        dbColumn: m.dbColumn,
        aliases: m.aliasesStr ? m.aliasesStr.split(',').map(s => s.trim()).filter(Boolean) : [],
      })),
      decimalFields: decimalFields.value,
      keyFields: keyFields.value,
      serialNumberRule: formData.serialNumberRule || undefined,
      fullColumnIdentifier: formData.fullColumnIdentifier || undefined,
      columnIdentifier: formData.columnIdentifier || undefined,
    }

    if (totalRowConfig.enabled) {
      config.totalRowDetection = {
        enabled: true,
        emptyFields: totalRowConfig.emptyFields,
        containsKeywords: totalRowConfig.containsKeywords,
      }
    }

    // 序列化转换规则
    if (transformRules.length > 0) {
      config.transformRules = transformRules.map(rule => {
        const r = { ...rule }
        if (r.mode === 'visual') {
          r.expression = generateExpression(rule)
        }
        return {
          targetColumn: r.targetColumn,
          sourceColumns: r.sourceColumns,
          transforms: r.transforms,
          expression: r.expression,
          mode: r.mode,
        }
      })
    }

    const configJson = JSON.stringify(config)

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
  }
}

// ==================== 初始化 ====================
// 暂存表数据已移至 watch(open) 中懒加载，避免组件未打开时就请求不存在的API

// ==================== 提取Excel列名 ====================
const extractColumnsFromFile = async (file: File) => {
  try {
    const fd = new FormData()
    fd.append('file', file)
    const res: any = await extractColumnsFromExcel(fd, formData.headerRow)
    const columns: string[] = Array.isArray(res) ? res : (Array.isArray(res?.data) ? res.data : [])
    if (columns.length > 0) {
      formData.fullColumnIdentifier = columns.join(',')
      message.success(`已提取 ${columns.length} 个列名`)
    } else {
      message.warning('未提取到列名，请检查表头行号是否正确')
    }
  } catch {
    message.error('Excel 文件解析失败')
  }
  return false // 阻止默认上传
}
</script>

<style lang="scss" scoped>
/* 步骤条美化 */
.excel-input-steps {
  padding-bottom: 16px;
  margin-bottom: 16px;
  border-bottom: 1px solid var(--border);
  cursor: pointer;
}

/* 步骤内容区域 */
.step-content {
  min-height: 200px;
  padding: 12px 4px;
}

/* 底部导航 */
.step-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.step-footer div {
  display: flex;
  gap: 8px;
}

/* 辅助提示文字 */
.hint-text {
  font-size: 12px;
  color: var(--text-3);
  margin-top: 4px;
}

/* 转换规则卡片 */
.transform-rule-card {
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 14px;
  margin-bottom: 12px;
  background: var(--bg-muted);
  transition: box-shadow 0.2s;

  &:hover {
    box-shadow: var(--shadow-sm);
  }
}

.rule-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.rule-index {
  font-weight: 600;
  color: var(--color-info);
  font-size: 14px;
}

.rule-header-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}
</style>
