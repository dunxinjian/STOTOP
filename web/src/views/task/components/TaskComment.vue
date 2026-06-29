<template>
  <div class="task-comment">
    <!-- 评论输入框 -->
    <div class="task-comment__input">
      <a-textarea
        v-model:value="newContent"
        :placeholder="replyTo ? `回复 ${replyTo.userName}...` : '写下你的评论...'"
        :rows="2"
        :maxlength="1000"
      />
      <div class="task-comment__input-actions">
        <a-button v-if="replyTo" size="small" @click="cancelReply">取消回复</a-button>
        <a-button type="primary" size="small" :loading="submitting" :disabled="!newContent.trim()" @click="handleSubmit">
          {{ replyTo ? '回复' : '评论' }}
        </a-button>
      </div>
    </div>

    <!-- 评论列表 -->
    <a-spin :spinning="loading">
      <a-empty v-if="!loading && comments.length === 0" description="暂无评论" :image="false" />
      <div v-else class="task-comment__list">
        <div v-for="comment in comments" :key="comment.id" class="task-comment__item">
          <div class="task-comment__header">
            <span class="task-comment__author">{{ comment.userName }}</span>
            <span class="task-comment__time">{{ formatTime(comment.createTime) }}</span>
            <div class="task-comment__actions">
              <a-button type="text" size="small" @click="startReply(comment)">回复</a-button>
              <a-button type="text" size="small" @click="startEdit(comment)">编辑</a-button>
              <a-popconfirm title="确定删除此评论？" @confirm="handleDelete(comment.id)">
                <a-button type="text" size="small" danger>删除</a-button>
              </a-popconfirm>
            </div>
          </div>

          <!-- 编辑模式 -->
          <div v-if="editingId === comment.id" class="task-comment__edit">
            <a-textarea v-model:value="editContent" :rows="2" />
            <div class="task-comment__edit-actions">
              <a-button size="small" @click="cancelEdit">取消</a-button>
              <a-button type="primary" size="small" :loading="updating" @click="handleUpdate(comment.id)">保存</a-button>
            </div>
          </div>
          <div v-else class="task-comment__body">{{ comment.content }}</div>

          <!-- 表情反应 -->
          <CommentReactions
            :task-id="taskId"
            :comment-id="comment.id"
            :reactions="comment.reactions"
            @change="(r) => comment.reactions = r"
          />

          <!-- 子回复 -->
          <div v-if="comment.replies && comment.replies.length" class="task-comment__replies">
            <div v-for="reply in comment.replies" :key="reply.id" class="task-comment__reply">
              <div class="task-comment__header">
                <span class="task-comment__author">{{ reply.userName }}</span>
                <span class="task-comment__time">{{ formatTime(reply.createTime) }}</span>
                <div class="task-comment__actions">
                  <a-popconfirm title="确定删除此回复？" @confirm="handleDelete(reply.id)">
                    <a-button type="text" size="small" danger>删除</a-button>
                  </a-popconfirm>
                </div>
              </div>
              <div class="task-comment__body">{{ reply.content }}</div>
              <CommentReactions
                :task-id="taskId"
                :comment-id="reply.id"
                :reactions="reply.reactions"
                @change="(r) => reply.reactions = r"
              />
            </div>
          </div>
        </div>
      </div>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import {
  getTaskComments,
  createTaskComment,
  updateTaskComment,
  deleteTaskComment,
} from '@/api/task'
import type { TaskCommentListDto } from '@/types/task'
import CommentReactions from './CommentReactions.vue'
import dayjs from 'dayjs'

const props = defineProps<{
  taskId: number
}>()

const loading = ref(false)
const submitting = ref(false)
const updating = ref(false)
const comments = ref<TaskCommentListDto[]>([])
const newContent = ref('')
const replyTo = ref<TaskCommentListDto | null>(null)
const editingId = ref<number | null>(null)
const editContent = ref('')

function formatTime(time: string) {
  return dayjs(time).format('YYYY-MM-DD HH:mm')
}

async function loadComments() {
  loading.value = true
  try {
    const res = await getTaskComments(props.taskId, { pageSize: 100 })
    comments.value = res.items
  } catch {
    message.error('加载评论失败')
  } finally {
    loading.value = false
  }
}

async function handleSubmit() {
  const content = newContent.value.trim()
  if (!content) return
  submitting.value = true
  try {
    await createTaskComment(props.taskId, {
      content,
      parentCommentId: replyTo.value?.id,
    })
    newContent.value = ''
    replyTo.value = null
    message.success('评论发布成功')
    await loadComments()
  } catch {
    message.error('评论发布失败')
  } finally {
    submitting.value = false
  }
}

function startReply(comment: TaskCommentListDto) {
  replyTo.value = comment
  newContent.value = ''
}

function cancelReply() {
  replyTo.value = null
  newContent.value = ''
}

function startEdit(comment: TaskCommentListDto) {
  editingId.value = comment.id
  editContent.value = comment.content
}

function cancelEdit() {
  editingId.value = null
  editContent.value = ''
}

async function handleUpdate(commentId: number) {
  const content = editContent.value.trim()
  if (!content) return
  updating.value = true
  try {
    await updateTaskComment(props.taskId, commentId, { content })
    message.success('评论已更新')
    cancelEdit()
    await loadComments()
  } catch {
    message.error('更新失败')
  } finally {
    updating.value = false
  }
}

async function handleDelete(commentId: number) {
  try {
    await deleteTaskComment(props.taskId, commentId)
    message.success('评论已删除')
    await loadComments()
  } catch {
    message.error('删除失败')
  }
}

watch(() => props.taskId, loadComments)
onMounted(loadComments)

defineExpose({ refresh: loadComments })
</script>

<style scoped lang="scss">
.task-comment {
  &__input {
    margin-bottom: 16px;
  }

  &__input-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 8px;
  }

  &__list {
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  &__item {
    padding: 12px;
    background: #fafafa;
    border-radius: 8px;
  }

  &__header {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 6px;
  }

  &__author {
    font-size: 14px;
    font-weight: 500;
    color: #333;
  }

  &__time {
    font-size: 12px;
    color: #8c8c8c;
  }

  &__actions {
    margin-left: auto;
    display: flex;
    gap: 0;
    opacity: 0;
    transition: opacity 0.2s;

    .task-comment__item:hover &,
    .task-comment__reply:hover & {
      opacity: 1;
    }
  }

  &__body {
    font-size: 14px;
    color: #333;
    line-height: 1.6;
    white-space: pre-wrap;
  }

  &__edit {
    margin-bottom: 8px;
  }

  &__edit-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 6px;
  }

  &__replies {
    margin-top: 12px;
    padding-left: 16px;
    border-left: 2px solid #e8e8e8;
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  &__reply {
    padding: 8px;
    background: #fff;
    border-radius: 6px;
  }
}
</style>
