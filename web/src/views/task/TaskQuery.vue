<template>
  <div class="task-query-page">
    <!-- 顶部：标题 + 视图切换 + 新建按钮 -->
    <div class="task-query-page__header">
      <h2>任务查询</h2>
      <div class="task-query-page__actions">
        <a-radio-group v-model:value="currentView" button-style="solid" @change="handleViewChange">
          <a-radio-button value="list">
            <UnorderedListOutlined /> 列表
          </a-radio-button>
          <a-radio-button value="kanban">
            <AppstoreOutlined /> 看板
          </a-radio-button>
          <a-radio-button value="calendar">
            <CalendarOutlined /> 日历
          </a-radio-button>
        </a-radio-group>
        <a-button type="primary" @click="handleCreate">
          <PlusOutlined /> 新建任务
        </a-button>
      </div>
    </div>

    <!-- 视图内容区 -->
    <TaskListView v-if="currentView === 'list'" :embedded="true" />
    <TaskKanbanView v-else-if="currentView === 'kanban'" :embedded="true" />
    <TaskCalendarView v-else-if="currentView === 'calendar'" :embedded="true" />

    <!-- 创建任务弹窗 -->
    <a-modal v-model:open="showCreateModal" title="创建任务" :footer="null" width="680px" destroy-on-close>
      <TaskForm @submit="handleCreateSubmit" @cancel="showCreateModal = false" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  UnorderedListOutlined,
  AppstoreOutlined,
  CalendarOutlined,
  PlusOutlined,
} from '@ant-design/icons-vue'
import TaskListView from './TaskList.vue'
import TaskKanbanView from './TaskKanban.vue'
import TaskCalendarView from './TaskCalendar.vue'
import TaskForm from './components/TaskForm.vue'

const route = useRoute()
const router = useRouter()

const currentView = ref<'list' | 'kanban' | 'calendar'>('list')
const showCreateModal = ref(false)

onMounted(() => {
  const view = route.query.view as string
  if (view === 'kanban' || view === 'calendar' || view === 'list') {
    currentView.value = view
  }
})

function handleViewChange() {
  router.replace({ query: { view: currentView.value } })
}

function handleCreate() {
  showCreateModal.value = true
}

function handleCreateSubmit() {
  showCreateModal.value = false
  // 刷新当前视图 - 通过切换重新触发子组件 onMounted
}
</script>

<style scoped lang="scss">
.task-query-page {
  padding: 20px;

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 16px;

    h2 {
      margin: 0;
      font-size: 20px;
      font-weight: 600;
    }
  }

  &__actions {
    display: flex;
    align-items: center;
    gap: 12px;
  }
}
</style>
