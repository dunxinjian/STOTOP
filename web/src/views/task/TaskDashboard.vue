<template>
  <div class="page-container">
    <PageHeader title="任务概览" />

    <!-- 第一行：KPI 统计卡片 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :xs="24" :sm="12" :md="6">
        <a-card :bordered="false" :body-style="{ padding: '20px 24px' }">
          <a-statistic
            title="本月目标数"
            :value="kpi.monthGoals"
            :value-style="{ color: 'var(--color-info)', fontSize: '28px', fontWeight: 600 }"
          >
            <template #prefix><AimOutlined style="font-size: 16px; margin-right: 4px" /></template>
          </a-statistic>
          <div class="kpi-footer">
            <span class="kpi-sub">总目标 {{ kpi.totalGoals }}</span>
            <!-- TODO: 对接真实API -->
            <span class="kpi-trend up"><ArrowUpOutlined /> 12%</span>
          </div>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card :bordered="false" :body-style="{ padding: '20px 24px' }">
          <a-statistic
            title="进行中任务"
            :value="kpi.inProgressTasks"
            :value-style="{ color: 'var(--color-warning)', fontSize: '28px', fontWeight: 600 }"
          >
            <template #prefix><ClockCircleOutlined style="font-size: 16px; margin-right: 4px" /></template>
          </a-statistic>
          <div class="kpi-footer">
            <span class="kpi-sub">待分配 {{ kpi.unassignedTasks }}</span>
            <!-- TODO: 对接真实API -->
            <span class="kpi-trend down"><ArrowDownOutlined /> 5%</span>
          </div>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card :bordered="false" :body-style="{ padding: '20px 24px' }">
          <a-statistic
            title="本月完成数"
            :value="kpi.monthCompleted"
            :value-style="{ color: 'var(--color-success)', fontSize: '28px', fontWeight: 600 }"
          >
            <template #prefix><CheckCircleOutlined style="font-size: 16px; margin-right: 4px" /></template>
          </a-statistic>
          <div class="kpi-footer">
            <span class="kpi-sub">完成率 {{ kpi.completionRate }}%</span>
            <!-- TODO: 对接真实API -->
            <span class="kpi-trend up"><ArrowUpOutlined /> 8%</span>
          </div>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card :bordered="false" :body-style="{ padding: '20px 24px' }">
          <a-statistic
            title="本月项目数"
            :value="kpi.monthProjects"
            :value-style="{ color: 'var(--biz-points)', fontSize: '28px', fontWeight: 600 }"
          >
            <template #prefix><ProjectOutlined style="font-size: 16px; margin-right: 4px" /></template>
          </a-statistic>
          <div class="kpi-footer">
            <span class="kpi-sub">活跃项目 {{ kpi.activeProjects }}</span>
            <!-- TODO: 对接真实API -->
            <span class="kpi-trend up"><ArrowUpOutlined /> 3%</span>
          </div>
        </a-card>
      </a-col>
    </a-row>

    <!-- 第二行：快捷操作区 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :xs="12" :sm="6" v-for="action in quickActions" :key="action.key">
        <a-card
          :bordered="false"
          hoverable
          :body-style="{ padding: '20px', textAlign: 'center', cursor: 'pointer' }"
          @click="router.push(action.route)"
        >
          <component :is="action.icon" :style="{ fontSize: '28px', color: action.color }" />
          <div style="margin-top: 8px; font-size: 14px; color: rgba(0,0,0,0.85)">{{ action.label }}</div>
        </a-card>
      </a-col>
    </a-row>

    <!-- 第三行：任务统计区（左右两栏） -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :xs="24" :md="12">
        <a-card title="任务状态分布" :bordered="false">
          <div class="status-bars">
            <div v-for="item in statusDistribution" :key="item.label" class="status-bar-item">
              <div class="status-bar-label">
                <span>{{ item.label }}</span>
                <span>{{ item.count }} ({{ item.percent }}%)</span>
              </div>
              <a-progress
                :percent="item.percent"
                :stroke-color="item.color"
                :show-info="false"
                size="small"
              />
            </div>
          </div>
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12">
        <a-card title="本周任务动态" :bordered="false">
          <a-list :data-source="recentActivities" size="small">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta :description="item.time">
                  <template #title>
                    <span style="color: rgba(0,0,0,0.85)">{{ item.user }}</span>
                    <span style="color: rgba(0,0,0,0.45); margin: 0 4px">{{ item.action }}</span>
                    <span style="color: var(--color-info)">{{ item.target }}</span>
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
          <a-empty v-if="!recentActivities.length" description="暂无动态" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 第四行：我的待办任务 -->
    <a-card title="我的待办任务" :bordered="false">
      <a-table
        :columns="todoColumns"
        :data-source="myTodoTasks"
        :pagination="false"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'priority'">
            <a-tag :color="priorityColor(record.priority)">{{ record.priority }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'status'">
            <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
          </template>
        </template>
      </a-table>
      <div style="text-align: center; margin-top: 12px">
        <a-button type="link" @click="router.push('/task/my-tasks')">查看全部</a-button>
      </div>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  AimOutlined,
  ClockCircleOutlined,
  CheckCircleOutlined,
  ProjectOutlined,
  ArrowUpOutlined,
  ArrowDownOutlined,
  PlusCircleOutlined,
  UnorderedListOutlined,
  FundProjectionScreenOutlined,
  TrophyOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'

const router = useRouter()

// ===== KPI 数据 =====
// TODO: 对接真实API
const kpi = ref({
  monthGoals: 12,
  totalGoals: 48,
  inProgressTasks: 23,
  unassignedTasks: 5,
  monthCompleted: 37,
  completionRate: 78,
  monthProjects: 6,
  activeProjects: 4,
})

// ===== 快捷操作 =====
const quickActions = [
  { key: 'goal', label: '新建目标', icon: AimOutlined, color: 'var(--color-info)', route: '/task/goals' },
  { key: 'task', label: '新建任务', icon: UnorderedListOutlined, color: 'var(--color-success)', route: '/task/tasks' },
  { key: 'project', label: '项目管理', icon: FundProjectionScreenOutlined, color: 'var(--color-warning)', route: '/task/projects' },
  { key: 'performance', label: '绩效评估', icon: TrophyOutlined, color: 'var(--biz-points)', route: '/task/performance/my' },
]

// ===== 任务状态分布 =====
// TODO: 对接真实API
const statusDistribution = ref([
  { label: '待处理', count: 15, percent: 20, color: 'var(--color-info)' },
  { label: '进行中', count: 23, percent: 31, color: 'var(--color-warning)' },
  { label: '已完成', count: 32, percent: 43, color: 'var(--color-success)' },
  { label: '已取消', count: 5, percent: 6, color: '#d9d9d9' },
])

// ===== 本周任务动态 =====
// TODO: 对接真实API
const recentActivities = ref([
  { user: '张三', action: '完成了任务', target: '月度报表整理', time: '10分钟前' },
  { user: '李四', action: '创建了目标', target: 'Q2 销售目标', time: '30分钟前' },
  { user: '王五', action: '更新了项目', target: '系统升级项目', time: '1小时前' },
  { user: '赵六', action: '提交了复盘', target: '3月工作总结', time: '2小时前' },
  { user: '孙七', action: '分配了任务', target: '客户拜访计划', time: '3小时前' },
])

// ===== 我的待办任务 =====
// TODO: 对接真实API
const myTodoTasks = ref([
  { id: 1, name: '完成季度销售报告', priority: '高', deadline: '2026-04-15', status: '进行中' },
  { id: 2, name: '客户回访跟进', priority: '中', deadline: '2026-04-12', status: '待处理' },
  { id: 3, name: '新员工培训方案', priority: '高', deadline: '2026-04-18', status: '进行中' },
  { id: 4, name: '月度绩效评估', priority: '中', deadline: '2026-04-20', status: '待处理' },
  { id: 5, name: '系统功能测试', priority: '低', deadline: '2026-04-25', status: '待处理' },
])

const todoColumns = [
  { title: '任务名称', dataIndex: 'name', ellipsis: true },
  { title: '优先级', dataIndex: 'priority', width: 80, align: 'center' as const },
  { title: '截止日期', dataIndex: 'deadline', width: 120 },
  { title: '状态', dataIndex: 'status', width: 90, align: 'center' as const },
]

function priorityColor(priority: string): string {
  const map: Record<string, string> = { '高': 'red', '中': 'orange', '低': 'blue' }
  return map[priority] ?? 'default'
}

function statusColor(status: string): string {
  const map: Record<string, string> = { '进行中': 'processing', '待处理': 'warning', '已完成': 'success', '已取消': 'default' }
  return map[status] ?? 'default'
}

onMounted(async () => {
  try {
    // TODO: 对接真实API，加载KPI数据、任务状态分布、最近动态、待办任务
    // const data = await getTaskDashboard()
    // kpi.value = data.kpi
    // statusDistribution.value = data.statusDistribution
    // recentActivities.value = data.recentActivities
    // myTodoTasks.value = data.myTodoTasks
  } catch {
    // API 不存在时使用模拟数据（已初始化）
  }
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.kpi-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid #f0f0f0;
}

.kpi-sub {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
}

.kpi-trend {
  font-size: 13px;
  font-weight: 500;

  &.up {
    color: var(--color-success);
  }

  &.down {
    color: var(--color-danger);
  }
}

.status-bars {
  .status-bar-item {
    margin-bottom: 16px;

    &:last-child {
      margin-bottom: 0;
    }
  }

  .status-bar-label {
    display: flex;
    justify-content: space-between;
    margin-bottom: 4px;
    font-size: 13px;
    color: rgba(0, 0, 0, 0.65);
  }
}
</style>
