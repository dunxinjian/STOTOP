<template>
  <div class="page-home">
    <!-- 顶部导航 -->
    <van-nav-bar title="STOTOP 工作台" :border="false" class="home-nav">
      <template #right>
        <span class="org-switch" @click="showOrgSheet = true">
          {{ currentOrg?.name || '选择组织' }}
          <van-icon name="arrow-down" size="12" />
        </span>
      </template>
    </van-nav-bar>

    <!-- 快捷操作区 -->
    <div class="quick-actions">
      <div class="quick-actions__item" @click="$router.push('/m/scan')">
        <van-icon name="scan" size="28" color="var(--text-2)" />
        <span>扫一扫</span>
      </div>
      <div class="quick-actions__item" @click="$router.push('/m/submit/0')">
        <van-icon name="add-square" size="28" color="var(--color-success)" />
        <span>发起卡片</span>
      </div>
    </div>

    <!-- 待处理标题 -->
    <div class="section-title">
      待处理<span class="count" v-if="total > 0">({{ total }})</span>
    </div>

    <!-- 待办列表 -->
    <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
      <van-list
        v-model:loading="loading"
        :finished="finished"
        finished-text="没有更多了"
        @load="loadMore"
      >
        <TodoCard
          v-for="item in todoList"
          :key="item.id"
          :item="item"
          @click="$router.push(`/m/card/${item.id}`)"
        />
      </van-list>

      <!-- 空状态 -->
      <van-empty v-if="!loading && todoList.length === 0" description="暂无待办" />
    </van-pull-refresh>

    <!-- 组织切换 ActionSheet -->
    <van-action-sheet
      v-model:show="showOrgSheet"
      :actions="orgActions"
      cancel-text="取消"
      @select="onOrgSelect"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { getTodos } from '@shared/api/cardflow'
import { useAuthStore } from '../stores/auth'
import TodoCard from '../components/TodoCard.vue'
import type { TodoCard as TodoCardType } from '@shared/types'

defineOptions({ name: 'MobileHome' })

const authStore = useAuthStore()

// 组织相关
const showOrgSheet = ref(false)
const currentOrg = computed(() => authStore.currentOrg)

const orgActions = computed(() =>
  authStore.organizations.map(org => ({
    name: org.name,
    value: org.id,
    color: org.id === authStore.currentOrgId ? 'var(--text-1)' : undefined,
  }))
)

function onOrgSelect(action: { value: number }) {
  authStore.setCurrentOrg(action.value)
  showOrgSheet.value = false
}

// 待办列表
const todoList = ref<TodoCardType[]>([])
const loading = ref(false)
const refreshing = ref(false)
const finished = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = 20

/** 加载更多 */
async function loadMore() {
  try {
    const res = await getTodos({ status: 'pending', page: page.value, pageSize })
    const items = res.items || []
    total.value = res.total || 0

    if (page.value === 1) {
      todoList.value = items
    } else {
      todoList.value.push(...items)
    }

    page.value++

    if (todoList.value.length >= total.value || items.length < pageSize) {
      finished.value = true
    }
  } catch (e) {
    console.error('[Home] 加载待办失败:', e)
    finished.value = true
  } finally {
    loading.value = false
    refreshing.value = false
  }
}

/** 下拉刷新 */
function onRefresh() {
  page.value = 1
  finished.value = false
  loading.value = true
  loadMore()
}

/** 组织切换后刷新列表 */
watch(() => authStore.currentOrgId, () => {
  onRefresh()
})
</script>

<style scoped lang="scss">
.page-home {
  min-height: 100vh;
  background: #f5f6f8;
}

.home-nav {
  :deep(.van-nav-bar__content) {
    background: #fff;
  }
}

.org-switch {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  font-size: 13px;
  color: #323233;
  padding: 4px 8px;
  border-radius: 14px;
  background: #f5f6f8;
}

.quick-actions {
  display: flex;
  gap: 16px;
  padding: 16px 20px;
  background: #fff;
  margin-bottom: 10px;

  &__item {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 6px;
    padding: 12px 20px;
    border-radius: 10px;
    background: #f7f8fa;
    flex: 1;
    cursor: pointer;
    transition: background 0.15s;

    &:active {
      background: #eef0f3;
    }

    span {
      font-size: 12px;
      color: #646566;
    }
  }
}

.section-title {
  padding: 12px 16px 8px;
  font-size: 15px;
  font-weight: 600;
  color: #323233;

  .count {
    font-size: 13px;
    color: #999;
    font-weight: 400;
    margin-left: 4px;
  }
}

:deep(.van-pull-refresh) {
  padding: 0 12px 12px;
}

:deep(.van-list__finished-text) {
  font-size: 12px;
  color: #c8c9cc;
}
</style>
