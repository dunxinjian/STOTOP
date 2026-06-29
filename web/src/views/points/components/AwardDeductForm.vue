<template>
  <a-modal
    v-model:open="visible"
    :title="isDeduct ? '扣分' : '奖分'"
    :confirm-loading="submitting"
    @ok="handleSubmit"
    @cancel="handleCancel"
    :width="520"
    destroy-on-close
  >
    <a-form ref="formRef" :model="form" :rules="rules" layout="vertical">
      <!-- 奖分/扣分切换 -->
      <a-form-item label="操作类型">
        <a-radio-group v-model:value="isDeduct" button-style="solid">
          <a-radio-button :value="false">
            <PlusCircleOutlined /> 奖分
          </a-radio-button>
          <a-radio-button :value="true">
            <MinusCircleOutlined /> 扣分
          </a-radio-button>
        </a-radio-group>
      </a-form-item>

      <!-- 选择员工 -->
      <a-form-item label="员工" name="userId">
        <a-select
          v-model:value="form.userId"
          placeholder="搜索并选择员工"
          show-search
          :filter-option="false"
          @search="handleUserSearch"
          :loading="userSearching"
          :options="userOptions"
          style="width: 100%"
        />
      </a-form-item>

      <!-- 选择来源 -->
      <a-form-item label="积分来源" name="sourceId">
        <a-select
          v-model:value="form.sourceId"
          placeholder="请选择积分来源"
          :options="sourceOptions"
          style="width: 100%"
        />
      </a-form-item>

      <!-- 积分值 -->
      <a-form-item label="积分值" name="pointValue">
        <a-input-number
          v-model:value="form.pointValue"
          :min="1"
          :max="99999"
          placeholder="请输入积分值"
          style="width: 100%"
        />
      </a-form-item>

      <!-- 备注 -->
      <a-form-item label="备注说明" name="remark">
        <a-textarea
          v-model:value="form.remark"
          :rows="3"
          placeholder="请输入备注说明"
          :maxlength="500"
          show-count
        />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PlusCircleOutlined, MinusCircleOutlined } from '@ant-design/icons-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import { awardPoints, deductPoints } from '@/api/points'
import { getPointSources } from '@/api/points'
import { getUserList } from '@/api/system'
import type { PointSourceDto } from '@/types/points'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'success'): void
}>()

const visible = ref(false)
const submitting = ref(false)
const formRef = ref<FormInstance>()
const isDeduct = ref(false)

// 员工搜索
const userSearching = ref(false)
const userOptions = ref<{ label: string; value: number }[]>([])
let searchTimer: ReturnType<typeof setTimeout> | null = null

// 来源选项
const sourceOptions = ref<{ label: string; value: number }[]>([])

const form = reactive({
  userId: undefined as number | undefined,
  sourceId: undefined as number | undefined,
  pointValue: undefined as number | undefined,
  remark: '',
})

const rules: Record<string, Rule[]> = {
  userId: [{ required: true, message: '请选择员工', trigger: 'change' }],
  sourceId: [{ required: true, message: '请选择积分来源', trigger: 'change' }],
  pointValue: [{ required: true, message: '请输入积分值', trigger: 'change', type: 'number' }],
  remark: [{ required: true, message: '请输入备注说明', trigger: 'blur' }],
}

watch(() => props.open, (val) => {
  visible.value = val
  if (val) {
    loadSources()
    resetForm()
  }
})

watch(visible, (val) => {
  if (!val) emit('update:open', false)
})

async function loadSources() {
  try {
    const res = await getPointSources()
    const list: PointSourceDto[] = Array.isArray(res) ? res : (res as any)?.data ?? []
    sourceOptions.value = list
      .filter(s => s.isEnabled)
      .map(s => ({ label: s.sourceName, value: s.id }))
  } catch {
    sourceOptions.value = []
  }
}

function handleUserSearch(keyword: string) {
  if (searchTimer) clearTimeout(searchTimer)
  if (!keyword) {
    userOptions.value = []
    return
  }
  searchTimer = setTimeout(async () => {
    userSearching.value = true
    try {
      const res = await getUserList({ keyword, pageIndex: 1, pageSize: 20 })
      const items = (res as any)?.data?.items ?? (res as any)?.items ?? []
      userOptions.value = items.map((u: any) => ({
        label: u.name || u.fName || u.account,
        value: u.id || u.fid,
      }))
    } catch {
      userOptions.value = []
    } finally {
      userSearching.value = false
    }
  }, 300)
}

function resetForm() {
  form.userId = undefined
  form.sourceId = undefined
  form.pointValue = undefined
  form.remark = ''
  isDeduct.value = false
  formRef.value?.clearValidate()
}

async function handleSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  submitting.value = true
  try {
    const payload = {
      userId: form.userId!,
      sourceId: form.sourceId!,
      pointValue: form.pointValue!,
      remark: form.remark,
    }
    if (isDeduct.value) {
      await deductPoints(payload)
      message.success('扣分成功')
    } else {
      await awardPoints(payload)
      message.success('奖分成功')
    }
    emit('success')
    visible.value = false
  } catch (e: any) {
    message.error(e?.message || (isDeduct.value ? '扣分失败' : '奖分失败'))
  } finally {
    submitting.value = false
  }
}

function handleCancel() {
  visible.value = false
}
</script>
