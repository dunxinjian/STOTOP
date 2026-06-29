<script setup lang="ts">
import { ref, computed } from 'vue'
import type { CardListDto } from '@/types/cardflow'
import {
  Popup as VanPopup,
  Search as VanSearch,
  List as VanList,
  Cell as VanCell,
  CellGroup as VanCellGroup,
  Tag as VanTag,
  Empty as VanEmpty,
  Button as VanButton,
} from 'vant'
import 'vant/es/popup/style'
import 'vant/es/search/style'
import 'vant/es/list/style'
import 'vant/es/cell/style'
import 'vant/es/cell-group/style'
import 'vant/es/tag/style'
import 'vant/es/empty/style'
import 'vant/es/button/style'
import { getAvailablePrerequisites } from '@/api/cardflow'

const props = defineProps<{
  cardId: number
  show: boolean
}>()

const emit = defineEmits<{
  (e: 'update:show', value: boolean): void
  (e: 'select', card: CardListDto): void
}>()

const keyword = ref('')
const loading = ref(false)
const finished = ref(false)
const cards = ref<CardListDto[]>([])

const filteredCards = computed(() => {
  if (!keyword.value) return cards.value
  const kw = keyword.value.toLowerCase()
  return cards.value.filter(
    c => (c.title || '').toLowerCase().includes(kw) ||
         (c.cardNumber || '').toLowerCase().includes(kw) ||
         c.flowName.toLowerCase().includes(kw)
  )
})

async function loadCards() {
  if (!props.cardId) return
  loading.value = true
  try {
    const result = await getAvailablePrerequisites(props.cardId)
    cards.value = result || []
    finished.value = true
  } catch {
    finished.value = true
  } finally {
    loading.value = false
  }
}

function onOpen() {
  cards.value = []
  keyword.value = ''
  finished.value = false
  loadCards()
}

function onSelect(card: CardListDto) {
  emit('select', card)
  emit('update:show', false)
}

function formatTime(time?: string | null): string {
  if (!time) return '-'
  const d = new Date(time)
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}
</script>

<template>
  <VanPopup
    :show="show"
    position="bottom"
    round
    :style="{ height: '70vh' }"
    @update:show="(v: boolean) => emit('update:show', v)"
    @open="onOpen"
  >
    <div class="relation-picker">
      <div class="picker-header">
        <span class="picker-title">选择关联卡片</span>
        <VanButton size="small" type="default" @click="emit('update:show', false)">关闭</VanButton>
      </div>

      <VanSearch v-model="keyword" placeholder="搜索卡片编号/标题/流程" show-action @cancel="emit('update:show', false)" />

      <VanList v-model:loading="loading" :finished="finished" finished-text="没有更多了">
        <VanCellGroup inset>
          <VanCell
            v-for="card in filteredCards"
            :key="card.id"
            is-link
            @click="onSelect(card)"
          >
            <template #title>
              <div class="card-item-title">
                <span>{{ card.title || card.cardNumber || '-' }}</span>
                <VanTag type="primary" size="medium">{{ card.flowName }}</VanTag>
              </div>
            </template>
            <template #label>
              <div class="card-item-meta">
                <span>{{ card.cardNumber }}</span>
                <span>{{ card.initiatorName }}</span>
                <span>{{ formatTime(card.submitTime) }}</span>
              </div>
            </template>
          </VanCell>
        </VanCellGroup>
        <VanEmpty v-if="finished && filteredCards.length === 0" description="未找到可关联的卡片" />
      </VanList>
    </div>
  </VanPopup>
</template>

<style scoped>
.relation-picker {
  height: 100%;
  display: flex;
  flex-direction: column;
}
.picker-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px 0;
}
.picker-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-1);
}
.card-item-title {
  display: flex;
  align-items: center;
  gap: 8px;
}
.card-item-meta {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: var(--text-3);
  margin-top: 4px;
}
</style>
