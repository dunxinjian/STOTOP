<template>
  <div class="basic-info-panel">
    <div class="panel-header">
      <h3 style="margin: 0">基本信息</h3>
      <a-button type="primary" :loading="saving" @click="handleSave">保存</a-button>
    </div>
    <a-form
      :model="formData"
      :label-col="{ span: 6 }"
      :wrapper-col="{ span: 18 }"
      style="margin-top: 16px"
    >
      <a-row :gutter="24">
        <a-col :span="12">
          <a-form-item label="活动名称" required>
            <a-input v-model:value="formData.name" placeholder="请输入活动名称" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="活动类型">
            <a-radio-group v-model:value="formData.type">
              <a-radio value="conference">会议/培训</a-radio>
              <a-radio value="wedding">婚礼</a-radio>
            </a-radio-group>
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="开始日期">
            <a-date-picker
              v-model:value="formData.startDate"
              value-format="YYYY-MM-DD"
              style="width: 100%"
              placeholder="请选择开始日期"
            />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="结束日期">
            <a-date-picker
              v-model:value="formData.endDate"
              value-format="YYYY-MM-DD"
              style="width: 100%"
              placeholder="请选择结束日期"
            />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="活动地点">
            <a-input v-model:value="formData.location" placeholder="请输入活动地点" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="负责人">
            <a-input v-model:value="formData.manager" placeholder="请输入负责人" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="预算金额">
            <a-input-number
              v-model:value="formData.budget"
              :min="0"
              :precision="2"
              style="width: 100%"
              placeholder="请输入预算金额"
            />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="联系电话">
            <a-input v-model:value="formData.managerPhone" placeholder="请输入联系电话" />
          </a-form-item>
        </a-col>
        <a-col :span="24">
          <a-form-item label="描述" :label-col="{ span: 3 }" :wrapper-col="{ span: 21 }">
            <a-textarea
              v-model:value="formData.description"
              :rows="3"
              placeholder="请输入活动描述"
            />
          </a-form-item>
        </a-col>
        <a-col :span="24">
          <a-form-item label="备注" :label-col="{ span: 3 }" :wrapper-col="{ span: 21 }">
            <a-textarea
              v-model:value="formData.remark"
              :rows="2"
              placeholder="请输入备注"
            />
          </a-form-item>
        </a-col>
        <template v-if="formData.type === 'wedding'">
          <a-col :span="12">
            <a-form-item label="新郎姓名">
              <a-input v-model:value="formData.groomName" placeholder="请输入新郎姓名" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="新娘姓名">
              <a-input v-model:value="formData.brideName" placeholder="请输入新娘姓名" />
            </a-form-item>
          </a-col>
        </template>
      </a-row>
    </a-form>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { updateEvent } from '@/api/conference'
import type { EventDto } from '@/api/conference'

const props = defineProps<{
  eventId: number
  eventData: EventDto
}>()

const emit = defineEmits<{
  (e: 'updated'): void
}>()

interface FormState {
  name: string
  type: string
  startDate: string
  endDate: string
  location: string
  manager: string
  managerPhone: string
  budget: number
  description: string
  remark: string
  groomName: string
  brideName: string
}

const formData = ref<FormState>({
  name: '',
  type: 'conference',
  startDate: '',
  endDate: '',
  location: '',
  manager: '',
  managerPhone: '',
  budget: 0,
  description: '',
  remark: '',
  groomName: '',
  brideName: '',
})

const saving = ref(false)

function loadFromEventData(data: EventDto | undefined) {
  if (!data) return
  formData.value = {
    name: data.name || '',
    type: data.type || 'conference',
    startDate: data.startDate ? data.startDate.substring(0, 10) : '',
    endDate: data.endDate ? data.endDate.substring(0, 10) : '',
    location: data.location || '',
    manager: data.manager || '',
    managerPhone: data.managerPhone || '',
    budget: data.budget ?? 0,
    description: data.description || '',
    remark: data.remark || '',
    groomName: data.groomName || '',
    brideName: data.brideName || '',
  }
}

onMounted(() => {
  loadFromEventData(props.eventData)
})

watch(
  () => props.eventData,
  (val) => loadFromEventData(val),
  { deep: true }
)

async function handleSave() {
  if (!formData.value.name.trim()) {
    message.warning('请输入活动名称')
    return
  }
  saving.value = true
  try {
    await updateEvent(props.eventId, {
      name: formData.value.name,
      description: formData.value.description || undefined,
      startDate: formData.value.startDate,
      endDate: formData.value.endDate,
      location: formData.value.location || undefined,
      manager: formData.value.manager || undefined,
      managerPhone: formData.value.managerPhone || undefined,
      budget: formData.value.budget,
      remark: formData.value.remark || undefined,
      type: formData.value.type || undefined,
      groomName: formData.value.type === 'wedding' ? formData.value.groomName || undefined : undefined,
      brideName: formData.value.type === 'wedding' ? formData.value.brideName || undefined : undefined,
    })
    message.success('保存成功')
    emit('updated')
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.basic-info-panel {
  max-width: 960px;
}
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
</style>
