<template>
  <div class="page-container">
    <PageHeader title="车辆概览" />

    <!-- 第一行：KPI 统计卡片 -->
    <a-row :gutter="16" style="margin-bottom: 16px">
      <a-col :xs="24" :sm="12" :md="6">
        <a-card :bordered="false" :body-style="{ padding: '20px 24px' }">
          <a-statistic
            title="总车辆数"
            :value="kpi.totalVehicles"
            :value-style="{ color: 'var(--color-info)', fontSize: '28px', fontWeight: 600 }"
          >
            <template #prefix><CarOutlined style="font-size: 16px; margin-right: 4px" /></template>
          </a-statistic>
          <div class="kpi-footer">
            <span class="kpi-sub">本月新增 {{ kpi.newThisMonth }}</span>
            <!-- TODO: 对接真实API -->
            <span class="kpi-trend up"><ArrowUpOutlined /> 5%</span>
          </div>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card :bordered="false" :body-style="{ padding: '20px 24px' }">
          <a-statistic
            title="可用车辆"
            :value="kpi.availableVehicles"
            :value-style="{ color: 'var(--color-success)', fontSize: '28px', fontWeight: 600 }"
          >
            <template #prefix><CheckCircleOutlined style="font-size: 16px; margin-right: 4px" /></template>
          </a-statistic>
          <div class="kpi-footer">
            <span class="kpi-sub">可用率 {{ kpi.availableRate }}%</span>
          </div>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card :bordered="false" :body-style="{ padding: '20px 24px' }">
          <a-statistic
            title="出租中"
            :value="kpi.rentedVehicles"
            :value-style="{ color: 'var(--color-warning)', fontSize: '28px', fontWeight: 600 }"
          >
            <template #prefix><SwapOutlined style="font-size: 16px; margin-right: 4px" /></template>
          </a-statistic>
          <div class="kpi-footer">
            <span class="kpi-sub">出租率 {{ kpi.rentalRate }}%</span>
          </div>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card :bordered="false" :body-style="{ padding: '20px 24px' }">
          <a-statistic
            title="待维护"
            :value="kpi.maintenanceVehicles"
            :value-style="{ color: 'var(--color-danger)', fontSize: '28px', fontWeight: 600 }"
          >
            <template #prefix><ToolOutlined style="font-size: 16px; margin-right: 4px" /></template>
          </a-statistic>
          <div class="kpi-footer">
            <span class="kpi-sub">紧急 {{ kpi.urgentMaintenance }} 辆</span>
            <!-- TODO: 对接真实API -->
            <span class="kpi-trend down"><ArrowDownOutlined /> 2%</span>
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

    <!-- 第三行：车辆概览区（左右两栏） -->
    <a-row :gutter="16">
      <a-col :xs="24" :md="12">
        <a-card title="车辆状态分布" :bordered="false">
          <div class="status-bars">
            <div v-for="item in statusDistribution" :key="item.label" class="status-bar-item">
              <div class="status-bar-label">
                <span>{{ item.label }}</span>
                <span>{{ item.count }} 辆 ({{ item.percent }}%)</span>
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
        <a-card title="近期提醒" :bordered="false">
          <a-list :data-source="reminders" size="small">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta>
                  <template #avatar>
                    <a-badge :count="item.count" :number-style="{ backgroundColor: item.color }" />
                  </template>
                  <template #title>
                    <a @click="router.push(item.route)" style="color: rgba(0,0,0,0.85)">{{ item.label }}</a>
                  </template>
                  <template #description>{{ item.desc }}</template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
          <EmptyState v-if="!reminders.length" description="暂无提醒" />
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  CarOutlined,
  CheckCircleOutlined,
  SwapOutlined,
  ToolOutlined,
  ArrowUpOutlined,
  ArrowDownOutlined,
  PlusCircleOutlined,
  SafetyOutlined,
  DollarOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'

const router = useRouter()

// ===== KPI 数据 =====
// TODO: 对接真实API
const kpi = ref({
  totalVehicles: 86,
  newThisMonth: 3,
  availableVehicles: 52,
  availableRate: 60,
  rentedVehicles: 28,
  rentalRate: 33,
  maintenanceVehicles: 6,
  urgentMaintenance: 2,
})

// ===== 快捷操作 =====
const quickActions = [
  { key: 'register', label: '车辆登记', icon: PlusCircleOutlined, color: 'var(--text-2)', route: '/vehicle/list' },
  { key: 'maintenance', label: '维护记录', icon: ToolOutlined, color: 'var(--color-success)', route: '/vehicle/maintenance' },
  { key: 'insurance', label: '保险管理', icon: SafetyOutlined, color: 'var(--color-warning)', route: '/vehicle/insurance' },
  { key: 'rental', label: '租赁收费', icon: DollarOutlined, color: 'var(--biz-waybill)', route: '/vehicle/rental' },
]

// ===== 车辆状态分布 =====
// TODO: 对接真实API
const statusDistribution = ref([
  { label: '可用', count: 52, percent: 60, color: 'var(--color-success)' },
  { label: '出租中', count: 28, percent: 33, color: 'var(--color-warning)' },
  { label: '维修中', count: 4, percent: 5, color: 'var(--color-danger)' },
  { label: '报废', count: 2, percent: 2, color: '#d9d9d9' },
])

// ===== 近期提醒 =====
// TODO: 对接真实API
const reminders = ref([
  { label: '保险即将到期', desc: '3辆车保险将在30天内到期', count: 3, color: 'var(--color-danger)', route: '/vehicle/insurance' },
  { label: '年检即将到期', desc: '2辆车年检将在30天内到期', count: 2, color: 'var(--color-warning)', route: '/vehicle/list' },
  { label: '待维护车辆', desc: '6辆车需要进行定期维护', count: 6, color: 'var(--color-info)', route: '/vehicle/maintenance' },
  { label: '租赁到期', desc: '4辆车租赁合同即将到期', count: 4, color: 'var(--color-info)', route: '/vehicle/rental' },
])

onMounted(async () => {
  try {
    // TODO: 对接真实API，加载KPI数据、车辆状态、提醒数据
    // const data = await getVehicleDashboard()
    // kpi.value = data.kpi
    // statusDistribution.value = data.statusDistribution
    // reminders.value = data.reminders
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
