<script setup lang="ts">
import { ref, reactive, watch, computed } from 'vue'
import { message } from 'ant-design-vue'
import { SaveOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import { useEnterpriseInfoStore } from '@/stores/enterpriseInfo'

const enterpriseInfoStore = useEnterpriseInfoStore()

// 本地编辑副本，不直接修改 store
const editForm = reactive({
  name: enterpriseInfoStore.name,
  shortName: enterpriseInfoStore.shortName,
  logoUrl: enterpriseInfoStore.logoUrl || '',
})

// 监听 store 变化同步到本地
watch(() => ({
  name: enterpriseInfoStore.name,
  shortName: enterpriseInfoStore.shortName,
  logoUrl: enterpriseInfoStore.logoUrl,
}), (val) => {
  editForm.name = val.name
  editForm.shortName = val.shortName
  editForm.logoUrl = val.logoUrl || ''
}, { deep: true })

// 预览用的显示名称
const previewDisplayName = computed(() => {
  return editForm.shortName || editForm.name || 'MDSTO'
})

// 预览用的是否有Logo
const previewHasLogo = computed(() => {
  return !!editForm.logoUrl && editForm.logoUrl.trim() !== ''
})

const saving = ref(false)

async function handleSave() {
  if (!editForm.name.trim()) {
    message.warning('企业全称不能为空')
    return
  }
  if (!editForm.shortName.trim()) {
    message.warning('企业简称不能为空')
    return
  }

  saving.value = true
  try {
    const success = await enterpriseInfoStore.updateInfo({
      name: editForm.name.trim(),
      shortName: editForm.shortName.trim(),
      logoUrl: editForm.logoUrl.trim() || undefined,
    })
    if (success) {
      message.success('企业信息保存成功')
      // 更新页面标题
      document.title = previewDisplayName.value
    } else {
      message.error('保存失败')
    }
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

function handleReset() {
  editForm.name = 'MDSTO'
  editForm.shortName = 'MDSTO'
  editForm.logoUrl = ''
  message.info('已恢复默认值')
}
</script>

<template>
  <div class="page-container">
    <PageHeader title="企业信息配置" description="配置企业名称和Logo，设置后将显示在系统界面和登录页" />

    <a-row :gutter="24">
      <!-- 左侧：配置区域 -->
      <a-col :xs="24" :lg="12" :xl="10">
        <a-card title="基本设置" :bordered="false" class="config-card">
          <a-form layout="horizontal" :label-col="{ span: 6 }" :wrapper-col="{ span: 18 }">
            <a-form-item label="企业全称" required>
              <a-input
                v-model:value="editForm.name"
                placeholder="请输入企业全称"
                :maxlength="100"
                show-count
              />
            </a-form-item>
            <a-form-item label="企业简称" required>
              <a-input
                v-model:value="editForm.shortName"
                placeholder="请输入企业简称（用于顶部Logo显示）"
                :maxlength="20"
                show-count
              />
            </a-form-item>
            <a-form-item label="Logo URL">
              <a-input
                v-model:value="editForm.logoUrl"
                placeholder="请输入Logo图片URL（可选）"
                :maxlength="500"
              />
              <template #extra>
                <div style="color: #999; font-size: 12px; margin-top: 4px">
                  建议使用透明背景的PNG图片，尺寸建议 120x40 像素。留空则显示文字Logo。
                </div>
              </template>
            </a-form-item>
          </a-form>
        </a-card>

        <!-- 操作按钮 -->
        <div class="action-buttons">
          <a-button type="primary" :loading="saving" @click="handleSave" size="large">
            <SaveOutlined v-if="!saving" style="margin-right: 4px" />
            保存配置
          </a-button>
          <a-button @click="handleReset" size="large" style="margin-left: 12px">
            <ReloadOutlined style="margin-right: 4px" />
            恢复默认
          </a-button>
        </div>
      </a-col>

      <!-- 右侧：实时预览 -->
      <a-col :xs="24" :lg="12" :xl="14">
        <a-card title="效果预览" :bordered="false" class="config-card preview-card">
          <div class="preview-section">
            <!-- 顶部导航预览 -->
            <div class="preview-block">
              <h4>顶部导航栏</h4>
              <div class="preview-topnav">
                <div class="preview-nav-left">
                  <div class="preview-logo">
                    <img
                      v-if="previewHasLogo"
                      :src="editForm.logoUrl"
                      :alt="previewDisplayName"
                      class="preview-logo-img"
                      @error="(e) => (e.target as HTMLImageElement).style.display = 'none'"
                    />
                    <span v-if="!previewHasLogo" class="preview-logo-text">{{ previewDisplayName }}</span>
                  </div>
                </div>
                <div class="preview-nav-right">
                  <div class="preview-user">管理员</div>
                </div>
              </div>
            </div>

            <!-- 登录页预览 -->
            <div class="preview-block">
              <h4>登录页</h4>
              <div class="preview-login">
                <div class="preview-login-card">
                  <div class="preview-login-header">
                    <h1 class="preview-login-title">{{ previewDisplayName }}</h1>
                    <p class="preview-login-subtitle">企业管理系统</p>
                  </div>
                  <div class="preview-login-form">
                    <div class="preview-input"></div>
                    <div class="preview-input"></div>
                    <div class="preview-btn"></div>
                  </div>
                </div>
              </div>
            </div>

            <!-- 仪表盘欢迎语预览 -->
            <div class="preview-block">
              <h4>仪表盘欢迎语</h4>
              <div class="preview-dashboard">
                <h3 class="preview-welcome-title">欢迎使用 {{ editForm.name }} 企业管理系统</h3>
              </div>
            </div>

            <!-- 浏览器标签预览 -->
            <div class="preview-block">
              <h4>浏览器标签页</h4>
              <div class="preview-browser-tab">
                <span class="preview-favicon">🔗</span>
                <span class="preview-tab-title">{{ previewDisplayName }}</span>
              </div>
            </div>
          </div>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<style scoped>
.config-card {
  margin-bottom: 16px;
}

.action-buttons {
  margin-bottom: 24px;
  padding: 16px 0;
}

.preview-card {
  position: sticky;
  top: 16px;
}

.preview-section {
  max-height: calc(100vh - 200px);
  overflow-y: auto;
}

.preview-block {
  margin-bottom: 24px;
}

.preview-block h4 {
  margin-bottom: 12px;
  color: #666;
  font-size: 13px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

/* 顶部导航预览样式 */
.preview-topnav {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 48px;
  padding: 0 16px;
  background: #001529;
  border-radius: 6px;
}

.preview-nav-left {
  display: flex;
  align-items: center;
}

.preview-logo {
  display: flex;
  align-items: center;
}

.preview-logo-img {
  height: 28px;
  max-width: 120px;
  object-fit: contain;
}

.preview-logo-text {
  font-size: 18px;
  font-weight: 700;
  color: #fff;
  letter-spacing: 1px;
}

.preview-nav-right {
  display: flex;
  align-items: center;
}

.preview-user {
  color: rgba(255, 255, 255, 0.85);
  font-size: 13px;
}

/* 登录页预览样式 */
.preview-login {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 200px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 6px;
  padding: 24px;
}

.preview-login-card {
  width: 100%;
  max-width: 280px;
  background: #fff;
  border-radius: 8px;
  padding: 24px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
}

.preview-login-header {
  text-align: center;
  margin-bottom: 16px;
}

.preview-login-title {
  font-size: 20px;
  font-weight: 800;
  color: var(--color-primary);
  margin: 0 0 4px;
  letter-spacing: 2px;
}

.preview-login-subtitle {
  font-size: 12px;
  color: #666;
  margin: 0;
}

.preview-login-form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.preview-input {
  height: 32px;
  background: #f5f5f5;
  border-radius: 4px;
}

.preview-btn {
  height: 32px;
  background: var(--color-primary);
  border-radius: 4px;
}

/* 仪表盘预览样式 */
.preview-dashboard {
  padding: 20px;
  background: linear-gradient(135deg, var(--color-primary) 0%, var(--color-primary-active) 100%);
  border-radius: 6px;
}

.preview-welcome-title {
  color: #fff;
  font-size: 16px;
  font-weight: 700;
  margin: 0;
}

/* 浏览器标签预览样式 */
.preview-browser-tab {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: #e8e8e8;
  border-radius: 8px 8px 0 0;
  border: 1px solid #d9d9d9;
  border-bottom: none;
}

.preview-favicon {
  font-size: 14px;
}

.preview-tab-title {
  font-size: 13px;
  color: #333;
}

.preview-block:last-child {
  margin-bottom: 0;
}
</style>
