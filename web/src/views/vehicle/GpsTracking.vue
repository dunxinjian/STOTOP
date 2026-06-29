<template>
  <div class="page-container">
    <PageHeader title="GPS车辆追踪" description="实时追踪车辆位置与轨迹">
    </PageHeader>

    <a-card :bordered="false">
      <a-form layout="inline" style="margin-bottom: 16px">
        <a-form-item label="选择车辆">
          <a-select
            v-model:value="selectedVehicleId"
            placeholder="请选择车辆"
            allow-clear
            show-search
            :filter-option="filterVehicleOption"
            :options="vehicleOptions"
            style="width: 250px"
          />
        </a-form-item>
      </a-form>

      <!-- 占位区域 -->
      <div class="gps-placeholder">
        <a-result
          title="GPS 功能正在建设中，敬请期待"
          sub-title="该功能将支持车辆实时定位和历史轨迹查看，待对接第三方GPS平台后开放使用。"
        >
          <template #icon>
            <EnvironmentOutlined style="color: var(--color-info)" />
          </template>
          <template #extra>
            <a-button type="primary" disabled>
              功能开发中
            </a-button>
          </template>
        </a-result>
      </div>

      <!-- 说明文字 -->
      <a-alert
        type="info"
        show-icon
        style="margin-top: 16px"
      >
        <template #message>
          <p style="margin: 0">
            该功能将支持以下能力：
          </p>
          <ul style="margin: 8px 0 0 0; padding-left: 20px">
            <li>车辆实时定位查看</li>
            <li>历史轨迹回放</li>
            <li>电子围栏设置</li>
            <li>异常告警通知</li>
          </ul>
        </template>
      </a-alert>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { EnvironmentOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getVehicleList,
  type VehicleListItemDto,
} from '@/api/vehicle'

// 车辆选择
const selectedVehicleId = ref<number | undefined>()
const vehicleOptions = ref<{ label: string; value: number }[]>([])

// 筛选车辆
function filterVehicleOption(input: string, option: any) {
  return option.label.toLowerCase().includes(input.toLowerCase())
}

// 获取车辆列表
async function fetchVehicleList() {
  try {
    const res = await getVehicleList({
      pageIndex: 1,
      pageSize: 1000,
    })
    if (res) {
      vehicleOptions.value = (res.items || []).map((v: VehicleListItemDto) => ({
        label: `${v.code}${v.plateNumber ? ` (${v.plateNumber})` : ''}`,
        value: v.id,
      }))
    }
  } catch (error) {
    console.error('获取车辆列表失败:', error)
  }
}

onMounted(() => {
  fetchVehicleList()
})
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.gps-placeholder {
  height: 500px;
  background-color: #f5f5f5;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
