<template>
  <div
    class="room-card"
    :class="[`room-card--${statusClass}`, { 'room-card--draggable': draggable }]"
    @click="emit('click', $event)"
    @dragover.prevent
    @drop="handleDrop"
  >
    <!-- Header: room number + type -->
    <div class="room-card__header">
      <span class="room-card__number">{{ room.roomNumber }}</span>
      <a-tag :color="roomTypeColor" size="small">{{ room.roomType || '标间' }}</a-tag>
    </div>

    <!-- Guest avatars -->
    <div class="room-card__guests">
      <template v-if="room.guests.length">
        <a-tooltip
          v-for="guest in room.guests"
          :key="guest.id"
          :title="`${guest.name}${guest.company ? ' · ' + guest.company : ''}`"
        >
          <a-avatar
            :size="28"
            :style="{ backgroundColor: getAvatarColor(guest.company || guest.name), cursor: 'default' }"
          >
            {{ getInitial(guest.name) }}
          </a-avatar>
        </a-tooltip>
      </template>
      <span v-else class="room-card__empty">空闲</span>
    </div>

    <!-- Capacity info -->
    <div class="room-card__info">
      {{ room.guests.length }} / {{ room.maxGuests }}
    </div>

    <!-- Status bar -->
    <div class="room-card__status-bar" :style="{ backgroundColor: statusBarColor }" />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

export interface GuestData {
  id: number
  name: string
  company?: string
  gender?: string
}

export interface RoomData {
  id: number
  roomNumber: string
  roomType: string
  status: string
  guests: GuestData[]
  maxGuests: number
}

interface Props {
  /** 房间数据 */
  room: RoomData
  /** 是否支持拖入 */
  draggable?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  draggable: true,
})

const emit = defineEmits<{
  click: [e: MouseEvent]
  drop: [roomId: number, event: DragEvent]
}>()

const roomTypeColor = computed(() => {
  switch (props.room.roomType) {
    case '套房': return 'purple'
    case '大床房': return 'orange'
    default: return 'blue'
  }
})

const statusClass = computed(() => {
  switch (props.room.status) {
    case '空闲': return 'free'
    case '已分配': return 'assigned'
    case '已入住': return 'checked-in'
    case '已退房': return 'checked-out'
    default: return 'free'
  }
})

const statusBarColor = computed(() => {
  const gs = props.room.guests.length
  const max = props.room.maxGuests
  if (gs === 0) return 'var(--color-success)'       // 空闲绿
  if (gs >= max) return 'var(--color-info)'       // 已满（信息）
  if (gs > 0 && gs < max) return 'var(--color-warning)' // 部分（警告）
  return '#d9d9d9'                      // 已退灰
})

function getInitial(name: string): string {
  return name ? name.charAt(name.length - 1) : '?'
}

/** 根据字符串哈希生成头像背景色 */
function getAvatarColor(str: string): string {
  const colors = [
    '#5B7290', '#6BA292', '#C99A6B', '#9B8AB8',
    '#C77B6B', '#8FB07E', '#7C9CB5', '#B0976A',
  ]
  let hash = 0
  for (let i = 0; i < str.length; i++) {
    hash = str.charCodeAt(i) + ((hash << 5) - hash)
  }
  return colors[Math.abs(hash) % colors.length]
}

function handleDrop(event: DragEvent) {
  if (props.draggable) {
    emit('drop', props.room.id, event)
  }
}
</script>

<style scoped lang="scss">
.room-card {
  background: #fff;
  border-radius: 8px;
  padding: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  cursor: pointer;
  transition: transform 0.2s, box-shadow 0.2s;
  position: relative;
  overflow: hidden;
  min-width: 140px;

  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
  }

  &--draggable {
    &[dragover] {
      border: 2px dashed var(--color-primary);
    }
  }

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 8px;
  }

  &__number {
    font-size: 15px;
    font-weight: 600;
    color: #262626;
  }

  &__guests {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    min-height: 28px;
    align-items: center;
    margin-bottom: 8px;
  }

  &__empty {
    color: #bfbfbf;
    font-size: 13px;
  }

  &__info {
    font-size: 12px;
    color: #8c8c8c;
    text-align: right;
  }

  &__status-bar {
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
    height: 4px;
  }
}
</style>
