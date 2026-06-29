<template>
  <div class="page-container surcharge-edit-page">
    <PageHeader :title="isEdit ? '编辑加收方案' : '新建加收方案'">
      <template #actions>
        <a-button @click="handleCancel">取消</a-button>
        <a-button type="primary" :loading="submitLoading" @click="handleSubmit">保存</a-button>
      </template>
    </PageHeader>

    <!-- 可滚动内容区 -->
    <div class="edit-scroll-body">
      <a-form
        ref="formRef"
        :model="form"
        :rules="formRules"
        :label-col="{ style: { width: '80px' } }"
      >
        <div class="edit-columns">
          <!-- 左栏：基本信息 + 配置项 -->
          <div class="edit-column-left">
            <!-- 基本信息 -->
            <div class="section-block section-basic">
              <div class="section-title">基本信息</div>
              <a-row :gutter="12">
                <a-col :span="8">
                  <a-form-item label="加收类型" name="surchargeType">
                    <a-select v-model:value="form.surchargeType" placeholder="请选择加收类型" :options="surchargeTypeOptions" />
                  </a-form-item>
                </a-col>
                <a-col :span="8">
                  <a-form-item label="作用域" name="scope">
                    <a-radio-group v-model:value="form.scope" @change="handleScopeChange">
                      <a-radio-button :value="0">全局</a-radio-button>
                      <a-radio-button :value="1">业务对象级</a-radio-button>
                      <a-radio-button :value="2">报价级</a-radio-button>
                    </a-radio-group>
                  </a-form-item>
                </a-col>
                <a-col :span="8">
                  <a-form-item label="品牌编码" name="brandCode">
                    <a-input v-model:value="form.brandCode" placeholder="品牌编码（如 ST）" />
                  </a-form-item>
                </a-col>
              </a-row>
              <a-row :gutter="12">
                <a-col :span="8">
                  <a-form-item label="网点编码" name="networkPointCode">
                    <a-input v-model:value="form.networkPointCode" placeholder="可选，留空=品牌全局" allow-clear />
                  </a-form-item>
                </a-col>
                <a-col :span="8">
                  <a-form-item label="生效日期" name="effectiveDate">
                    <a-date-picker v-model:value="form.effectiveDate" style="width: 100%" />
                  </a-form-item>
                </a-col>
              </a-row>
            </div>

            <!-- 加收明细项 -->
            <div class="section-block section-config">
              <div class="section-title">
                配置项
                <a-button type="dashed" size="small" @click="addItem" style="margin-left: 12px">
                  <template #icon><PlusOutlined /></template>添加配置项
                </a-button>
              </div>

              <div
                v-for="(item, idx) in form.items"
                :key="idx"
                class="config-item-card"
              >
                <div class="config-item-header">
                  <span class="config-item-label">配置项 #{{ idx + 1 }}</span>
                  <a-button type="link" danger size="small" @click="removeItem(idx)">删除</a-button>
                </div>
                <a-row :gutter="8">
                  <a-col :span="5">
                    <a-form-item label="计费" :label-col="{ style: { width: '40px' } }">
                      <a-select v-model:value="item.calcMethod" :options="calcMethodOptions" />
                    </a-form-item>
                  </a-col>
                  <a-col :span="5">
                    <a-form-item label="重量类型" :label-col="{ style: { width: '64px' } }">
                      <a-select v-model:value="item.weightType" :options="weightTypeOptions" allow-clear />
                    </a-form-item>
                  </a-col>
                  <a-col :span="4">
                    <a-form-item label="重量起" :label-col="{ style: { width: '50px' } }">
                      <a-input-number v-model:value="item.weightFrom" style="width: 100%" :min="0" />
                    </a-form-item>
                  </a-col>
                  <a-col :span="4">
                    <a-form-item label="重量止" :label-col="{ style: { width: '50px' } }">
                      <a-input-number v-model:value="item.weightTo" style="width: 100%" :min="0" />
                    </a-form-item>
                  </a-col>
                  <a-col :span="3">
                    <a-form-item label="金额" :label-col="{ style: { width: '36px' } }">
                      <a-input-number v-model:value="item.amount" style="width: 100%" :min="0" :step="0.01" />
                    </a-form-item>
                  </a-col>
                  <a-col :span="3">
                    <a-form-item label="排序" :label-col="{ style: { width: '36px' } }">
                      <a-input-number v-model:value="item.sortOrder" style="width: 100%" :min="0" />
                    </a-form-item>
                  </a-col>
                </a-row>

                <a-row v-if="form.surchargeType === '6'" :gutter="12">
                  <a-col :span="5">
                    <a-form-item label="日单量起">
                      <a-input-number v-model:value="item.dailyVolumeFrom" style="width: 100%" :min="0" />
                    </a-form-item>
                  </a-col>
                  <a-col :span="5">
                    <a-form-item label="日单量止">
                      <a-input-number v-model:value="item.dailyVolumeTo" style="width: 100%" :min="0" />
                    </a-form-item>
                  </a-col>
                </a-row>

                <a-row :gutter="12">
                  <a-col :span="24">
                    <a-form-item label="目的地">
                      <a-select
                        v-model:value="item._selectedProvinceIds"
                        mode="multiple"
                        placeholder="选择省份（可多选）"
                        show-search
                        :filter-option="filterProvince"
                        :options="provinceOptions"
                        style="width: 100%"
                        allow-clear
                      />
                    </a-form-item>
                  </a-col>
                </a-row>
              </div>
            </div>
          </div>

          <!-- 右栏：作用域关联 -->
          <div class="edit-column-right">
            <!-- 作用域关联：业务对象级 (scope=1) -->
            <div v-if="form.scope === 1" class="section-block section-scope">
              <div class="section-title">业务对象关联</div>
              <div class="scope-list">
                <div v-for="(s, idx) in form.scopes" :key="idx" class="scope-row">
                  <a-select
                    v-model:value="s.linkedType"
                    placeholder="类型"
                    :options="bizObjectTypeOptions"
                    style="width: 140px"
                    @change="handleBizTypeChange(idx)"
                  />
                  <a-select
                    v-model:value="s.linkedId"
                    show-search
                    :filter-option="false"
                    placeholder="搜索编号或名称"
                    :options="bizObjOptionsMap[idx] || []"
                    :loading="bizObjLoadingMap[idx]"
                    :dropdown-match-select-width="false"
                    :dropdown-style="{ minWidth: '260px' }"
                    style="flex: 1"
                    @search="(val: string) => handleBizObjSearch(val, idx)"
                    @focus="handleBizObjFocus(idx)"
                    allow-clear
                  />
                  <a-button type="link" danger size="small" @click="removeScope(idx)">删除</a-button>
                </div>
                <a-button type="dashed" size="small" @click="addScope" style="margin-top: 4px">
                  <template #icon><PlusOutlined /></template>添加业务对象
                </a-button>
              </div>
            </div>

            <!-- 作用域关联：报价级 (scope=2) -->
            <div v-if="form.scope === 2" class="section-block section-scope">
              <div class="section-title">报价方案关联</div>
              <div class="scope-list">
                <div v-for="(s, idx) in form.scopes" :key="idx" class="scope-row">
                  <a-input v-model:value="s.linkedId" placeholder="报价方案ID" style="flex: 1" />
                  <a-button type="link" danger size="small" @click="removeScope(idx)">删除</a-button>
                </div>
                <a-button type="dashed" size="small" @click="addScope" style="margin-top: 4px">
                  <template #icon><PlusOutlined /></template>添加报价方案
                </a-button>
              </div>
            </div>

            <!-- scope=0 时显示提示 -->
            <div v-if="form.scope === 0" class="section-block section-scope scope-empty-hint">
              <div class="section-title">作用域关联</div>
              <div style="color: rgba(0,0,0,0.35); font-size: 13px; text-align: center; padding: 16px 0;">
                当前为全局作用域，无需关联业务对象
              </div>
            </div>
          </div>
        </div>
      </a-form>
    </div>


  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import type { Dayjs } from 'dayjs'
import dayjs from 'dayjs'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  getPriceSurchargeDetail,
  createPriceSurcharge,
  updatePriceSurcharge,
  getProvinceList,
  getClientQuotationSummary,
  type SurchargeItem,
  type SurchargeScopeDto,
} from '@/api/express'

const route = useRoute()
const router = useRouter()

const isEdit = computed(() => !!route.params.id)
const editId = computed(() => Number(route.params.id))

// 加收类型选项
const surchargeTypeOptions = [
  { label: '电商大促', value: '1' },
  { label: '春节涨价', value: '2' },
  { label: '目的地加收', value: '3' },
  { label: '周期性加收', value: '4' },
  { label: '拦截费', value: '5' },
  { label: '单量加收', value: '6' },
  { label: '其他', value: '7' },
]

// 计费方式选项
const calcMethodOptions = [
  { label: '单票', value: 1 },
  { label: '按公斤', value: 2 },
  { label: '按比例', value: 3 },
]

// 重量类型选项
const weightTypeOptions = [
  { label: '计费重量', value: 1 },
  { label: '实重', value: 2 },
  { label: '抛重', value: 3 },
]

// 业务对象类型选项（scope=1 时使用）
const bizObjectTypeOptions = [
  { label: '客户 (KH)', value: 'KH' },
  { label: '代理 (DL)', value: 'DL' },
  { label: '网点 (WD)', value: 'WD' },
  { label: '业务员 (YW)', value: 'YW' },
]

// 省份选项
const provinceOptions = ref<{ label: string; value: number }[]>([])
async function loadProvinceOptions() {
  try {
    const list = await getProvinceList()
    provinceOptions.value = list.map(p => ({ label: p.name, value: p.id }))
  } catch { /* ignore */ }
}
function filterProvince(input: string, option: any) {
  return (option?.label ?? '').includes(input)
}

// 业务对象搜索（远程下拉）
const bizObjOptionsMap = ref<Record<number, { label: string; value: string }[]>>({})
const bizObjLoadingMap = ref<Record<number, boolean>>({})
let bizObjSearchTimer: ReturnType<typeof setTimeout> | null = null

function handleBizObjSearch(keyword: string, idx: number) {
  if (bizObjSearchTimer) clearTimeout(bizObjSearchTimer)
  bizObjSearchTimer = setTimeout(() => {
    fetchBizObjOptions(keyword, idx)
  }, 300)
}

async function fetchBizObjOptions(keyword: string, idx: number) {
  const scopeItem = form.scopes[idx]
  if (!scopeItem) return
  bizObjLoadingMap.value[idx] = true
  try {
    const res = await getClientQuotationSummary({
      type: scopeItem.linkedType,
      keyword: keyword || undefined,
      pageIndex: 1,
      pageSize: 20,
    })
    bizObjOptionsMap.value[idx] = (res.items || []).map(item => ({
      label: `${item.code} - ${item.name}`,
      value: item.code,
    }))
  } catch {
    bizObjOptionsMap.value[idx] = []
  } finally {
    bizObjLoadingMap.value[idx] = false
  }
}

// 切换业务对象类型时重新搜索
function handleBizTypeChange(idx: number) {
  form.scopes[idx].linkedId = ''
  bizObjOptionsMap.value[idx] = []
  fetchBizObjOptions('', idx)
}

// scope 行获得焦点时预加载
function handleBizObjFocus(idx: number) {
  if (!bizObjOptionsMap.value[idx]?.length) {
    fetchBizObjOptions('', idx)
  }
}

// 表单
interface ItemForm extends Omit<SurchargeItem, 'destinations'> {
  _selectedProvinceIds: number[]
  destinations: { destType: number; provinceId?: number; cityName?: string }[]
}

const formRef = ref<FormInstance>()
const submitLoading = ref(false)

const form = reactive({
  surchargeType: undefined as string | undefined,
  scope: 0 as number,
  brandCode: 'ST',
  networkPointCode: undefined as string | undefined,
  effectiveDate: undefined as Dayjs | undefined,
  scopes: [] as SurchargeScopeDto[],
  items: [] as ItemForm[],
})

const formRules: Record<string, Rule[]> = {
  surchargeType: [{ required: true, message: '请选择加收类型', trigger: 'change' }],
  scope: [{ required: true, message: '请选择作用域', trigger: 'change' }],
  brandCode: [{ required: true, message: '请输入品牌编码', trigger: 'blur' }],
}

// scope 切换时清空关联数据
function handleScopeChange() {
  form.scopes = []
}

// 添加/删除 scope 关联
function addScope() {
  if (form.scope === 1) {
    form.scopes.push({ linkedType: 'KH', linkedId: '' })
  } else if (form.scope === 2) {
    form.scopes.push({ linkedType: 'QUOTATION', linkedId: '' })
  }
}

function removeScope(idx: number) {
  form.scopes.splice(idx, 1)
}

// 添加/删除配置项
function addItem() {
  form.items.push({
    calcMethod: 1,
    weightRoundingMethod: undefined,
    weightFrom: 0,
    weightTo: 999,
    weightType: 1,
    dailyVolumeFrom: undefined,
    dailyVolumeTo: undefined,
    amount: 0,
    sortOrder: form.items.length,
    destinations: [],
    _selectedProvinceIds: [],
  })
}

function removeItem(idx: number) {
  form.items.splice(idx, 1)
}

// 加载编辑数据
async function loadDetail() {
  if (!isEdit.value) return
  try {
    const detail = await getPriceSurchargeDetail(editId.value)
    form.surchargeType = detail.surchargeType
    form.scope = detail.scope
    form.brandCode = detail.brandCode
    form.networkPointCode = detail.networkPointCode
    form.effectiveDate = detail.effectiveDate ? dayjs(detail.effectiveDate) : undefined
    form.scopes = detail.scopes ?? []
    // 为已有 scope 行加载业务对象选项（编辑模式回显）
    form.scopes.forEach((s, idx) => {
      if (s.linkedType && s.linkedId) {
        fetchBizObjOptions(s.linkedId, idx)
      }
    })
    form.items = (detail.items ?? []).map(item => ({
      ...item,
      _selectedProvinceIds: (item.destinations ?? []).filter(d => d.provinceId).map(d => d.provinceId!),
    }))
  } catch {
    message.error('获取加收详情失败')
  }
}

// 提交
async function handleSubmit() {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }

  // 校验 scope 关联
  if (form.scope === 1 || form.scope === 2) {
    if (form.scopes.length === 0) {
      message.warning('请至少添加一条作用域关联')
      return
    }
    const hasEmpty = form.scopes.some(s => !s.linkedId)
    if (hasEmpty) {
      message.warning('作用域关联中有未填写的编号')
      return
    }
  }

  const payload = {
    surchargeType: form.surchargeType!,
    scope: form.scope,
    brandCode: form.brandCode,
    networkPointCode: form.networkPointCode || undefined,
    effectiveDate: form.effectiveDate?.format('YYYY-MM-DD'),
    scopes: form.scope === 0 ? [] : form.scopes,
    items: form.items.map(item => ({
      calcMethod: item.calcMethod,
      weightRoundingMethod: item.weightRoundingMethod,
      weightFrom: item.weightFrom,
      weightTo: item.weightTo,
      weightType: item.weightType,
      dailyVolumeFrom: item.dailyVolumeFrom,
      dailyVolumeTo: item.dailyVolumeTo,
      amount: item.amount,
      sortOrder: item.sortOrder,
      destinations: item._selectedProvinceIds.map(pid => ({ destType: 1, provinceId: pid })),
    })),
  }

  submitLoading.value = true
  try {
    if (isEdit.value) {
      await updatePriceSurcharge(editId.value, payload)
      message.success('更新成功')
    } else {
      await createPriceSurcharge(payload)
      message.success('创建成功')
    }
    router.push('/express/surcharge')
  } catch { /* handled */ } finally {
    submitLoading.value = false
  }
}

function handleCancel() {
  router.push('/express/surcharge')
}

onMounted(() => {
  loadProvinceOptions()
  loadDetail()
})
</script>

<style scoped lang="scss">
.surcharge-edit-page {
  overflow: hidden !important;
  padding: 0 !important;
}

.edit-scroll-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}

// 左右两栏布局
.edit-columns {
  display: flex;
  gap: 12px;
  align-items: flex-start;
}

.edit-column-left {
  flex: 1;
  min-width: 0;
}

.edit-column-right {
  width: 240px;
  flex-shrink: 0;
}

// 区域块通用样式
.section-block {
  padding: 16px;
  border-radius: 6px;
  margin-bottom: 12px;
}

.section-basic {
  background: #ffffff;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
  border: 1px solid #f0f0f0;
}

.section-scope {
  background: #f7f9fc;
  border: 1px solid #e8edf5;
}

.section-config {
  background: #f5f5f5;
  border: 1px solid #ebebeb;
}

.section-title {
  font-size: 14px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.85);
  margin-bottom: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  align-items: center;
}

// 紧凑表单项
.edit-scroll-body :deep(.ant-form-item) {
  margin-bottom: 12px;
}

// 作用域关联列表
.scope-list {
  margin-bottom: 16px;
}

.scope-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
  flex-wrap: wrap;
}

// 右栏 scope-row 内选择器自适应
.edit-column-right .scope-row {
  flex-direction: column;
  align-items: stretch;

  .ant-select,
  .ant-input {
    width: 100% !important;
  }
}

// scope=0 时的空状态提示
.scope-empty-hint {
  opacity: 0.7;
}

// 配置项卡片（白色背景形成层次感）
.config-item-card {
  background: #ffffff;
  border-radius: 6px;
  padding: 12px;
  margin-bottom: 10px;
  border: 1px solid #e8e8e8;
}

.config-item-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.config-item-label {
  font-size: 13px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.65);
}

// 配置项内部的表单项更紧凑
.config-item-card :deep(.ant-form-item) {
  margin-bottom: 8px;
}


</style>
