<template>
  <div class="page-container">
    <PageHeader title="安全配置" description="管理系统安全策略与参数设置" />

    <a-spin :spinning="loading">
      <a-form :model="formState" layout="vertical">
        <!-- 会话超时设置 -->
        <a-card title="会话超时设置" :bordered="false" style="margin-bottom: 16px">
          <a-row :gutter="24">
            <a-col :span="8">
              <a-form-item label="空闲警告时间（分钟）">
                <a-input-number
                  v-model:value="formState.idleWarningMinutes"
                  :min="1"
                  :max="120"
                  style="width: 100%"
                />
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="空闲锁屏时间（分钟）">
                <a-input-number
                  v-model:value="formState.idleLockscreenMinutes"
                  :min="1"
                  :max="120"
                  style="width: 100%"
                />
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="空闲退出时间（分钟）">
                <a-input-number
                  v-model:value="formState.idleLogoutMinutes"
                  :min="1"
                  :max="240"
                  style="width: 100%"
                />
              </a-form-item>
            </a-col>
          </a-row>
        </a-card>

        <!-- 设备控制 -->
        <a-card title="设备控制" :bordered="false" style="margin-bottom: 16px">
          <a-row :gutter="24">
            <a-col :span="8">
              <a-form-item label="最大同时在线设备数">
                <a-input-number
                  v-model:value="formState.maxDevicesPerUser"
                  :min="1"
                  :max="10"
                  style="width: 100%"
                />
              </a-form-item>
            </a-col>
          </a-row>
        </a-card>

        <!-- Token配置 -->
        <a-card title="Token配置" :bordered="false" style="margin-bottom: 16px">
          <a-row :gutter="24">
            <a-col :span="8">
              <a-form-item label="Access Token有效期（分钟）">
                <a-input-number
                  v-model:value="formState.accessTokenMinutes"
                  :min="5"
                  :max="1440"
                  style="width: 100%"
                />
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="Refresh Token有效期（天）">
                <a-input-number
                  v-model:value="formState.refreshTokenDays"
                  :min="1"
                  :max="30"
                  style="width: 100%"
                />
              </a-form-item>
            </a-col>
          </a-row>
        </a-card>

        <!-- 异常检测 -->
        <a-card title="异常检测" :bordered="false" style="margin-bottom: 16px">
          <a-row :gutter="24">
            <a-col :span="8">
              <a-form-item label="IP变化强制重新认证">
                <a-switch v-model:checked="formState.ipChangeForceReauth" />
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="设备指纹变化强制重新认证">
                <a-switch v-model:checked="formState.fingerprintChangeForceReauth" />
              </a-form-item>
            </a-col>
          </a-row>
        </a-card>

        <!-- 登录保护 -->
        <a-card title="登录保护" :bordered="false" style="margin-bottom: 16px">
          <a-row :gutter="24">
            <a-col :span="8">
              <a-form-item label="登录失败锁定次数">
                <a-input-number
                  v-model:value="formState.loginFailLockCount"
                  :min="3"
                  :max="20"
                  style="width: 100%"
                />
              </a-form-item>
            </a-col>
            <a-col :span="8">
              <a-form-item label="登录失败锁定时长（分钟）">
                <a-input-number
                  v-model:value="formState.loginFailLockMinutes"
                  :min="5"
                  :max="1440"
                  style="width: 100%"
                />
              </a-form-item>
            </a-col>
          </a-row>
        </a-card>

        <!-- 保存按钮 -->
        <div class="form-actions">
          <a-button type="primary" :loading="saving" @click="handleSave">
            保存配置
          </a-button>
        </div>
      </a-form>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@/components/PageHeader.vue'
import { getSecurityConfigs, updateSecurityConfigs, type SecurityConfigItem } from '@/api/security'

interface FormState {
  idleWarningMinutes: number
  idleLockscreenMinutes: number
  idleLogoutMinutes: number
  maxDevicesPerUser: number
  accessTokenMinutes: number
  refreshTokenDays: number
  ipChangeForceReauth: boolean
  fingerprintChangeForceReauth: boolean
  loginFailLockCount: number
  loginFailLockMinutes: number
}

const loading = ref(false)
const saving = ref(false)

const formState = reactive<FormState>({
  idleWarningMinutes: 25,
  idleLockscreenMinutes: 28,
  idleLogoutMinutes: 30,
  maxDevicesPerUser: 3,
  accessTokenMinutes: 480,
  refreshTokenDays: 7,
  ipChangeForceReauth: false,
  fingerprintChangeForceReauth: false,
  loginFailLockCount: 5,
  loginFailLockMinutes: 30,
})

/** 配置key与表单字段的映射 */
const configKeyMap: Record<string, keyof FormState> = {
  'IdleWarningMinutes': 'idleWarningMinutes',
  'IdleLockscreenMinutes': 'idleLockscreenMinutes',
  'IdleLogoutMinutes': 'idleLogoutMinutes',
  'MaxDevicesPerUser': 'maxDevicesPerUser',
  'AccessTokenMinutes': 'accessTokenMinutes',
  'RefreshTokenDays': 'refreshTokenDays',
  'IpChangeForceReauth': 'ipChangeForceReauth',
  'FingerprintChangeForceReauth': 'fingerprintChangeForceReauth',
  'LoginFailLockCount': 'loginFailLockCount',
  'LoginFailLockMinutes': 'loginFailLockMinutes',
}

const booleanKeys = new Set(['ipChangeForceReauth', 'fingerprintChangeForceReauth'])

async function fetchConfigs() {
  loading.value = true
  try {
    const list = await getSecurityConfigs()
    list.forEach((item: SecurityConfigItem) => {
      const field = configKeyMap[item.configKey]
      if (field) {
        if (booleanKeys.has(field)) {
          ;(formState as any)[field] = item.configValue === 'true' || item.configValue === '1'
        } else {
          ;(formState as any)[field] = Number(item.configValue) || 0
        }
      }
    })
  } catch (e: any) {
    message.error(e.message || '获取安全配置失败')
  } finally {
    loading.value = false
  }
}

async function handleSave() {
  saving.value = true
  try {
    const configs: Record<string, string> = {}
    for (const [configKey, field] of Object.entries(configKeyMap)) {
      const val = (formState as any)[field]
      configs[configKey] = String(val)
    }
    await updateSecurityConfigs(configs)
    message.success('安全配置保存成功')
  } catch (e: any) {
    message.error(e.message || '保存安全配置失败')
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  fetchConfigs()
})
</script>

<style scoped lang="scss">
.form-actions {
  display: flex;
  justify-content: flex-start;
  padding: 16px 0;
}
</style>
