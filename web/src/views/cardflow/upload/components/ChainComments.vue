<template>
  <div class="comment-section">
    <div class="comment-title">
      批次评论{{ comments.length ? ` (${comments.length})` : '' }}
    </div>

    <!-- 评论列表 -->
    <div v-if="comments.length > 0" class="comment-list">
      <div v-for="(c, ci) in comments" :key="ci" class="comment-item">
        <span class="comment-avatar" :style="{ background: getAvatarColor(c.author) }">
          {{ c.author?.[0] || '?' }}
        </span>
        <div class="comment-body">
          <div class="comment-header">
            <span class="comment-author">{{ c.author }}</span>
            <span class="comment-time">{{ c.time }}</span>
          </div>
          <div class="comment-text">{{ c.text }}</div>
        </div>
      </div>
    </div>
    <div v-else class="comment-empty">暂无评论</div>

    <!-- 发布评论 -->
    <div class="comment-input-area">
      <a-input
        v-model:value="newComment"
        :placeholder="`以${currentUserName}身份评论...`"
        size="small"
        @pressEnter="handleSend"
      />
      <a-button type="primary" size="small" @click="handleSend" :disabled="!newComment.trim()">
        发送
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { CommentItem } from '../composables/useUploadCenter'

const props = defineProps<{
  comments: CommentItem[]
  currentUserName: string
}>()

const emit = defineEmits<{
  addComment: [text: string]
}>()

const newComment = ref('')

function handleSend() {
  const text = newComment.value.trim()
  if (!text) return
  emit('addComment', text)
  newComment.value = ''
}

// avatar categorical palette — JS 内联 style 不解析 var()，保留真 hex
const AVATAR_FALLBACK = '#8c8c8c'
const AVATAR_COLORS = ['#5B7290', '#6BA292', '#C99A6B', '#9B8AB8', '#C77B6B', '#8FB07E']

function getAvatarColor(name: string): string {
  if (!name) return AVATAR_FALLBACK
  let hash = 0
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash)
  }
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length]
}
</script>

<style scoped lang="scss">
.comment-section {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid var(--border);
}

.comment-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-1);
  margin-bottom: 8px;
}

.comment-list {
  max-height: 240px;
  overflow-y: auto;
}

.comment-item {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
  padding: 8px 10px;
  background: var(--bg-muted);
  border-radius: 6px;
}

.comment-avatar {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  color: var(--text-on-accent);
  flex-shrink: 0;
  margin-top: 2px;
}

.comment-body {
  flex: 1;
  min-width: 0;
}

.comment-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.comment-author {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-1);
}

.comment-time {
  font-size: 12px;
  color: var(--text-3);
}

.comment-text {
  font-size: 13px;
  color: var(--text-2);
  margin-top: 4px;
  word-break: break-word;
}

.comment-empty {
  font-size: 13px;
  color: var(--text-3);
  padding: 8px 0;
}

.comment-input-area {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}
</style>
