<template>
  <a-modal
    :open="visible"
    title="外部凭证迁移向导"
    :width="960"
    :centered="true"
    :maskClosable="false"
    :destroyOnClose="true"
    :footer="null"
    @cancel="handleClose"
  >
    <!-- 步骤条 -->
    <a-steps :current="currentStep" size="small" style="margin-bottom: 24px;">
      <a-step v-for="(step, idx) in visibleSteps" :key="idx" :title="step.title" />
    </a-steps>

    <!-- Step 内容区 -->
    <div class="wizard-content">
      <!-- Step 1: 文件上传 -->
      <div v-if="currentStepKey === 'upload'" class="step-panel">
        <a-alert message="上传需要迁移的外部凭证 Excel 文件，系统将自动解析列名。" type="info" show-icon style="margin-bottom: 16px;" />
        <a-upload-dragger
          :fileList="fileList"
          :beforeUpload="handleBeforeUpload"
          :maxCount="1"
          accept=".xlsx,.xls"
          @remove="handleFileRemove"
        >
          <p class="ant-upload-drag-icon"><InboxOutlined /></p>
          <p class="ant-upload-text">点击或拖拽 Excel 文件到此区域</p>
          <p class="ant-upload-hint">支持 .xlsx / .xls 格式</p>
        </a-upload-dragger>

        <a-divider orientation="left" plain>可选：源系统科目表</a-divider>
        <a-upload
          :fileList="subjectFileList"
          :beforeUpload="handleSubjectFileUpload"
          :maxCount="1"
          accept=".xlsx,.xls"
          @remove="handleSubjectFileRemove"
        >
          <a-button size="small"><UploadOutlined />上传科目表（可选）</a-button>
        </a-upload>
      </div>

      <!-- Step 2: 列角色分配 -->
      <div v-if="currentStepKey === 'columns'" class="step-panel">
        <a-alert message="为每一列分配角色，告诉系统每列代表什么数据。" type="info" show-icon style="margin-bottom: 16px;" />
        <a-table
          :columns="columnRoleTableCols"
          :dataSource="parsedColumns"
          :pagination="false"
          rowKey="index"
          bordered
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'role'">
              <a-select
                v-model:value="record.role"
                style="width: 180px;"
                placeholder="请选择角色"
                allowClear
              >
                <a-select-option v-for="r in columnRoles" :key="r.value" :value="r.value" :disabled="isRoleUsed(r.value, record.index)">
                  {{ r.label }}
                </a-select-option>
              </a-select>
            </template>
            <template v-if="column.dataIndex === 'sample'">
              <span class="sample-text">{{ record.sampleValues?.slice(0, 3).join(' / ') || '-' }}</span>
            </template>
            <template v-if="column.dataIndex === 'extra'">
              <a-input
                v-if="record.role === 'auxiliary'"
                v-model:value="record.auxSeparator"
                placeholder="分隔符(如 |)"
                style="width: 100px;"
              />
            </template>
          </template>
        </a-table>
      </div>

      <!-- Step 3: 科目自动匹配 -->
      <div v-if="currentStepKey === 'accountMatch'" class="step-panel">
        <a-alert message="系统已自动匹配科目。请检查并修正未匹配的项目。" type="info" show-icon style="margin-bottom: 16px;" />
        <a-spin :spinning="matchLoading">
          <div class="match-summary" style="margin-bottom: 12px;">
            <a-tag color="success">已匹配 {{ matchedAccounts.length }} 项</a-tag>
            <a-tag color="error">未匹配 {{ unmatchedAccounts.length }} 项</a-tag>
          </div>

          <a-collapse v-model:activeKey="matchCollapseKeys" :bordered="false">
            <a-collapse-panel key="matched" :header="`已匹配（${matchedAccounts.length}）`">
              <a-table
                :columns="matchResultCols"
                :dataSource="matchedAccounts"
                :pagination="false"
                rowKey="sourceCode"
                bordered
                size="small"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'targetCode'">
                    <a-input v-model:value="record.targetCode" size="small" style="width: 120px;" />
                  </template>
                  <template v-if="column.dataIndex === 'targetName'">
                    <a-input v-model:value="record.targetName" size="small" />
                  </template>
                </template>
              </a-table>
            </a-collapse-panel>
            <a-collapse-panel key="unmatched" :header="`未匹配（${unmatchedAccounts.length}）`">
              <a-table
                :columns="matchResultCols"
                :dataSource="unmatchedAccounts"
                :pagination="false"
                rowKey="sourceCode"
                bordered
                size="small"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'targetCode'">
                    <a-input v-model:value="record.targetCode" size="small" style="width: 120px;" placeholder="目标编码" />
                  </template>
                  <template v-if="column.dataIndex === 'targetName'">
                    <a-input v-model:value="record.targetName" size="small" placeholder="目标名称" />
                  </template>
                </template>
              </a-table>
            </a-collapse-panel>
          </a-collapse>
        </a-spin>
      </div>

      <!-- Step 4: 辅助核算配置 (条件步骤) -->
      <div v-if="currentStepKey === 'auxiliary'" class="step-panel">
        <a-alert message="配置辅助核算列的格式和映射关系。" type="info" show-icon style="margin-bottom: 16px;" />
        <a-form :labelCol="{ style: { width: '120px' } }">
          <a-form-item label="辅助列格式">
            <a-radio-group v-model:value="auxConfig.format">
              <a-radio value="single">单列（含分隔符）</a-radio>
              <a-radio value="multi">多列独立</a-radio>
            </a-radio-group>
          </a-form-item>
          <a-form-item v-if="auxConfig.format === 'single'" label="分隔符">
            <a-input v-model:value="auxConfig.separator" placeholder="如 | 或 /" style="width: 120px;" />
          </a-form-item>
        </a-form>

        <a-divider plain>辅助编码预览与映射</a-divider>
        <a-table
          :columns="auxMappingCols"
          :dataSource="auxPreviewData"
          :pagination="false"
          rowKey="sourceCode"
          bordered
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'targetCode'">
              <a-input v-model:value="record.targetCode" size="small" placeholder="目标编码" />
            </template>
            <template v-if="column.dataIndex === 'targetName'">
              <a-input v-model:value="record.targetName" size="small" placeholder="目标名称" />
            </template>
            <template v-if="column.dataIndex === 'action'">
              <a-select v-model:value="record.strategy" size="small" style="width: 100px;">
                <a-select-option value="map">映射</a-select-option>
                <a-select-option value="create">创建</a-select-option>
                <a-select-option value="ignore">忽略</a-select-option>
              </a-select>
            </template>
          </template>
        </a-table>
      </div>

      <!-- Step 5: 资产关联配置 (条件步骤) -->
      <div v-if="currentStepKey === 'asset'" class="step-panel">
        <a-alert message="配置源资产编号与目标资产卡片的关联关系。" type="info" show-icon style="margin-bottom: 16px;" />
        <a-table
          :columns="assetMappingCols"
          :dataSource="assetPreviewData"
          :pagination="false"
          rowKey="sourceCode"
          bordered
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="record.targetCode ? 'success' : 'warning'">
                {{ record.targetCode ? '已匹配' : '未匹配' }}
              </a-tag>
            </template>
            <template v-if="column.dataIndex === 'targetCode'">
              <a-input v-model:value="record.targetCode" size="small" placeholder="目标资产编号" />
            </template>
            <template v-if="column.dataIndex === 'targetName'">
              <a-input v-model:value="record.targetName" size="small" placeholder="资产名称" />
            </template>
            <template v-if="column.dataIndex === 'action'">
              <a-checkbox v-model:checked="record.ignored">忽略</a-checkbox>
            </template>
          </template>
        </a-table>
      </div>

      <!-- Step 6: 预览确认 -->
      <div v-if="currentStepKey === 'preview'" class="step-panel">
        <a-alert message="以下是前几行数据的转换预览，请确认无误后提交。" type="info" show-icon style="margin-bottom: 16px;" />
        <a-spin :spinning="previewLoading">
          <a-table
            :columns="previewCols"
            :dataSource="previewData"
            :pagination="false"
            rowKey="_idx"
            bordered
            size="small"
            :scroll="{ x: 900 }"
          />
        </a-spin>
      </div>

      <!-- Step 7: 提交生成 -->
      <div v-if="currentStepKey === 'commit'" class="step-panel">
        <div v-if="commitStatus === 'idle'" style="text-align: center; padding: 40px 0;">
          <ThunderboltOutlined style="font-size: 48px; color: var(--color-info);" />
          <p style="margin-top: 16px; font-size: 16px;">所有配置已就绪，点击「提交」开始生成迁移数据。</p>
        </div>
        <div v-if="commitStatus === 'loading'" style="text-align: center; padding: 40px 0;">
          <a-spin size="large" />
          <p style="margin-top: 16px;">正在生成迁移凭证...</p>
        </div>
        <div v-if="commitStatus === 'success'" style="text-align: center; padding: 40px 0;">
          <CheckCircleOutlined style="font-size: 48px; color: var(--color-success);" />
          <p style="margin-top: 16px; font-size: 16px;">迁移完成！</p>
          <p v-if="commitResult">共生成 {{ commitResult.voucherCount }} 张凭证，{{ commitResult.entryCount }} 条分录</p>
          <a-space style="margin-top: 24px;">
            <a-button type="primary" @click="handleClose">关闭</a-button>
          </a-space>
        </div>
        <div v-if="commitStatus === 'error'" style="text-align: center; padding: 40px 0;">
          <CloseCircleOutlined style="font-size: 48px; color: var(--color-danger);" />
          <p style="margin-top: 16px; color: var(--color-danger);">{{ commitError || '提交失败，请重试' }}</p>
          <a-button style="margin-top: 16px;" @click="commitStatus = 'idle'">重试</a-button>
        </div>
      </div>
    </div>

    <!-- 底部操作按钮 -->
    <div class="wizard-footer">
      <a-button v-if="currentStep > 0 && commitStatus !== 'success'" @click="handlePrev">
        上一步
      </a-button>
      <div style="flex: 1;" />
      <a-button
        v-if="currentStepKey !== 'commit'"
        type="primary"
        :loading="stepLoading"
        @click="handleNext"
      >
        下一步
      </a-button>
      <a-button
        v-if="currentStepKey === 'commit' && commitStatus === 'idle'"
        type="primary"
        :loading="stepLoading"
        @click="handleCommit"
      >
        提交生成
      </a-button>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  InboxOutlined,
  UploadOutlined,
  ThunderboltOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from '@ant-design/icons-vue'
import {
  wizardParseColumns,
  wizardExtractSubjects,
  wizardAutoMatch,
  wizardPreview,
  wizardCommit,
} from '@/api/finance'

const props = defineProps<{
  visible: boolean
  schemeId?: string
  accountSetId?: number
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'success'): void
}>()

// ========== 步骤管理 ==========
interface StepDef { key: string; title: string; condition?: () => boolean }

const allSteps: StepDef[] = [
  { key: 'upload', title: '文件上传' },
  { key: 'columns', title: '列角色分配' },
  { key: 'accountMatch', title: '科目匹配' },
  { key: 'auxiliary', title: '辅助核算配置', condition: () => hasAuxColumn.value },
  { key: 'asset', title: '资产关联配置', condition: () => hasAssetColumn.value },
  { key: 'preview', title: '预览确认' },
  { key: 'commit', title: '提交生成' },
]

const visibleSteps = computed(() => allSteps.filter(s => !s.condition || s.condition()))
const currentStep = ref(0)
const currentStepKey = computed(() => visibleSteps.value[currentStep.value]?.key || 'upload')
const stepLoading = ref(false)

// ========== Step 1: 文件上传 ==========
const fileList = ref<any[]>([])
const subjectFileList = ref<any[]>([])
const uploadedFile = ref<File | null>(null)
const subjectFile = ref<File | null>(null)
const uploadedFileId = ref<string>('')  // parse-columns 返回，后续步骤复用

function handleBeforeUpload(file: File) {
  uploadedFile.value = file
  uploadedFileId.value = ''  // 更换文件时清掉旧 fileId
  fileList.value = [file]
  return false
}

function handleFileRemove() {
  uploadedFile.value = null
  uploadedFileId.value = ''
  fileList.value = []
}

function handleSubjectFileUpload(file: File) {
  subjectFile.value = file
  subjectFileList.value = [file]
  return false
}

function handleSubjectFileRemove() {
  subjectFile.value = null
  subjectFileList.value = []
}

// ========== Step 2: 列角色分配 ==========
interface ParsedColumn {
  index: number
  name: string
  role: string
  sampleValues: string[]
  auxSeparator: string
}

const parsedColumns = ref<ParsedColumn[]>([])

const columnRoles = [
  { value: 'voucherDate', label: '凭证日期' },
  { value: 'voucherNo', label: '凭证号' },
  { value: 'voucherWord', label: '凭证字' },
  { value: 'summary', label: '摘要' },
  { value: 'accountCode', label: '科目编码' },
  { value: 'accountName', label: '科目名称' },
  { value: 'debitAmount', label: '借方金额' },
  { value: 'creditAmount', label: '贷方金额' },
  { value: 'auxiliary', label: '辅助核算' },
  { value: 'assetCode', label: '资产编号' },
  { value: 'ignore', label: '忽略' },
]

const columnRoleTableCols = [
  { title: '列名', dataIndex: 'name', width: 150 },
  { title: '示例数据', dataIndex: 'sample', minWidth: 200 },
  { title: '分配角色', dataIndex: 'role', width: 200 },
  { title: '附加配置', dataIndex: 'extra', width: 130 },
]

function isRoleUsed(role: string, currentIdx: number): boolean {
  if (role === 'ignore' || role === 'auxiliary') return false
  return parsedColumns.value.some(c => c.role === role && c.index !== currentIdx)
}

const hasAuxColumn = computed(() => parsedColumns.value.some(c => c.role === 'auxiliary'))
const hasAssetColumn = computed(() => parsedColumns.value.some(c => c.role === 'assetCode'))

// ========== Step 3: 科目匹配 ==========
interface MatchItem {
  sourceCode: string
  sourceName: string
  targetCode: string
  targetName: string
  confidence: string  // exact_code / exact_name / fuzzy_name / none
}

const matchedAccounts = ref<MatchItem[]>([])
const unmatchedAccounts = ref<MatchItem[]>([])
const matchLoading = ref(false)
const matchCollapseKeys = ref(['unmatched'])

const matchResultCols = [
  { title: '源科目编码', dataIndex: 'sourceCode', width: 130 },
  { title: '源科目名称', dataIndex: 'sourceName', width: 160 },
  { title: '→', dataIndex: 'arrow', width: 40, align: 'center' as const, customRender: () => '→' },
  { title: '目标编码', dataIndex: 'targetCode', width: 140 },
  { title: '目标名称', dataIndex: 'targetName', minWidth: 160 },
]

// ========== Step 4: 辅助核算配置 ==========
const auxConfig = reactive({ format: 'single', separator: '|' })
const auxPreviewData = ref<any[]>([])

const auxMappingCols = [
  { title: '辅助类型', dataIndex: 'auxType', width: 100 },
  { title: '源编码', dataIndex: 'sourceCode', width: 120 },
  { title: '源名称', dataIndex: 'sourceName', width: 140 },
  { title: '目标编码', dataIndex: 'targetCode', width: 140 },
  { title: '目标名称', dataIndex: 'targetName', width: 140 },
  { title: '策略', dataIndex: 'action', width: 120 },
]

// ========== Step 5: 资产关联配置 ==========
const assetPreviewData = ref<any[]>([])

const assetMappingCols = [
  { title: '源资产编号', dataIndex: 'sourceCode', width: 150 },
  { title: '状态', dataIndex: 'status', width: 100 },
  { title: '目标资产编号', dataIndex: 'targetCode', width: 150 },
  { title: '目标资产名称', dataIndex: 'targetName', minWidth: 180 },
  { title: '操作', dataIndex: 'action', width: 80, align: 'center' as const },
]

// ========== Step 6: 预览 ==========
const previewLoading = ref(false)
const previewData = ref<any[]>([])
const previewCols = ref<any[]>([])

// ========== Step 7: 提交 ==========
const commitStatus = ref<'idle' | 'loading' | 'success' | 'error'>('idle')
const commitResult = ref<any>(null)
const commitError = ref('')

// ========== 步骤导航 ==========
function handleClose() {
  emit('close')
}

async function handleNext() {
  const key = currentStepKey.value
  stepLoading.value = true
  try {
    if (key === 'upload') {
      await processUpload()
    } else if (key === 'columns') {
      await processColumns()
    } else if (key === 'accountMatch') {
      // 科目匹配步骤，直接过
    } else if (key === 'auxiliary') {
      // 辅助配置，直接过
    } else if (key === 'asset') {
      // 资产配置，直接过
    } else if (key === 'preview') {
      // 预览步骤切到提交
    }
    currentStep.value++
    // 如果进入预览步骤，加载预览数据
    if (visibleSteps.value[currentStep.value]?.key === 'preview') {
      await loadPreview()
    }
  } catch (err: any) {
    message.error(err?.message || '处理失败，请检查数据')
  } finally {
    stepLoading.value = false
  }
}

function handlePrev() {
  if (currentStep.value > 0) currentStep.value--
}

async function processUpload() {
  if (!uploadedFile.value) {
    throw new Error('请先上传凭证文件')
  }
  const res = await wizardParseColumns(uploadedFile.value) as any
  if (res?.columns?.length) {
    uploadedFileId.value = res.fileId || ''
    parsedColumns.value = res.columns.map((col: any, idx: number) => ({
      index: idx,
      name: col.name || col,
      role: col.suggestedRole || '',
      sampleValues: col.sampleValues || [],
      auxSeparator: '',
    }))
  } else {
    throw new Error('未能解析出列信息')
  }
}

async function processColumns() {
  // 验证必要角色
  const roles = parsedColumns.value.map(c => c.role)
  if (!roles.includes('accountCode') && !roles.includes('accountName')) {
    throw new Error('至少需要指定科目编码或科目名称列')
  }
  if (!roles.includes('debitAmount') && !roles.includes('creditAmount')) {
    throw new Error('至少需要指定借方金额或贷方金额列')
  }
  if (!uploadedFileId.value) {
    throw new Error('文件尚未上传成功，请返回上一步重新上传')
  }
  if (!props.accountSetId) {
    throw new Error('未选择目标账套，无法自动匹配科目')
  }

  // 提取科目 + 自动匹配
  matchLoading.value = true
  try {
    const subjectCodeColumn = parsedColumns.value.find(c => c.role === 'accountCode')?.name || ''
    const subjectNameColumn = parsedColumns.value.find(c => c.role === 'accountName')?.name

    const extractRes = await wizardExtractSubjects({
      fileId: uploadedFileId.value,
      subjectCodeColumn,
      subjectNameColumn,
    }) as any

    const subjects = extractRes?.subjects || []

    const matchRes = await wizardAutoMatch({
      schemeId: props.schemeId,
      targetAccountSetId: props.accountSetId,
      subjects,
    }) as any

    const allMatches: any[] = matchRes?.matches || []
    matchedAccounts.value = allMatches
      .filter(m => m.confidence && m.confidence !== 'none')
      .map(m => ({
        sourceCode: m.sourceCode,
        sourceName: m.sourceName,
        targetCode: m.targetCode || '',
        targetName: m.targetName || '',
        confidence: m.confidence,
      }))
    // unmatched 返回的是 SourceSubjectInfo 结构（code/name/count）
    unmatchedAccounts.value = (matchRes?.unmatched || []).map((m: any) => ({
      sourceCode: m.code || m.sourceCode || '',
      sourceName: m.name || m.sourceName || '',
      targetCode: '',
      targetName: '',
      confidence: 'none',
    }))
  } finally {
    matchLoading.value = false
  }
}

async function loadPreview() {
  previewLoading.value = true
  try {
    const columnMapping = parsedColumns.value
      .filter(c => c.role)
      .map(c => ({ column: c.name, role: c.role, separator: c.auxSeparator }))

    const res = await wizardPreview({
      schemeId: props.schemeId,
      fileId: uploadedFileId.value,
      accountSetId: props.accountSetId,
      columnMapping,
      accountMappings: [...matchedAccounts.value, ...unmatchedAccounts.value].filter(m => m.targetCode),
      auxiliaryMappings: auxPreviewData.value.filter(a => a.strategy !== 'ignore'),
      assetMappings: assetPreviewData.value.filter(a => !a.ignored && a.targetCode),
      auxConfig: { ...auxConfig },
    }) as any

    if (res?.rows?.length) {
      previewData.value = res.rows.map((r: any, idx: number) => ({ ...r, _idx: idx }))
      previewCols.value = res.columns?.map((c: any) => ({
        title: c.title || c,
        dataIndex: c.dataIndex || c.key || c,
        width: c.width,
        ellipsis: true,
      })) || []
    } else {
      previewData.value = []
      previewCols.value = []
    }
  } catch {
    message.warning('预览加载失败，可直接提交')
  } finally {
    previewLoading.value = false
  }
}

async function handleCommit() {
  commitStatus.value = 'loading'
  commitError.value = ''
  try {
    const columnMapping = parsedColumns.value
      .filter(c => c.role)
      .map(c => ({ column: c.name, role: c.role, separator: c.auxSeparator }))

    const res = await wizardCommit({
      schemeId: props.schemeId,
      accountSetId: props.accountSetId,
      columnMapping,
      accountMappings: [...matchedAccounts.value, ...unmatchedAccounts.value].filter(m => m.targetCode),
      auxiliaryMappings: auxPreviewData.value.filter(a => a.strategy !== 'ignore'),
      assetMappings: assetPreviewData.value.filter(a => !a.ignored && a.targetCode),
      auxConfig: { ...auxConfig },
    }) as any

    commitResult.value = res
    commitStatus.value = 'success'
    message.success('迁移提交成功')
    emit('success')
  } catch (err: any) {
    commitError.value = err?.message || '提交失败'
    commitStatus.value = 'error'
  }
}
</script>

<style scoped lang="scss">
.wizard-content {
  min-height: 360px;
  max-height: 520px;
  overflow-y: auto;
}

.step-panel {
  padding: 4px 0;
}

.wizard-footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  padding-top: 16px;
  border-top: 1px solid #f0f0f0;
  margin-top: 16px;
}

.sample-text {
  color: #888;
  font-size: 12px;
}

.match-summary {
  display: flex;
  gap: 8px;
}
</style>
