<template>
  <a-modal
    :open="open"
    title="新增报价"
    :width="600"
    :destroy-on-close="true"
    @cancel="emit('update:open', false)"
  >
    <a-form
      ref="formRef"
      :model="form"
      :rules="formRules"
      :label-col="{ style: { width: '110px' } }"
      style="padding: 16px 20px 0"
    >
      <!-- 店铺选择（多选） -->
      <a-form-item label="店铺名称" name="shopNames">
        <a-select
          v-model:value="form.shopNames"
          mode="multiple"
          placeholder="请搜索选择店铺（可多选）"
          show-search
          :filter-option="false"
          :loading="shopSearchLoading"
          @search="handleShopSearch"
        >
          <a-select-option v-for="s in shopOptions" :key="s.name" :value="s.name">
            {{ s.name }}
          </a-select-option>
        </a-select>
      </a-form-item>

      <!-- 报价方案选择 -->
      <a-form-item label="报价方案" name="pricePlanMode">
        <a-select
          v-model:value="form.pricePlanMode"
          placeholder="请选择报价方案"
          :loading="planLoading"
          @change="handlePlanModeChange"
        >
          <a-select-option v-for="p in pricePlanOptions" :key="p.id" :value="p.id">
            {{ p.planName }}
            <a-tag v-if="p.status === 0" color="blue" style="margin-left: 4px">草稿</a-tag>
            <a-tag v-else-if="p.status === 1" color="green" style="margin-left: 4px">生效</a-tag>
            <a-tag v-else-if="p.status === 2" color="default" style="margin-left: 4px">过期</a-tag>
          </a-select-option>
          <a-select-option :value="-1">
            ➕ 新建方案
          </a-select-option>
        </a-select>
      </a-form-item>

      <!-- 新建方案额外字段 -->
      <template v-if="form.pricePlanMode === -1">
        <a-form-item label="方案名称" name="newPlanName" :rules="[{ required: true, message: '请输入方案名称' }]">
          <a-input v-model:value="form.newPlanName" placeholder="请输入方案名称" :maxlength="100" />
        </a-form-item>
        <a-form-item label="品牌" name="newBrandCode" :rules="[{ required: true, message: '请选择品牌' }]">
          <a-select v-model:value="form.newBrandCode" placeholder="请选择品牌" :loading="brandLoading">
            <a-select-option v-for="b in brandOptions" :key="b.code" :value="b.code">
              {{ b.name }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="结算重量环节" name="newSettlementWeightStage">
          <a-radio-group v-model:value="form.newSettlementWeightStage">
            <a-radio :value="1">计费重</a-radio>
            <a-radio :value="2">实重</a-radio>
          </a-radio-group>
        </a-form-item>
      </template>

      <!-- 生效日期 -->
      <a-form-item label="生效日期" name="effectiveDate">
        <a-date-picker
          v-model:value="form.effectiveDate"
          style="width: 100%"
          value-format="YYYY-MM-DD"
          placeholder="请选择生效日期"
        />
      </a-form-item>

      <!-- 备注 -->
      <a-form-item label="备注" name="remark">
        <a-textarea v-model:value="form.remark" :rows="2" placeholder="请输入备注" :maxlength="500" show-count />
      </a-form-item>
    </a-form>

    <template #footer>
      <a-button @click="emit('update:open', false)">取消</a-button>
      <a-button type="primary" :loading="submitting" @click="handleSubmit">确定</a-button>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive, watch } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import {
  getExpShopList,
  getQuotationList,
  getExpBrandOptions,
  addQuotationShops,
  type ShopListItemDto,
  type QuotationListItemDto,
} from '@/api/express'

const props = defineProps<{
  open: boolean
  quotationId?: number
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'saved'): void
}>()

const formRef = ref<FormInstance>()
const submitting = ref(false)

const form = reactive({
  shopNames: [] as string[],
  pricePlanMode: undefined as number | undefined,  // pricePlanId or -1 for new
  newPlanName: '',
  newBrandCode: undefined as string | undefined,
  newSettlementWeightStage: 1,
  effectiveDate: undefined as string | undefined,
  remark: '',
})

const formRules: Record<string, Rule[]> = {
  shopNames: [{ required: true, message: '请选择店铺', trigger: 'change', type: 'array' }],
  pricePlanMode: [{ required: true, message: '请选择报价方案', trigger: 'change' }],
  effectiveDate: [{ required: true, message: '请选择生效日期', trigger: 'change' }],
}

// ===== 店铺搜索 =====
const shopOptions = ref<ShopListItemDto[]>([])
const shopSearchLoading = ref(false)
let shopSearchTimer: any = null

function handleShopSearch(value: string) {
  if (shopSearchTimer) clearTimeout(shopSearchTimer)
  if (!value) {
    shopOptions.value = []
    return
  }
  shopSearchLoading.value = true
  shopSearchTimer = setTimeout(async () => {
    try {
      const res = await getExpShopList({ keyword: value, pageIndex: 1, pageSize: 50 })
      shopOptions.value = Array.isArray(res) ? res : (res.items || [])
    } catch {
      shopOptions.value = []
    } finally {
      shopSearchLoading.value = false
    }
  }, 300)
}

// ===== 报价方案列表 =====
const pricePlanOptions = ref<QuotationListItemDto[]>([])
const planLoading = ref(false)

async function loadQuotations() {
  planLoading.value = true
  try {
    const res = await getQuotationList({ pageIndex: 1, pageSize: 500 })
    pricePlanOptions.value = res.items || []
  } catch {
    pricePlanOptions.value = []
  } finally {
    planLoading.value = false
  }
}

function handlePlanModeChange() {
  if (form.pricePlanMode === -1) {
    loadBrands()
  }
}

// ===== 品牌列表 =====
const brandOptions = ref<{ code: string; name: string }[]>([])
const brandLoading = ref(false)

async function loadBrands() {
  if (brandOptions.value.length > 0) return
  brandLoading.value = true
  try {
    const res = await getExpBrandOptions()
    brandOptions.value = res.items || []
  } catch {
    brandOptions.value = []
  } finally {
    brandLoading.value = false
  }
}

// ===== 表单重置 =====
function resetForm() {
  form.shopNames = []
  form.pricePlanMode = undefined
  form.newPlanName = ''
  form.newBrandCode = undefined
  form.newSettlementWeightStage = 1
  form.effectiveDate = undefined
  form.remark = ''
}

watch(() => props.open, (val) => {
  if (val) {
    resetForm()
    loadQuotations()
  }
})

// ===== 提交 =====
async function handleSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  submitting.value = true
  try {
    const data: any = {
      shopNames: form.shopNames,
      remark: form.remark || undefined,
    }
    if (form.pricePlanMode === -1) {
      // For new plan creation, redirect to quotation create page
      message.info('请使用报价管理页面创建新报价')
      emit('update:open', false)
      return
    }
    const quotationId = props.quotationId || form.pricePlanMode
    if (!quotationId) {
      message.warning('请选择报价方案')
      return
    }
    await addQuotationShops(quotationId, data)
    message.success('创建成功')
    emit('update:open', false)
    emit('saved')
  } catch {
    message.error('保存失败')
  } finally {
    submitting.value = false
  }
}
</script>
