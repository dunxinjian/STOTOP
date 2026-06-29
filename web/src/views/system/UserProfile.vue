<template>
  <div class="page-container profile-page">
    <div class="profile-shell">
      <!-- 身份头部带 -->
      <section class="identity-band">
        <a-avatar :size="64" :src="userStore.userInfo?.avatar" class="identity-band__avatar">
          {{ userInitial }}
        </a-avatar>

        <div class="identity-band__main">
          <div class="identity-band__title">
            <h1 class="identity-band__name">{{ displayName }}</h1>
            <a-tag class="role-tag">{{ roleLabel }}</a-tag>
          </div>

          <div class="meta-chips">
            <span class="meta-chip">
              <IdcardOutlined class="meta-chip__icon" />
              <span class="meta-chip__label">账号</span>
              <span class="meta-chip__value">{{ userStore.userInfo?.username }}</span>
            </span>
            <span class="meta-chip">
              <PhoneOutlined class="meta-chip__icon" />
              <span class="meta-chip__label">手机号</span>
              <span class="meta-chip__value">{{ userStore.userInfo?.phone || '-' }}</span>
            </span>
            <span class="meta-chip">
              <MailOutlined class="meta-chip__icon" />
              <span class="meta-chip__label">邮箱</span>
              <span class="meta-chip__value">{{ userStore.userInfo?.email || '-' }}</span>
            </span>
          </div>
        </div>
      </section>

      <!-- 设置网格 -->
      <div class="settings-grid">
        <!-- 基本信息 -->
        <section class="section-card">
          <header class="section-card__head">
            <span class="section-card__strip" />
            <UserOutlined class="section-card__icon" />
            <h2 class="section-card__title">基本信息</h2>
          </header>

          <a-form
            ref="profileFormRef"
            :model="profileForm"
            :rules="profileRules"
            layout="vertical"
            class="dense-form"
          >
            <div class="field-grid field-grid--two">
              <a-form-item label="真实姓名" name="realName">
                <a-input
                  v-model:value="profileForm.realName"
                  placeholder="请输入真实姓名"
                  :maxLength="50"
                  allow-clear
                />
              </a-form-item>
              <a-form-item label="邮箱" name="email">
                <a-input
                  v-model:value="profileForm.email"
                  placeholder="请输入邮箱地址"
                  :maxLength="100"
                  allow-clear
                />
              </a-form-item>
              <a-form-item label="手机号" name="phone">
                <a-input
                  v-model:value="profileForm.phone"
                  placeholder="请输入手机号"
                  :maxLength="20"
                  allow-clear
                />
              </a-form-item>
              <a-form-item label="头像链接" name="avatar">
                <a-input
                  v-model:value="profileForm.avatar"
                  placeholder="请输入头像图片链接"
                  :maxLength="500"
                  allow-clear
                />
              </a-form-item>
            </div>

            <footer class="section-card__foot">
              <a-button
                type="primary"
                :loading="savingProfile"
                @click="handleSaveProfile"
              >
                <template #icon><SaveOutlined /></template>
                保存基本信息
              </a-button>
            </footer>
          </a-form>
        </section>

        <!-- 安全设置 -->
        <section class="section-card section-card--security">
          <header class="section-card__head">
            <span class="section-card__strip section-card__strip--danger" />
            <span class="section-card__icon-chip">
              <SafetyCertificateOutlined />
            </span>
            <h2 class="section-card__title">安全设置</h2>
            <span class="section-card__hint">修改后需重新登录</span>
          </header>

          <div class="security-note">
            <LockOutlined class="security-note__icon" />
            <span>定期更换密码可有效保障账号安全，请妥善保管新密码。</span>
          </div>

          <a-form
            ref="passwordFormRef"
            :model="passwordForm"
            :rules="passwordRules"
            layout="vertical"
            class="dense-form"
          >
            <div class="field-grid">
              <a-form-item label="旧密码" name="oldPassword">
                <a-input-password
                  v-model:value="passwordForm.oldPassword"
                  placeholder="请输入旧密码"
                  autocomplete="current-password"
                />
              </a-form-item>
              <a-form-item label="新密码" name="newPassword">
                <a-input-password
                  v-model:value="passwordForm.newPassword"
                  placeholder="请输入新密码（至少6位）"
                  autocomplete="new-password"
                />
              </a-form-item>
              <a-form-item label="确认密码" name="confirmPassword">
                <a-input-password
                  v-model:value="passwordForm.confirmPassword"
                  placeholder="请再次输入新密码"
                  autocomplete="new-password"
                />
              </a-form-item>
            </div>

            <footer class="section-card__foot">
              <a-button
                class="security-btn"
                :loading="changingPassword"
                @click="handleChangePassword"
              >
                <template #icon><LockOutlined /></template>
                修改密码
              </a-button>
            </footer>
          </a-form>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  SaveOutlined,
  LockOutlined,
  UserOutlined,
  SafetyCertificateOutlined,
  IdcardOutlined,
  PhoneOutlined,
  MailOutlined,
} from '@ant-design/icons-vue'
import { useUserStore } from '@/stores/user'
import { pinyinInitial } from '@/utils/pinyin'
import type { Rule } from 'ant-design-vue/es/form'

const userStore = useUserStore()

// 表单引用
const profileFormRef = ref()
const passwordFormRef = ref()

// 加载状态
const savingProfile = ref(false)
const changingPassword = ref(false)

// 展示用计算属性
const displayName = computed(() => userStore.userInfo?.realName || userStore.userInfo?.username || '用户')
const roleLabel = computed(() => userStore.userInfo?.roleName || '普通用户')
const userInitial = computed(() => pinyinInitial(displayName.value) || 'U')

// 基本信息表单
const profileForm = reactive({
  realName: '',
  email: '',
  phone: '',
  avatar: '',
})

// 密码表单
const passwordForm = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: '',
})

// 基本信息表单验证规则
const profileRules: Record<string, Rule[]> = {
  realName: [
    { required: true, message: '请输入真实姓名', trigger: 'blur' },
    { max: 50, message: '姓名最多50个字符', trigger: 'blur' },
  ],
  email: [
    { type: 'email', message: '请输入有效的邮箱地址', trigger: 'blur' },
  ],
  phone: [
    { pattern: /^1[3-9]\d{9}$/, message: '请输入有效的手机号', trigger: 'blur' },
  ],
}

// 密码表单验证规则
const passwordRules: Record<string, Rule[]> = {
  oldPassword: [
    { required: true, message: '请输入旧密码', trigger: 'blur' },
  ],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于6位', trigger: 'blur' },
  ],
  confirmPassword: [
    { required: true, message: '请确认密码', trigger: 'blur' },
    {
      validator: (_rule: any, value: string) => {
        if (value && value !== passwordForm.newPassword) {
          return Promise.reject('两次输入的密码不一致')
        }
        return Promise.resolve()
      },
      trigger: 'blur',
    },
  ],
}

// 初始化表单数据
onMounted(() => {
  const userInfo = userStore.userInfo
  if (userInfo) {
    profileForm.realName = userInfo.realName || ''
    profileForm.email = userInfo.email || ''
    profileForm.phone = userInfo.phone || ''
    profileForm.avatar = userInfo.avatar || ''
  }
})

// 保存基本信息
async function handleSaveProfile() {
  try {
    await profileFormRef.value.validate()
    savingProfile.value = true

    // 更新本地存储
    userStore.updateUserInfo({
      realName: profileForm.realName,
      email: profileForm.email,
      phone: profileForm.phone,
      avatar: profileForm.avatar,
    })

    message.success('个人信息保存成功')
  } catch (error) {
    console.error('保存个人信息失败:', error)
  } finally {
    savingProfile.value = false
  }
}

// 修改密码
async function handleChangePassword() {
  try {
    await passwordFormRef.value.validate()
    changingPassword.value = true

    message.success('密码修改成功，请重新登录')

    // 清空密码表单
    passwordForm.oldPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
  } catch (error) {
    console.error('修改密码失败:', error)
  } finally {
    changingPassword.value = false
  }
}
</script>

<style scoped lang="scss">
.profile-page {
  gap: var(--space-lg16);
}

.profile-shell {
  display: flex;
  flex-direction: column;
  gap: var(--space-lg16);
  width: 100%;
  max-width: 1120px;
}

/* ===== 身份头部带 ===== */
.identity-band {
  display: flex;
  align-items: center;
  gap: var(--space-lg16);
  padding: var(--space-lg16) var(--space-xl24);
  background:
    linear-gradient(
      90deg,
      var(--color-primary-light) 0%,
      var(--bg-card) 62%
    );
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
}

.identity-band__avatar {
  flex: none;
  font-size: var(--font-xl);
  font-weight: 600;
  color: var(--text-on-accent);
  background: var(--color-primary);
}

.identity-band__main {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm8);
  min-width: 0;
}

.identity-band__title {
  display: flex;
  align-items: center;
  gap: var(--space-md12);
}

.identity-band__name {
  margin: 0;
  font-size: var(--font-xl);
  font-weight: 600;
  line-height: 1.25;
  color: var(--text-1);
}

.role-tag {
  margin: 0;
  padding: 0 var(--space-sm8);
  height: 22px;
  line-height: 20px;
  font-size: var(--font-sm);
  font-weight: 500;
  color: var(--color-primary);
  background: var(--bg-card);
  border: 1px solid var(--color-primary-border);
  border-radius: var(--radius-pill);
}

/* ===== 元信息芯片 ===== */
.meta-chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-sm8);
}

.meta-chip {
  display: inline-flex;
  align-items: center;
  gap: var(--space-xs4);
  padding: var(--space-xs4) var(--space-md12);
  font-size: var(--font-sm2);
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-pill);
  white-space: nowrap;
}

.meta-chip__icon {
  font-size: var(--font-sm2);
  color: var(--text-3);
}

.meta-chip__label {
  color: var(--text-3);
}

.meta-chip__value {
  color: var(--text-1);
  font-weight: 500;
  max-width: 220px;
  overflow: hidden;
  text-overflow: ellipsis;
  font-variant-numeric: tabular-nums;
}

/* ===== 设置网格 ===== */
.settings-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.35fr) minmax(0, 1fr);
  gap: var(--space-lg16);
  align-items: start;
}

@media (max-width: 980px) {
  .settings-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}

/* ===== 区块卡片 ===== */
.section-card {
  display: flex;
  flex-direction: column;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  padding: var(--space-lg16);
}

.section-card__head {
  position: relative;
  display: flex;
  align-items: center;
  gap: var(--space-sm8);
  padding-bottom: var(--space-md12);
  padding-left: var(--space-md12);
  margin-bottom: var(--space-lg16);
  border-bottom: 1px solid var(--border);
}

.section-card__strip {
  position: absolute;
  left: 0;
  top: 2px;
  width: 3px;
  height: var(--font-lg);
  background: var(--color-primary);
  border-radius: var(--radius-pill);
}

.section-card__strip--danger {
  background: var(--color-danger);
}

.section-card__icon {
  font-size: var(--font-lg);
  color: var(--color-primary);
}

.section-card__icon-chip {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: var(--radius-md);
  color: var(--color-danger-text);
  background: var(--color-danger-light);
  border: 1px solid var(--color-danger-border);
  font-size: var(--font-base);
}

.section-card__title {
  margin: 0;
  font-size: var(--font-base);
  font-weight: 600;
  color: var(--text-1);
}

.section-card__hint {
  margin-left: auto;
  font-size: var(--font-sm);
  color: var(--text-3);
}

.section-card--security {
  .section-card__head {
    border-bottom-color: var(--color-danger-border);
  }
}

/* ===== 安全提示横幅 ===== */
.security-note {
  display: flex;
  align-items: center;
  gap: var(--space-sm8);
  margin-bottom: var(--space-lg16);
  padding: var(--space-sm8) var(--space-md12);
  font-size: var(--font-sm2);
  color: var(--color-warning-text);
  background: var(--color-warning-light);
  border: 1px solid var(--color-warning-border);
  border-radius: var(--radius-md);
}

.security-note__icon {
  flex-shrink: 0;
  color: var(--color-warning-text);
}

/* ===== 表单密度 ===== */
.field-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  column-gap: var(--space-lg16);
  row-gap: var(--space-xs4);
  max-width: 720px;
}

.field-grid--two {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

@media (max-width: 560px) {
  .field-grid--two {
    grid-template-columns: minmax(0, 1fr);
  }
}

.dense-form {
  :deep(.ant-form-item) {
    margin-bottom: var(--space-md12);
  }

  :deep(.ant-form-item-label) {
    padding-bottom: var(--space-xs4);

    > label {
      height: auto;
      font-size: var(--font-sm2);
      color: var(--text-2);
    }
  }
}

.section-card__foot {
  display: flex;
  justify-content: flex-end;
  padding-top: var(--space-md12);
  margin-top: var(--space-xs4);
  border-top: 1px solid var(--border);
}

/* 安全动作：克制的危险/安全强调，非旧式红下划线 */
.security-btn {
  color: var(--color-danger-text);
  background: var(--color-danger-light);
  border-color: var(--color-danger-border);
  font-weight: 500;

  &:hover,
  &:focus {
    color: var(--text-on-accent);
    background: var(--color-danger);
    border-color: var(--color-danger);
  }

  :deep(.anticon) {
    color: inherit;
  }
}
</style>
