<template>
  <div class="event-workbench">
    <PageHeader :title="eventData?.name || '活动详情'">
      <template #actions>
        <a-badge :count="alertCount" :offset="[-6, 2]">
          <a-button type="text" @click="activePanel = 'dashboard'">
            <template #icon><DashboardOutlined /></template>
          </a-button>
        </a-badge>
      </template>
    </PageHeader>

    <a-layout class="workbench-layout">
      <a-layout-sider
        v-model:collapsed="collapsed"
        :width="200"
        :collapsed-width="48"
        collapsible
        theme="light"
        class="workbench-sider"
      >
        <a-menu
          v-model:selectedKeys="selectedKeys"
          mode="inline"
          @click="handleMenuClick"
        >
          <a-menu-item v-for="item in navItems" :key="item.key">
            <template #icon>
              <component :is="item.icon" />
            </template>
            <span>{{ item.label }}</span>
            <a-badge
              v-if="item.key === 'dashboard' && alertCount > 0"
              :count="alertCount"
              :number-style="{ fontSize: '10px', minWidth: '16px', height: '16px', lineHeight: '16px' }"
              style="margin-left: 8px;"
            />
          </a-menu-item>
        </a-menu>
      </a-layout-sider>

      <a-layout-content class="workbench-content">
        <component
          :is="currentPanel"
          v-if="currentPanel"
          :event-id="eventId"
          :event-data="eventData"
          @navigate="handlePanelNavigate"
          @updated="refreshEventData"
        />
        <a-empty v-else description="未知面板" />
      </a-layout-content>
    </a-layout>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, h, defineAsyncComponent } from 'vue'
import { useRoute } from 'vue-router'
import {
  DashboardOutlined,
  EditOutlined,
  UserOutlined,
  CalendarOutlined,
  CarOutlined,
  FileTextOutlined,
  HomeOutlined,
  CoffeeOutlined,
  TableOutlined,
  ShoppingOutlined,
  DollarOutlined,
  GiftOutlined,
  OrderedListOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { getEvent, getEventAlerts } from '@/api/conference'
import type { EventDto, AlertItemDto } from '@/api/conference'

const route = useRoute()

const navItems = computed(() => {
  const isWedding = eventData.value?.type === 'wedding'
  const baseItems = [
    { key: 'basic-info', icon: h(EditOutlined), label: '基本信息' },
    { key: 'dashboard', icon: h(DashboardOutlined), label: '总览' },
    { key: 'attendee', icon: h(UserOutlined), label: isWedding ? '宾客管理' : '参会人员' },
    { key: 'schedule', icon: h(CalendarOutlined), label: '日程安排' },
    { key: 'transport', icon: h(CarOutlined), label: '接送调度' },
    { key: 'vehicle-schedule', icon: h(FileTextOutlined), label: '车辆日程' },
    { key: 'accommodation', icon: h(HomeOutlined), label: '住宿管理' },
    { key: 'meal', icon: h(CoffeeOutlined), label: '餐食安排' },
    { key: 'table', icon: h(TableOutlined), label: '桌次编排' },
    { key: 'material', icon: h(ShoppingOutlined), label: '物品管理' },
    { key: 'finance', icon: h(DollarOutlined), label: '收入登记' },
  ]

  if (isWedding) {
    // 在"宾客管理"后面插入"礼金登记"
    const attendeeIndex = baseItems.findIndex(i => i.key === 'attendee')
    baseItems.splice(attendeeIndex + 1, 0,
      { key: 'gift', icon: h(GiftOutlined), label: '礼金登记' }
    )
    // 在"日程安排"后面插入"仪式流程"
    const scheduleIndex = baseItems.findIndex(i => i.key === 'schedule')
    baseItems.splice(scheduleIndex + 1, 0,
      { key: 'rundown', icon: h(OrderedListOutlined), label: '仪式流程' }
    )
  }

  return baseItems
})

const panelComponents: Record<string, any> = {
  'basic-info': defineAsyncComponent(() => import('./panels/BasicInfoPanel.vue')),
  'dashboard': defineAsyncComponent(() => import('./panels/DashboardPanel.vue')),
  'attendee': defineAsyncComponent(() => import('./panels/AttendeePanel.vue')),
  'schedule': defineAsyncComponent(() => import('./panels/SchedulePanel.vue')),
  'transport': defineAsyncComponent(() => import('./panels/TransportPanel.vue')),
  'vehicle-schedule': defineAsyncComponent(() => import('./panels/VehicleSchedulePanel.vue')),
  'accommodation': defineAsyncComponent(() => import('./panels/AccommodationPanel.vue')),
  'meal': defineAsyncComponent(() => import('./panels/MealPanel.vue')),
  'table': defineAsyncComponent(() => import('./panels/TablePanel.vue')),
  'material': defineAsyncComponent(() => import('./panels/MaterialPanel.vue')),
  'finance': defineAsyncComponent(() => import('./panels/FinancePanel.vue')),
  'gift': defineAsyncComponent(() => import('./panels/GiftPanel.vue')),
  'rundown': defineAsyncComponent(() => import('./panels/RundownPanel.vue')),
}

const collapsed = ref(false)
const activePanel = ref('dashboard')
const selectedKeys = ref<string[]>(['dashboard'])
const eventId = ref(0)
const eventData = ref<EventDto>()
const alertCount = ref(0)

const currentPanel = computed(() => panelComponents[activePanel.value])

function handleMenuClick({ key }: { key: string | number }) {
  const k = String(key)
  activePanel.value = k
  selectedKeys.value = [k]
}

function handlePanelNavigate(panel: string) {
  if (panelComponents[panel]) {
    activePanel.value = panel
    selectedKeys.value = [panel]
  }
}

async function refreshEventData() {
  try {
    eventData.value = await getEvent(eventId.value) as any
  } catch { /* ignore */ }
}

onMounted(async () => {
  eventId.value = parseInt(route.params.id as string)
  await refreshEventData()
  try {
    const alerts = await getEventAlerts(eventId.value) as any
    const list: AlertItemDto[] = alerts?.items ?? alerts ?? []
    alertCount.value = list.filter((a) => a.level === 'Critical').length
  } catch { /* ignore */ }
})
</script>

<style scoped lang="scss">
.event-workbench {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.workbench-layout {
  flex: 1;
  background: transparent;
}

.workbench-sider {
  border-right: 1px solid #f0f0f0;
  background: #fff;
}

.workbench-content {
  padding: 16px;
  background: transparent;
  overflow: auto;
}
</style>
