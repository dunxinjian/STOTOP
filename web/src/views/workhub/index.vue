<template>
  <div class="todo-page">
    <!-- 左：纵向四类页签 + 列表 -->
    <div class="todo-main">
      <!-- 纵向四类页签（OneNote 卡片式） -->
      <div class="todo-rail">
        <button
          v-for="c in categories"
          :key="c.key"
          type="button"
          class="rail-tab"
          :class="{ active: activeCategory === c.key }"
          @click="selectCategory(c.key)"
        >
          <span class="rail-bar" />
          <component :is="c.icon" class="rail-ico" />
          <span class="rail-name">{{ c.label }}</span>
          <span class="rail-count">{{ statOf(c.statKey) }}</span>
        </button>
      </div>

      <!-- 列表区 -->
      <div class="todo-list-wrap">
        <div class="list-head">
          <span class="list-title">{{ activeLabel }}待办</span>
          <span class="list-count">{{ hub.totalCount.value }} 项</span>
          <span class="list-spacer" />
          <button
            type="button"
            class="list-act"
            :class="{ on: hub.isMultiSelectMode.value }"
            @click="toggleMulti"
          >
            <CheckSquareOutlined /> 多选
          </button>
        </div>

        <div class="todo-list">
          <!-- 新待办横幅 -->
          <div v-if="hub.newItemsCount.value > 0" class="new-banner" @click="hub.flushPendingItems()">
            <ArrowUpOutlined class="nb-ico" />
            <span class="nb-text">有 {{ hub.newItemsCount.value }} 条新待办送达，点击查看</span>
            <CloseOutlined class="nb-close" @click.stop="hub.dismissPendingItems()" />
          </div>

          <!-- 加载骨架 -->
          <template v-if="hub.loading.value && hub.items.value.length === 0">
            <div v-for="n in 4" :key="`sk-${n}`" class="todo-skeleton" />
          </template>

          <!-- 列表项 -->
          <TodoItemCard
            v-for="item in hub.items.value"
            :key="item.id"
            :item="item"
            :selected="hub.selectedItemId.value === item.id"
            @select="selectItem(item)"
          />

          <!-- 空态 -->
          <div v-if="!hub.loading.value && hub.items.value.length === 0" class="todo-empty">
            <InboxOutlined class="te-ico" />
            <span>暂无{{ activeLabel }}待办</span>
          </div>

          <!-- 稍后处理 -->
          <div v-if="hub.deferredItems.value.length" class="todo-group">
            <button type="button" class="group-head" @click="showDeferred = !showDeferred">
              <DownOutlined v-if="showDeferred" class="gh-caret" />
              <RightOutlined v-else class="gh-caret" />
              稍后处理 {{ hub.deferredItems.value.length }}
            </button>
            <div v-if="showDeferred" class="group-body">
              <TodoItemCard
                v-for="item in hub.deferredItems.value"
                :key="item.id"
                :item="item"
                :selected="hub.selectedItemId.value === item.id"
                @select="selectItem(item)"
              />
            </div>
          </div>

          <!-- 已归档 -->
          <div v-if="hub.archivedItems.value.length" class="todo-group">
            <button type="button" class="group-head" @click="showArchived = !showArchived">
              <DownOutlined v-if="showArchived" class="gh-caret" />
              <RightOutlined v-else class="gh-caret" />
              已归档 {{ hub.archivedItems.value.length }}
            </button>
            <div v-if="showArchived" class="group-body">
              <TodoItemCard
                v-for="item in hub.archivedItems.value"
                :key="item.id"
                :item="item"
                :selected="hub.selectedItemId.value === item.id"
                @select="selectItem(item)"
              />
            </div>
          </div>
        </div>

        <!-- 底部：撤销吐司 + J/K 提示 -->
        <div class="todo-foot">
          <template v-if="latestPending">
            <CheckCircleOutlined class="tf-ok" />
            <span class="tf-label">{{ latestPending.label }}</span>
            <button type="button" class="tf-undo" @click="hub.undoAction(latestPending.id)">撤销</button>
          </template>
          <span class="tf-spacer" />
          <span class="tf-hint">J / K 切换 · Enter 处理</span>
        </div>
      </div>
    </div>

    <!-- 右：详情 -->
    <div class="todo-detail">
      <WorkHubDetail
        v-if="selectedItem"
        :selected-item="selectedItem"
        @close="handleDetailClose"
      />
      <div v-else class="detail-empty">
        <InboxOutlined class="de-ico" />
        <span>从左侧选择待办查看详情</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted, type Component } from 'vue'
import { useRoute } from 'vue-router'
import {
  PartitionOutlined, CheckSquareOutlined, WarningOutlined, BellOutlined,
  ArrowUpOutlined, CloseOutlined, RightOutlined, DownOutlined,
  InboxOutlined, CheckCircleOutlined,
} from '@ant-design/icons-vue'
import { useWorkHub } from '@/composables/useWorkHub'
import { useUserStore } from '@/stores/user'
import type { WorkItem, WorkHubStats } from '@/api/workhub'
import TodoItemCard from './TodoItemCard.vue'
import WorkHubDetail from './WorkHubDetail.vue'

const route = useRoute()
const hub = useWorkHub()
const userStore = useUserStore()

// —— 四类纵向页签
type CatKey = 'approval' | 'task' | 'alert' | 'notification'
const categories: { key: CatKey; label: string; icon: Component; statKey: keyof WorkHubStats }[] = [
  { key: 'approval', label: '流程', icon: PartitionOutlined, statKey: 'approval' },
  { key: 'task', label: '任务', icon: CheckSquareOutlined, statKey: 'task' },
  { key: 'alert', label: '异常', icon: WarningOutlined, statKey: 'alert' },
  { key: 'notification', label: '通知', icon: BellOutlined, statKey: 'notification' },
]
const activeCategory = ref<CatKey>('approval')
const activeLabel = computed(() => categories.find(c => c.key === activeCategory.value)?.label ?? '')
function statOf(key: keyof WorkHubStats): number {
  return hub.stats.value[key] ?? 0
}

function selectCategory(key: CatKey) {
  if (activeCategory.value === key) return
  activeCategory.value = key
  selectedItem.value = null
  hub.selectedItemId.value = null
  hub.setFilter('category', key)
}

// —— 右栏选中项
type SelectedItem = { type: 'workitem'; id: string; data: WorkItem }
const selectedItem = ref<SelectedItem | null>(null)

function selectItem(item: WorkItem) {
  selectedItem.value = { type: 'workitem', id: item.id, data: item }
  hub.selectedItemId.value = item.id
}
function handleDetailClose() {
  selectedItem.value = null
  hub.selectedItemId.value = null
}

// 键盘 J/K 改变 hub.selectedItemId → 同步右栏
watch(() => hub.selectedItemId.value, (newId) => {
  if (!newId) {
    selectedItem.value = null
    return
  }
  if (selectedItem.value?.id === newId) return
  const target = hub.items.value.find(i => i.id === newId)
  if (target) selectedItem.value = { type: 'workitem', id: target.id, data: target }
})

// —— 撤销吐司（取最新一条 pending）
const latestPending = computed(() => {
  const list = hub.pendingActions.value
  return list.length ? list[list.length - 1] : null
})

// —— 折叠组
const showDeferred = ref(false)
const showArchived = ref(false)

// —— 多选
function toggleMulti() {
  if (hub.isMultiSelectMode.value) hub.exitMultiSelect()
  else hub.enterMultiSelect()
}

// —— 键盘 J/K
function handleKeydown(e: KeyboardEvent) {
  const target = e.target as HTMLElement | null
  const tag = target?.tagName?.toLowerCase()
  if (tag === 'input' || tag === 'textarea' || tag === 'select') return
  if (target?.isContentEditable) return
  if (!hub.selectedItemId.value) return
  if (e.key === 'j' || e.key === 'J') {
    e.preventDefault()
    hub.navigateNext()
  } else if (e.key === 'k' || e.key === 'K') {
    e.preventDefault()
    hub.navigatePrev()
  }
}

onMounted(async () => {
  // 通知入口可经 ?cat=notification 直达对应类别
  const q = route.query.cat
  if (typeof q === 'string' && categories.some(c => c.key === q)) {
    activeCategory.value = q as CatKey
  }
  hub.filters.value.category = activeCategory.value
  await hub.fetchStats()
  await hub.fetchItems(true)
  if (userStore.userInfo?.id) {
    hub.connect(userStore.userInfo.id)
  }
  document.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeydown)
})
</script>

<style scoped lang="scss">
.todo-page {
  display: flex;
  height: 100%;
  min-height: 0;
  background: var(--bg-card);
}

.todo-main {
  flex: 1;
  min-width: 0;
  display: flex;
}

// —— 纵向四类页签
.todo-rail {
  width: 80px;
  flex-shrink: 0;
  border-right: 1px solid var(--border);
  background: var(--bg-page);
  display: flex;
  flex-direction: column;
  padding: 6px 0;
  overflow-y: auto;
}

.rail-tab {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 3px;
  padding: 10px 0;
  border: none;
  background: transparent;
  color: var(--text-2);
  cursor: pointer;
  transition: background 0.15s, color 0.15s;

  &:hover {
    background: var(--bg-muted);
    color: var(--text-1);
  }

  &.active {
    background: var(--bg-card);
    color: var(--color-primary);
  }
}

.rail-bar {
  position: absolute;
  left: 0;
  top: 6px;
  bottom: 6px;
  width: 2.5px;
  background: transparent;
  border-radius: 2px;
}

.rail-tab.active .rail-bar {
  background: var(--color-primary);
}

.rail-ico {
  font-size: 18px;
}

.rail-name {
  font-size: 12px;
  font-weight: 500;
}

.rail-count {
  font-size: 10px;
  color: var(--text-3);
}

.rail-tab.active .rail-count {
  color: var(--color-primary);
}

// —— 列表区
.todo-list-wrap {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.list-head {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 14px 8px;
  border-bottom: 1px solid var(--border-faint);
}

.list-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-1);
}

.list-count {
  font-size: 11px;
  color: var(--text-3);
}

.list-spacer {
  flex: 1;
}

.list-act {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: var(--text-2);
  border: none;
  background: transparent;
  cursor: pointer;
  padding: 2px 6px;
  border-radius: var(--radius-sm);

  &:hover {
    background: var(--bg-muted);
    color: var(--text-1);
  }

  &.on {
    color: var(--color-primary);
  }
}

.todo-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px 14px;
  display: flex;
  flex-direction: column;
  gap: 7px;
}

.new-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
  background: var(--color-danger-light);
  border: 1px solid var(--color-danger-border);
  border-radius: var(--radius-md);
  padding: 6px 10px;
  font-size: 11.5px;
  color: var(--color-danger-text);
  cursor: pointer;
}

.nb-ico {
  font-size: 13px;
}

.nb-text {
  flex: 1;
}

.nb-close {
  font-size: 12px;
  opacity: 0.7;

  &:hover {
    opacity: 1;
  }
}

.todo-skeleton {
  height: 64px;
  flex-shrink: 0;
  border-radius: var(--radius-lg);
  background: var(--bg-muted);
  animation: td-pulse 1.4s ease-in-out infinite;
}

@keyframes td-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.todo-empty,
.detail-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  color: var(--text-3);
  font-size: 13px;
  padding: 40px 0;
}

.te-ico,
.de-ico {
  font-size: 32px;
  opacity: 0.4;
}

.todo-group {
  display: flex;
  flex-direction: column;
  gap: 7px;
  flex-shrink: 0;
}

.group-head {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-2);
  border: none;
  background: transparent;
  cursor: pointer;
  padding: 6px 4px;
  text-align: left;

  &:hover {
    color: var(--text-1);
  }
}

.gh-caret {
  font-size: 11px;
  color: var(--text-3);
}

.group-body {
  display: flex;
  flex-direction: column;
  gap: 7px;
}

.todo-foot {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 14px;
  border-top: 1px solid var(--border);
  background: var(--bg-card);
  flex-shrink: 0;
}

.tf-ok {
  font-size: 14px;
  color: var(--color-success);
}

.tf-label {
  font-size: 11.5px;
  color: var(--text-2);
}

.tf-undo {
  font-size: 11.5px;
  color: var(--color-primary);
  border: none;
  background: transparent;
  cursor: pointer;
  padding: 0;
}

.tf-spacer {
  flex: 1;
}

.tf-hint {
  font-size: 11px;
  color: var(--text-3);
}

// —— 右栏详情
.todo-detail {
  width: 320px;
  flex-shrink: 0;
  background: var(--bg-card);
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

@media (max-width: 1280px) {
  .todo-detail {
    width: 300px;
  }
}
</style>
