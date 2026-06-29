<template>
  <div class="attachment-picker">
    <!-- 图片区域 -->
    <div class="image-section">
      <div class="image-grid">
        <div
          v-for="item in imageItems"
          :key="item.id"
          class="image-item"
          @click="previewImage(item)"
        >
          <van-image :src="item.url" width="72" height="72" fit="cover" radius="4" />
          <van-loading v-if="item.uploading" size="20" class="upload-overlay" />
          <div v-if="item.uploading && item.progress" class="progress-bar">
            <div class="progress-fill" :style="{ width: item.progress + '%' }" />
          </div>
          <van-icon
            name="clear"
            class="delete-btn"
            @click.stop="removeItem(item)"
          />
        </div>
        <!-- 拍照/选择图片按钮 -->
        <div
          v-if="canAddMore && (accept === 'image' || accept === 'all')"
          class="add-image-btn"
          @click="chooseImages"
        >
          <van-icon name="photograph" size="24" color="#999" />
          <span class="add-text">拍照</span>
        </div>
      </div>
    </div>

    <!-- 文件区域 -->
    <div v-if="accept === 'file' || accept === 'all'" class="file-section">
      <div v-for="item in fileItems" :key="item.id" class="file-item">
        <van-icon name="description" size="20" color="var(--text-2)" />
        <span class="file-name">{{ item.name }}</span>
        <van-loading v-if="item.uploading" size="16" />
        <span v-else-if="item.progress !== undefined && item.progress < 100" class="file-progress">
          {{ item.progress }}%
        </span>
        <van-icon v-else name="cross" class="file-delete" @click="removeItem(item)" />
      </div>
      <div v-if="canAddMore" class="add-file-btn" @click="chooseFiles">
        <van-icon name="plus" size="16" />
        <span>选择文件</span>
      </div>
    </div>

    <!-- 隐藏 input -->
    <input
      ref="fileInputRef"
      type="file"
      :accept="fileAccept"
      multiple
      style="display: none"
      @change="onFileInputChange"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  Image as VanImage,
  Icon as VanIcon,
  Loading as VanLoading,
  showImagePreview,
} from 'vant'
import { bridge } from '../utils/dingtalk-bridge'
import { uploadMobileFile, transferMedia } from '@shared/api/mobile'

export interface AttachmentItem {
  id: string
  name: string
  url: string
  type: 'image' | 'file'
  uploading?: boolean
  progress?: number
}

const props = withDefaults(defineProps<{
  modelValue: AttachmentItem[]
  maxCount?: number
  accept?: 'image' | 'file' | 'all'
}>(), {
  maxCount: 9,
  accept: 'all',
})

const emit = defineEmits<{
  'update:modelValue': [value: AttachmentItem[]]
}>()

const fileInputRef = ref<HTMLInputElement | null>(null)

const imageItems = computed(() => props.modelValue.filter(i => i.type === 'image'))
const fileItems = computed(() => props.modelValue.filter(i => i.type === 'file'))
const canAddMore = computed(() => props.modelValue.length < props.maxCount)

const fileAccept = computed(() => {
  if (props.accept === 'image') return 'image/*'
  return '*/*'
})

function generateId() {
  return 'att_' + Date.now() + '_' + Math.random().toString(36).slice(2, 8)
}

function updateItems(newList: AttachmentItem[]) {
  emit('update:modelValue', newList)
}

function removeItem(item: AttachmentItem) {
  updateItems(props.modelValue.filter(i => i.id !== item.id))
}

function previewImage(item: AttachmentItem) {
  if (item.uploading) return
  const urls = imageItems.value.filter(i => !i.uploading).map(i => i.url)
  const startIdx = urls.indexOf(item.url)
  showImagePreview({ images: urls, startPosition: Math.max(0, startIdx) })
}

/** 拍照/选择图片 — 钉钉 JSAPI */
async function chooseImages() {
  const remaining = props.maxCount - props.modelValue.length
  if (remaining <= 0) return

  try {
    const filePaths = await bridge.chooseImage({ count: Math.min(remaining, 9) })
    for (const filePath of filePaths) {
      const id = generateId()
      const placeholder: AttachmentItem = {
        id,
        name: '图片上传中...',
        url: filePath,
        type: 'image',
        uploading: true,
        progress: 0,
      }
      updateItems([...props.modelValue, placeholder])

      // 立即上传转存，避免 mediaId 过期
      transferSingleImage(id, filePath)
    }
  } catch (e) {
    console.error('[AttachmentPicker] chooseImage failed:', e)
  }
}

async function transferSingleImage(itemId: string, filePath: string) {
  try {
    const mediaId = await bridge.uploadImage(filePath)
    // 更新进度为 50%
    updateItemProgress(itemId, 50)

    const res = await transferMedia(mediaId)
    // 完成
    const list = props.modelValue.map(i =>
      i.id === itemId
        ? { ...i, url: res.url, name: '图片', uploading: false, progress: 100 }
        : i
    )
    updateItems(list)
  } catch (e) {
    console.error('[AttachmentPicker] transfer image failed:', e)
    // 移除失败项
    updateItems(props.modelValue.filter(i => i.id !== itemId))
  }
}

function updateItemProgress(itemId: string, progress: number) {
  const list = props.modelValue.map(i =>
    i.id === itemId ? { ...i, progress } : i
  )
  updateItems(list)
}

/** 选择文件 — 原生 input */
function chooseFiles() {
  fileInputRef.value?.click()
}

async function onFileInputChange(e: Event) {
  const input = e.target as HTMLInputElement
  const files = Array.from(input.files || [])
  input.value = '' // reset

  for (const file of files) {
    if (props.modelValue.length >= props.maxCount) break

    const id = generateId()
    const placeholder: AttachmentItem = {
      id,
      name: file.name,
      url: '',
      type: 'file',
      uploading: true,
      progress: 0,
    }
    updateItems([...props.modelValue, placeholder])

    uploadSingleFile(id, file)
  }
}

async function uploadSingleFile(itemId: string, file: File) {
  try {
    const res = await uploadMobileFile(file, (percent) => {
      updateItemProgress(itemId, percent)
    })
    const url = res?.data?.url || res?.url || ''
    const list = props.modelValue.map(i =>
      i.id === itemId
        ? { ...i, url, uploading: false, progress: 100 }
        : i
    )
    updateItems(list)
  } catch (e) {
    console.error('[AttachmentPicker] upload file failed:', e)
    updateItems(props.modelValue.filter(i => i.id !== itemId))
  }
}
</script>

<style scoped>
.attachment-picker {
  padding: 8px 0;
}

.image-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.image-item {
  position: relative;
  width: 72px;
  height: 72px;
  border-radius: 4px;
  overflow: hidden;
}

.upload-overlay {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}

.progress-bar {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: rgba(0, 0, 0, 0.3);
}

.progress-fill {
  height: 100%;
  background: var(--color-primary);
  transition: width 0.3s;
}

.delete-btn {
  position: absolute;
  top: 2px;
  right: 2px;
  font-size: 16px;
  color: rgba(0, 0, 0, 0.6);
  background: rgba(255, 255, 255, 0.8);
  border-radius: 50%;
}

.add-image-btn {
  width: 72px;
  height: 72px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border: 1px dashed #ddd;
  border-radius: 4px;
  cursor: pointer;
}

.add-text {
  font-size: 11px;
  color: #999;
  margin-top: 4px;
}

.file-section {
  margin-top: 12px;
}

.file-item {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  background: #f7f8fa;
  border-radius: 4px;
  margin-bottom: 8px;
  gap: 8px;
}

.file-name {
  flex: 1;
  font-size: 13px;
  color: #333;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-progress {
  font-size: 12px;
  color: var(--color-primary);
}

.file-delete {
  font-size: 16px;
  color: #999;
  cursor: pointer;
}

.add-file-btn {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 8px 12px;
  font-size: 13px;
  color: var(--color-primary);
  cursor: pointer;
}
</style>
