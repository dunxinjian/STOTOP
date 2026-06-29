<template>
  <div class="flow-designer">
    <!-- 顶部工具栏 -->
    <div class="toolbar">
      <div class="toolbar__left">
        <a-button @click="handleBack"><ArrowLeftOutlined /> 返回</a-button>
        <a-divider type="vertical" />
        <a-input
          v-model:value="taskName"
          placeholder="任务名称"
          style="width: 200px"
          allowClear
        />
        <a-input
          v-model:value="targetUrl"
          placeholder="目标网址"
          style="width: 240px"
          allowClear
        />
        <a-input
          v-model:value="cronExpression"
          placeholder="Cron 表达式（如 0 0 8 * * ?）"
          style="width: 240px"
          allowClear
        />
      </div>
      <div class="toolbar__right">
        <a-button @click="handleViewLogs"><EyeOutlined /> 运行日志</a-button>
        <a-button type="primary" style="background: var(--color-success); border-color: var(--color-success)" @click="handleTestRun" :loading="testRunning"><CaretRightOutlined /> 测试运行</a-button>
        <a-button type="primary" @click="handleSave" :loading="saving"><CheckOutlined /> 保存</a-button>
      </div>
    </div>

    <!-- 三栏区域 -->
    <div class="designer-body">
      <!-- 左侧节点面板 -->
      <div class="node-panel" :class="{ collapsed: nodePanelCollapsed }">
        <div class="node-panel__header">
          <span v-show="!nodePanelCollapsed">节点库</span>
          <a-button
            type="link"
            @click="nodePanelCollapsed = !nodePanelCollapsed"
          >
            <DoubleRightOutlined v-if="nodePanelCollapsed" />
            <DoubleLeftOutlined v-else />
          </a-button>
        </div>
        <div v-show="!nodePanelCollapsed" class="node-panel__body">
          <a-collapse v-model:activeKey="expandedGroups">
            <a-collapse-panel
              v-for="group in nodeGroups"
              :key="group.name"
              :header="group.name"
            >
              <div
                v-for="node in group.items"
                :key="node.type"
                class="node-item"
                draggable="true"
                @dragstart="onDragStart($event, node)"
              >
                <div class="node-item__icon" :style="{ color: node.color }">
                  <component :is="node.icon" :style="{ fontSize: '18px' }" />
                </div>
                <div class="node-item__info">
                  <div class="node-item__name">{{ node.label }}</div>
                  <div class="node-item__desc">{{ node.description }}</div>
                </div>
              </div>
            </a-collapse-panel>
          </a-collapse>
        </div>
      </div>

      <!-- 中央画布 -->
      <div class="canvas-wrapper">
        <VueFlow
          v-model:nodes="nodes"
          v-model:edges="edges"
          :default-viewport="{ zoom: 1, x: 0, y: 0 }"
          @drop="onDrop"
          @dragover="onDragOver"
          @node-click="onNodeClick"
          @pane-click="onPaneClick"
          fit-view-on-init
          :snap-to-grid="true"
          :snap-grid="[16, 16]"
          class="vue-flow-canvas"
        >
          <Background />
          <Controls />
          <MiniMap />

          <template #node-action="nodeProps">
            <div
              class="custom-node action-node"
              :class="{ selected: selectedNodeId === nodeProps.id }"
              :style="{ '--node-color': nodeProps.data.color || 'var(--color-primary)' }"
            >
              <div class="custom-node__stripe"></div>
              <div class="custom-node__body">
                <component :is="nodeProps.data.icon" :style="{ fontSize: '20px' }" class="custom-node__icon" />
                <div class="custom-node__content">
                  <div class="custom-node__label">{{ nodeProps.data.label }}</div>
                  <div class="custom-node__preview">{{ getConfigPreview(nodeProps.data) }}</div>
                </div>
              </div>
              <Handle type="target" :position="Position.Top" />
              <Handle type="source" :position="Position.Bottom" />
            </div>
          </template>

          <template #node-condition="nodeProps">
            <div
              class="custom-node condition-node"
              :class="{ selected: selectedNodeId === nodeProps.id }"
              :style="{ '--node-color': nodeProps.data.color || 'var(--color-warning)' }"
            >
              <div class="custom-node__stripe"></div>
              <div class="custom-node__body">
                <component :is="nodeProps.data.icon" :style="{ fontSize: '20px' }" class="custom-node__icon" />
                <div class="custom-node__content">
                  <div class="custom-node__label">{{ nodeProps.data.label }}</div>
                  <div class="custom-node__preview">{{ getConfigPreview(nodeProps.data) }}</div>
                </div>
              </div>
              <Handle type="target" :position="Position.Top" />
              <Handle
                type="source"
                :position="Position.Bottom"
                id="true"
                :style="{ left: '30%' }"
              />
              <Handle
                type="source"
                :position="Position.Bottom"
                id="false"
                :style="{ left: '70%' }"
              />
              <div class="condition-labels">
                <span class="condition-label true-label">是</span>
                <span class="condition-label false-label">否</span>
              </div>
            </div>
          </template>
        </VueFlow>
      </div>

      <!-- 右侧属性面板 -->
      <div class="property-panel" v-if="selectedNode">
        <div class="property-panel__header">
          <span>节点属性</span>
          <a-button type="link" danger @click="handleDeleteNode"><DeleteOutlined /> 删除</a-button>
        </div>
        <div class="property-panel__body" style="overflow-y: auto;">
          <a-form layout="vertical">
            <!-- 通用属性 -->
            <a-form-item label="节点名称">
              <a-input v-model:value="selectedNode.data.label" @change="syncNodeData" />
            </a-form-item>
            <a-form-item label="说明">
              <a-input v-model:value="selectedNode.data.description" @change="syncNodeData" />
            </a-form-item>
            <a-divider />
            <!-- 动态配置 -->
            <template v-for="field in currentNodeFields" :key="field.key">
              <a-form-item
                v-if="isFieldVisible(field)"
                :label="field.label"
              >
                <a-input
                  v-if="field.type === 'input'"
                  v-model:value="selectedNode.data.config[field.key]"
                  :placeholder="field.placeholder"
                  @change="syncNodeData"
                />
                <a-input-number
                  v-else-if="field.type === 'number'"
                  v-model:value="selectedNode.data.config[field.key]"
                  :min="0"
                  style="width: 100%"
                  @change="syncNodeData"
                />
                <a-select
                  v-else-if="field.type === 'select'"
                  v-model:value="selectedNode.data.config[field.key]"
                  style="width: 100%"
                  @change="syncNodeData"
                >
                  <a-select-option
                    v-for="opt in field.options"
                    :key="opt"
                    :value="opt"
                  >{{ opt }}</a-select-option>
                </a-select>
                <a-date-picker
                  v-else-if="field.type === 'date'"
                  v-model:value="selectedNode.data.config[field.key]"
                  style="width: 100%"
                  valueFormat="YYYY-MM-DD"
                  @change="syncNodeData"
                />
                <a-textarea
                  v-else-if="field.type === 'textarea'"
                  v-model:value="selectedNode.data.config[field.key]"
                  :rows="3"
                  :placeholder="field.placeholder"
                  @change="syncNodeData"
                />
              </a-form-item>
            </template>
          </a-form>
        </div>
      </div>
    </div>

    <!-- 日志弹窗 -->
    <a-modal v-model:open="logDialogVisible" title="运行日志" width="800px" centered :destroyOnClose="true">
      <a-table
        :columns="logColumns"
        :dataSource="logList"
        :loading="logLoading"
        rowKey="startTime"
        bordered
        :pagination="false"
        :scroll="{ y: 400 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="record.status === 1 ? 'success' : record.status === 2 ? 'error' : 'processing'">
              {{ record.status === 1 ? '成功' : record.status === 2 ? '失败' : '运行中' }}
            </a-tag>
          </template>
        </template>
      </a-table>
      <template #footer>
        <a-button @click="logDialogVisible = false">关闭</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { VueFlow, useVueFlow, Position, Handle } from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import '@vue-flow/controls/dist/style.css'
import '@vue-flow/minimap/dist/style.css'
import { message } from 'ant-design-vue'
import {
  ArrowLeftOutlined, CheckOutlined, CaretRightOutlined, EyeOutlined,
  DeleteOutlined, DoubleLeftOutlined, DoubleRightOutlined,
  LinkOutlined, AimOutlined, EditOutlined, ClockCircleOutlined,
  CheckCircleOutlined, DownloadOutlined, SwapOutlined, RedoOutlined,
  BarChartOutlined, BellOutlined, LockOutlined, CalendarOutlined,
} from '@ant-design/icons-vue'
import {
  getDownloadTask,
  createDownloadTask,
  updateDownloadTask,
  triggerDownloadTask,
  getDownloadLogs,
} from '@/api/cardflow'

import type { Node, Edge } from '@vue-flow/core'

// ==================== 节点类型定义 ====================

interface NodeConfigField {
  key: string
  label: string
  type: 'input' | 'number' | 'select' | 'date' | 'textarea'
  placeholder?: string
  default?: any
  options?: string[]
  showIf?: string
}

interface NodeTypeItem {
  type: string
  label: string
  icon: any
  color: string
  description: string
  category: string
}

const nodeTypeList: NodeTypeItem[] = [
  { type: 'Navigate', label: '打开网页', icon: LinkOutlined, color: 'var(--color-primary)', description: '导航到指定URL', category: '基础操作' },
  { type: 'Click', label: '点击', icon: AimOutlined, color: 'var(--color-primary)', description: '点击页面元素', category: '基础操作' },
  { type: 'Fill', label: '填写', icon: EditOutlined, color: 'var(--color-primary)', description: '填写表单输入', category: '基础操作' },
  { type: 'Wait', label: '等待', icon: ClockCircleOutlined, color: 'var(--color-primary)', description: '等待时间或元素', category: '基础操作' },
  { type: 'Select', label: '选择', icon: CheckCircleOutlined, color: 'var(--color-primary)', description: '下拉框选择', category: '基础操作' },
  { type: 'Download', label: '下载', icon: DownloadOutlined, color: 'var(--color-primary)', description: '触发文件下载', category: '基础操作' },
  { type: 'Condition', label: '条件判断', icon: SwapOutlined, color: 'var(--color-warning)', description: '根据条件分支', category: '高级操作' },
  { type: 'Loop', label: '循环', icon: RedoOutlined, color: 'var(--color-warning)', description: '循环执行操作', category: '高级操作' },
  { type: 'Extract', label: '数据提取', icon: BarChartOutlined, color: 'var(--color-warning)', description: '提取页面数据', category: '高级操作' },
  { type: 'Notify', label: '通知', icon: BellOutlined, color: 'var(--color-warning)', description: '发送通知消息', category: '高级操作' },
  { type: 'Login', label: '登录', icon: LockOutlined, color: 'var(--color-success)', description: '网站登录认证', category: '认证' },
  { type: 'DateFilter', label: '日期筛选', icon: CalendarOutlined, color: 'var(--color-success)', description: '设置日期过滤', category: '日期' },
]

const nodeGroups = computed(() => {
  const map = new Map<string, NodeTypeItem[]>()
  nodeTypeList.forEach(n => {
    if (!map.has(n.category)) map.set(n.category, [])
    map.get(n.category)!.push(n)
  })
  return Array.from(map.entries()).map(([name, items]) => ({ name, items }))
})

const nodeConfigs: Record<string, NodeConfigField[]> = {
  Navigate: [
    { key: 'url', label: 'URL 地址', type: 'input', placeholder: 'https://...' },
    { key: 'waitAfter', label: '导航后等待(ms)', type: 'number', default: 1000 },
  ],
  Click: [
    { key: 'selector', label: 'CSS/XPath 选择器', type: 'input' },
    { key: 'waitAfter', label: '点击后等待(ms)', type: 'number', default: 500 },
  ],
  Fill: [
    { key: 'selector', label: '输入框选择器', type: 'input' },
    { key: 'value', label: '输入值', type: 'input', placeholder: '支持 variable 变量' },
  ],
  Wait: [
    { key: 'duration', label: '等待时间(ms)', type: 'number', default: 1000 },
    { key: 'selector', label: '或等待元素出现', type: 'input', placeholder: 'CSS 选择器(可选)' },
  ],
  Select: [
    { key: 'selector', label: '下拉框选择器', type: 'input' },
    { key: 'value', label: '选项值', type: 'input' },
  ],
  Download: [
    { key: 'triggerSelector', label: '触发下载的元素', type: 'input' },
    { key: 'savePath', label: '保存路径', type: 'input', placeholder: '留空使用默认路径' },
    { key: 'waitTimeout', label: '等待下载超时(ms)', type: 'number', default: 30000 },
  ],
  Condition: [
    { key: 'conditionType', label: '条件类型', type: 'select', options: ['元素存在', 'URL匹配', '文本包含'] },
    { key: 'selector', label: '选择器/表达式', type: 'input' },
    { key: 'value', label: '匹配值', type: 'input' },
  ],
  Loop: [
    { key: 'loopType', label: '循环类型', type: 'select', options: ['日期范围', '列表元素', '固定次数'] },
    { key: 'startDate', label: '开始日期', type: 'date', showIf: 'loopType === "日期范围"' },
    { key: 'endDate', label: '结束日期', type: 'date', showIf: 'loopType === "日期范围"' },
    { key: 'selector', label: '列表选择器', type: 'input', showIf: 'loopType === "列表元素"' },
    { key: 'count', label: '循环次数', type: 'number', showIf: 'loopType === "固定次数"' },
  ],
  Extract: [
    { key: 'selector', label: '目标选择器', type: 'input' },
    { key: 'extractType', label: '提取类型', type: 'select', options: ['文本', '属性值', '表格'] },
    { key: 'variableName', label: '存储到变量', type: 'input', placeholder: '变量名' },
  ],
  Notify: [
    { key: 'message', label: '通知内容', type: 'textarea' },
    { key: 'level', label: '级别', type: 'select', options: ['info', 'warning', 'error'] },
  ],
  Login: [
    { key: 'loginUrl', label: '登录页URL', type: 'input' },
    { key: 'usernameSelector', label: '用户名输入框', type: 'input' },
    { key: 'passwordSelector', label: '密码输入框', type: 'input' },
    { key: 'submitSelector', label: '登录按钮', type: 'input' },
  ],
  DateFilter: [
    { key: 'startSelector', label: '开始日期选择器', type: 'input' },
    { key: 'endSelector', label: '结束日期选择器', type: 'input' },
    { key: 'dateFormat', label: '日期格式', type: 'input', default: 'YYYY-MM-DD' },
    { key: 'relativeDate', label: '相对日期', type: 'select', options: ['今天', '昨天', '本周', '上周', '本月', '上月'] },
  ],
}

// ==================== Vue Flow ====================

const route = useRoute()
const router = useRouter()
const taskId = computed(() => route.params.id ? Number(route.params.id) : null)
const isEdit = computed(() => !!taskId.value)

const nodes = ref<Node[]>([])
const edges = ref<Edge[]>([])
const taskName = ref('')
const targetUrl = ref('')
const cronExpression = ref('')
const saving = ref(false)
const testRunning = ref(false)
const nodePanelCollapsed = ref(false)
const expandedGroups = ref(['基础操作', '高级操作', '认证', '日期'])
const selectedNodeId = ref<string | null>(null)


// 日志
const logDialogVisible = ref(false)
const logLoading = ref(false)
const logList = ref<any[]>([])

const logColumns = [
  { title: '执行时间', dataIndex: 'startTime', key: 'startTime', width: 180 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 100, align: 'center' as const },
  { title: '下载文件数', dataIndex: 'downloadFileCount', key: 'downloadFileCount', width: 110, align: 'center' as const },
  { title: '错误信息', dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
]

const { addNodes, removeNodes, screenToFlowCoordinate, onConnect, addEdges } = useVueFlow()

onConnect((params) => {
  addEdges([{ ...params, id: `e-${params.source}-${params.sourceHandle || 'default'}-${params.target}`, animated: true }])
})

const selectedNode = computed(() => {
  if (!selectedNodeId.value) return null
  return nodes.value.find(n => n.id === selectedNodeId.value) || null
})

const currentNodeFields = computed(() => {
  if (!selectedNode.value) return []
  return nodeConfigs[selectedNode.value.data.nodeType] || []
})

function isFieldVisible(field: NodeConfigField): boolean {
  if (!field.showIf || !selectedNode.value) return true
  const config = selectedNode.value.data.config || {}
  try {
    const match = field.showIf.match(/^(\w+)\s*===\s*"(.+)"$/)
    if (match) return config[match[1]] === match[2]
    return true
  } catch {
    return true
  }
}

function getConfigPreview(data: any): string {
  const config = data.config || {}
  const type = data.nodeType
  switch (type) {
    case 'Navigate': return config.url ? truncate(config.url, 25) : '未配置URL'
    case 'Click': return config.selector || '未配置选择器'
    case 'Fill': return config.selector ? `${truncate(config.selector, 15)} = ${truncate(config.value || '', 10)}` : '未配置'
    case 'Wait': return config.duration ? `${config.duration}ms` : config.selector || '未配置'
    case 'Download': return config.triggerSelector || '未配置'
    case 'Condition': return config.conditionType || '未配置条件'
    case 'Loop': return config.loopType || '未配置循环'
    case 'Login': return config.loginUrl ? truncate(config.loginUrl, 25) : '未配置'
    case 'DateFilter': return config.relativeDate || '未配置'
    default: return ''
  }
}

function truncate(s: string, max: number) {
  return s.length > max ? s.slice(0, max) + '...' : s
}

function getNodeMeta(type: string) {
  return nodeTypeList.find(n => n.type === type)
}

// ==================== 拖放 ====================

function onDragStart(event: DragEvent, node: NodeTypeItem) {
  event.dataTransfer!.setData('application/vueflow-node-type', node.type)
  event.dataTransfer!.effectAllowed = 'move'
}

function onDragOver(event: DragEvent) {
  event.preventDefault()
  event.dataTransfer!.dropEffect = 'move'
}

function onDrop(event: DragEvent) {
  const nodeType = event.dataTransfer!.getData('application/vueflow-node-type')
  if (!nodeType) return

  const meta = getNodeMeta(nodeType)
  if (!meta) return

  const position = screenToFlowCoordinate({ x: event.clientX, y: event.clientY })

  const defaultConfig: Record<string, any> = {}
  const fields = nodeConfigs[nodeType] || []
  fields.forEach(f => {
    if (f.default !== undefined) defaultConfig[f.key] = f.default
  })

  const newNode: Node = {
    id: `node-${Date.now()}`,
    type: nodeType === 'Condition' ? 'condition' : 'action',
    position,
    data: {
      nodeType,
      label: meta.label,
      description: '',
      icon: meta.icon,
      color: meta.color,
      config: defaultConfig,
    },
  }
  addNodes([newNode])

  nextTick(() => {
    selectedNodeId.value = newNode.id
  })
}

function onNodeClick({ node }: { node: Node }) {
  selectedNodeId.value = node.id
}

function onPaneClick() {
  selectedNodeId.value = null
}

function syncNodeData() {
  // Reactivity handles it
}

function handleDeleteNode() {
  if (!selectedNodeId.value) return
  removeNodes([selectedNodeId.value])
  selectedNodeId.value = null
}

// ==================== 保存 / 加载 ====================

async function handleSave() {
  if (!taskName.value.trim()) {
    message.warning('请输入任务名称')
    return
  }
  if (!targetUrl.value.trim()) {
    message.warning('请输入目标网址')
    return
  }
  saving.value = true
  try {
    const scriptConfig = JSON.stringify({
      nodes: nodes.value.map(n => ({
        id: n.id,
        type: n.type,
        position: n.position,
        data: {
          nodeType: n.data.nodeType,
          label: n.data.label,
          description: n.data.description,
          config: n.data.config,
          color: n.data.color,
          iconName: n.data.nodeType,
        },
      })),
      edges: edges.value,
    })
    const payload = {
      taskName: taskName.value,
      targetUrl: targetUrl.value,
      cronExpression: cronExpression.value,
      scriptConfig: scriptConfig,
    }
    if (isEdit.value) {
      await updateDownloadTask(taskId.value!, payload)
      message.success('保存成功')
    } else {
      const res = await createDownloadTask(payload)
      message.success('创建成功')
      const newId = res?.data?.id || res?.id
      if (newId) {
        router.replace({ name: 'FlowDesigner', params: { id: newId } })
      }
    }
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

async function loadTask() {
  if (!taskId.value) return
  try {
    const res = await getDownloadTask(taskId.value)
    const task = res.data ?? res
    taskName.value = task.taskName || ''
    targetUrl.value = task.targetUrl || ''
    cronExpression.value = task.cronExpression || ''
    if (task.scriptConfig) {
      try {
        const config = JSON.parse(task.scriptConfig)
        nodes.value = (config.nodes || []).map((n: any) => ({
          ...n,
          data: {
            ...n.data,
            icon: getNodeMeta(n.data.iconName || n.data.nodeType)?.icon || LinkOutlined,
            color: n.data.color || getNodeMeta(n.data.nodeType)?.color || 'var(--color-primary)',
          },
        }))
        edges.value = config.edges || []
      } catch {
        nodes.value = []
        edges.value = []
      }
    }
  } catch {
    message.error('加载任务详情失败')
  }
}

// ==================== 其他操作 ====================

function handleBack() {
  router.push({ name: 'AutomationList' })
}

async function handleTestRun() {
  if (!isEdit.value) {
    message.warning('请先保存任务')
    return
  }
  testRunning.value = true
  try {
    await triggerDownloadTask(taskId.value!)
    message.success('测试任务已触发')
  } catch {
    message.error('触发失败')
  } finally {
    testRunning.value = false
  }
}

async function handleViewLogs() {
  if (!isEdit.value) {
    message.info('任务尚未保存，暂无日志')
    return
  }
  logDialogVisible.value = true
  logLoading.value = true
  try {
    const res = await getDownloadLogs(taskId.value!, { pageSize: 50 })
    logList.value = res.data ?? res ?? []
  } catch {
    message.error('加载日志失败')
  } finally {
    logLoading.value = false
  }
}

onMounted(() => {
  loadTask()
})
</script>

<style lang="scss" scoped>
@use '@/styles/variables' as *;

.flow-designer {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

// ==================== 工具栏 ====================
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px $page-padding;
  background: $bg-header;
  border-bottom: 1px solid $border-color-light;
  flex-shrink: 0;

  &__left, &__right {
    display: flex;
    align-items: center;
    gap: 8px;
  }
}

// ==================== 三栏布局 ====================
.designer-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

// ==================== 左侧节点面板 ====================
.node-panel {
  width: 240px;
  background: $bg-card;
  border-right: 1px solid $border-color-light;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  transition: width $transition-normal;

  &.collapsed {
    width: 40px;
  }

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 10px 12px;
    font-weight: 600;
    font-size: $font-size-base;
    border-bottom: 1px solid $border-color-lighter;
  }

  &__body {
    flex: 1;
    overflow-y: auto;
    padding: 4px 0;

    :deep(.ant-collapse) {
      border: none;
      background: transparent;
    }
    :deep(.ant-collapse-header) {
      padding: 8px 12px !important;
      font-size: $font-size-sm;
      font-weight: 600;
    }
    :deep(.ant-collapse-content-box) {
      padding: 0 8px 8px !important;
    }
  }
}

.node-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  margin-bottom: 4px;
  border-radius: $border-radius-sm;
  cursor: grab;
  border: 1px solid $border-color-lighter;
  background: $bg-card;
  transition: all $transition-fast;

  &:hover {
    border-color: $color-primary;
    box-shadow: $shadow-sm;
    background: color-mix(in srgb, var(--color-primary) 8%, transparent);
  }

  &:active {
    cursor: grabbing;
  }

  &__icon {
    flex-shrink: 0;
    width: 32px;
    height: 32px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: $border-radius-sm;
    background: color-mix(in srgb, var(--text-1) 4%, transparent);
  }

  &__info {
    min-width: 0;
  }

  &__name {
    font-size: $font-size-sm;
    font-weight: 500;
    color: $text-primary;
    line-height: 1.3;
  }

  &__desc {
    font-size: 11px;
    color: $text-secondary;
    line-height: 1.3;
    margin-top: 1px;
  }
}

// ==================== 中央画布 ====================
.canvas-wrapper {
  flex: 1;
  position: relative;
  background: var(--bg-page);
}

.vue-flow-canvas {
  width: 100%;
  height: 100%;
}

// ==================== 自定义节点 ====================
.custom-node {
  min-width: 180px;
  max-width: 240px;
  background: $bg-card;
  border-radius: $border-radius-md;
  border: 2px solid $border-color-light;
  box-shadow: $shadow-sm;
  overflow: visible;
  position: relative;
  transition: all $transition-fast;

  &.selected {
    border-color: var(--node-color);
    box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary) 20%, transparent), $shadow-md;
  }

  &__stripe {
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 4px;
    background: var(--node-color);
    border-radius: $border-radius-md 0 0 $border-radius-md;
  }

  &__body {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 10px 12px 10px 14px;
  }

  &__icon {
    flex-shrink: 0;
    color: var(--node-color);
  }

  &__content {
    min-width: 0;
  }

  &__label {
    font-size: 13px;
    font-weight: 600;
    color: $text-primary;
    line-height: 1.3;
  }

  &__preview {
    font-size: 11px;
    color: $text-secondary;
    line-height: 1.3;
    margin-top: 2px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
}

.condition-node {
  .condition-labels {
    display: flex;
    justify-content: space-between;
    padding: 0 20px 4px;
    position: absolute;
    bottom: -18px;
    left: 0;
    right: 0;

    .condition-label {
      font-size: 10px;
      font-weight: 500;
    }
    .true-label { color: $color-success; }
    .false-label { color: $color-danger; }
  }
}

// ==================== 右侧属性面板 ====================
.property-panel {
  width: 300px;
  background: $bg-card;
  border-left: 1px solid $border-color-light;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 10px 16px;
    font-weight: 600;
    font-size: $font-size-base;
    border-bottom: 1px solid $border-color-lighter;
  }

  &__body {
    flex: 1;
    padding: 16px;
    overflow-y: auto;

    :deep(.ant-form-item) {
      margin-bottom: 14px;
    }
    :deep(.ant-form-item-label > label) {
      font-size: $font-size-sm;
      color: $text-regular;
    }
  }
}

// ==================== Vue Flow 样式覆盖 ====================
:deep(.vue-flow__handle) {
  width: 10px;
  height: 10px;
  border: 2px solid $color-primary;
  background: var(--bg-card);
}

:deep(.vue-flow__edge-path) {
  stroke: $color-primary;
  stroke-width: 2;
}

:deep(.vue-flow__minimap) {
  border-radius: $border-radius-md;
  box-shadow: $shadow-md;
}

:deep(.vue-flow__controls) {
  border-radius: $border-radius-md;
  box-shadow: $shadow-md;
}
</style>
