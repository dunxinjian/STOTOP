<template>
  <div class="city-price-matrix-wrapper">
    <!-- 城市加收列表 -->
    <div class="city-list">
      <!-- 已有城市行 -->
      <div
        v-for="row in rows"
        :key="row.cityId"
        class="city-row"
      >
        <div class="city-row-name">{{ row.cityName }}</div>
        <div class="city-row-price">
          <a-input-number
            :value="getFixedPrice(row)"
            size="small"
            :precision="2"
            :min="0"
            placeholder="0.00"
            :controls="false"
            style="width: 100px"
            @change="(val: ValueType) => handleFixedPriceChange(row.cityId, val == null ? null : Number(val))"
          />
          <span class="price-unit">元</span>
        </div>
        <a-button
          type="text"
          size="small"
          class="city-row-delete"
          @click="emit('removeCity', row.cityId)"
        >
          <template #icon><CloseOutlined /></template>
        </a-button>
      </div>

      <!-- 新增行：内联添加 -->
      <div class="city-row city-row-add">
        <div class="city-row-name">
          <a-select
            v-model:value="selectedCity"
            show-search
            :filter-option="false"
            placeholder="输入城市名搜索..."
            size="small"
            style="width: 100%"
            :options="cityOptions"
            :loading="searching"
            @search="handleCitySearch"
            @select="(val: SelectValue) => handleCitySelect(Number(val))"
            allow-clear
            :not-found-content="searching ? '搜索中...' : '无匹配城市'"
          />
        </div>
        <div class="city-row-price">
          <a-input-number
            v-model:value="newPrice"
            size="small"
            :precision="2"
            :min="0"
            placeholder="单价"
            :controls="false"
            style="width: 100px"
            @press-enter="handleAddWithPrice"
          />
          <span class="price-unit">元</span>
        </div>
        <a-button
          type="text"
          size="small"
          class="city-row-confirm"
          :disabled="!selectedCity"
          @click="handleAddWithPrice"
        >
          <template #icon><PlusOutlined /></template>
        </a-button>
      </div>
    </div>

    <!-- 空状态提示 -->
    <div v-if="rows.length === 0" class="empty-hint">
      <span>在上方输入城市名称并设置加收单价，按 Enter 快速添加</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { CloseOutlined, PlusOutlined } from '@ant-design/icons-vue'
import type { ValueType } from 'ant-design-vue/es/input-number/src/utils/MiniDecimal'
import type { SelectValue } from 'ant-design-vue/es/select'
import type { WeightSegment, PriceCell } from '@/views/express/quotation/composables/usePriceMatrix'
import { getCityList, type CityDto } from '@/api/express'

export interface CityRow {
  cityId: number
  cityName: string
  provinceId: number
  prices: Record<number, PriceCell>
}

const props = defineProps<{
  segments?: WeightSegment[]
  rows: CityRow[]
}>()

const emit = defineEmits<{
  cellChange: [cityId: number, segmentIndex: number, cell: PriceCell]
  addCity: [city: CityDto]
  removeCity: [cityId: number]
}>()

// ==================== 城市搜索 ====================

const selectedCity = ref<number | undefined>(undefined)
const newPrice = ref<number | undefined>(undefined)
const cityOptions = ref<{ label: string; value: number; disabled?: boolean }[]>([])
const searching = ref(false)
let searchTimer: ReturnType<typeof setTimeout> | null = null

// 缓存最近一次搜索结果，供 select 时查找完整 CityDto
const lastSearchResult = ref<CityDto[]>([])

function handleCitySearch(keyword: string) {
  if (searchTimer) clearTimeout(searchTimer)
  if (!keyword?.trim()) {
    cityOptions.value = []
    lastSearchResult.value = []
    return
  }
  searchTimer = setTimeout(async () => {
    searching.value = true
    try {
      const list = await getCityList(keyword.trim())
      lastSearchResult.value = list
      const addedIds = new Set(props.rows.map(r => r.cityId))
      cityOptions.value = list.map(c => ({
        label: `${c.cityName}（${c.provinceName}）`,
        value: c.id,
        disabled: addedIds.has(c.id),
      }))
    } catch {
      cityOptions.value = []
      lastSearchResult.value = []
    } finally {
      searching.value = false
    }
  }, 300)
}

function handleCitySelect(cityId: number) {
  const opt = cityOptions.value.find(o => o.value === cityId)
  if (!opt || opt.disabled) return
  // 不立即添加，等用户点击确认或按Enter（带价格一起添加）
  selectedCity.value = cityId
}

function handleAddWithPrice() {
  if (!selectedCity.value) return
  const match = lastSearchResult.value.find(c => c.id === selectedCity.value)
  if (!match) return

  emit('addCity', match)

  // 如果填了价格，同时设置价格
  if (newPrice.value != null && newPrice.value > 0) {
    // 需要在下一个tick等addCity处理完再设置价格
    const cityId = match.id
    const price = newPrice.value
    setTimeout(() => {
      emit('cellChange', cityId, 0, {
        segmentIndex: 0,
        provinceId: 0,
        basePrice: price,
        firstWeight: 0,
        continuePrice: 0,
        continueStep: 1,
        roundingMethodOverride: null,
        truncParamOverride: null,
        ceilParamOverride: null,
      })
    }, 0)
  }

  // 重置输入
  selectedCity.value = undefined
  newPrice.value = undefined
  cityOptions.value = []
}

// ==================== 价格操作 ====================

function getFixedPrice(row: CityRow): number | undefined {
  const cell = row.prices[0]
  if (!cell) return undefined
  const val = cell.basePrice
  return val == null || val === 0 ? undefined : val
}

function handleFixedPriceChange(cityId: number, value: number | null) {
  const row = props.rows.find(r => r.cityId === cityId)
  if (!row) return
  const existing = row.prices[0] ?? {
    segmentIndex: 0,
    provinceId: 0,
    basePrice: 0,
    firstWeight: 0,
    continuePrice: 0,
    continueStep: 1,
    roundingMethodOverride: null,
    truncParamOverride: null,
    ceilParamOverride: null,
  }
  emit('cellChange', cityId, 0, {
    ...existing,
    basePrice: value ?? 0,
  })
}
</script>

<style scoped lang="scss">
.city-price-matrix-wrapper {
  width: 100%;
}

.city-list {
  background: #fff;
  border-radius: 8px;
  border: 1px solid #e8eaed;
  overflow: hidden;
}

.city-row {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  border-bottom: 1px solid #f0f0f0;
  transition: background 0.15s;

  &:hover {
    background: #fafafa;
  }

  &:last-child {
    border-bottom: none;
  }
}

.city-row-name {
  flex: 1;
  font-size: 13px;
  font-weight: 500;
  color: #262626;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.city-row-price {
  display: flex;
  align-items: center;
  gap: 4px;
  margin: 0 12px;

  .price-unit {
    font-size: 12px;
    color: #8c8c8c;
  }

  :deep(.ant-input-number) {
    border-radius: 4px;
  }

  :deep(.ant-input-number-input) {
    text-align: right;
    font-size: 13px;
    padding-right: 6px;
  }
}

.city-row-delete {
  color: #bfbfbf;
  flex-shrink: 0;

  &:hover {
    color: var(--color-danger);
  }
}

.city-row-confirm {
  color: var(--color-primary);
  flex-shrink: 0;

  &:hover:not(:disabled) {
    color: var(--color-primary-hover);
  }

  &:disabled {
    color: #d9d9d9;
  }
}

.city-row-add {
  background: #fafbfc;

  .city-row-name {
    :deep(.ant-select-selector) {
      border-color: transparent !important;
      background: transparent !important;
      box-shadow: none !important;
    }

    :deep(.ant-select-focused .ant-select-selector) {
      border-color: var(--color-primary) !important;
      background: #fff !important;
    }
  }
}

.empty-hint {
  text-align: center;
  padding: 12px 0 4px;
  font-size: 12px;
  color: #bfbfbf;
}
</style>
