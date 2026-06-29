<template>
  <div class="step-editor">
    <div class="step-editor-toolbar">
      <a-button type="primary" ghost @click="addStep">
        <template #icon><PlusOutlined /></template>添加步骤
      </a-button>
      <span class="step-count">共 {{ localSteps.length }} 个步骤</span>
    </div>

    <div v-if="localSteps.length === 0" class="step-empty">
      <a-empty description="暂无步骤，点击上方按钮添加" />
    </div>

    <div class="step-list">
      <div v-for="(step, index) in localSteps" :key="step._uid" class="step-card">
        <div class="step-card-header">
          <span class="step-number">#{{ index + 1 }}</span>
          <span class="step-desc-text" v-if="step.description">{{ step.description }}</span>
          <div class="step-card-actions">
            <a-button type="link" :disabled="index === 0" @click="moveStep(index, -1)">
              <template #icon><ArrowUpOutlined /></template>
            </a-button>
            <a-button type="link" :disabled="index === localSteps.length - 1" @click="moveStep(index, 1)">
              <template #icon><ArrowDownOutlined /></template>
            </a-button>
            <a-button type="link" danger @click="removeStep(index)">
              <template #icon><DeleteOutlined /></template>
            </a-button>
          </div>
        </div>
        <div class="step-card-body">
          <a-row :gutter="12">
            <a-col :span="6">
              <div class="step-field">
                <label>步骤类型</label>
                <a-select v-model:value="step.stepType" placeholder="选择类型" style="width: 100%" @change="onStepTypeChange(step)">
                  <a-select-option value="navigate">打开页面</a-select-option>
                  <a-select-option value="click">点击元素</a-select-option>
                  <a-select-option value="input">输入文本</a-select-option>
                  <a-select-option value="select">选择下拉</a-select-option>
                  <a-select-option value="wait">等待</a-select-option>
                  <a-select-option value="download">下载文件</a-select-option>
                  <a-select-option value="screenshot">截图</a-select-option>
                  <a-select-option value="condition">条件判断</a-select-option>
                  <a-select-option value="loop">循环</a-select-option>
                </a-select>
              </div>
            </a-col>
            <a-col :span="6" v-if="needsSelector(step.stepType)">
              <div class="step-field">
                <label>选择器</label>
                <a-input v-model:value="step.selector" placeholder="CSS/XPath 选择器" @change="emitUpdate" />
              </div>
            </a-col>
            <a-col :span="6" v-if="needsValue(step.stepType)">
              <div class="step-field">
                <label>值</label>
                <a-input
                  :ref="(el: any) => setValueRef(index, el)"
                  v-model:value="step.value"
                  placeholder="URL/文本/值"
                  @change="emitUpdate"
                  @focus="activeValueIndex = index"
                />
              </div>
            </a-col>
            <a-col :span="3" v-if="needsWait(step.stepType)">
              <div class="step-field">
                <label>等待(ms)</label>
                <a-input-number v-model:value="step.waitMs" :min="0" :step="500" style="width: 100%" @change="emitUpdate" />
              </div>
            </a-col>
            <a-col :span="needsSelector(step.stepType) || needsValue(step.stepType) ? 3 : 6">
              <div class="step-field">
                <label>描述</label>
                <a-input v-model:value="step.description" placeholder="步骤说明" @change="emitUpdate" />
              </div>
            </a-col>
          </a-row>
          <!-- 变量插入按钮 -->
          <div class="variable-buttons" v-if="needsValue(step.stepType)">
            <span class="variable-label">插入变量：</span>
            <a-button v-for="v in variables" :key="v.key" size="small" @click="insertVariable(index, v.key)">
              {{ v.label }}
            </a-button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { PlusOutlined, ArrowUpOutlined, ArrowDownOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import { genTempId } from '@/utils/tempId'

export interface DownloadStep {
  stepType: string
  selector?: string
  value?: string
  waitMs?: number
  description?: string
  sortOrder?: number
  _uid?: string
}

const props = defineProps<{
  steps: DownloadStep[]
}>()

const emit = defineEmits<{
  (e: 'update:steps', steps: DownloadStep[]): void
}>()

const localSteps = ref<DownloadStep[]>([])
const activeValueIndex = ref<number>(-1)
const valueRefs = ref<Record<number, any>>({})

const variables = [
  { key: '{{account}}', label: '账号' },
  { key: '{{password}}', label: '密码' },
  { key: '{{startDate}}', label: '开始日期' },
  { key: '{{endDate}}', label: '结束日期' },
  { key: '{{networkCode}}', label: '网点编码' },
]

watch(() => props.steps, (val) => {
  localSteps.value = val ? JSON.parse(JSON.stringify(val)).map((s: DownloadStep) => ({ ...s, _uid: s._uid || genTempId() })) : []
}, { immediate: true, deep: true })

function setValueRef(index: number, el: any) {
  if (el) valueRefs.value[index] = el
}

function emitUpdate() {
  localSteps.value.forEach((s, i) => s.sortOrder = i + 1)
  emit('update:steps', JSON.parse(JSON.stringify(localSteps.value)))
}

function addStep() {
  localSteps.value.push({
    _uid: genTempId(),
    stepType: 'navigate',
    selector: '',
    value: '',
    waitMs: 1000,
    description: '',
    sortOrder: localSteps.value.length + 1,
  })
  emitUpdate()
}

function removeStep(index: number) {
  localSteps.value.splice(index, 1)
  emitUpdate()
}

function moveStep(index: number, direction: number) {
  const target = index + direction
  if (target < 0 || target >= localSteps.value.length) return
  const temp = localSteps.value[index]
  localSteps.value[index] = localSteps.value[target]
  localSteps.value[target] = temp
  localSteps.value = [...localSteps.value]
  emitUpdate()
}

function onStepTypeChange(step: DownloadStep) {
  if (!needsSelector(step.stepType)) step.selector = ''
  if (!needsValue(step.stepType)) step.value = ''
  emitUpdate()
}

function needsSelector(type: string): boolean {
  return ['click', 'input', 'select', 'download'].includes(type)
}

function needsValue(type: string): boolean {
  return ['navigate', 'input', 'select', 'condition', 'loop'].includes(type)
}

function needsWait(type: string): boolean {
  return ['wait', 'click', 'navigate', 'download'].includes(type)
}

function insertVariable(index: number, varKey: string) {
  const step = localSteps.value[index]
  if (!step) return
  step.value = (step.value || '') + varKey
  emitUpdate()
}
</script>

<style scoped lang="scss">
@use '@/styles/variables' as *;

.step-editor-toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;

  .step-count {
    color: #909399;
    font-size: 13px;
  }
}

.step-empty {
  padding: 20px 0;
}

.step-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.step-card {
  border: 1px solid #e4e7ed;
  border-radius: 6px;
  overflow: hidden;

  &:hover {
    border-color: var(--color-primary);
  }
}

.step-card-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: #fafafa;
  border-bottom: 1px solid #e4e7ed;

  .step-number {
    font-weight: 600;
    color: var(--color-info);
    font-size: 13px;
    min-width: 28px;
  }

  .step-desc-text {
    color: #606266;
    font-size: 13px;
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .step-card-actions {
    margin-left: auto;
    display: flex;
    align-items: center;
    gap: 2px;
  }
}

.step-card-body {
  padding: 12px;
}

.step-field {
  label {
    display: block;
    font-size: 12px;
    color: #909399;
    margin-bottom: 4px;
  }
}

.variable-buttons {
  margin-top: 8px;
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;

  .variable-label {
    font-size: 12px;
    color: #909399;
  }
}
</style>
