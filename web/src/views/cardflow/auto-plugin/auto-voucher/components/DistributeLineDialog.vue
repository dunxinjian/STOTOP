<!-- 分发分录到其他规则组弹窗 -->
<template>
  <a-modal
    :open="visible"
    title="分发分录到其他规则组"
    :width="600"
    @cancel="handleClose"
    :footer="null"
    destroyOnClose
  >
    <!-- 空状态 -->
    <div v-if="groupStats.length === 0" class="empty-state">
      <EmptyState description="暂无其他规则组可供分发" />
    </div>

    <template v-else>
      <!-- 快捷操作 -->
      <div class="quick-actions">
        <a-button size="small" @click="selectAll">全选</a-button>
        <a-button size="small" @click="selectMissing">仅选缺失组</a-button>
        <a-button size="small" @click="clearAll">清空</a-button>
      </div>

      <!-- 规则组表格 -->
      <a-table
        :dataSource="groupStats"
        :columns="columns"
        :pagination="false"
        :row-key="(r: GroupStat) => r.groupId"
        size="small"
        :scroll="{ y: 400 }"
        class="distribute-table"
      >
        <template #bodyCell="{ column, record }">
          <!-- 复选框列 -->
          <template v-if="column.dataIndex === 'checked'">
            <a-checkbox
              :checked="checkedIds.has(record.groupId)"
              @change="toggleCheck(record.groupId)"
            />
          </template>
          <!-- 标签列 -->
          <template v-else-if="column.dataIndex === 'tag'">
            <a-tag v-if="isMissing(record as GroupStat)" color="red">
              缺少{{ lineDirection }}方
            </a-tag>
          </template>
        </template>
      </a-table>
    </template>

    <!-- 底部按钮 -->
    <div class="dialog-footer">
      <a-button @click="handleClose">取消</a-button>
      <a-button
        type="primary"
        :disabled="checkedIds.size === 0"
        @click="handleConfirm"
      >
        确认分发{{ checkedIds.size > 0 ? `(${checkedIds.size})` : '' }}
      </a-button>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { message } from 'ant-design-vue'
import { useAutoVoucherRuleStore } from '@/stores/autoVoucherRule'

const store = useAutoVoucherRuleStore()

// ==================== Props & Emits ====================
const props = defineProps<{
  /** 控制弹窗显隐 */
  visible: boolean
  /** 源分录ID */
  lineId: string
  /** 源分录方向 */
  lineDirection: '借' | '贷'
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
}>()

// ==================== 类型 ====================
interface GroupStat {
  groupId: string
  groupName: string
  debitCount: number
  creditCount: number
}

// ==================== 表格列定义 ====================
const columns = [
  { title: '', dataIndex: 'checked', width: 40 },
  { title: '组名', dataIndex: 'groupName', ellipsis: true },
  { title: '借方分录数', dataIndex: 'debitCount', width: 90, align: 'center' as const },
  { title: '贷方分录数', dataIndex: 'creditCount', width: 90, align: 'center' as const },
  { title: '标签', dataIndex: 'tag', width: 100 },
]

// ==================== 状态 ====================
const checkedIds = ref<Set<string>>(new Set())

// 获取规则组方向统计
const groupStats = computed<GroupStat[]>(() => {
  if (!props.visible) return []
  return store.getGroupDirectionStats()
})

// ==================== 智能预选 ====================
watch(
  () => props.visible,
  (val) => {
    if (val) {
      // 弹窗打开时，自动预选缺乏该方向分录的组
      const missing = new Set<string>()
      for (const g of groupStats.value) {
        if (isMissing(g)) {
          missing.add(g.groupId)
        }
      }
      checkedIds.value = missing
    } else {
      checkedIds.value = new Set()
    }
  }
)

// ==================== 辅助方法 ====================

/** 判断规则组是否缺少源分录方向 */
function isMissing(record: GroupStat): boolean {
  if (props.lineDirection === '借') return record.debitCount === 0
  return record.creditCount === 0
}

/** 切换单行选中 */
function toggleCheck(groupId: string) {
  const newSet = new Set(checkedIds.value)
  if (newSet.has(groupId)) {
    newSet.delete(groupId)
  } else {
    newSet.add(groupId)
  }
  checkedIds.value = newSet
}

/** 全选 */
function selectAll() {
  checkedIds.value = new Set(groupStats.value.map(g => g.groupId))
}

/** 仅选缺失组 */
function selectMissing() {
  checkedIds.value = new Set(
    groupStats.value.filter(g => isMissing(g)).map(g => g.groupId)
  )
}

/** 清空选择 */
function clearAll() {
  checkedIds.value = new Set()
}

// ==================== 操作 ====================

/** 关闭弹窗 */
function handleClose() {
  emit('update:visible', false)
}

/** 确认分发 */
function handleConfirm() {
  const targetIds = Array.from(checkedIds.value)
  if (targetIds.length === 0) return

  const count = store.distributeLineToGroups(props.lineId, targetIds)
  if (count > 0) {
    message.success(`已分发到 ${count} 个规则组`)
  }
  handleClose()
}
</script>

<style lang="scss" scoped>
.empty-state {
  padding: 40px 0;
}

.quick-actions {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}

.distribute-table {
  margin-bottom: 16px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-top: 12px;
  border-top: 1px solid var(--border);
}
</style>
