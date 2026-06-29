<template>
  <a-modal
    :open="open"
    title="新建 AutoVoucher 规则"
    :confirm-loading="loading"
    @ok="handleOk"
    @cancel="handleCancel"
  >
    <a-form ref="formRef" :model="form" :rules="rules" layout="vertical">
      <a-form-item label="规则名称" name="ruleName">
        <a-input v-model:value="form.ruleName" placeholder="请输入规则名称" />
      </a-form-item>
      <a-form-item label="暂存表" name="stagingTable">
        <a-select
          v-model:value="form.stagingTable"
          placeholder="请选择暂存表"
          :loading="stagingLoading"
          show-search
          :filter-option="filterOption"
        >
          <a-select-option v-for="t in stagingTables" :key="t" :value="t">{{ t }}</a-select-option>
        </a-select>
      </a-form-item>
      <a-form-item label="账套" name="accountSetId">
        <a-select
          v-model:value="form.accountSetId"
          placeholder="请选择账套"
          :loading="accountSetLoading"
        >
          <a-select-option v-for="s in accountSets" :key="s.id" :value="s.id">{{ s.fName }}</a-select-option>
        </a-select>
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, watch } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import { getStagingTables, createAutoPluginRule } from '@/api/cardflow'
import { getAccountSets } from '@/api/finance'
import type { AccountSetDto } from '@/api/finance'

const props = defineProps<{
  open: boolean
  typeCode: string
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  created: [ruleId: number]
}>()

const formRef = ref<FormInstance>()
const loading = ref(false)
const stagingLoading = ref(false)
const accountSetLoading = ref(false)

const stagingTables = ref<string[]>([])
const accountSets = ref<AccountSetDto[]>([])

const form = reactive({
  ruleName: '',
  stagingTable: undefined as string | undefined,
  accountSetId: undefined as number | undefined,
})

const rules: Record<string, Rule[]> = {
  ruleName: [{ required: true, message: '请输入规则名称', trigger: 'blur' }],
  stagingTable: [{ required: true, message: '请选择暂存表', trigger: 'change' }],
  accountSetId: [{ required: true, message: '请选择账套', trigger: 'change' }],
}

const filterOption = (input: string, option: any) => {
  const label = option.children?.[0]?.children ?? option.value ?? ''
  return String(label).toLowerCase().includes(input.toLowerCase())
}

async function loadOptions() {
  stagingLoading.value = true
  accountSetLoading.value = true
  try {
    const tables = await getStagingTables()
    stagingTables.value = (tables || []).map((t: any) => (typeof t === 'string' ? t : t.tableName))
  } finally {
    stagingLoading.value = false
  }
  try {
    const sets = await getAccountSets()
    accountSets.value = sets || []
  } finally {
    accountSetLoading.value = false
  }
}

watch(
  () => props.open,
  (val) => {
    if (val) {
      loadOptions()
    }
  },
)

async function handleOk() {
  try {
    await formRef.value?.validateFields()
  } catch {
    return
  }

  loading.value = true
  try {
    const configJson = JSON.stringify({
      version: 2,
      accountSetId: form.accountSetId,
      ruleConfig: {
        mode: 'rulesBased',
        stagingTable: form.stagingTable,
        accountSetId: form.accountSetId,
        ruleGroups: [],
      },
    })
    const res: any = await createAutoPluginRule({
      typeCode: props.typeCode,
      ruleName: form.ruleName,
      configJson,
    })
    message.success('规则创建成功')
    emit('created', res.id ?? res)
    handleCancel()
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  emit('update:open', false)
  formRef.value?.resetFields()
  form.ruleName = ''
  form.stagingTable = undefined
  form.accountSetId = undefined
}
</script>
