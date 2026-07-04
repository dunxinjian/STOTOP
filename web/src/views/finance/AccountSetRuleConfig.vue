<template>
  <div class="page-container">
    <PageHeader title="账套规则">
      <template #left>
        <AccountSetSelector style="width: 200px;" />
      </template>
      <template #actions>
        <a-button
          v-if="canEdit"
          type="primary"
          :loading="saving"
          :disabled="!accountSetId"
          @click="handleSave"
        >保存</a-button>
      </template>
    </PageHeader>

    <div class="rule-content">
      <a-spin :spinning="loading">
        <a-alert
          message="账套级会计控制规则：未配置项一律回退系统默认行为（零行为变更）。规则仅对新业务生效，不回溯历史凭证。"
          type="info"
          show-icon
          class="rule-tip"
        />

        <a-form :label-col="{ style: { width: '160px' } }" class="rule-form">
          <a-divider orientation="left">凭证审核控制</a-divider>
          <a-form-item label="制单审核分离">
            <a-switch v-model:checked="form.fRequireAuditSeparation" :disabled="!canEdit" />
            <div class="field-hint">开启后制单人不可审核本人制单的凭证（批量审核逐张跳过并留痕）；默认关闭=不校验。</div>
          </a-form-item>

          <a-divider orientation="left">期末结转科目</a-divider>
          <a-alert
            v-if="hasClosed"
            message="该账套已存在已结账期间：修改结转科目仅影响下次结转，历史结转凭证需反结账后重新结账才会使用新科目。"
            type="warning"
            show-icon
            class="rule-tip"
          />
          <a-form-item label="本年利润科目编码">
            <a-input
              v-model:value="form.fProfitAccountCode"
              placeholder="留空 = 默认 3103"
              style="width: 240px"
              allow-clear
              :disabled="!canEdit"
            />
            <div class="field-hint">损益结转目标科目（本年利润）；编码须在当前账套科目表中存在。</div>
          </a-form-item>
          <a-form-item label="未分配利润科目编码">
            <a-input
              v-model:value="form.fRetainedAccountCode"
              placeholder="留空 = 默认 310405"
              style="width: 240px"
              allow-clear
              :disabled="!canEdit"
            />
            <div class="field-hint">12月年度利润结转目标科目（利润分配-未分配利润）。</div>
          </a-form-item>

          <a-divider orientation="left">凭证字</a-divider>
          <a-form-item label="启用凭证字">
            <a-checkbox-group v-model:value="form.fEnabledVoucherWords" :disabled="!canEdit">
              <a-checkbox
                v-for="word in ALL_VOUCHER_WORDS"
                :key="word"
                :value="word"
                :disabled="word === '记'"
              >{{ word }}</a-checkbox>
            </a-checkbox-group>
            <div class="field-hint">
              限制新建/导入凭证可用的凭证字；「记」为系统默认字不可停用。移除仅影响新建，历史凭证不受影响。
            </div>
          </a-form-item>
        </a-form>
      </a-spin>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { useAccountSetStore } from '@/stores/accountSet'
import { usePermission, FinancePermissions } from '@/utils/permission'
import {
  getAccountSetRule,
  updateAccountSetRule,
  hasClosedPeriod,
} from '@/api/finance'

const ALL_VOUCHER_WORDS = ['记', '收', '付', '转']

const accountSetStore = useAccountSetStore()
const { has } = usePermission()
const canEdit = computed(() => has(FinancePermissions.AccountSetRuleEdit))

const accountSetId = computed(() => accountSetStore.getCurrentAccountSetId())
const loading = ref(false)
const saving = ref(false)
const hasClosed = ref(false)

const form = ref({
  fRequireAuditSeparation: false,
  fProfitAccountCode: undefined as string | undefined,
  fRetainedAccountCode: undefined as string | undefined,
  fEnabledVoucherWords: [...ALL_VOUCHER_WORDS] as string[],
})

async function loadData() {
  const id = accountSetId.value
  if (!id) return
  loading.value = true
  try {
    const [rule, closed] = await Promise.all([
      getAccountSetRule(id),
      hasClosedPeriod(id).catch(() => false),
    ])
    form.value = {
      fRequireAuditSeparation: rule.fRequireAuditSeparation,
      fProfitAccountCode: rule.fProfitAccountCode ?? undefined,
      fRetainedAccountCode: rule.fRetainedAccountCode ?? undefined,
      fEnabledVoucherWords: rule.fEnabledVoucherWords?.length
        ? rule.fEnabledVoucherWords
        : [...ALL_VOUCHER_WORDS],
    }
    hasClosed.value = closed
  } catch (e) {
    console.error('加载账套规则失败', e)
  } finally {
    loading.value = false
  }
}

async function handleSave() {
  const id = accountSetId.value
  if (!id) {
    message.warning('请先选择账套')
    return
  }
  saving.value = true
  try {
    const saved = await updateAccountSetRule(id, {
      fRequireAuditSeparation: form.value.fRequireAuditSeparation,
      fProfitAccountCode: form.value.fProfitAccountCode ?? null,
      fRetainedAccountCode: form.value.fRetainedAccountCode ?? null,
      fEnabledVoucherWords: form.value.fEnabledVoucherWords,
    })
    form.value = {
      fRequireAuditSeparation: saved.fRequireAuditSeparation,
      fProfitAccountCode: saved.fProfitAccountCode ?? undefined,
      fRetainedAccountCode: saved.fRetainedAccountCode ?? undefined,
      fEnabledVoucherWords: saved.fEnabledVoucherWords?.length
        ? saved.fEnabledVoucherWords
        : [...ALL_VOUCHER_WORDS],
    }
    message.success('账套规则已保存')
  } catch (e) {
    // 请求层已弹出后端 message（如科目不存在校验），此处仅记录
    console.error('保存账套规则失败', e)
  } finally {
    saving.value = false
  }
}

watch(() => accountSetStore.currentAccountSetId, () => loadData())
onMounted(loadData)
</script>

<style scoped lang="scss">
.rule-content {
  padding: 16px;
  background: var(--color-bg-container);
  border-radius: 8px;
}

.rule-tip {
  margin-bottom: 16px;
}

.rule-form {
  max-width: 720px;
}

.field-hint {
  margin-top: 4px;
  font-size: 12px;
  color: var(--color-text-tertiary);
  line-height: 1.6;
}
</style>
