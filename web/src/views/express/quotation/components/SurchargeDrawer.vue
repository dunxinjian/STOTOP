<template>
  <a-drawer
    :open="visible"
    title="加收方案管理"
    placement="right"
    :width="480"
    @close="emit('update:visible', false)"
  >
    <a-spin :spinning="loading" tip="加载中...">
      <div class="surcharge-content">
        <!-- 已关联方案 -->
        <div class="section">
          <div class="section-title">已关联方案 ({{ linkedSchemes.length }})</div>
          <div class="scheme-list" v-if="linkedSchemes.length > 0">
            <div v-for="s in linkedSchemes" :key="s.id" class="scheme-item linked" @dblclick="handleViewDetail(s.id)">
              <div class="scheme-info">
                <span class="scheme-name">{{ s.name }}</span>
                <span class="scheme-desc">{{ s.description }}</span>
              </div>
              <a-button type="text" size="small" danger @click="handleUnlink(s.id)">
                取消关联
              </a-button>
            </div>
          </div>
          <a-empty v-else :image="null" description="暂无已关联方案" />
        </div>

        <!-- 可选方案 -->
        <div class="section">
          <div class="section-title">可选方案</div>
          <div class="scheme-list" v-if="availableSchemes.length > 0">
            <div v-for="s in availableSchemes" :key="s.id" class="scheme-item available" @dblclick="handleViewDetail(s.id)">
              <div class="scheme-info">
                <span class="scheme-name">{{ s.name }}</span>
                <span class="scheme-desc">{{ s.description }}</span>
              </div>
              <a-button type="link" size="small" @click="handleLink(s.id)">
                关联
              </a-button>
            </div>
          </div>
          <a-empty v-else :image="null" description="暂无可选方案" />
        </div>
      </div>
    </a-spin>
  </a-drawer>

  <!-- 方案详情 Modal -->
  <a-modal
    v-model:open="detailModalVisible"
    title="加收方案详情"
    :width="740"
    :footer="null"
    :destroyOnClose="true"
  >
    <a-spin :spinning="detailLoading">
      <template v-if="detailData">
        <a-descriptions :column="2" bordered size="small" class="detail-descriptions">
          <a-descriptions-item label="名称">{{ detailData.name }}</a-descriptions-item>
          <a-descriptions-item label="加收类型">{{ surchargeTypeMap[detailData.surchargeType] || '未知' }}</a-descriptions-item>
          <a-descriptions-item label="生效日期">{{ detailData.effectiveDate?.slice(0, 10) }}</a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="detailData.isActive ? 'green' : 'default'">{{ detailData.isActive ? '启用' : '停用' }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="备注">{{ detailData.remark || '-' }}</a-descriptions-item>
        </a-descriptions>

        <div class="detail-items-title">配置项明细</div>
        <a-table
          :dataSource="detailData.items"
          :columns="detailColumns"
          :pagination="false"
          size="small"
          rowKey="sortOrder"
          bordered
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'calcMethod'">
              {{ calcMethodMap[record.calcMethod] || '-' }}
            </template>
            <template v-else-if="column.dataIndex === 'weightRange'">
              <template v-if="record.weightFrom != null || record.weightTo != null">
                {{ record.weightFrom ?? 0 }}~{{ record.weightTo ?? '∞' }} kg
                <span v-if="record.weightType" class="weight-type-tag">({{ weightTypeMap[record.weightType] || '' }})</span>
              </template>
              <template v-else>-</template>
            </template>
            <template v-else-if="column.dataIndex === 'dailyVolume'">
              <template v-if="record.dailyVolumeFrom != null || record.dailyVolumeTo != null">
                {{ record.dailyVolumeFrom ?? 0 }}~{{ record.dailyVolumeTo ?? '∞' }} 单
              </template>
              <template v-else>-</template>
            </template>
            <template v-else-if="column.dataIndex === 'amount'">
              {{ record.amount != null ? record.amount : '-' }}
            </template>
            <template v-else-if="column.dataIndex === 'destinations'">
              <template v-if="record.destinations && record.destinations.length > 0">
                <a-tag v-for="(d, idx) in record.destinations" :key="idx" size="small">
                  {{ d.cityName || `省份${d.provinceId}` }}
                </a-tag>
              </template>
              <template v-else>-</template>
            </template>
          </template>
        </a-table>
      </template>
    </a-spin>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { getPriceSurcharges, getPriceSurchargeDetail, type PriceSurchargeListItem, type PriceSurchargeDetail } from '@/api/express'

export interface SurchargeScheme {
  id: number | string
  name: string
  description?: string
}

const props = withDefaults(defineProps<{
  visible: boolean
  linkedIds?: (number | string)[]
}>(), {
  linkedIds: () => [],
})

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
  (e: 'link', id: number | string): void
  (e: 'unlink', id: number | string): void
}>()

// 加收类型映射
const surchargeTypeMap: Record<string, string> = {
  '1': '电商大促',
  '2': '春节涨价',
  '3': '目的地加收',
  '4': '旺季加收',
  '5': '偏远加收',
  '6': '超规加收',
  '7': '其他',
}

const loading = ref(false)
const allSchemes = ref<SurchargeScheme[]>([])

// Drawer 打开时加载数据
watch(() => props.visible, async (val) => {
  if (val) {
    await fetchSurcharges()
  }
})

async function fetchSurcharges() {
  loading.value = true
  try {
    const res = await getPriceSurcharges({ isActive: true, pageIndex: 1, pageSize: 999 })
    allSchemes.value = (res.items || []).map(mapToScheme)
  } catch {
    allSchemes.value = []
  } finally {
    loading.value = false
  }
}

function mapToScheme(item: PriceSurchargeListItem): SurchargeScheme {
  const typeName = surchargeTypeMap[item.surchargeType] ?? '加收'
  const datePart = item.effectiveDate?.slice(0, 10) ?? ''
  const desc = `${typeName} · 生效 ${datePart}`
  return { id: item.id, name: item.name ?? '', description: desc }
}

const linkedSchemes = computed(() =>
  allSchemes.value.filter(s => props.linkedIds.includes(s.id))
)

const availableSchemes = computed(() =>
  allSchemes.value.filter(s => !props.linkedIds.includes(s.id))
)

function handleLink(id: number | string) {
  emit('link', id)
}

function handleUnlink(id: number | string) {
  emit('unlink', id)
}

// ===== 详情 Modal =====
const detailModalVisible = ref(false)
const detailLoading = ref(false)
const detailData = ref<PriceSurchargeDetail | null>(null)

const calcMethodMap: Record<number, string> = {
  1: '单票',
  2: '按公斤',
  3: '按比例',
}

const weightTypeMap: Record<number, string> = {
  1: '实重',
  2: '计费重',
}

const detailColumns = [
  { title: '序号', dataIndex: 'sortOrder', width: 60, align: 'center' as const },
  { title: '计费方式', dataIndex: 'calcMethod', width: 90 },
  { title: '重量范围', dataIndex: 'weightRange', width: 150 },
  { title: '日单量', dataIndex: 'dailyVolume', width: 120 },
  { title: '金额', dataIndex: 'amount', width: 80, align: 'right' as const },
  { title: '目的地', dataIndex: 'destinations' },
]

async function handleViewDetail(id: number | string) {
  detailModalVisible.value = true
  detailLoading.value = true
  detailData.value = null
  try {
    detailData.value = await getPriceSurchargeDetail(Number(id))
  } catch {
    detailData.value = null
  } finally {
    detailLoading.value = false
  }
}
</script>

<style scoped lang="scss">
@use '@/styles/variables.scss' as *;

.surcharge-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.section {
  .section-title {
    font-size: $font-size-base;
    font-weight: 500;
    color: $text-primary;
    margin-bottom: 12px;
  }
}

.scheme-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.scheme-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border-radius: $border-radius-md;
  border: 1px solid $border-color-light;
  transition: $transition-base;
  cursor: pointer;
  user-select: none;

  &:hover {
    border-color: $color-primary;
    background: $color-primary-bg;
  }

  &.linked {
    background: var(--color-success-light);
    border-color: var(--color-success);
  }

  .scheme-info {
    display: flex;
    flex-direction: column;
    gap: 2px;
    min-width: 0;
    flex: 1;

    .scheme-name {
      font-size: $font-size-base;
      color: $text-primary;
    }

    .scheme-desc {
      font-size: $font-size-sm;
      color: $text-secondary;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  }
}

.detail-descriptions {
  margin-bottom: 16px;
}

.detail-items-title {
  font-size: $font-size-base;
  font-weight: 500;
  color: $text-primary;
  margin-bottom: 8px;
}

.weight-type-tag {
  font-size: $font-size-sm;
  color: $text-secondary;
}
</style>
