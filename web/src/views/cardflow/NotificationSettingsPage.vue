<script setup lang="ts">
/**
 * NotificationSettingsPage.vue — 通知渠道配置页
 *
 * 路由：/cardflow/settings/notification  权限：cardflow:admin
 * 通过 Tab 切换不同通知渠道的配置：
 *   - 钉钉：可配置 AppKey / AppSecret / AgentId / detailUrl 模板 / 回调URL
 *   - 企业微信、微信小程序：占位（即将支持）
 */
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  SaveOutlined,
  ApiOutlined,
  CheckCircleFilled,
  CloseCircleFilled,
  WechatOutlined,
  DingdingOutlined,
  AppstoreOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { get, post, put } from '@/api/request'

// ==================== 渠道定义 ====================

type ChannelKey = 'dingtalk' | 'wecom' | 'wxmini'

interface ChannelMeta {
  key: ChannelKey
  label: string
  enabled: boolean
}

const channels: ChannelMeta[] = [
  { key: 'dingtalk', label: '钉钉', enabled: true },
  { key: 'wecom', label: '企业微信', enabled: false },
  { key: 'wxmini', label: '微信小程序', enabled: false },
]

const activeKey = ref<ChannelKey>('dingtalk')

// ==================== 钉钉配置表单 ====================

interface DingtalkForm {
  appKey: string
  appSecret: string
  agentId: string
  todoSyncEnabled: boolean
  detailUrlTemplate: string
  callbackUrl: string
}

const dingForm = reactive<DingtalkForm>({
  appKey: '',
  appSecret: '',
  agentId: '',
  todoSyncEnabled: false,
  detailUrlTemplate: '',
  callbackUrl: '',
})

const dingFormRef = ref<any>(null)
function setDingFormRef(el: any) {
  if (el) dingFormRef.value = el
}

const dingRules = {
  appKey: [{ required: true, message: '请输入 AppKey' }],
  appSecret: [{ required: true, message: '请输入 AppSecret' }],
  agentId: [{ required: true, message: '请输入 AgentId' }],
}

// ==================== 状态 ====================

const loading = ref(false)
const saving = ref(false)
const testing = ref(false)

const testResult = ref<{ success: boolean; message: string } | null>(null)

// ==================== 加载配置 ====================

async function loadConfig() {
  loading.value = true
  try {
    const res: any = await get('/cardflow/notification-settings')
    if (res) {
      // 兼容历史字段命名
      dingForm.appKey = res.dingtalkAppKey ?? res.appKey ?? ''
      dingForm.appSecret = res.dingtalkAppSecret ?? res.appSecret ?? ''
      dingForm.agentId = res.dingtalkAgentId ?? res.agentId ?? ''
      dingForm.todoSyncEnabled =
        res.dingtalkEnabled ?? res.todoSyncEnabled ?? false
      dingForm.detailUrlTemplate = res.detailUrlTemplate ?? ''
      dingForm.callbackUrl = res.callbackUrl ?? buildDefaultCallback()
    } else {
      dingForm.callbackUrl = buildDefaultCallback()
    }
  } catch {
    dingForm.callbackUrl = buildDefaultCallback()
  } finally {
    loading.value = false
  }
}

function buildDefaultCallback(): string {
  const origin = typeof window !== 'undefined' ? window.location.origin : ''
  return `${origin}/api/cardflow/callback/dingtalk`
}

// ==================== 保存当前 Tab 配置 ====================

async function handleSave() {
  if (activeKey.value !== 'dingtalk') {
    message.info('该渠道暂未开放，无需保存')
    return
  }

  try {
    await dingFormRef.value?.validate()
  } catch {
    return
  }

  saving.value = true
  try {
    await put('/cardflow/notification-settings', {
      dingtalkAppKey: dingForm.appKey,
      dingtalkAppSecret: dingForm.appSecret,
      dingtalkAgentId: dingForm.agentId,
      dingtalkEnabled: dingForm.todoSyncEnabled,
      detailUrlTemplate: dingForm.detailUrlTemplate,
    })
    message.success('保存成功')
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

// ==================== 测试连接 ====================

async function handleTest() {
  if (!dingForm.appKey || !dingForm.appSecret || !dingForm.agentId) {
    message.warning('请先填写 AppKey、AppSecret 与 AgentId')
    return
  }
  testing.value = true
  testResult.value = null
  try {
    const res: any = await post('/cardflow/notification-settings/test', {
      channel: 'dingtalk',
      appKey: dingForm.appKey,
      appSecret: dingForm.appSecret,
      agentId: dingForm.agentId,
    })
    testResult.value = {
      success: true,
      message: res?.message || '连接成功，已成功获取访问令牌',
    }
  } catch (e: any) {
    testResult.value = {
      success: false,
      message: e?.message || e?.data?.message || '连接失败，请检查配置后重试',
    }
  } finally {
    testing.value = false
  }
}

// ==================== Tab 标签渲染 ====================

const tabTitle = computed(() => (key: ChannelKey) => {
  const ch = channels.find(c => c.key === key)
  if (!ch) return ''
  return ch.label
})

function iconOf(key: ChannelKey) {
  if (key === 'dingtalk') return DingdingOutlined
  if (key === 'wecom') return WechatOutlined
  return AppstoreOutlined
}

void tabTitle

onMounted(() => {
  loadConfig()
})
</script>

<template>
  <div class="page-container">
    <PageHeader>
      <template #actions>
        <a-button type="primary" :loading="saving" @click="handleSave">
          <template #icon><SaveOutlined /></template>
          保存
        </a-button>
      </template>
    </PageHeader>

    <a-spin :spinning="loading" wrapper-class-name="notif-spin">
      <a-tabs v-model:active-key="activeKey" class="notif-tabs" type="line">
        <a-tab-pane v-for="ch in channels" :key="ch.key">
          <template #tab>
            <span class="tab-label">
              <component :is="iconOf(ch.key)" />
              <span>{{ ch.label }}</span>
              <span v-if="!ch.enabled" class="tab-disabled">（未启用）</span>
            </span>
          </template>

          <!-- 钉钉配置 -->
          <div v-if="ch.key === 'dingtalk'" class="tab-content">
            <a-form
              :ref="setDingFormRef"
              :model="dingForm"
              :rules="dingRules"
              layout="vertical"
              class="dingtalk-form"
            >
              <a-row :gutter="24">
                <a-col :span="12">
                  <a-form-item label="AppKey" name="appKey">
                    <a-input
                      v-model:value="dingForm.appKey"
                      placeholder="请输入钉钉应用 AppKey"
                      allow-clear
                    />
                  </a-form-item>
                </a-col>
                <a-col :span="12">
                  <a-form-item label="AppSecret" name="appSecret">
                    <a-input-password
                      v-model:value="dingForm.appSecret"
                      placeholder="请输入钉钉应用 AppSecret"
                      allow-clear
                    />
                  </a-form-item>
                </a-col>
              </a-row>

              <a-row :gutter="24">
                <a-col :span="12">
                  <a-form-item label="AgentId" name="agentId">
                    <a-input
                      v-model:value="dingForm.agentId"
                      placeholder="请输入钉钉应用 AgentId"
                      allow-clear
                    />
                  </a-form-item>
                </a-col>
                <a-col :span="12">
                  <a-form-item label="启用待办同步">
                    <a-switch
                      v-model:checked="dingForm.todoSyncEnabled"
                      checked-children="已启用"
                      un-checked-children="已禁用"
                    />
                    <span class="form-help inline">
                      开启后，新发起 / 流转的卡片将自动推送至钉钉待办。
                    </span>
                  </a-form-item>
                </a-col>
              </a-row>

              <a-form-item label="detailUrl 模板">
                <a-input
                  v-model:value="dingForm.detailUrlTemplate"
                  placeholder="https://yourdomain.com/cardflow/approve/{id}"
                  allow-clear
                />
                <div class="form-help">使用 {id} 作为卡片 ID 占位符</div>
              </a-form-item>

              <a-form-item label="回调 URL">
                <a-input :value="dingForm.callbackUrl" disabled />
                <div class="form-help">
                  此 URL 需配置在钉钉开放平台的事件回调中
                </div>
              </a-form-item>

              <a-form-item label=" " :colon="false">
                <a-space :size="12" align="start">
                  <a-button :loading="testing" @click="handleTest">
                    <template #icon><ApiOutlined /></template>
                    测试连接
                  </a-button>
                  <div v-if="testResult" class="test-result" :class="{ ok: testResult.success, fail: !testResult.success }">
                    <CheckCircleFilled v-if="testResult.success" />
                    <CloseCircleFilled v-else />
                    <span>{{ testResult.success ? '连接成功' : '连接失败' }}</span>
                    <span class="test-detail">{{ testResult.message }}</span>
                  </div>
                </a-space>
              </a-form-item>
            </a-form>
          </div>

          <!-- 企业微信 / 微信小程序：占位 -->
          <div v-else class="tab-content">
            <a-empty
              :description="false"
              :image="undefined"
              class="coming-soon"
            >
              <template #image>
                <component :is="iconOf(ch.key)" class="coming-icon" />
              </template>
              <div class="coming-title">即将支持</div>
              <div class="coming-tip">{{ ch.label }} 渠道正在适配中，敬请期待。</div>
            </a-empty>
          </div>
        </a-tab-pane>
      </a-tabs>
    </a-spin>
  </div>
</template>

<style scoped lang="scss">
.page-container {
  padding: 16px 16px 24px;
}

.notif-tabs {
  background: var(--bg-card);
  border-radius: 8px;
  padding: 8px 24px 24px;
  border: 1px solid var(--border);

  :deep(.ant-tabs-tab) {
    font-size: 14px;
    padding: 12px 4px;
  }

  :deep(.ant-tabs-tab + .ant-tabs-tab) {
    margin-left: 24px;
  }
}

.tab-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;

  .anticon {
    font-size: 16px;
  }
}

.tab-disabled {
  color: var(--text-3);
  font-size: 12px;
  margin-left: 2px;
}

.tab-content {
  padding-top: 8px;
  min-height: 320px;
}

.dingtalk-form {
  max-width: 880px;

  :deep(.ant-form-item-label > label) {
    font-weight: 500;
    color: var(--text-1);
  }
}

.form-help {
  margin-top: 4px;
  font-size: 12px;
  color: var(--text-3);
  line-height: 1.6;

  &.inline {
    display: inline-block;
    margin-left: 12px;
    margin-top: 0;
  }
}

.test-result {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 5px 12px;
  border-radius: 4px;
  font-size: 13px;
  line-height: 22px;

  &.ok {
    color: var(--color-success-text);
    background: var(--color-success-light);
    border: 1px solid color-mix(in srgb, var(--color-success) 35%, transparent);
  }
  &.fail {
    color: var(--color-danger-text);
    background: var(--color-danger-light);
    border: 1px solid color-mix(in srgb, var(--color-danger) 35%, transparent);
  }

  .test-detail {
    color: inherit;
    opacity: 0.8;
    margin-left: 4px;
  }
}

.coming-soon {
  margin: 48px auto;

  .coming-icon {
    font-size: 64px;
    color: var(--text-3);
  }

  .coming-title {
    margin-top: 12px;
    font-size: 16px;
    font-weight: 600;
    color: var(--text-2);
  }

  .coming-tip {
    margin-top: 4px;
    font-size: 13px;
    color: var(--text-3);
  }
}
</style>
