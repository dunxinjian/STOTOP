<template>
  <div class="attachment-upload">
    <a-upload-dragger
      :file-list="fileList"
      :custom-request="handleUpload"
      :multiple="true"
      :show-upload-list="false"
      class="attachment-upload__dragger"
    >
      <p class="ant-upload-drag-icon"><InboxOutlined /></p>
      <p class="ant-upload-text">点击或拖拽文件到此区域上传</p>
    </a-upload-dragger>

    <a-spin :spinning="loading">
      <div v-if="attachments.length > 0" class="attachment-upload__list">
        <div
          v-for="file in attachments"
          :key="file.id"
          class="attachment-upload__item"
        >
          <div class="attachment-upload__info">
            <PaperClipOutlined />
            <a :href="getDownloadUrl(file.id)" target="_blank" class="attachment-upload__name">
              {{ file.originalFileName }}
            </a>
            <span class="attachment-upload__size">{{ formatSize(file.fileSize) }}</span>
            <span class="attachment-upload__meta">
              {{ file.userName }} · {{ formatTime(file.createTime) }}
            </span>
          </div>
          <a-popconfirm title="确定删除此附件？" @confirm="handleDelete(file.id)">
            <a-button type="text" size="small" danger>
              <DeleteOutlined />
            </a-button>
          </a-popconfirm>
        </div>
      </div>
      <a-empty v-else-if="!loading" description="暂无附件" :image="false" />
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { InboxOutlined, PaperClipOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import { uploadAttachment, getAttachments, deleteAttachment, getAttachmentDownloadUrl } from '@/api/task'
import type { AttachmentListDto } from '@/types/task'
import type { UploadRequestOption } from 'ant-design-vue/es/vc-upload/interface'
import dayjs from 'dayjs'

const props = defineProps<{
  relatedType: number
  relatedId: number
}>()

const emit = defineEmits<{
  (e: 'change', attachments: AttachmentListDto[]): void
}>()

const loading = ref(false)
const attachments = ref<AttachmentListDto[]>([])
const fileList = ref<any[]>([])

function formatSize(bytes: number) {
  if (bytes < 1024) return bytes + 'B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + 'KB'
  return (bytes / 1024 / 1024).toFixed(1) + 'MB'
}

function formatTime(time: string) {
  return dayjs(time).format('MM-DD HH:mm')
}

function getDownloadUrl(id: number) {
  return getAttachmentDownloadUrl(id)
}

async function loadAttachments() {
  loading.value = true
  try {
    attachments.value = await getAttachments(props.relatedType, props.relatedId)
    emit('change', attachments.value)
  } catch {
    message.error('加载附件列表失败')
  } finally {
    loading.value = false
  }
}

async function handleUpload(options: UploadRequestOption) {
  const formData = new FormData()
  formData.append('file', options.file as File)
  formData.append('relationType', String(props.relatedType))
  formData.append('relationId', String(props.relatedId))
  try {
    await uploadAttachment(formData)
    message.success('上传成功')
    options.onSuccess?.({})
    await loadAttachments()
  } catch (err: any) {
    message.error('上传失败')
    options.onError?.(err)
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAttachment(id)
    message.success('附件已删除')
    await loadAttachments()
  } catch {
    message.error('删除失败')
  }
}

watch(() => [props.relatedType, props.relatedId], loadAttachments)
onMounted(loadAttachments)

defineExpose({ refresh: loadAttachments })
</script>

<style scoped lang="scss">
.attachment-upload {
  &__dragger {
    margin-bottom: 12px;
  }

  &__list {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  &__item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 8px 12px;
    background: #fafafa;
    border-radius: 6px;
    border: 1px solid #f0f0f0;
    transition: background 0.2s;

    &:hover {
      background: #f5f5f5;
    }
  }

  &__info {
    display: flex;
    align-items: center;
    gap: 8px;
    overflow: hidden;
  }

  &__name {
    font-size: 14px;
    color: var(--text-1);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 240px;

    &:hover {
      color: var(--color-primary);
    }
  }

  &__size {
    font-size: 12px;
    color: #8c8c8c;
    flex-shrink: 0;
  }

  &__meta {
    font-size: 12px;
    color: #bfbfbf;
    flex-shrink: 0;
  }
}
</style>
