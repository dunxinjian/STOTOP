<template>
  <a-modal
    :open="props.visible"
    title="导入报价"
    width="560px"
    :destroy-on-close="true"
    :confirm-loading="importing"
    @cancel="handleClose"
  >
    <a-alert type="info" show-icon style="margin-bottom: 16px;">
      <template #message>
        请先 <a @click="handleDownloadTemplate">下载导入模板</a>，按模板格式填写报价数据后上传。
      </template>
    </a-alert>

    <a-form :label-col="{ style: { width: '100px' } }">
      <a-form-item label="品牌" required>
        <a-select
          v-model:value="form.brandCode"
          placeholder="请选择品牌"
          :options="brandOptions"
          :loading="brandLoading"
        />
      </a-form-item>
      <a-form-item label="方案名称" required>
        <a-input v-model:value="form.planName" placeholder="请输入方案名称" />
      </a-form-item>
      <a-form-item label="Excel文件" required>
        <a-upload
          :before-upload="handleBeforeUpload"
          :file-list="fileList"
          :max-count="1"
          accept=".xlsx,.xls"
          @remove="handleRemoveFile"
        >
          <a-button>
            <UploadOutlined />
            选择文件
          </a-button>
        </a-upload>
        <div v-if="fileList.length" style="margin-top: 4px; color: var(--text-3); font-size: 12px;">
          支持 .xlsx、.xls 格式
        </div>
      </a-form-item>
    </a-form>

    <template #footer>
      <a-button @click="handleClose">取消</a-button>
      <a-button type="primary" :loading="importing" @click="handleImport">确认导入</a-button>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { UploadOutlined } from '@ant-design/icons-vue'
import { downloadQuotationTemplate, importQuotation, getExpBrandOptions } from '@/api/express'
import { downloadBlob } from '@/utils/download'

const props = defineProps<{
  visible: boolean
}>()
const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
  (e: 'success'): void
}>()

const router = useRouter()
const importing = ref(false)
const fileList = ref<any[]>([])
const form = reactive({
  brandCode: undefined as string | undefined,
  planName: '',
})

const brandOptions = ref<{ label: string; value: string }[]>([])
const brandLoading = ref(false)

async function loadBrands() {
  brandLoading.value = true
  try {
    const res = await getExpBrandOptions()
    const list = Array.isArray(res) ? res : (res.items || [])
    brandOptions.value = list.map((b: any) => ({ label: b.name || b.code, value: b.code }))
  } catch {
    brandOptions.value = []
  } finally {
    brandLoading.value = false
  }
}

onMounted(loadBrands)

// 弹窗关闭时重置
watch(() => props.visible, (val) => {
  if (!val) {
    form.brandCode = undefined
    form.planName = ''
    fileList.value = []
  }
})

function handleClose() {
  emit('update:visible', false)
}

async function handleDownloadTemplate() {
  try {
    const blob = await downloadQuotationTemplate()
    downloadBlob(new Blob([blob]), '报价导入模板.xlsx')
  } catch (e: any) {
    message.error('下载模板失败')
  }
}

function handleBeforeUpload(file: File) {
  fileList.value = [file]
  return false
}

function handleRemoveFile() {
  fileList.value = []
}

async function handleImport() {
  if (!form.brandCode) {
    message.warning('请选择品牌')
    return
  }
  if (!form.planName) {
    message.warning('请填写方案名称')
    return
  }
  if (!fileList.value.length) {
    message.warning('请选择Excel文件')
    return
  }

  const formData = new FormData()
  formData.append('brandCode', form.brandCode)
  formData.append('planName', form.planName)
  formData.append('file', fileList.value[0])

  importing.value = true
  try {
    const result = await importQuotation(formData)
    message.success('导入成功，已创建草稿报价')
    emit('update:visible', false)
    emit('success')
    router.push(`/express/quotation/edit/${result.id}`)
  } catch (e: any) {
    message.error(e.message || '导入失败，请检查文件格式')
  } finally {
    importing.value = false
  }
}
</script>
