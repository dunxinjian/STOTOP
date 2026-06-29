<template>
  <div class="comment-reactions">
    <!-- 已有反应 -->
    <span
      v-for="reaction in reactions"
      :key="reaction.emojiCode"
      class="reaction-item"
      :class="{ 'reaction-item--active': reaction.hasReacted }"
      @click="handleToggle(reaction.emojiCode)"
    >
      {{ reaction.emojiCode }} {{ reaction.count }}
    </span>
    <!-- 添加反应按钮 -->
    <a-popover trigger="click" v-model:open="popoverOpen" placement="bottom">
      <template #content>
        <div class="emoji-picker">
          <span
            v-for="emoji in emojiList"
            :key="emoji"
            class="emoji-item"
            @click="handleAdd(emoji)"
          >{{ emoji }}</span>
        </div>
      </template>
      <a-button size="small" type="text" class="add-reaction-btn">
        <SmileOutlined />
      </a-button>
    </a-popover>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { message } from 'ant-design-vue'
import { SmileOutlined } from '@ant-design/icons-vue'
import { toggleCommentReaction } from '@/api/task'
import type { ReactionSummaryDto } from '@/types/task'

const props = defineProps<{
  taskId: number
  commentId: number
  reactions: ReactionSummaryDto[]
}>()

const emit = defineEmits<{
  (e: 'change', reactions: ReactionSummaryDto[]): void
}>()

const popoverOpen = ref(false)

const emojiList = ['👍', '👎', '❤️', '🎉', '😄', '😕', '🚀', '👀']

async function handleToggle(emojiCode: string) {
  try {
    const res = await toggleCommentReaction(props.taskId, props.commentId, { emojiCode })
    emit('change', res)
  } catch {
    message.error('操作失败')
  }
}

async function handleAdd(emoji: string) {
  popoverOpen.value = false
  await handleToggle(emoji)
}
</script>

<style scoped lang="scss">
.comment-reactions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 4px;
}

.reaction-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 13px;
  cursor: pointer;
  background: #f5f5f5;
  border: 1px solid transparent;
  transition: all 0.2s;

  &:hover {
    background: #e8e8e8;
  }

  &--active {
    background: var(--bg-muted);
    border-color: var(--border-strong);
    color: var(--text-1);
  }
}

.emoji-picker {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 4px;
}

.emoji-item {
  font-size: 20px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  text-align: center;
  transition: background 0.2s;

  &:hover {
    background: #f0f0f0;
  }
}

.add-reaction-btn {
  color: #8c8c8c;
}
</style>
