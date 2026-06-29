<template>
  <div class="card-fields">
    <van-cell-group inset>
      <template v-for="field in fields" :key="field.label">
        <!-- 图片类型 -->
        <van-cell v-if="field.type === 'image'" :title="field.label" class="field-image-cell">
          <template #value>
            <van-image
              v-for="(url, idx) in normalizeImages(field.value)"
              :key="idx"
              width="60"
              height="60"
              fit="cover"
              :src="url"
              class="field-image"
              @click="previewImage(url, normalizeImages(field.value))"
            />
          </template>
        </van-cell>

        <!-- 文件类型 -->
        <van-cell v-else-if="field.type === 'file'" :title="field.label" class="field-file-cell">
          <template #value>
            <div
              v-for="(file, idx) in normalizeFiles(field.value)"
              :key="idx"
              class="file-item"
            >
              <van-icon name="description" class="file-icon" />
              <a :href="file.url" target="_blank" class="file-link">{{ file.name }}</a>
            </div>
          </template>
        </van-cell>

        <!-- 金额类型 -->
        <van-cell v-else-if="field.type === 'money'" :title="field.label" :value="formatMoney(field.value)" />

        <!-- 日期类型 -->
        <van-cell v-else-if="field.type === 'date'" :title="field.label" :value="formatDate(field.value)" />

        <!-- 文本/数字默认 -->
        <van-cell v-else :title="field.label" :value="String(field.value ?? '')" />
      </template>
    </van-cell-group>
  </div>
</template>

<script setup lang="ts">
import { Cell as VanCell, CellGroup as VanCellGroup, Image as VanImage, Icon as VanIcon, showImagePreview } from 'vant'

defineOptions({ name: 'CardFields' })

export interface CardField {
  label: string
  value: any
  type: string
}

defineProps<{
  fields: CardField[]
}>()

function normalizeImages(value: any): string[] {
  if (Array.isArray(value)) return value
  if (typeof value === 'string') return [value]
  return []
}

function normalizeFiles(value: any): Array<{ name: string; url: string }> {
  if (Array.isArray(value)) return value
  if (typeof value === 'object' && value) return [value]
  return []
}

function formatMoney(value: any): string {
  const num = Number(value)
  if (isNaN(num)) return String(value ?? '')
  return '¥' + num.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function formatDate(value: any): string {
  if (!value) return ''
  const d = new Date(value)
  if (isNaN(d.getTime())) return String(value)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function previewImage(current: string, images: string[]) {
  showImagePreview({ images, startPosition: images.indexOf(current) })
}
</script>

<style scoped lang="scss">
.card-fields {
  padding: 8px 0;
}

.field-image-cell {
  .field-image {
    margin-right: 8px;
    border-radius: 4px;
    overflow: hidden;
  }
}

.field-file-cell {
  .file-item {
    display: flex;
    align-items: center;
    margin-bottom: 4px;

    .file-icon {
      margin-right: 4px;
      color: var(--text-2);
    }

    .file-link {
      color: var(--text-1);
      text-decoration: none;
      font-size: 14px;

      &:hover {
        color: var(--color-primary);
      }
    }
  }
}
</style>
