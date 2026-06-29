<template>
  <div class="upload-section" :class="{ collapsed: collapsed }">
    <div class="upload-header" :class="{ 'no-margin': collapsed }">
      <h3>{{ collapsed ? '点击展开上传区域' : '文件上传' }}</h3>
      <span class="upload-toggle" @click="emit('update:collapsed', !collapsed)">
        {{ collapsed ? '展开' : '收起' }}
        <DownOutlined v-if="collapsed" :style="{ fontSize: '10px' }" />
        <UpOutlined v-else :style="{ fontSize: '10px' }" />
      </span>
    </div>
    <div v-show="!collapsed" class="upload-dragger-wrap">
      <a-upload-dragger
        :multiple="true"
        :showUploadList="false"
        accept=".xlsx,.xls,.csv"
        :beforeUpload="handleBeforeUpload"
        :customRequest="() => {}"
        :disabled="disabled"
        class="upload-dragger"
      >
        <p class="upload-icon">
          <InboxOutlined />
        </p>
        <p class="upload-text">点击或拖拽文件到此区域上传</p>
        <p class="upload-hint">支持 .xlsx / .xls / .csv 格式，单次最多5个文件，单文件不超过50MB</p>
      </a-upload-dragger>
    </div>
  </div>
</template>

<script setup lang="ts">
import { InboxOutlined, UpOutlined, DownOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import type { UploadFile } from 'ant-design-vue'

defineProps<{
  collapsed: boolean
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:collapsed': [value: boolean]
  upload: [file: File]
}>()

const MAX_FILE_SIZE = 50 * 1024 * 1024 // 50MB

// [Fix#5] 合法的 MIME type 映射（扩展名 → 允许的 MIME 列表）
const VALID_MIME_TYPES: Record<string, string[]> = {
  xlsx: ['application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'],
  xls: ['application/vnd.ms-excel'],
  csv: ['text/csv', 'text/plain', 'application/vnd.ms-excel'],
}

/**
 * [Fix#5] 校验文件 MIME type 是否与扩展名匹配
 * 某些操作系统/浏览器可能返回空或不准确的 MIME，因此不匹配时仅警告不拒绝
 */
function validateMimeType(file: File, ext: string): boolean {
  const validMimes = VALID_MIME_TYPES[ext]
  if (!validMimes) return true // 无法校验的扩展名，跳过

  // 部分系统（尤其是 Windows 旧版浏览器）可能不提供 MIME type
  if (!file.type || file.type === '') {
    console.debug(`[UploadDropZone] 文件 ${file.name} 无 MIME type 信息，跳过校验`)
    return true
  }

  if (!validMimes.includes(file.type)) {
    message.warning(
      `文件 ${file.name} 的类型(${file.type})与扩展名(.${ext})不匹配，` +
      '请确认文件内容是否正确。已允许上传，但可能导致解析失败。'
    )
    return true // 警告但不拒绝
  }
  return true
}

function handleBeforeUpload(file: File, fileList: File[]) {
  if (file.size > MAX_FILE_SIZE) {
    message.error(`文件 ${file.name} 超过50MB限制`)
    return false
  }
  const ext = file.name.split('.').pop()?.toLowerCase()
  if (!['xlsx', 'xls', 'csv'].includes(ext || '')) {
    message.error(`文件 ${file.name} 格式不支持，仅支持 .xlsx / .xls / .csv`)
    return false
  }
  // [Fix#5] MIME type 校验（警告模式，不阻止上传）
  validateMimeType(file, ext!)
  emit('upload', file)
  return false // 阻止 ant-design 默认上传行为
}
</script>

<style scoped lang="scss">
.upload-section {
  background: var(--bg-card);
  border-radius: 8px;
  box-shadow: 0 2px 8px var(--shadow-sm);
  padding: 20px;
  margin-bottom: 16px;
  transition: all 0.3s ease;

  &.collapsed {
    padding: 12px 20px;
  }
}

.upload-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;

  &.no-margin {
    margin-bottom: 0;
  }

  h3 {
    font-size: 15px;
    color: var(--text-1);
    font-weight: 500;
    margin: 0;
  }
}

.upload-toggle {
  cursor: pointer;
  color: var(--text-1);
  font-size: 13px;
  user-select: none;
  display: flex;
  align-items: center;
  gap: 4px;

  &:hover {
    color: var(--color-primary);
  }
}

.upload-dragger-wrap {
  :deep(.ant-upload-drag) {
    border: 2px dashed var(--border);
    border-radius: 8px;
    background: var(--bg-muted);
    transition: all 0.3s;

    &:hover {
      border-color: var(--color-primary);
      background: color-mix(in srgb, var(--color-info) 8%, transparent);
    }
  }
}

.upload-icon {
  font-size: 48px;
  color: var(--text-2);
  margin-bottom: 8px;
}

.upload-text {
  font-size: 15px;
  color: var(--text-1);
  margin-bottom: 4px;
}

.upload-hint {
  font-size: 13px;
  color: var(--text-2);
}
</style>
