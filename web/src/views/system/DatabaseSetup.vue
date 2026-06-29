<template>
  <div class="database-setup">
    <!-- 认证门禁 -->
    <div v-if="!setupAuthenticated && !pageLoading" class="auth-gate">
      <div class="auth-gate-card">
        <div class="auth-gate-header">
          <SettingOutlined :style="{ fontSize: '42px', color: 'var(--text-2)' }" />
          <h2>系统配置</h2>
          <p class="auth-gate-desc">请选择认证方式以继续</p>
        </div>
        <div class="auth-gate-options">
          <div class="auth-option" @click="handleLoginAuth">
            <UserOutlined :style="{ fontSize: '32px', color: 'var(--text-2)' }" />
            <h3>用户登录认证</h3>
            <p>使用系统账号登录后进入配置</p>
          </div>
          <div class="auth-option" @click="showPassphraseInput = true" v-if="!showPassphraseInput">
            <LockOutlined :style="{ fontSize: '32px', color: 'var(--color-warning)' }" />
            <h3>管理员口令认证</h3>
            <p>输入管理员口令直接进入配置</p>
          </div>
          <div class="auth-passphrase" v-if="showPassphraseInput">
            <LockOutlined :style="{ fontSize: '32px', color: 'var(--color-warning)' }" />
            <h3>管理员口令认证</h3>
            <a-input-password v-model:value="passphrase" placeholder="请输入管理员口令" @pressEnter="handlePassphraseAuth" style="margin-top: 12px;" />
            <div style="margin-top: 12px; display: flex; gap: 8px; justify-content: center;">
              <a-button @click="showPassphraseInput = false; passphrase = ''">取消</a-button>
              <a-button type="primary" style="background: var(--color-warning); border-color: var(--color-warning)" :loading="passphraseLoading" @click="handlePassphraseAuth">验证</a-button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 页面加载中 -->
    <div v-else-if="pageLoading" class="loading-wrapper">
      <LoadingOutlined :style="{ fontSize: '40px' }" spin />
      <p>正在检测数据库状态...</p>
    </div>

    <!-- 模式A：引导模式 -->
    <div v-else-if="!initialized || reinitMode" class="guide-mode">
      <div class="guide-card">
        <div class="guide-header">
          <div v-if="reinitMode" class="guide-back">
            <a-button type="text" @click="handleBackToManage">
              <ArrowLeftOutlined />返回管理
            </a-button>
          </div>
          <DollarCircleOutlined :style="{ fontSize: '42px', color: 'var(--text-2)' }" />
          <h2>{{ reinitMode ? '全新初始化 - 重新配置数据库' : '系统初始化 - 配置主数据库' }}</h2>
          <p class="guide-desc">请填写 SQL Server 数据库连接信息，完成系统初始化</p>
        </div>
        <a-form ref="guideFormRef" :model="guideForm" :rules="guideRules" :label-col="{ style: { width: '130px' } }" class="guide-form">
          <a-form-item label="服务器地址" name="server">
            <a-input v-model:value="guideForm.server" placeholder="例如: localhost 或 192.168.1.100" />
          </a-form-item>
          <a-form-item label="数据库名" name="database">
            <a-input v-model:value="guideForm.database" placeholder="例如: MDSTO" />
          </a-form-item>
          <a-form-item label="认证方式">
            <a-radio-group v-model:value="guideForm.useWindowsAuth">
              <a-radio :value="true">Windows 认证</a-radio>
              <a-radio :value="false">SQL Server 认证</a-radio>
            </a-radio-group>
          </a-form-item>
          <template v-if="!guideForm.useWindowsAuth">
            <a-form-item label="用户名" name="username">
              <a-input v-model:value="guideForm.username" placeholder="请输入用户名" />
            </a-form-item>
            <a-form-item label="密码" name="password">
              <a-input-password v-model:value="guideForm.password" placeholder="请输入密码" />
            </a-form-item>
          </template>
          <a-form-item label="信任服务器证书">
            <a-switch v-model:checked="guideForm.trustServerCertificate" />
            <span class="form-hint">开发环境使用自签名证书时建议开启</span>
          </a-form-item>
        </a-form>
        <div class="guide-actions">
          <a-button :loading="guideTesting" @click="handleGuideTest">
            <ApiOutlined />测试连接
          </a-button>
          <a-button type="primary" :loading="guideInitializing" @click="handleGuideInit">
            <SettingOutlined />全新初始化
          </a-button>
        </div>
      </div>
    </div>

    <!-- 模式B：管理模式 -->
    <div v-else class="manage-mode">
      <div v-if="!isLoggedIn" class="login-prompt">
        <WarningOutlined :style="{ fontSize: '48px', color: 'var(--color-warning)' }" />
        <h3>需要登录</h3>
        <p>数据库连接管理需要登录权限，请先登录系统。</p>
        <a-button type="primary" @click="$router.push('/login?redirect=/setup')">前往登录</a-button>
      </div>
      <template v-else>
        <div class="manage-header">
          <h2>数据库连接管理</h2>
          <a-button type="primary" @click="handleAdd"><PlusOutlined />新增连接</a-button>
        </div>
        <a-table :columns="connColumns" :dataSource="connections" :loading="tableLoading" rowKey="id" bordered :pagination="false">
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'connectionType'">
              <a-tag v-if="record.connectionType === '系统'" color="error">系统</a-tag>
              <a-tag v-else color="default">扩展</a-tag>
            </template>
            <template v-if="column.dataIndex === 'status'">
              <a-tag :color="record.status === 1 ? 'success' : 'default'">{{ record.status === 1 ? '启用' : '禁用' }}</a-tag>
            </template>
            <template v-if="column.dataIndex === 'action'">
              <a-button type="link" size="small" @click="handleEdit(record)">编辑</a-button>
              <template v-if="record.connectionType === '系统'">
                <a-button type="link" size="small" style="color: var(--color-warning)" @click="handleFullInit(record)">全新初始化</a-button>
                <a-button type="link" size="small" @click="handlePreserveInit(record)">保留初始化</a-button>
              </template>
              <template v-else>
                <a-button type="link" size="small" danger @click="handleDeleteConn(record)">删除</a-button>
              </template>
            </template>
          </template>
          <template #emptyText><EmptyState description="暂无数据库连接" /></template>
        </a-table>

        <!-- 自动备份配置 -->
        <div class="backup-config-section" v-if="hasSystemConnection">
          <a-card class="backup-card">
            <template #title>
              <div class="backup-card-header">
                <ClockCircleOutlined style="color: var(--color-info)" />
                <span>自动备份配置</span>
              </div>
            </template>
            <a-form :model="backupConfig" :label-col="{ style: { width: '120px' } }">
              <a-form-item label="启用自动备份">
                <a-switch v-model:checked="backupConfig.enabled" />
              </a-form-item>
              <a-form-item label="备份频率" v-show="backupConfig.enabled">
                <a-select v-model:value="selectedFrequency" @change="onFrequencyChange" style="width: 220px"
                  :options="[
                    { label: '每天凌晨2点', value: '0 2 * * *' },
                    { label: '每天凌晨4点', value: '0 4 * * *' },
                    { label: '每周日凌晨2点', value: '0 2 * * 0' },
                    { label: '每12小时', value: '0 */12 * * *' },
                    { label: '自定义', value: 'custom' },
                  ]"
                />
                <a-input v-if="selectedFrequency === 'custom'" v-model:value="backupConfig.cronExpression" placeholder="Cron 表达式" style="margin-top: 8px; max-width: 300px" />
              </a-form-item>
              <a-form-item label="备份目录" v-show="backupConfig.enabled">
                <a-input v-model:value="backupConfig.backupDirectory" placeholder="SQL Server 服务器上的路径" style="max-width: 400px" />
                <div class="form-item-desc">备份文件将保存在 SQL Server 服务器上的此目录中</div>
              </a-form-item>
              <a-form-item label="文件名规则" v-show="backupConfig.enabled">
                <a-input v-model:value="backupConfig.fileNamePattern" placeholder="{dbName}_{timestamp}.bak" style="max-width: 360px" />
                <div class="form-item-desc">可用变量：{dbName} 数据库名，{timestamp} 时间戳</div>
              </a-form-item>
              <a-form-item label="保留备份数" v-show="backupConfig.enabled">
                <a-input-number v-model:value="backupConfig.retentionCount" :min="1" :max="100" />
                <span class="backup-retention-hint">超过此数量的旧备份将自动删除</span>
              </a-form-item>
              <a-form-item v-show="backupConfig.enabled" :wrapper-col="{ offset: 5 }">
                <a-button type="primary" :loading="backupConfigSaving" @click="saveBackupConfiguration">保存备份配置</a-button>
              </a-form-item>
            </a-form>
          </a-card>
        </div>
      </template>
    </div>

    <!-- 新增/编辑连接对话框 -->
    <a-modal v-model:open="connDialogVisible" :title="connDialogType === 'add' ? '新增数据库连接' : '编辑数据库连接'" :width="620" :centered="true" :destroyOnClose="true">
      <a-form ref="connFormRef" :model="connForm" :rules="connRules" :label-col="{ style: { width: '130px' } }" style="padding: 10px 20px">
        <a-form-item label="连接类型" name="connectionType">
          <a-select v-model:value="connForm.connectionType" placeholder="请选择连接类型" :disabled="connDialogType === 'edit'" style="width: 100%"
            :options="[
              { label: '系统', value: '系统', disabled: hasSystemConnection && connForm.connectionType !== '系统' },
              { label: '扩展', value: '扩展' },
            ]"
          />
        </a-form-item>
        <a-form-item label="连接名称" name="name">
          <a-input v-model:value="connForm.name" placeholder="请输入连接名称" :maxlength="100" :disabled="connDialogType === 'edit'" />
        </a-form-item>
        <a-form-item label="服务器地址" name="server">
          <a-input v-model:value="connForm.server" placeholder="请输入服务器地址" :disabled="connDialogType === 'edit'" />
        </a-form-item>
        <a-form-item label="数据库名" name="databaseName">
          <a-input v-model:value="connForm.databaseName" placeholder="请输入数据库名" :disabled="connDialogType === 'edit'" />
        </a-form-item>
        <a-form-item label="认证方式">
          <a-radio-group v-model:value="connForm.windowsAuth">
            <a-radio :value="true">Windows 认证</a-radio>
            <a-radio :value="false">SQL Server 认证</a-radio>
          </a-radio-group>
        </a-form-item>
        <template v-if="!connForm.windowsAuth">
          <a-form-item label="用户名" name="username"><a-input v-model:value="connForm.username" placeholder="请输入用户名" /></a-form-item>
          <a-form-item label="密码" name="password">
            <a-input-password v-model:value="connForm.password" :placeholder="connDialogType === 'edit' ? '不修改请留空' : '请输入密码'" />
          </a-form-item>
        </template>
        <a-form-item label="信任服务器证书"><a-switch v-model:checked="connForm.trustServerCertificate" /></a-form-item>
        <a-form-item label="说明"><a-textarea v-model:value="connForm.description" :rows="2" placeholder="可选，填写连接说明" /></a-form-item>
      </a-form>
      <template #footer>
        <a-button :loading="connTesting" @click="handleConnTest"><ApiOutlined />测试连接</a-button>
        <a-button @click="connDialogVisible = false">取消</a-button>
        <a-button type="primary" :loading="connSubmitting" @click="handleConnSubmit">保存</a-button>
      </template>
    </a-modal>

    <!-- 初始化进度对话框 -->
    <a-modal v-model:open="progressDialogVisible" title="数据库初始化" :width="480" :centered="true" :maskClosable="false" :keyboard="false" :closable="!isProgressing">
      <div class="progress-content">
        <a-progress :percent="progressPercent" :status="progressStatus === 'exception' ? 'exception' : progressStatus === 'success' ? 'success' : 'active'" :strokeWidth="12" />
        <p class="progress-text">{{ progressText }}</p>
      </div>
      <template #footer><a-button v-if="!isProgressing" @click="progressDialogVisible = false">关闭</a-button></template>
    </a-modal>

    <!-- 保留初始化对话框 -->
    <a-modal v-model:open="preserveDialogVisible" title="保留初始化" :width="800" :centered="true" :bodyStyle="{ maxHeight: 'calc(100vh - 220px)', overflowY: 'auto', padding: '16px 24px' }" :maskClosable="false" :keyboard="false" :destroyOnClose="true">
      <div class="preserve-init-wrapper">
        <div v-if="analyzing" class="preserve-loading">
          <LoadingOutlined :style="{ fontSize: '36px' }" spin />
          <p>正在分析数据库表结构，请稍候...</p>
        </div>
        <template v-else-if="!preserveExecuting && !preserveDone && !preserveError">
          <div class="preserve-backup-config">
            <a-form :label-col="{ style: { width: '100px' } }">
              <a-form-item label="备份目录">
                <a-input v-model:value="preserveBackupPath" placeholder="留空则跳过备份" allowClear style="max-width: 500px">
                  <template #addonBefore>SQL Server 路径</template>
                </a-input>
                <div style="margin-top: 4px; color: #909399; font-size: 12px;">备份文件将保存在 SQL Server 服务器上的此目录，留空则跳过备份</div>
              </a-form-item>
            </a-form>
          </div>
          <div class="preserve-table-wrapper">
            <a-table :columns="preserveTableColumns" :dataSource="unifiedTableList" bordered :pagination="false" :scroll="{ y: 400 }" rowKey="name" size="small">
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'category'">
                  <span v-if="record.group === 'nonSystem'">非系统</span>
                  <span v-else-if="record.group === 'existing'">系统</span>
                  <span v-else-if="record.group === 'hangfire'">Hangfire</span>
                  <span v-else>系统（缺失）</span>
                </template>
                <template v-if="column.dataIndex === 'rowCount'">
                  <span v-if="record.rowCount != null">{{ record.rowCount }}</span>
                  <span v-else style="color: #c0c4cc">—</span>
                </template>
                <template v-if="column.dataIndex === 'tableAction'">
                  <a-select v-if="record.group === 'existing'" v-model:value="tableActionMap[record.name]" size="small" :disabled="record.rowCount === 0" style="width: 120px"
                    :options="[{ label: '\u4fdd\u7559\u6570\u636e', value: 'preserve' }, { label: '\u6e05\u7a7a\u91cd\u5efa', value: 'clear' }]" />
                  <span v-else style="color: #c0c4cc">—</span>
                </template>
                <template v-if="column.dataIndex === 'statusTag'">
                  <a-tag v-if="record.group === 'nonSystem'" color="error">删除</a-tag>
                  <a-tag v-else-if="record.group === 'missing'" color="blue">新建</a-tag>
                  <a-tag v-else-if="record.group === 'hangfire'" color="purple">强制重建</a-tag>
                  <a-tag v-else :color="tableActionMap[record.name] === 'preserve' ? 'success' : 'warning'">
                    {{ tableActionMap[record.name] === 'preserve' ? '保留' : '重建' }}
                  </a-tag>
                </template>
              </template>
            </a-table>
          </div>
        </template>
        <template v-else-if="preserveExecuting">
          <div class="preserve-executing">
            <div class="preserve-steps-container">
              <a-steps :current="currentStepIndex" direction="vertical" size="small">
                <a-step v-for="step in preserveSteps" :key="step.name" :title="step.title" :status="step.status === 'success' ? 'finish' : step.status" :description="step.description" />
              </a-steps>
            </div>
            <p class="preserve-executing-hint">正在执行保留初始化，请勿关闭页面...</p>
          </div>
        </template>
        <template v-else-if="preserveDone">
          <div class="preserve-done">
            <a-result status="success" title="保留初始化完成" sub-title="所有步骤已成功完成，即将自动关闭..." />
            <div class="preserve-done-steps">
              <a-steps direction="vertical" size="small" :current="preserveSteps.length">
                <a-step v-for="step in preserveSteps" :key="step.name" :title="step.title" :description="step.description" status="finish" />
              </a-steps>
            </div>
          </div>
        </template>
        <template v-else-if="preserveError">
          <div class="preserve-error">
            <a-result status="error" title="保留初始化失败" :sub-title="preserveErrorMsg">
              <template #extra>
                <a-button type="primary" @click="handleRetryPreserve">重试</a-button>
                <a-button @click="preserveDialogVisible = false">关闭</a-button>
              </template>
            </a-result>
            <div class="preserve-error-steps">
              <a-steps direction="vertical" size="small" :current="currentStepIndex">
                <a-step v-for="step in preserveSteps" :key="step.name" :title="step.title" :status="step.status === 'success' ? 'finish' : step.status" :description="step.description" />
              </a-steps>
            </div>
          </div>
        </template>
      </div>
      <template #footer>
        <template v-if="!analyzing && !preserveExecuting && !preserveDone && !preserveError">
          <a-button @click="preserveDialogVisible = false">取消</a-button>
          <a-button type="primary" @click="handleExecutePreserve">执行初始化</a-button>
        </template>
        <template v-else-if="preserveDone">
          <a-button @click="preserveDialogVisible = false">关闭</a-button>
        </template>
      </template>
    </a-modal>

    <!-- dry-run 预览确认对话框 -->
    <a-modal v-model:open="dryRunDialogVisible" title="操作预览 - 请确认后执行" :width="680" :centered="true" :maskClosable="false">
      <div class="dryrun-content" v-if="dryRunResult">
        <a-alert v-if="dryRunResult.estimatedDataLossRows > 0" type="error" :closable="false" class="dryrun-alert"
          :message="`⚠️ 预估将丢失 ${dryRunResult.estimatedDataLossRows} 行数据，操作不可逆！`" />
        <a-alert v-for="(warn, idx) in dryRunResult.warnings" :key="idx" type="warning" :message="warn" :closable="false" class="dryrun-alert" showIcon />
        <a-row :gutter="16" class="dryrun-tables">
          <a-col :span="12" v-if="dryRunResult.tablesToDelete.length > 0">
            <div class="dryrun-section dryrun-section--danger">
              <div class="dryrun-section-title"><DeleteOutlined style="color: var(--color-danger)" /> 将删除的非系统表（{{ dryRunResult.tablesToDelete.length }}）</div>
              <div class="dryrun-tag-list"><a-tag v-for="t in dryRunResult.tablesToDelete" :key="t" color="error" class="dryrun-tag">{{ t }}</a-tag></div>
            </div>
          </a-col>
          <a-col :span="12" v-if="dryRunResult.tablesToPreserve.length > 0">
            <div class="dryrun-section dryrun-section--success">
              <div class="dryrun-section-title"><CheckCircleOutlined style="color: var(--color-success)" /> 将保留数据的系统表（{{ dryRunResult.tablesToPreserve.length }}）</div>
              <div class="dryrun-tag-list"><a-tag v-for="t in dryRunResult.tablesToPreserve" :key="t" color="success" class="dryrun-tag">{{ t }}</a-tag></div>
            </div>
          </a-col>
          <a-col :span="12" v-if="dryRunResult.tablesToRebuild.length > 0">
            <div class="dryrun-section dryrun-section--warning">
              <div class="dryrun-section-title"><RedoOutlined style="color: var(--color-warning)" /> 将清空重建的系统表（{{ dryRunResult.tablesToRebuild.length }}）</div>
              <div class="dryrun-tag-list"><a-tag v-for="t in dryRunResult.tablesToRebuild" :key="t" color="warning" class="dryrun-tag">{{ t }}</a-tag></div>
            </div>
          </a-col>
          <a-col :span="12" v-if="dryRunResult.tablesToCreate.length > 0">
            <div class="dryrun-section dryrun-section--primary">
              <div class="dryrun-section-title"><PlusOutlined style="color: var(--text-2)" /> 将新建的缺失表（{{ dryRunResult.tablesToCreate.length }}）</div>
              <div class="dryrun-tag-list"><a-tag v-for="t in dryRunResult.tablesToCreate" :key="t" color="blue" class="dryrun-tag">{{ t }}</a-tag></div>
            </div>
          </a-col>
          <a-col :span="12" v-if="dryRunResult.hangfireTablesToDelete.length > 0">
            <div class="dryrun-section dryrun-section--info">
              <div class="dryrun-section-title"><MinusCircleOutlined style="color: #909399" /> 将清理 Hangfire 表（{{ dryRunResult.hangfireTablesToDelete.length }}）</div>
              <div class="dryrun-tag-list"><a-tag v-for="t in dryRunResult.hangfireTablesToDelete" :key="t" color="default" class="dryrun-tag">{{ t }}</a-tag></div>
            </div>
          </a-col>
        </a-row>
      </div>
      <template #footer>
        <a-button @click="dryRunDialogVisible = false">返回修改</a-button>
        <a-button type="primary" danger :loading="confirmExecuting" @click="handleConfirmExecute">确认执行</a-button>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import {
  SettingOutlined, UserOutlined, LockOutlined, LoadingOutlined, DollarCircleOutlined,
  ApiOutlined, PlusOutlined, WarningOutlined, ArrowLeftOutlined, ClockCircleOutlined,
  DeleteOutlined, CheckCircleOutlined, RedoOutlined, MinusCircleOutlined,
} from '@ant-design/icons-vue'
import { useRouter } from 'vue-router'
import axios from 'axios'
import { getToken, setToken } from '@/utils/auth'
import {
  createDatabaseProgressConnection,
  startAndGetConnectionId,
  stopDatabaseConnection,
} from '@/utils/databaseSignalr'
import {
  testDatabaseConnection, fullInitializeDatabase, preserveInitializeDatabase,
  analyzeDatabaseTables, previewPreserveInitialize, getDefaultBackupDirectory,
  getBackupConfig, saveBackupConfig, getDbConnections, getDbConnection,
  createDbConnection, updateDbConnection, deleteDbConnection, testDbConnection,
  checkDbConnectionStatus,
  type DbConnection, type DbConnectionCreate, type PreserveDryRunResult, type BackupConfig,
} from '@/api/system'

const router = useRouter()

const connColumns = [
  { title: '连接名称', dataIndex: 'name', key: 'name', ellipsis: true },
  { title: '类型', dataIndex: 'connectionType', key: 'connectionType', width: 90, align: 'center' as const },
  { title: '服务器', dataIndex: 'server', key: 'server', ellipsis: true },
  { title: '数据库名', dataIndex: 'databaseName', key: 'databaseName', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 90, align: 'center' as const },
  { title: '操作', dataIndex: 'action', key: 'action', width: 330, align: 'center' as const, fixed: 'right' as const },
]

const preserveTableColumns = [
  { title: '表名', dataIndex: 'name', key: 'name', ellipsis: true },
  { title: '类别', dataIndex: 'category', key: 'category', width: 140, align: 'center' as const },
  { title: '数据行数', dataIndex: 'rowCount', key: 'rowCount', width: 120, align: 'center' as const },
  { title: '操作', dataIndex: 'tableAction', key: 'tableAction', width: 160, align: 'center' as const },
  { title: '状态标记', dataIndex: 'statusTag', key: 'statusTag', width: 120, align: 'center' as const },
]

// ==================== 认证门禁 ====================
const setupAuthenticated = ref(false)
const showPassphraseInput = ref(false)
const passphrase = ref('')
const passphraseLoading = ref(false)

const handleLoginAuth = () => { router.push('/login?redirect=/setup') }

const handlePassphraseAuth = async () => {
  if (!passphrase.value) { message.warning('请输入口令'); return }
  passphraseLoading.value = true
  try {
    const res = await axios.post('/api/system/database/setup-auth', { passphrase: passphrase.value })
    if (res.data?.code === 200 && res.data?.data?.token) {
      setToken(res.data.data.token); setupAuthenticated.value = true; message.success('认证成功'); await checkDatabaseStatus()
    } else { message.error(res.data?.message || '口令错误') }
  } catch (error: any) {
    const msg = error?.response?.data?.message || error?.response?.data?.Message
    message.error(msg || '认证失败')
  } finally { passphraseLoading.value = false }
}

// ==================== 页面状态 ====================
const pageLoading = ref(true)
const initialized = ref(false)
const isLoggedIn = computed(() => !!getToken())

// ==================== 引导模式 ====================
const reinitMode = ref(false)
const reinitConnId = ref<number | null>(null)
const guideFormRef = ref<FormInstance>()
const guideTesting = ref(false)
const guideInitializing = ref(false)

const guideForm = reactive({ server: '', database: '', useWindowsAuth: false, username: '', password: '', trustServerCertificate: true })

const guideRules = computed(() => {
  const rules: any = {
    server: [{ required: true, message: '请输入服务器地址', trigger: 'blur' }],
    database: [{ required: true, message: '请输入数据库名', trigger: 'blur' }],
  }
  if (!guideForm.useWindowsAuth) {
    rules.username = [{ required: true, message: '请输入用户名', trigger: 'blur' }]
    rules.password = [{ required: true, message: '请输入密码', trigger: 'blur' }]
  }
  return rules
})

function buildGuideConnectionString(): string {
  const parts = [`Server=${guideForm.server}`, `Database=${guideForm.database}`]
  if (guideForm.useWindowsAuth) { parts.push('Trusted_Connection=True') }
  else { parts.push(`User Id=${guideForm.username}`, `Password=${guideForm.password}`) }
  if (guideForm.trustServerCertificate) { parts.push('TrustServerCertificate=True') }
  return parts.join(';')
}

function handleBackToManage() {
  reinitMode.value = false; reinitConnId.value = null
  guideForm.server = ''; guideForm.database = ''; guideForm.useWindowsAuth = false
  guideForm.username = ''; guideForm.password = ''; guideForm.trustServerCertificate = true
}

async function handleGuideTest() {
  try { await guideFormRef.value?.validate() } catch { return }
  guideTesting.value = true
  try {
    const cs = buildGuideConnectionString()
    const res = await testDatabaseConnection({ connectionString: cs })
    if (res?.success === true) { message.success('连接成功') }
    else { message.error(res?.message || '连接失败') }
  } catch (e: any) {
    message.error(e?.message || '连接测试失败')
  } finally { guideTesting.value = false }
}

async function handleGuideInit() {
  try { await guideFormRef.value?.validate() } catch { return }
  Modal.confirm({
    title: '全新初始化',
    content: '将删除目标数据库中的所有表并重新创建，此操作不可逆！确定要继续吗？',
    okType: 'danger',
    async onOk() {
      guideInitializing.value = true
      progressDialogVisible.value = true
      progressPercent.value = 10
      progressText.value = '正在初始化数据库...'
      progressStatus.value = 'active'
      isProgressing.value = true
      try {
        const cs = buildGuideConnectionString()
        await fullInitializeDatabase({ connectionString: cs })
        progressPercent.value = 100
        progressText.value = '初始化成功！'
        progressStatus.value = 'success'
        message.success('数据库初始化成功')
        initialized.value = true
        reinitMode.value = false
        await fetchConnections()
      } catch (e: any) {
        progressPercent.value = 100
        progressText.value = e?.message || '初始化失败'
        progressStatus.value = 'exception'
      } finally {
        guideInitializing.value = false
        isProgressing.value = false
      }
    }
  })
}

// ==================== 管理模式 ====================
const connections = ref<DbConnection[]>([])
const tableLoading = ref(false)
const hasSystemConnection = computed(() => connections.value.some(c => c.connectionType === '系统'))

async function fetchConnections() {
  tableLoading.value = true
  try {
    const res = await getDbConnections() as any
    connections.value = Array.isArray(res) ? res : []
  } catch { connections.value = [] }
  finally { tableLoading.value = false }
}

// 连接对话框
const connDialogVisible = ref(false)
const connDialogType = ref<'add' | 'edit'>('add')
const connFormRef = ref<FormInstance>()
const connTesting = ref(false)
const connSubmitting = ref(false)
const editingConnId = ref<number | null>(null)

const connForm = reactive({
  connectionType: '扩展', name: '', server: '', databaseName: '',
  windowsAuth: false, username: '', password: '', trustServerCertificate: true, description: '',
})

const connRules = computed(() => {
  const rules: any = {
    connectionType: [{ required: true, message: '请选择连接类型' }],
    name: [{ required: true, message: '请输入连接名称', trigger: 'blur' }],
    server: [{ required: true, message: '请输入服务器地址', trigger: 'blur' }],
    databaseName: [{ required: true, message: '请输入数据库名', trigger: 'blur' }],
  }
  if (!connForm.windowsAuth) {
    rules.username = [{ required: true, message: '请输入用户名', trigger: 'blur' }]
    if (connDialogType.value === 'add') {
      rules.password = [{ required: true, message: '请输入密码', trigger: 'blur' }]
    }
  }
  return rules
})

function resetConnForm() {
  connForm.connectionType = '扩展'; connForm.name = ''; connForm.server = ''
  connForm.databaseName = ''; connForm.windowsAuth = false; connForm.username = ''
  connForm.password = ''; connForm.trustServerCertificate = true; connForm.description = ''
  editingConnId.value = null
}

function handleAdd() {
  resetConnForm()
  connDialogType.value = 'add'
  connDialogVisible.value = true
}

async function handleEdit(record: any) {
  resetConnForm()
  connDialogType.value = 'edit'
  editingConnId.value = record.id
  try {
    const data = await getDbConnection(record.id) as any
    if (data) {
      connForm.connectionType = data.connectionType; connForm.name = data.name
      connForm.server = data.server; connForm.databaseName = data.databaseName
      connForm.windowsAuth = data.windowsAuth; connForm.username = data.username || ''
      connForm.password = ''; connForm.trustServerCertificate = data.trustServerCertificate
      connForm.description = data.description || ''
    }
  } catch { message.error('获取连接详情失败') }
  connDialogVisible.value = true
}

async function handleConnTest() {
  try { await connFormRef.value?.validate() } catch { return }
  connTesting.value = true
  try {
    const res = await testDbConnection({
      databaseType: 'SqlServer', server: connForm.server, databaseName: connForm.databaseName,
      windowsAuth: connForm.windowsAuth, username: connForm.username, password: connForm.password,
      trustServerCertificate: connForm.trustServerCertificate,
    })
    if (res?.success === true) { message.success('连接成功') }
    else { message.error(res?.message || '连接失败') }
  } catch (e: any) {
    message.error(e?.message || '连接测试失败')
  } finally { connTesting.value = false }
}

async function handleConnSubmit() {
  try { await connFormRef.value?.validate() } catch { return }
  connSubmitting.value = true
  try {
    const payload: DbConnectionCreate = {
      name: connForm.name, databaseType: 'SqlServer', connectionType: connForm.connectionType,
      server: connForm.server, databaseName: connForm.databaseName,
      windowsAuth: connForm.windowsAuth, username: connForm.username, password: connForm.password,
      trustServerCertificate: connForm.trustServerCertificate, description: connForm.description, status: 1,
    }
    if (connDialogType.value === 'add') {
      await createDbConnection(payload)
      message.success('新增成功'); connDialogVisible.value = false; await fetchConnections()
    } else {
      await updateDbConnection(editingConnId.value!, payload)
      message.success('更新成功'); connDialogVisible.value = false; await fetchConnections()
    }
  } catch (e: any) {
    message.error(e?.message || '操作失败')
  } finally { connSubmitting.value = false }
}

function handleDeleteConn(record: any) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除连接「${record.name}」吗？`,
    okType: 'danger',
    async onOk() {
      try {
        await deleteDbConnection(record.id)
        message.success('删除成功'); await fetchConnections()
      } catch (e: any) { message.error(e?.message || '删除失败') }
    }
  })
}

function handleFullInit(record: any) {
  reinitMode.value = true
  reinitConnId.value = record.id
  guideForm.server = record.server
  guideForm.database = record.databaseName
  guideForm.useWindowsAuth = record.windowsAuth
  guideForm.username = record.username || ''
  guideForm.password = ''
  guideForm.trustServerCertificate = record.trustServerCertificate
}

// ==================== 初始化进度 ====================
const progressDialogVisible = ref(false)
const progressPercent = ref(0)
const progressText = ref('')
const progressStatus = ref<'active' | 'success' | 'exception'>('active')
const isProgressing = ref(false)

// ==================== 保留初始化 ====================
const preserveDialogVisible = ref(false)
const analyzing = ref(false)
const preserveExecuting = ref(false)
const preserveDone = ref(false)
const preserveError = ref(false)
const preserveErrorMsg = ref('')
const preserveBackupPath = ref('')
const preserveConnString = ref('')  // 保留备用（setup 模式）
const preserveConnId = ref<number | null>(null)  // 已初始化后使用 ID 而非明文密码

interface TableInfo {
  name: string
  group: 'existing' | 'missing' | 'nonSystem' | 'hangfire'
  rowCount: number | null
}

const analyzeResult = reactive({
  existingSystemTables: [] as { name: string; rowCount: number }[],
  missingSystemTables: [] as string[],
  nonSystemTables: [] as { name: string; rowCount: number }[],
  hangfireTables: [] as any[],
})

const tableActionMap = reactive<Record<string, string>>({})

const unifiedTableList = computed<TableInfo[]>(() => {
  const list: TableInfo[] = []
  for (const t of analyzeResult.existingSystemTables) {
    list.push({ name: t.name, group: 'existing', rowCount: t.rowCount })
  }
  for (const name of analyzeResult.missingSystemTables) {
    list.push({ name, group: 'missing', rowCount: null })
  }
  for (const t of analyzeResult.nonSystemTables) {
    list.push({ name: t.name, group: 'nonSystem', rowCount: t.rowCount })
  }
  // Hangfire 表（强制重建，不可自选）
  for (const t of analyzeResult.hangfireTables) {
    list.push({ name: `[${t.schemaName}].${t.name}`, group: 'hangfire', rowCount: t.rowCount })
  }
  return list
})

// SignalR 步骤进度
interface PreserveStep {
  name: string; title: string; status: 'wait' | 'process' | 'finish' | 'error' | 'success'; description: string
}

const STEP_DEFINITIONS: { name: string; title: string }[] = [
  { name: 'backup', title: '备份数据库' },
  { name: 'analyze', title: '分析表结构' },
  { name: 'export', title: '导出保留数据' },
  { name: 'drop', title: '清理数据库' },
  { name: 'migrate', title: '重新迁移' },
  { name: 'import', title: '导入保留数据' },
  { name: 'seed', title: '种子数据' },
  { name: 'cleanup', title: '清理临时文件' },
]

const preserveSteps = ref<PreserveStep[]>([])
const currentStepIndex = computed(() => {
  const idx = preserveSteps.value.findIndex(s => s.status === 'process' || s.status === 'error')
  return idx >= 0 ? idx : preserveSteps.value.filter(s => s.status === 'success' || s.status === 'finish').length
})

function initPreserveSteps() {
  preserveSteps.value = STEP_DEFINITIONS.map(s => ({ ...s, status: 'wait' as const, description: '' }))
}

function handleProgressEvent(eventName: string, data: any) {
  if (eventName === 'StepStarted') {
    const step = preserveSteps.value.find(s => s.name === data.stepName)
    if (step) { step.status = 'process'; step.description = data.message || '执行中...' }
  } else if (eventName === 'StepCompleted') {
    const step = preserveSteps.value.find(s => s.name === data.stepName)
    if (step) { step.status = 'success'; step.description = data.message || '完成' }
  } else if (eventName === 'StepFailed') {
    const step = preserveSteps.value.find(s => s.name === data.stepName)
    if (step) { step.status = 'error'; step.description = data.message || '失败' }
  } else if (eventName === 'StepProgress') {
    const step = preserveSteps.value.find(s => s.name === data.stepName)
    if (step) { step.description = data.message || step.description }
  }
}

// dry-run
const dryRunDialogVisible = ref(false)
const dryRunResult = ref<PreserveDryRunResult | null>(null)
const confirmExecuting = ref(false)

async function handleExecutePreserve() {
  // 先做 dry-run 预览
  try {
    if (preserveConnId.value == null) {
      message.error('数据库连接无效，请关闭对话框后重新选择')
      return
    }
    const res = await previewPreserveInitialize({
      connectionId: preserveConnId.value,
      tableActions: { ...tableActionMap },
    }) as any
    dryRunResult.value = res
    dryRunDialogVisible.value = true
  } catch (e: any) { message.error(e?.message || '预览请求失败') }
}

async function handleConfirmExecute() {
  confirmExecuting.value = true
  dryRunDialogVisible.value = false
  await doExecutePreserve()
  confirmExecuting.value = false
}

function handleRetryPreserve() {
  preserveError.value = false
  preserveErrorMsg.value = ''
  doExecutePreserve()
}

async function doExecutePreserve() {
  // 验证连接ID有效
  if (preserveConnId.value == null) {
    message.error('数据库连接无效，请关闭对话框后重新选择')
    return
  }
  preserveExecuting.value = true
  preserveDone.value = false
  preserveError.value = false
  preserveErrorMsg.value = ''
  initPreserveSteps()

  let signalRConn: any = null
  try {
    signalRConn = createDatabaseProgressConnection()
    signalRConn.on('StepStarted', (data: any) => handleProgressEvent('StepStarted', data))
    signalRConn.on('StepCompleted', (data: any) => handleProgressEvent('StepCompleted', data))
    signalRConn.on('StepFailed', (data: any) => handleProgressEvent('StepFailed', data))
    signalRConn.on('StepProgress', (data: any) => handleProgressEvent('StepProgress', data))
    const _connId = await startAndGetConnectionId(signalRConn)
    void _connId

    await preserveInitializeDatabase({
      connectionId: preserveConnId.value!,
      tableActions: { ...tableActionMap },
      backupPath: preserveBackupPath.value || undefined,
    })

    preserveDone.value = true
    message.success('保留初始化完成')
    await fetchConnections()
    setTimeout(() => { preserveDialogVisible.value = false }, 3000)
  } catch (e: any) {
    preserveError.value = true
    preserveErrorMsg.value = e?.message || '初始化失败'
  } finally {
    preserveExecuting.value = false
    if (signalRConn) { try { await stopDatabaseConnection(signalRConn) } catch {} }
  }
}

async function handlePreserveInit(record: any) {
  preserveDialogVisible.value = true
  analyzing.value = true
  preserveDone.value = false
  preserveError.value = false
  preserveErrorMsg.value = ''
  preserveBackupPath.value = ''
  preserveConnId.value = null

  // 使用连接 ID 而非明文密码
  preserveConnId.value = record.id

  try {
    // 获取默认备份目录
    try {
      const backupDir = await getDefaultBackupDirectory({ connectionId: record.id }) as any
      if (backupDir?.backupDirectory) { preserveBackupPath.value = backupDir.backupDirectory }
    } catch { /* 忽略 */ }
    // 分析表结构
    const d = await analyzeDatabaseTables({ connectionId: record.id }) as any
    analyzeResult.existingSystemTables = d.existingSystemTables || []
    analyzeResult.missingSystemTables = d.missingSystemTables || []
    analyzeResult.nonSystemTables = d.nonSystemTables || []
    analyzeResult.hangfireTables = d.hangfireTables || []
    // 初始化 tableActionMap
    Object.keys(tableActionMap).forEach(k => delete tableActionMap[k])
    for (const t of analyzeResult.existingSystemTables) {
      tableActionMap[t.name] = t.rowCount > 0 ? 'preserve' : 'clear'
    }
  } catch (e: any) {
    message.error(e?.message || '分析请求失败')
    preserveDialogVisible.value = false
  } finally { analyzing.value = false }
}

// ==================== 备份配置 ====================
const backupConfig = reactive<BackupConfig>({
  enabled: false, cronExpression: '0 2 * * *', backupDirectory: '',
  fileNamePattern: '{dbName}_{timestamp}.bak', retentionCount: 7,
})
const selectedFrequency = ref('0 2 * * *')
const backupConfigSaving = ref(false)

function onFrequencyChange(val: any) {
  if (val !== 'custom') { backupConfig.cronExpression = val }
}

async function loadBackupConfig() {
  try {
    const d = await getBackupConfig() as any
    if (d) {
      backupConfig.enabled = d.enabled
      backupConfig.cronExpression = d.cronExpression || '0 2 * * *'
      backupConfig.backupDirectory = d.backupDirectory || ''
      backupConfig.fileNamePattern = d.fileNamePattern || '{dbName}_{timestamp}.bak'
      backupConfig.retentionCount = d.retentionCount || 7
      // 匹配预设频率
      const presets = ['0 2 * * *', '0 4 * * *', '0 2 * * 0', '0 */12 * * *']
      selectedFrequency.value = presets.includes(backupConfig.cronExpression) ? backupConfig.cronExpression : 'custom'
    }
  } catch {}
}

async function saveBackupConfiguration() {
  backupConfigSaving.value = true
  try {
    await saveBackupConfig({ ...backupConfig })
    message.success('备份配置保存成功')
  } catch (e: any) {
    message.error(e?.message || '保存失败')
  } finally { backupConfigSaving.value = false }
}

// ==================== 初始化检测 ====================
async function checkDatabaseStatus() {
  pageLoading.value = true
  try {
    const res = await checkDbConnectionStatus() as any
    initialized.value = !!res?.hasSystemConnection
    if (initialized.value) {
      await fetchConnections()
      await loadBackupConfig()
    }
  } catch {
    initialized.value = false
  } finally { pageLoading.value = false }
}

onMounted(async () => {
  if (getToken()) {
    setupAuthenticated.value = true
    await checkDatabaseStatus()
  } else {
    pageLoading.value = false
  }
})
</script>

<style scoped>
.database-setup {
  min-height: 100vh;
  background: #f5f7fa;
}

/* 认证门禁 */
.auth-gate {
  display: flex; justify-content: center; align-items: center; min-height: 100vh;
}
.auth-gate-card {
  background: #fff; border-radius: 12px; padding: 48px 40px; max-width: 600px; width: 100%;
  box-shadow: 0 4px 24px rgba(0,0,0,0.08); text-align: center;
}
.auth-gate-header h2 { margin: 16px 0 8px; }
.auth-gate-desc { color: #909399; margin-bottom: 32px; }
.auth-gate-options { display: flex; gap: 24px; justify-content: center; flex-wrap: wrap; }
.auth-option {
  border: 1px solid #e4e7ed; border-radius: 8px; padding: 24px; cursor: pointer;
  transition: all .3s; flex: 1; min-width: 200px; max-width: 250px;
}
.auth-option:hover { border-color: var(--color-primary); box-shadow: 0 2px 12px var(--color-primary-border); }
.auth-option h3 { margin: 12px 0 4px; font-size: 15px; }
.auth-option p { color: #909399; font-size: 13px; margin: 0; }
.auth-passphrase {
  border: 1px solid #e4e7ed; border-radius: 8px; padding: 24px;
  flex: 1; min-width: 200px; max-width: 250px; text-align: center;
}
.auth-passphrase h3 { margin: 12px 0 4px; font-size: 15px; }

/* 加载中 */
.loading-wrapper {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  min-height: 100vh; color: #606266;
}

/* 引导模式 */
.guide-mode {
  display: flex; justify-content: center; align-items: center; min-height: 100vh; padding: 20px;
}
.guide-card {
  background: #fff; border-radius: 12px; padding: 48px 40px; max-width: 640px; width: 100%;
  box-shadow: 0 4px 24px rgba(0,0,0,0.08);
}
.guide-header { text-align: center; margin-bottom: 32px; position: relative; }
.guide-header h2 { margin: 16px 0 8px; }
.guide-desc { color: #909399; }
.guide-back { position: absolute; left: 0; top: 0; }
.guide-form { max-width: 480px; margin: 0 auto; }
.form-hint { color: #909399; font-size: 12px; margin-left: 8px; }
.guide-actions { display: flex; justify-content: center; gap: 16px; margin-top: 32px; }

/* 管理模式 */
.manage-mode { padding: 24px; max-width: 1200px; margin: 0 auto; }
.manage-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.manage-header h2 { margin: 0; }
.login-prompt {
  text-align: center; padding: 80px 20px; background: #fff; border-radius: 8px;
}

/* 备份配置 */
.backup-config-section { margin-top: 24px; }
.backup-card-header { display: flex; align-items: center; gap: 8px; }
.form-item-desc { color: #909399; font-size: 12px; margin-top: 4px; }
.backup-retention-hint { color: #909399; font-size: 12px; margin-left: 8px; }

/* 初始化进度 */
.progress-content { text-align: center; padding: 20px 0; }
.progress-text { margin-top: 16px; color: #606266; }

/* 保留初始化 */
.preserve-init-wrapper { min-height: 200px; }
.preserve-loading { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 80px 0; }
.preserve-backup-config { margin-bottom: 16px; }
.preserve-table-wrapper { overflow: auto; }
.preserve-executing { padding: 40px; }
.preserve-steps-container { max-width: 500px; margin: 0 auto; }
.preserve-executing-hint { text-align: center; color: #909399; margin-top: 24px; }
.preserve-done { padding: 40px 0; }
.preserve-done-steps { max-width: 500px; margin: 24px auto 0; }
.preserve-error { padding: 20px 0; }
.preserve-error-steps { max-width: 500px; margin: 24px auto 0; }

/* dry-run */
.dryrun-content { max-height: 60vh; overflow: auto; }
.dryrun-alert { margin-bottom: 12px; }
.dryrun-tables { margin-top: 16px; }
.dryrun-section { border: 1px solid #e4e7ed; border-radius: 6px; padding: 12px; margin-bottom: 12px; }
.dryrun-section--danger { border-color: #fde2e2; background: var(--color-danger-light); }
.dryrun-section--success { border-color: #d9f7be; background: var(--color-success-light); }
.dryrun-section--warning { border-color: #ffeeba; background: var(--color-warning-light); }
.dryrun-section--primary { border-color: var(--color-primary-border); background: var(--color-primary-light); }
.dryrun-section--info { border-color: #e4e7ed; background: #f4f4f5; }
.dryrun-section-title { font-weight: 500; margin-bottom: 8px; display: flex; align-items: center; gap: 6px; }
.dryrun-tag-list { display: flex; flex-wrap: wrap; gap: 4px; }
.dryrun-tag { margin: 0; }
</style>
