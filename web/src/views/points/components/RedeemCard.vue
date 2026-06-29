<template>
  <a-card hoverable class="redeem-card" :class="{ 'redeem-card--disabled': !canExchange }">
    <template #cover>
      <div class="redeem-card__cover">
        <img v-if="item.image" :src="item.image" :alt="item.name" />
        <div v-else class="redeem-card__placeholder">
          <GiftOutlined style="font-size: 48px; color: #d9d9d9" />
        </div>
      </div>
    </template>
    <a-card-meta>
      <template #title>
        <div class="redeem-card__title">{{ item.name }}</div>
      </template>
      <template #description>
        <div class="redeem-card__info">
          <div class="redeem-card__points">
            <span class="points-value">{{ item.requiredPoints }}</span>
            <span class="points-label">积分</span>
          </div>
          <div class="redeem-card__stock">
            <a-tag v-if="item.stock <= 0" color="red">已兑完</a-tag>
            <span v-else class="stock-text">剩余 {{ item.stock }} 件</span>
          </div>
        </div>
      </template>
    </a-card-meta>
    <template #actions>
      <a-tooltip v-if="!canExchange" :title="disableReason">
        <a-button type="primary" size="small" disabled block>
          {{ item.stock <= 0 ? '已兑完' : '积分不足' }}
        </a-button>
      </a-tooltip>
      <a-button v-else type="primary" size="small" block @click="handleExchange">
        立即兑换
      </a-button>
    </template>
  </a-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { GiftOutlined } from '@ant-design/icons-vue'
import type { RedeemItemListDto } from '@/types/points'

const props = defineProps<{
  item: RedeemItemListDto
  myPoints: number
}>()

const emit = defineEmits<{
  (e: 'exchange', itemId: number): void
}>()

const canExchange = computed(() => {
  return props.item.stock > 0 && props.myPoints >= props.item.requiredPoints
})

const disableReason = computed(() => {
  if (props.item.stock <= 0) return '库存不足'
  if (props.myPoints < props.item.requiredPoints)
    return `还需 ${props.item.requiredPoints - props.myPoints} 积分`
  return ''
})

function handleExchange() {
  emit('exchange', props.item.id)
}
</script>

<style scoped lang="scss">
.redeem-card {
  border-radius: 8px;
  overflow: hidden;
  transition: transform 0.2s, box-shadow 0.2s;

  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  }

  &--disabled {
    opacity: 0.7;
  }

  &__cover {
    height: 160px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #fafafa;
    overflow: hidden;

    img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
  }

  &__placeholder {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 100%;
    height: 100%;
  }

  &__title {
    font-size: 14px;
    font-weight: 500;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__info {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-top: 4px;
  }

  &__points {
    .points-value {
      font-size: 18px;
      font-weight: 700;
      color: var(--color-danger);
    }
    .points-label {
      font-size: 12px;
      color: #999;
      margin-left: 4px;
    }
  }

  &__stock {
    .stock-text {
      font-size: 12px;
      color: #999;
    }
  }
}
</style>
