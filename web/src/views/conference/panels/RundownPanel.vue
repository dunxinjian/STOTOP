<template>
  <div class="rundown-panel">
    <!-- 顶部智能操作栏 -->
    <SmartActionBar description="编排婚礼仪式流程">
      <a-button type="primary" @click="showAddDrawer"><PlusOutlined />添加环节</a-button>
      <a-dropdown>
        <a-button><ThunderboltOutlined />预设模板</a-button>
        <template #overlay>
          <a-menu @click="handleApplyTemplate">
            <a-menu-item key="standard">标准婚礼仪式</a-menu-item>
            <a-menu-item key="western">西式婚礼</a-menu-item>
            <a-menu-item key="chinese">中式婚礼</a-menu-item>
          </a-menu>
        </template>
      </a-dropdown>
      <a-button @click="handleExportRundown"><CopyOutlined />导出执行手卡</a-button>
    </SmartActionBar>

    <!-- 阶段Tab切换 -->
    <a-tabs v-model:activeKey="activePhase" style="margin-bottom: 12px">
      <a-tab-pane key="all" tab="全部" />
      <a-tab-pane key="迎宾" tab="迎宾" />
      <a-tab-pane key="仪式" tab="仪式" />
      <a-tab-pane key="宴席" tab="宴席" />
      <a-tab-pane key="送客" tab="送客" />
    </a-tabs>

    <!-- 时间轴视图 -->
    <a-spin :spinning="loading">
      <div v-if="filteredItems.length > 0" class="rundown-list">
        <div class="rundown-item" v-for="(item, index) in filteredItems" :key="item.id">
          <div class="time-badge">{{ item.startTime }}</div>
          <div class="item-content">
            <div class="item-title">
              {{ item.name }}
              <a-tag color="blue" style="margin-left: 8px">{{ item.duration }} 分钟</a-tag>
              <a-tag>{{ item.phase }}</a-tag>
            </div>
            <div class="item-details">
              <span v-if="item.responsible">&#x1F464; {{ item.responsible }}</span>
              <span v-if="item.music">&#x1F3B5; {{ item.music }}</span>
              <span v-if="item.lighting">&#x1F4A1; {{ item.lighting }}</span>
              <span v-if="item.props">&#x1F4E6; {{ item.props }}</span>
            </div>
          </div>
          <div class="item-actions">
            <a-button size="small" :disabled="index === 0" @click="moveUp(item, index)"><ArrowUpOutlined /></a-button>
            <a-button size="small" :disabled="index === filteredItems.length - 1" @click="moveDown(item, index)"><ArrowDownOutlined /></a-button>
            <a-button size="small" @click="showEditDrawer(item)"><EditOutlined /></a-button>
            <a-popconfirm title="确认删除该环节？" @confirm="handleDelete(item.id)">
              <a-button size="small" danger><DeleteOutlined /></a-button>
            </a-popconfirm>
          </div>
        </div>
      </div>
      <EmptyState v-else size="small" title="暂无仪式环节" />
    </a-spin>

    <!-- 新增/编辑 Drawer -->
    <a-drawer
      v-model:open="showDrawer"
      :title="editingId ? '编辑环节' : '添加环节'"
      :width="520"
      :destroyOnClose="true"
      @close="resetForm"
    >
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }">
        <a-form-item label="环节名称" required>
          <a-input v-model:value="formData.name" placeholder="请输入环节名称" />
        </a-form-item>
        <a-form-item label="开始时间">
          <a-time-picker
            v-model:value="formStartTime"
            format="HH:mm"
            placeholder="选择时间"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="时长(分钟)">
          <a-input-number v-model:value="formData.duration" :min="1" :max="600" placeholder="分钟" style="width: 100%" />
        </a-form-item>
        <a-form-item label="阶段">
          <a-select v-model:value="formData.phase" placeholder="请选择阶段">
            <a-select-option value="迎宾">迎宾</a-select-option>
            <a-select-option value="仪式">仪式</a-select-option>
            <a-select-option value="宴席">宴席</a-select-option>
            <a-select-option value="送客">送客</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="负责人">
          <a-input v-model:value="formData.responsible" placeholder="请输入负责人" />
        </a-form-item>
        <a-form-item label="背景音乐">
          <a-input v-model:value="formData.music" placeholder="请输入背景音乐" />
        </a-form-item>
        <a-form-item label="灯光方案">
          <a-select v-model:value="formData.lighting" placeholder="请选择灯光方案" allowClear>
            <a-select-option value="全亮">全亮</a-select-option>
            <a-select-option value="暗场">暗场</a-select-option>
            <a-select-option value="追光">追光</a-select-option>
            <a-select-option value="暖光">暖光</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="道具物品">
          <a-input v-model:value="formData.props" placeholder="请输入道具物品" />
        </a-form-item>
        <a-form-item label="备注">
          <a-textarea v-model:value="formData.remark" placeholder="请输入备注" :rows="3" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="showDrawer = false">取消</a-button>
          <a-button type="primary" :loading="saving" @click="handleSave">保存</a-button>
        </a-space>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import dayjs, { type Dayjs } from 'dayjs'
import {
  PlusOutlined,
  ThunderboltOutlined,
  CopyOutlined,
  EditOutlined,
  DeleteOutlined,
  ArrowUpOutlined,
  ArrowDownOutlined,
} from '@ant-design/icons-vue'
import SmartActionBar from '../components/SmartActionBar.vue'
import {
  getCeremonyItems,
  createCeremonyItem,
  updateCeremonyItem,
  deleteCeremonyItem,
  reorderCeremony,
  exportRundown,
} from '@/api/conference'
import type {
  CeremonyItemDto,
  CreateCeremonyItemRequest,
  UpdateCeremonyItemRequest,
} from '@/api/conference'

const props = defineProps<{ eventId: number; eventData?: any }>()

// ==================== 列表状态 ====================
const loading = ref(false)
const items = ref<CeremonyItemDto[]>([])
const activePhase = ref<string>('all')

const filteredItems = computed(() => {
  if (activePhase.value === 'all') return items.value
  return items.value.filter((i) => i.phase === activePhase.value)
})

async function loadData() {
  loading.value = true
  try {
    const res: any = await getCeremonyItems(props.eventId)
    items.value = (res.data ?? res) as CeremonyItemDto[]
  } catch {
    message.error('加载仪式环节失败')
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

// ==================== 排序 ====================
async function moveUp(item: CeremonyItemDto, index: number) {
  if (index <= 0) return
  const list = [...items.value]
  const currentIdx = list.findIndex((i) => i.id === item.id)
  if (currentIdx <= 0) return
  ;[list[currentIdx - 1], list[currentIdx]] = [list[currentIdx], list[currentIdx - 1]]
  items.value = list
  await persistOrder()
}

async function moveDown(item: CeremonyItemDto, index: number) {
  const list = [...items.value]
  const currentIdx = list.findIndex((i) => i.id === item.id)
  if (currentIdx < 0 || currentIdx >= list.length - 1) return
  ;[list[currentIdx], list[currentIdx + 1]] = [list[currentIdx + 1], list[currentIdx]]
  items.value = list
  await persistOrder()
}

async function persistOrder() {
  try {
    await reorderCeremony(props.eventId, { itemIds: items.value.map((i) => i.id) })
  } catch {
    message.error('排序保存失败')
    await loadData()
  }
}

// ==================== 删除 ====================
async function handleDelete(id: number) {
  try {
    await deleteCeremonyItem(id)
    message.success('已删除')
    await loadData()
  } catch {
    message.error('删除失败')
  }
}

// ==================== Drawer 表单 ====================
const showDrawer = ref(false)
const saving = ref(false)
const editingId = ref<number | null>(null)
const formStartTime = ref<Dayjs | undefined>(undefined)

const formData = ref<CreateCeremonyItemRequest & { remark?: string }>({
  name: '',
  startTime: '',
  duration: 5,
  phase: '仪式',
  responsible: '',
  music: '',
  lighting: undefined,
  props: '',
  remark: '',
})

function resetForm() {
  editingId.value = null
  formStartTime.value = undefined
  formData.value = {
    name: '',
    startTime: '',
    duration: 5,
    phase: '仪式',
    responsible: '',
    music: '',
    lighting: undefined,
    props: '',
    remark: '',
  }
}

function showAddDrawer() {
  resetForm()
  showDrawer.value = true
}

function showEditDrawer(item: CeremonyItemDto) {
  editingId.value = item.id
  formData.value = {
    name: item.name,
    startTime: item.startTime,
    duration: item.duration,
    phase: item.phase,
    responsible: item.responsible ?? '',
    music: item.music ?? '',
    lighting: item.lighting,
    props: item.props ?? '',
    remark: item.remark ?? '',
  }
  formStartTime.value = item.startTime ? dayjs(item.startTime, 'HH:mm') : undefined
  showDrawer.value = true
}

async function handleSave() {
  if (!formData.value.name) {
    message.warning('请输入环节名称')
    return
  }
  saving.value = true
  // dayjs 对象转字符串
  const startTimeStr = formStartTime.value ? formStartTime.value.format('HH:mm') : ''
  const payload = { ...formData.value, startTime: startTimeStr }
  try {
    if (editingId.value) {
      await updateCeremonyItem(editingId.value, payload as UpdateCeremonyItemRequest)
      message.success('环节已更新')
    } else {
      await createCeremonyItem(props.eventId, payload)
      message.success('环节已添加')
    }
    showDrawer.value = false
    await loadData()
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

// ==================== 导出执行手卡 ====================
async function handleExportRundown() {
  try {
    const res: any = await exportRundown(props.eventId)
    const text = res.data ?? res
    const content = typeof text === 'string' ? text : JSON.stringify(text, null, 2)
    showRundownModal(content)
  } catch {
    message.error('导出执行手卡失败')
  }
}

function showRundownModal(text: string) {
  const modal = Modal.success({
    title: '执行手卡',
    width: 700,
    okText: '关闭',
    content: text,
  })
  // 直接复制到剪贴板
  navigator.clipboard.writeText(text).then(() => {
    message.success('已复制到剪贴板')
  }).catch(() => {
    // 静默失败，用户可手动复制
  })
}

// ==================== 预设模板 ====================
interface TemplateItem {
  name: string
  startTime: string
  duration: number
  phase: string
  music?: string
  lighting?: string
}

const templates: Record<string, TemplateItem[]> = {
  standard: [
    { name: '宾客签到', startTime: '17:30', duration: 30, phase: '迎宾', lighting: '全亮' },
    { name: '新人就位', startTime: '17:55', duration: 5, phase: '迎宾' },
    { name: '主持人开场', startTime: '18:00', duration: 5, phase: '仪式', lighting: '全亮' },
    { name: '新人入场', startTime: '18:05', duration: 5, phase: '仪式', music: '婚礼进行曲', lighting: '追光' },
    { name: '证婚人致辞', startTime: '18:10', duration: 8, phase: '仪式', lighting: '暖光' },
    { name: '交换戒指', startTime: '18:18', duration: 5, phase: '仪式', lighting: '追光' },
    { name: '新人宣誓', startTime: '18:23', duration: 5, phase: '仪式', lighting: '暖光' },
    { name: '父母致辞', startTime: '18:28', duration: 10, phase: '仪式', lighting: '全亮' },
    { name: '香槟塔', startTime: '18:38', duration: 5, phase: '仪式', lighting: '全亮' },
    { name: '抛捧花', startTime: '18:43', duration: 5, phase: '仪式', lighting: '全亮' },
    { name: '敬酒第一轮', startTime: '18:50', duration: 20, phase: '宴席' },
    { name: '敬酒第二轮', startTime: '19:10', duration: 20, phase: '宴席' },
    { name: '送客', startTime: '20:00', duration: 30, phase: '送客' },
  ],
  western: [
    { name: '宾客入场', startTime: '15:00', duration: 30, phase: '迎宾', lighting: '全亮' },
    { name: '花童撒花', startTime: '15:30', duration: 3, phase: '仪式', lighting: '全亮' },
    { name: '新娘入场', startTime: '15:33', duration: 5, phase: '仪式', music: 'Canon in D', lighting: '追光' },
    { name: '牧师致辞', startTime: '15:38', duration: 10, phase: '仪式', lighting: '暖光' },
    { name: '交换誓词', startTime: '15:48', duration: 8, phase: '仪式', lighting: '暖光' },
    { name: '交换戒指', startTime: '15:56', duration: 5, phase: '仪式', lighting: '追光' },
    { name: '亲吻新娘', startTime: '16:01', duration: 2, phase: '仪式', lighting: '追光' },
    { name: '抛捧花', startTime: '16:03', duration: 5, phase: '仪式', lighting: '全亮' },
    { name: '鸡尾酒会', startTime: '16:10', duration: 40, phase: '宴席', lighting: '全亮' },
    { name: '晚宴', startTime: '17:00', duration: 90, phase: '宴席' },
    { name: '第一支舞', startTime: '18:30', duration: 5, phase: '宴席', music: 'First Dance', lighting: '追光' },
    { name: '送客', startTime: '20:00', duration: 30, phase: '送客' },
  ],
  chinese: [
    { name: '宾客签到', startTime: '11:00', duration: 30, phase: '迎宾', lighting: '全亮' },
    { name: '司仪暖场', startTime: '11:28', duration: 2, phase: '仪式', lighting: '全亮' },
    { name: '新郎接亲回放', startTime: '11:30', duration: 5, phase: '仪式', lighting: '暗场' },
    { name: '新人入场', startTime: '11:35', duration: 5, phase: '仪式', music: '喜庆音乐', lighting: '追光' },
    { name: '拜天地', startTime: '11:40', duration: 5, phase: '仪式', lighting: '全亮' },
    { name: '敬茶改口', startTime: '11:45', duration: 10, phase: '仪式', lighting: '暖光' },
    { name: '交杯酒', startTime: '11:55', duration: 5, phase: '仪式', lighting: '追光' },
    { name: '新人致辞', startTime: '12:00', duration: 5, phase: '仪式', lighting: '全亮' },
    { name: '开宴', startTime: '12:05', duration: 10, phase: '宴席', lighting: '全亮' },
    { name: '敬酒第一轮', startTime: '12:15', duration: 25, phase: '宴席' },
    { name: '敬酒第二轮', startTime: '12:40', duration: 25, phase: '宴席' },
    { name: '送客', startTime: '13:30', duration: 30, phase: '送客' },
  ],
}

async function handleApplyTemplate({ key }: { key: string | number }) {
  const tpl = templates[String(key)]
  if (!tpl) return
  Modal.confirm({
    title: '应用预设模板',
    content: `将批量添加 ${tpl.length} 个环节到仪式流程中，是否继续？`,
    okText: '确认',
    cancelText: '取消',
    onOk: async () => {
      loading.value = true
      try {
        for (const item of tpl) {
          await createCeremonyItem(props.eventId, {
            name: item.name,
            startTime: item.startTime,
            duration: item.duration,
            phase: item.phase,
            music: item.music,
            lighting: item.lighting,
          })
        }
        message.success(`已添加 ${tpl.length} 个环节`)
        await loadData()
      } catch {
        message.error('模板应用失败')
      } finally {
        loading.value = false
      }
    },
  })
}
</script>

<style scoped lang="scss">
.rundown-panel {
  padding: 0;
}

.rundown-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.rundown-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 12px 16px;
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  transition: box-shadow 0.2s;

  &:hover {
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  }
}

.time-badge {
  flex-shrink: 0;
  min-width: 56px;
  padding: 4px 10px;
  background: var(--color-info);
  color: #fff;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 600;
  text-align: center;
  line-height: 22px;
}

.item-content {
  flex: 1;
  min-width: 0;
}

.item-title {
  font-size: 15px;
  font-weight: 500;
  margin-bottom: 6px;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
}

.item-details {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  font-size: 13px;
  color: #666;

  span {
    white-space: nowrap;
  }
}

.item-actions {
  flex-shrink: 0;
  display: flex;
  gap: 4px;
  align-items: center;
}
</style>
