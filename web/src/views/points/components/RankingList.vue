<template>
  <div class="ranking-list">
    <div
      v-for="item in rankings"
      :key="item.userId"
      class="ranking-item"
      :class="{ 'ranking-item--me': item.userId === myUserId }"
    >
      <div class="ranking-item__rank">
        <template v-if="item.rank <= 3">
          <TrophyOutlined :style="{ color: medalColors[item.rank - 1], fontSize: '20px' }" />
        </template>
        <template v-else>
          <span class="ranking-item__rank-num">{{ item.rank }}</span>
        </template>
      </div>
      <a-avatar class="ranking-item__avatar" :size="36">
        {{ (item.userName || '?').charAt(0) }}
      </a-avatar>
      <div class="ranking-item__info">
        <div class="ranking-item__name">
          {{ item.userName }}
          <a-tag v-if="item.userId === myUserId" color="blue" size="small">我</a-tag>
        </div>
        <div class="ranking-item__dept">{{ item.departmentName }}</div>
      </div>
      <div class="ranking-item__points">
        <span class="ranking-item__points-value">{{ item.totalPoints }}</span>
        <span class="ranking-item__points-label">积分</span>
      </div>
    </div>
    <a-empty v-if="!rankings?.length" description="暂无排名数据" />
  </div>
</template>

<script setup lang="ts">
import { TrophyOutlined } from '@ant-design/icons-vue'
import type { RankingListDto } from '@/types/points'

defineProps<{
  rankings: RankingListDto[]
  myUserId?: number
}>()

const medalColors = ['#faad14', '#bfbfbf', '#d48806']
</script>

<style scoped lang="scss">
.ranking-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.ranking-item {
  display: flex;
  align-items: center;
  padding: 12px 16px;
  border-radius: 8px;
  background: #fafafa;
  transition: all 0.2s;

  &:hover {
    background: #f0f5ff;
  }

  &--me {
    background: var(--color-info-light);
    border: 1px solid var(--color-info);
  }

  &__rank {
    width: 36px;
    text-align: center;
    flex-shrink: 0;
  }

  &__rank-num {
    font-size: 16px;
    font-weight: 600;
    color: #8c8c8c;
  }

  &__avatar {
    margin-left: 8px;
    flex-shrink: 0;
    background: var(--color-info);
  }

  &__info {
    margin-left: 12px;
    flex: 1;
    min-width: 0;
  }

  &__name {
    font-size: 14px;
    font-weight: 500;
    color: #1a1a1a;
    display: flex;
    align-items: center;
    gap: 6px;
  }

  &__dept {
    font-size: 12px;
    color: #8c8c8c;
    margin-top: 2px;
  }

  &__points {
    text-align: right;
    flex-shrink: 0;
    margin-left: 12px;
  }

  &__points-value {
    font-size: 20px;
    font-weight: 700;
    color: var(--color-info);
  }

  &__points-label {
    font-size: 12px;
    color: #8c8c8c;
    margin-left: 4px;
  }
}
</style>
