<template>
  <a-modal
    :open="open"
    :title="rule ? '编辑规则' : '新增规则'"
    :destroyOnClose="true"
    centered
    width="680px"
    :bodyStyle="{ height: '70vh', overflowY: 'auto', padding: '16px 20px' }"
    @ok="handleSubmit"
    @cancel="$emit('update:open', false)"
    :confirmLoading="submitting"
  >
    <a-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      :labelCol="{ style: { width: '110px' } }"
      style="margin-top: 16px;"
    >
      <!-- 基础信息区 -->
      <a-divider orientation="left" style="margin-top: 0;">基础信息</a-divider>

      <a-form-item label="规则名称" name="ruleName">
        <a-input v-model:value="formData.ruleName" placeholder="请输入规则名称" />
      </a-form-item>

      <a-form-item label="说明" name="description">
        <a-textarea v-model:value="formData.description" :rows="2" placeholder="请输入规则说明" />
      </a-form-item>

      <a-form-item v-if="rule" label="状态" name="status">
        <a-switch
          :checked="formData.status === 1"
          @change="(val: boolean | string | number) => formData.status = val ? 1 : 0"
          checkedChildren="启用"
          unCheckedChildren="禁用"
        />
      </a-form-item>

      <!-- 配置参数区（Schema 驱动） -->
      <template v-if="configSchema.length">
        <template v-for="(group, gIdx) in groupedFields" :key="gIdx">
          <a-divider orientation="left">{{ group.name }}</a-divider>

          <a-form-item
            v-for="field in group.fields"
            :key="field.key"
            :label="field.label"
            :required="field.required"
          >
            <!-- string -->
            <a-input
              v-if="field.fieldType === 'string'"
              v-model:value="configForm[field.key]"
              :placeholder="field.placeholder || `请输入${field.label}`"
            />
            <!-- number / decimal -->
            <a-input-number
              v-else-if="field.fieldType === 'number' || field.fieldType === 'decimal'"
              v-model:value="configForm[field.key]"
              :placeholder="field.placeholder || `请输入${field.label}`"
              style="width: 100%;"
              :precision="field.fieldType === 'decimal' ? 2 : 0"
            />
            <!-- boolean -->
            <a-switch
              v-else-if="field.fieldType === 'boolean'"
              :checked="!!configForm[field.key]"
              @change="(val: boolean | string | number) => configForm[field.key] = Boolean(val)"
            />
            <!-- select -->
            <a-select
              v-else-if="field.fieldType === 'select'"
              v-model:value="configForm[field.key]"
              :placeholder="field.placeholder || `请选择${field.label}`"
              allowClear
            >
              <a-select-option
                v-for="opt in (field.options ?? [])"
                :key="opt.value"
                :value="opt.value"
              >{{ opt.label }}</a-select-option>
            </a-select>
            <!-- custom: tableSelect -->
            <a-select
              v-else-if="field.fieldType === 'custom' && field.component === 'tableSelect'"
              v-model:value="configForm[field.key]"
              :placeholder="field.placeholder || `请选择${field.label}`"
              showSearch
              allowClear
              :filterOption="filterTableOption"
              :loading="isResultTableField(field) ? resultTablesLoading : stagingTablesLoading"
            >
              <a-select-option
                v-for="t in getFilteredTables(field)"
                :key="t.tableName"
                :value="t.tableName"
              >{{ t.tableName }}</a-select-option>
            </a-select>
            <!-- custom: brandSelect -->
            <a-select
              v-else-if="field.fieldType === 'custom' && field.component === 'brandSelect'"
              v-model:value="configForm[field.key]"
              :placeholder="field.placeholder || '请选择品牌'"
              showSearch
              allowClear
              :filterOption="filterTableOption"
              :loading="brandOptionsLoading"
              :options="brandOptions"
            />
            <!-- custom: columnMapping -->
            <div
              v-else-if="field.fieldType === 'custom' && field.component === 'columnMapping'"
              class="column-mapping-editor"
            >
              <div v-if="!configForm.sourceTable" style="color: var(--text-3);">
                请先选择数据源暂存表
              </div>
              <div v-else>
                <a-button size="small" type="primary" ghost @click="autoMatchColumns(field)" style="margin-bottom: 8px;">
                  自动匹配
                </a-button>
                <a-spin :spinning="stgColumnsLoading">
                  <div
                    v-for="sf in getStandardFields(field)"
                    :key="sf.key"
                    style="display: flex; align-items: center; margin-bottom: 8px;"
                  >
                    <span style="width: 140px; text-align: right; margin-right: 8px; flex-shrink: 0;">
                      <span v-if="sf.required" style="color: var(--color-danger);">* </span>
                      {{ sf.label }}：
                    </span>
                    <a-select
                      v-model:value="columnMappingData[sf.key]"
                      showSearch
                      allowClear
                      placeholder="选择 STG 表列"
                      :filterOption="filterTableOption"
                      :options="stgColumnOptions"
                      style="flex: 1;"
                      @change="syncColumnMapping"
                    />
                  </div>
                </a-spin>
              </div>
            </div>
            <!-- json -->
            <a-textarea
              v-else-if="field.fieldType === 'json'"
              v-model:value="configForm[field.key]"
              :placeholder="field.placeholder || '请输入 JSON'"
              :autoSize="{ minRows: 3, maxRows: 8 }"
              style="font-family: monospace;"
            />
            <!-- fallback -->
            <a-input
              v-else
              v-model:value="configForm[field.key]"
              :placeholder="field.placeholder || `请输入${field.label}`"
            />

            <template #extra v-if="field.description">
              <span style="color: var(--text-3); font-size: 12px;">{{ field.description }}</span>
            </template>
          </a-form-item>
        </template>
      </template>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { createAutoPluginRule, updateAutoPluginRule, getAutoPluginRule, getStagingTables, getStagingTableColumns } from '@/api/cardflow'
import type { AutoPluginRuleDto, ConfigFieldSchema, StagingTableInfo, StagingColumnInfo } from '@/api/cardflow'
import { getExpBrandOptions } from '@/api/express'

const props = defineProps<{
  open: boolean
  rule: AutoPluginRuleDto | null
  typeCode: string
  configSchema: ConfigFieldSchema[]
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved'): void
}>()

const formRef = ref<FormInstance>()
const submitting = ref(false)

const formData = reactive({
  ruleName: '',
  description: '',
  status: 1,
})

const configForm = reactive<Record<string, any>>({})

// ==================== 暂存表下拉数据 ====================
const stagingTableOptions = ref<StagingTableInfo[]>([])
const stagingTablesLoading = ref(false)

async function loadStagingTables() {
  stagingTablesLoading.value = true
  try {
    const res = await getStagingTables()
    stagingTableOptions.value = (res as any) ?? []
  } catch {
    console.warn('获取暂存表列表失败')
  } finally {
    stagingTablesLoading.value = false
  }
}

// ==================== 结果表下拉数据（EXP前缀） ====================
const resultTableOptions = ref<StagingTableInfo[]>([])
const resultTablesLoading = ref(false)

async function loadResultTables() {
  resultTablesLoading.value = true
  try {
    const res = await getStagingTables('EXP')
    const allTables = ((res as any) ?? []) as StagingTableInfo[]
    // 过滤：名称包含"计费结果"，且不包含"_明细"和"_历史"
    resultTableOptions.value = allTables.filter(t =>
      t.tableName.includes('计费结果') &&
      !t.tableName.includes('_明细') &&
      !t.tableName.includes('_历史')
    )
  } catch {
    console.warn('获取结果表列表失败')
  } finally {
    resultTablesLoading.value = false
  }
}

/** 判断 field 是否为结果表字段（EXP前缀） */
function isResultTableField(field: ConfigFieldSchema): boolean {
  if (!field.extra) return false
  try {
    const parsed = JSON.parse(field.extra)
    const prefix = parsed.tablePrefix as string | undefined
    return !!prefix && prefix.startsWith('EXP')
  } catch { return false }
}

/** 根据 field.extra 中的 tablePrefix 过滤表列表 */
function getFilteredTables(field: ConfigFieldSchema) {
  if (!field.extra) return stagingTableOptions.value
  try {
    const parsed = JSON.parse(field.extra)
    const prefix = parsed.tablePrefix as string | undefined
    if (prefix) {
      // EXP 前缀使用独立的结果表数据源
      if (prefix.startsWith('EXP')) {
        return resultTableOptions.value.filter(t => t.tableName.startsWith(prefix))
      }
      return stagingTableOptions.value.filter(t => t.tableName.startsWith(prefix))
    }
  } catch { /* ignore */ }
  return stagingTableOptions.value
}

/** 下拉搜索过滤（通用） */
function filterTableOption(input: string, option: any) {
  const label = (option?.children?.[0]?.children || option?.label || '').toString().toLowerCase()
  const value = (option?.value || '').toString().toLowerCase()
  const keyword = input.toLowerCase()
  return value.includes(keyword) || label.includes(keyword)
}

// ==================== 品牌下拉数据 ====================
const brandOptions = ref<{ label: string; value: string }[]>([])
const brandOptionsLoading = ref(false)

async function loadBrandOptions() {
  brandOptionsLoading.value = true
  try {
    const res = await getExpBrandOptions()
    const items = (res as any)?.items ?? (res as any) ?? []
    brandOptions.value = items.map((b: any) => ({ label: b.name, value: b.code }))
  } catch {
    console.warn('获取品牌列表失败')
  } finally {
    brandOptionsLoading.value = false
  }
}

// ==================== 列映射数据 ====================
const columnMappingData = reactive<Record<string, string | undefined>>({})
const stgColumnOptions = ref<{ label: string; value: string }[]>([])
const stgColumnsLoading = ref(false)

/** 从 field.extra 解析标准字段列表 */
function getStandardFields(field: ConfigFieldSchema): { key: string; label: string; required: boolean }[] {
  if (!field.extra) return []
  try {
    const parsed = JSON.parse(field.extra)
    return parsed.standardFields ?? parsed ?? []
  } catch {
    return []
  }
}

/** 加载指定暂存表的列列表 */
async function loadStgColumns(tableName: string) {
  stgColumnsLoading.value = true
  try {
    const cols = await getStagingTableColumns(tableName) as StagingColumnInfo[]
    stgColumnOptions.value = (cols ?? []).map(c => ({ label: c.columnName, value: c.columnName }))
  } catch {
    console.warn('获取暂存表列列表失败')
    stgColumnOptions.value = []
  } finally {
    stgColumnsLoading.value = false
  }
}

/** 同步 columnMappingData 到 configForm（嵌套对象，不要二次序列化） */
function syncColumnMapping() {
  configForm.columnMapping = { ...columnMappingData }
}

// ==================== 自动匹配列映射 ====================
function autoMatchColumns(field: ConfigFieldSchema) {
  if (!stgColumnOptions.value.length) {
    message.warning('请先选择数据源暂存表')
    return
  }

  const systemFields = new Set(['FID', 'F批次ID', 'F原始行号', 'F业务主键',
    'F流水号', 'F其他列数据', 'F处理状态', 'F错误信息',
    'F关联凭证ID', 'F创建时间'])
  const candidates = stgColumnOptions.value.filter(o => !systemFields.has(o.value))

  const standardFields = getStandardFields(field)
  let matchCount = 0

  for (const sf of standardFields) {
    const matched = findBestMatch(sf.label, candidates)
    if (matched && !columnMappingData[sf.key]) {
      columnMappingData[sf.key] = matched
      matchCount++
    }
  }

  syncColumnMapping()
  message.success(`自动匹配完成，成功匹配 ${matchCount} 个字段`)
}

function findBestMatch(label: string, candidates: { label: string; value: string }[]): string | null {
  // 阶段1：精确匹配（去F前缀后完全等于标准字段label）
  const exact = candidates.find(c => c.value.replace(/^F/, '') === label)
  if (exact) return exact.value

  // 阶段2：暂存表列名包含标准字段名
  const contains = candidates.find(c => c.value.replace(/^F/, '').includes(label))
  if (contains) return contains.value

  // 阶段3：标准字段名包含暂存表列名（去F前缀、需长度>=2）
  const reverse = candidates.find(c => {
    const col = c.value.replace(/^F/, '')
    return col.length >= 2 && label.includes(col)
  })
  if (reverse) return reverse.value

  return null
}

const formRules = {
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' as const }],
}

// 按 group 分组
const groupedFields = computed(() => {
  const map = new Map<string, ConfigFieldSchema[]>()
  for (const field of props.configSchema) {
    const g = field.group || '基本配置'
    if (!map.has(g)) map.set(g, [])
    map.get(g)!.push(field)
  }
  return Array.from(map.entries()).map(([name, fields]) => ({ name, fields }))
})

// 初始化表单
watch(
  () => props.open,
  async (val) => {
    if (!val) return

    // 编辑模式：列表API不返回configJson，需补充加载详情
    let rule = props.rule
    if (rule && !rule.configJson) {
      try {
        rule = await getAutoPluginRule(rule.id)
      } catch (error) {
        console.error('加载规则详情失败', error)
        return
      }
    }

    // 基础信息
    formData.ruleName = rule?.ruleName ?? ''
    formData.description = rule?.description ?? ''
    formData.status = rule?.status ?? 1

    // 配置表单：先填默认值，再用已有 configJson 覆盖
    const defaults: Record<string, any> = {}
    for (const field of props.configSchema) {
      defaults[field.key] = field.defaultValue ?? (field.fieldType === 'boolean' ? false : undefined)
    }

    let existing: Record<string, any> = {}
    if (rule?.configJson) {
      try {
        existing = JSON.parse(rule.configJson)
      } catch {
        // ignore
      }
    }

    // 清空再赋值
    Object.keys(configForm).forEach(k => delete configForm[k])
    Object.assign(configForm, { ...defaults, ...existing })

    // 初始化列映射数据
    Object.keys(columnMappingData).forEach(k => delete columnMappingData[k])
    if (existing.columnMapping) {
      try {
        const mapping = typeof existing.columnMapping === 'string'
          ? JSON.parse(existing.columnMapping)
          : existing.columnMapping
        Object.assign(columnMappingData, mapping)
      } catch { /* ignore */ }
    }

    // 如果已有 sourceTable，加载其列
    if (configForm.sourceTable) {
      loadStgColumns(configForm.sourceTable)
    }
  },
)

async function handleSubmit() {
  if (!formRef.value) return
  await formRef.value.validate()

  // 校验 required 配置字段
  for (const field of props.configSchema) {
    if (field.required) {
      const val = configForm[field.key]
      if (val === undefined || val === null || val === '') {
        message.warning(`请填写 "${field.label}"`)
        return
      }
    }
    // 校验列映射中必填的标准字段
    if (field.fieldType === 'custom' && field.component === 'columnMapping') {
      const standardFields = getStandardFields(field)
      for (const sf of standardFields) {
        if (sf.required && !columnMappingData[sf.key]) {
          message.warning(`请映射必填字段 "${sf.label}"`)
          return
        }
      }
      // 保存前同步
      syncColumnMapping()
    }
  }

  submitting.value = true
  try {
    // 构建 configJson
    const configObj: Record<string, any> = props.configSchema.length ? { ...configForm } : {}
    delete configObj.orgId // 清除历史遗留的 orgId（已由 FOrgId 字段隔离）
    const configJson = Object.keys(configObj).length > 0 ? JSON.stringify(configObj) : undefined

    if (props.rule) {
      await updateAutoPluginRule(props.rule.id, {
        typeCode: props.typeCode,
        ruleName: formData.ruleName,
        description: formData.description,
        status: formData.status,
        configJson,
      })
      message.success('更新成功')
    } else {
      await createAutoPluginRule({
        typeCode: props.typeCode,
        ruleName: formData.ruleName,
        description: formData.description,
        configJson,
      })
      message.success('创建成功')
    }
    emit('saved')
  } catch {
    message.error(props.rule ? '更新失败' : '创建失败')
  } finally {
    submitting.value = false
  }
}

// ==================== 数据懒加载 ====================
const hasTableSelect = computed(() =>
  props.configSchema.some(f => f.fieldType === 'custom' && f.component === 'tableSelect')
)
const hasResultTableSelect = computed(() =>
  props.configSchema.some(f => f.fieldType === 'custom' && f.component === 'tableSelect' && isResultTableField(f))
)
const hasBrandSelect = computed(() =>
  props.configSchema.some(f => f.fieldType === 'custom' && f.component === 'brandSelect')
)

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen && hasTableSelect.value && stagingTableOptions.value.length === 0) {
      loadStagingTables()
    }
    if (isOpen && hasResultTableSelect.value && resultTableOptions.value.length === 0) {
      loadResultTables()
    }
    if (isOpen && hasBrandSelect.value && brandOptions.value.length === 0) {
      loadBrandOptions()
    }

  },
)

// Watch sourceTable 变化 → 重新加载列
watch(
  () => configForm.sourceTable,
  (newTable) => {
    if (newTable) {
      loadStgColumns(newTable)
    } else {
      stgColumnOptions.value = []
    }
  },
)
</script>
