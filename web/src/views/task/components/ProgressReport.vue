<template>
  <a-card title="进度上报" size="small" class="progress-report">
    <a-form :model="formState" layout="vertical" @finish="handleSubmit">
      <a-form-item label="当前进度" name="progress" :rules="[{ required: true, message: '请设置进度' }]">
        <div class="progress-report__slider">
          <a-slider v-model:value="formState.progress" :min="0" :max="100" :tip-formatter="(val?: number) => `${val ?? 0}%`" />
          <span class="progress-report__percent">{{ formState.progress }}%</span>
        </div>
      </a-form-item>
      <a-form-item label="进度说明" name="content" :rules="[{ required: true, message: '请填写进度说明' }]">
        <a-textarea
          v-model:value="formState.content"
          placeholder="请描述当前进度、遇到的问题等"
          :rows="3"
          :maxlength="500"
          show-count
        />
      </a-form-item>
      <a-form-item label="工时（小时）" name="hours">
        <a-input-number v-model:value="formState.hours" :min="0" :max="999" placeholder="本次投入工时" style="width: 160px" />
      </a-form-item>
      <a-form-item>
        <a-button type="primary" html-type="submit" :loading="submitting">提交上报</a-button>
      </a-form-item>
    </a-form>
  </a-card>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { message } from 'ant-design-vue'
import { createProgressReport } from '@/api/task'

const props = defineProps<{
  taskId: number
}>()

const emit = defineEmits<{
  (e: 'success'): void
}>()

const submitting = ref(false)

const formState = reactive({
  progress: 0,
  content: '',
  hours: undefined as number | undefined,
})

async function handleSubmit() {
  submitting.value = true
  try {
    await createProgressReport(props.taskId, {
      progress: formState.progress,
      content: formState.content,
      hours: formState.hours,
    })
    message.success('进度上报成功')
    formState.progress = 0
    formState.content = ''
    formState.hours = undefined
    emit('success')
  } catch {
    message.error('进度上报失败')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped lang="scss">
.progress-report {
  &__slider {
    display: flex;
    align-items: center;
    gap: 12px;

    .ant-slider {
      flex: 1;
    }
  }

  &__percent {
    font-size: 16px;
    font-weight: 600;
    color: var(--color-info);
    min-width: 48px;
    text-align: right;
  }
}
</style>
