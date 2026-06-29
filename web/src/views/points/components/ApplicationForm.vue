<template>
  <a-modal
    v-model:open="visible"
    title="申请积分"
    :confirmLoading="submitting"
    @ok="handleSubmit"
    @cancel="handleCancel"
    width="520px"
  >
    <a-form
      ref="formRef"
      :model="formState"
      :rules="rules"
      layout="vertical"
    >
      <a-form-item label="积分规则" name="ruleId">
        <a-select
          v-model:value="formState.ruleId"
          placeholder="请选择积分规则"
          show-search
          :filter-option="filterOption"
          :loading="rulesLoading"
        >
          <a-select-option
            v-for="rule in ruleOptions"
            :key="rule.id"
            :value="rule.id"
          >
            {{ rule.ruleName }}（{{ rule.pointValue > 0 ? '+' : '' }}{{ rule.pointValue }}分）
          </a-select-option>
        </a-select>
      </a-form-item>
      <a-form-item label="申请说明" name="applicationNote">
        <a-textarea
          v-model:value="formState.applicationNote"
          placeholder="请简要说明申请积分的原因"
          :rows="4"
          :maxlength="500"
          show-count
        />
      </a-form-item>
      <a-form-item label="附件（可选）" name="attachment">
        <a-input
          v-model:value="formState.attachment"
          placeholder="附件链接（可选）"
        />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import { getPointRules } from '@/api/points'
import { submitPointApplication } from '@/api/points'
import type { PointRuleListDto } from '@/types/points'

const visible = defineModel<boolean>('open', { default: false })

const emit = defineEmits<{
  (e: 'success'): void
}>()

const formRef = ref<FormInstance>()
const submitting = ref(false)
const rulesLoading = ref(false)
const ruleOptions = ref<PointRuleListDto[]>([])

const formState = reactive({
  ruleId: undefined as number | undefined,
  applicationNote: '',
  attachment: '',
})

const rules: Record<string, Rule[]> = {
  ruleId: [{ required: true, message: '请选择积分规则', trigger: 'change' }],
  applicationNote: [{ required: true, message: '请填写申请说明', trigger: 'blur' }],
}

function filterOption(input: string, option: any) {
  return option.children?.[0]?.toLowerCase().includes(input.toLowerCase())
}

async function loadRules() {
  rulesLoading.value = true
  try {
    const res = await getPointRules({ pageSize: 200, isEnabled: true })
    ruleOptions.value = res.items
  } catch {
    message.error('加载规则列表失败')
  } finally {
    rulesLoading.value = false
  }
}

async function handleSubmit() {
  try {
    await formRef.value?.validateFields()
  } catch {
    return
  }
  submitting.value = true
  try {
    await submitPointApplication({
      ruleId: formState.ruleId!,
      applicationNote: formState.applicationNote,
      attachment: formState.attachment || null,
    })
    message.success('申请提交成功')
    visible.value = false
    resetForm()
    emit('success')
  } catch {
    message.error('提交失败')
  } finally {
    submitting.value = false
  }
}

function handleCancel() {
  resetForm()
}

function resetForm() {
  formState.ruleId = undefined
  formState.applicationNote = ''
  formState.attachment = ''
  formRef.value?.resetFields()
}

onMounted(() => {
  loadRules()
})
</script>
