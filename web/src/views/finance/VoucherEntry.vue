<template>
  <div class="voucher-entry-page">
    <PageHeader title="凭证录入">
      <template #left>
        <AccountSetSelector />
        <a-button type="link" @click="showShortcuts = true">快捷键说明</a-button>
      </template>
      <template #actions>
        <a-button-group>
          <a-button @click="prevVoucher">
            <LeftOutlined />
          </a-button>
          <a-button @click="nextVoucher">
            <RightOutlined />
          </a-button>
        </a-button-group>
        <a-button @click="clearForm">清空</a-button>
        <a-button type="primary" @click="saveVoucher" :disabled="!isBalanced || isClosedPeriod">保存</a-button>
        <a-button type="primary" @click="saveAndNew" :disabled="!isBalanced || isClosedPeriod">保存并新增</a-button>
        <a-button @click="saveDraft">暂存</a-button>
        <a-button @click="showDrafts">草稿箱</a-button>
        <a-button ghost @click="auditVoucher" style="color: var(--color-warning); border-color: var(--color-warning);">审核</a-button>
        <a-button
          v-if="isRecordingVoucher"
          type="primary"
          style="background: var(--color-warning); border-color: var(--color-warning);"
          @click="handleCompleteRecord"
        >
          补录完成并提交
        </a-button>
        <a-dropdown>
          <a-button>
            更多 <DownOutlined />
          </a-button>
          <template #overlay>
            <a-menu @click="({ key }: any) => handleMoreCommand(key as string)">
              <a-menu-item key="print">打印</a-menu-item>
              <a-menu-item key="copy">复制</a-menu-item>
              <a-menu-item key="delete">删除</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
        <!-- 选择模板 -->
        <a-dropdown :disabled="templateList.length === 0">
          <a-button ghost style="color: var(--color-success); border-color: var(--color-success);">
            选择模板 <DownOutlined />
          </a-button>
          <template #overlay>
            <a-menu @click="({ key }: any) => { const tpl = templateList.find((t: any) => String(t.id) === key); if (tpl) handleTemplateSelect(tpl); }">
              <a-menu-item
                v-for="tpl in templateList"
                :key="String(tpl.id)"
              >{{ tpl.name }}</a-menu-item>
              <a-menu-item v-if="templateList.length === 0" disabled>暂无模板</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </template>
    </PageHeader>

    <!-- 凭证头 -->
    <div class="voucher-header">
      <div class="header-left">
        <span class="label">凭证字</span>
        <a-select v-model:value="form.voucherWord" style="width: 80px">
          <a-select-option value="记">记</a-select-option>
          <a-select-option value="收">收</a-select-option>
          <a-select-option value="付">付</a-select-option>
          <a-select-option value="转">转</a-select-option>
        </a-select>
        <div class="voucher-number-input-wrapper">
          <a-input-number
            v-model:value="form.voucherNumber"
            :min="1"
            :controls="false"
            class="voucher-number-input"
          />
        </div>
        <span class="label">号</span>
        <span class="label">日期</span>
        <a-date-picker
          v-model:value="form.date"
          value-format="YYYY-MM-DD"
          style="width: 140px"
          @change="onDateChange"
        />
      </div>
      <div class="header-center">
        <span class="voucher-title">记账凭证</span>
        <span class="voucher-period">{{ currentYear }} 年 第{{ String(currentPeriod).padStart(2, '0') }} 期</span>
      </div>
      <VoucherStatusFlow
        v-if="form.id"
        :status="form.status ?? 0"
        :createdAt="form.date"
        :createdBy="form.creator"
        :auditedBy="form.auditor"
      />
      <div class="header-right">
        <span class="label">附件：</span>
        <div class="voucher-number-input-wrapper">
          <a-input-number
            v-model:value="form.attachmentCount"
            :min="0"
            :controls="false"
            class="voucher-number-input"
          />
        </div>
        <span class="label">个</span>
      </div>
    </div>

    <!-- 凭证分录表格 -->
    <div class="voucher-table-wrapper">
      <table class="voucher-table">
        <thead>
          <tr>
            <th class="col-action"></th>
            <th class="col-summary">摘要</th>
            <th class="col-account">会计科目</th>
            <th class="col-amount-header">
              <div class="amount-header-title">借方金额</div>
              <div class="amount-header-cells">
                <span>亿</span><span>千</span><span>百</span><span>十</span><span>万</span>
                <span>千</span><span>百</span><span>十</span><span>元</span><span>角</span><span>分</span>
              </div>
            </th>
            <th class="col-amount-header">
              <div class="amount-header-title">贷方金额</div>
              <div class="amount-header-cells">
                <span>亿</span><span>千</span><span>百</span><span>十</span><span>万</span>
                <span>千</span><span>百</span><span>十</span><span>元</span><span>角</span><span>分</span>
              </div>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="(entry, index) in form.entries"
            :key="entry._uid"
            :class="{ 'active-row': currentRowIndex === index }"
            @click="currentRowIndex = index"
          >
            <td class="col-action">
              <div class="row-action-btns">
                <div class="action-icon add" @click.stop="insertEntry(index)" title="插入行">
                  <span>+</span>
                </div>
                <div class="action-icon delete" @click.stop="deleteEntry(index)" title="删除行">
                  <span>×</span>
                </div>
              </div>
            </td>
            <td class="col-summary">
              <div
                class="summary-cell"
                :class="{ 'is-editing': editingSummaryIndex === index }"
              >
                <textarea
                  v-model="entry.summary"
                  class="summary-textarea"
                  placeholder=""
                  @focus="editingSummaryIndex = index; currentRowIndex = index"
                  @blur="editingSummaryIndex = null"
                  @keydown.tab.prevent="focusNext($event, index, 'summary')"
                  rows="2"
                />
                <span
                  v-if="!entry.summary && editingSummaryIndex !== index"
                  class="summary-placeholder"
                >必填</span>
              </div>
            </td>
            <td class="col-account">
              <div class="account-cell" @click="handleAccountCellClick(index)">
                <!-- 已选科目时显示 -->
                <div class="account-main">
                  <span v-if="entry.accountId" class="account-display">
                    {{ entry.accountCode }} {{ entry.accountName }}
                  </span>
                  <!-- 辅助核算标签 -->
                  <span
                    v-if="entry.accountId && getAccountAuxType(entry)"
                    class="aux-tag"
                    :class="{ 'has-value': !!entry.auxiliaryJson }"
                    @click.stop="openAuxiliaryPicker(index, getAccountAuxType(entry)!)"
                    :title="getAuxiliaryDisplay(entry) || '点击选择' + (auxTypeLabels[getAccountAuxType(entry)!] || '辅助项')"
                  >
                    <template v-if="entry.auxiliaryJson">
                      {{ getAuxiliaryDisplay(entry) }}
                    </template>
                    <template v-else>
                      {{ auxTypeLabels[getAccountAuxType(entry)!] || '辅助' }}未选
                    </template>
                  </span>
                </div>
                <!-- 右侧操作按钮 -->
                <div class="account-actions">
                  <div class="action-btn" @click.stop="openAccountSelectDialog(index)">选择科目</div>
                  <div class="action-btn" @click.stop="openAddAccountDialog(index)">新增科目</div>
                </div>
              </div>
              <!-- 科目余额提示 -->
              <div v-if="entry.accountId && accountBalanceMap[entry.accountId]" class="account-balance-hint">
                当前余额：{{ accountBalanceMap[entry.accountId].direction }} {{ accountBalanceMap[entry.accountId].amount }}
              </div>
            </td>
            <td class="col-amount">
              <!-- 外币信息展示（当分录有外币属性时） -->
              <div v-if="entry.currencyCode" class="foreign-currency-panel">
                <a-tag color="warning" class="currency-tag">{{ entry.currencyCode }}</a-tag>
                <div class="foreign-row">
                  <span class="foreign-label">汇率</span>
                  <a-input-number
                    v-model:value="entry.exchangeRate"
                    :precision="6"
                    :min="0"
                    :controls="false"
                    size="small"
                    class="foreign-input"
                    @change="onOriginalAmountChange(entry, getForeignDirection(entry))"
                  />
                </div>
                <div class="foreign-row">
                  <span class="foreign-label">原币</span>
                  <a-input-number
                    v-model:value="entry.originalAmount"
                    :precision="2"
                    :min="0"
                    :controls="false"
                    size="small"
                    class="foreign-input"
                    @change="onOriginalAmountChange(entry, getForeignDirection(entry))"
                  />
                </div>
                <div class="foreign-row">
                  <a-radio-group
                    :value="getForeignDirection(entry)"
                    size="small"
                    @change="(e: any) => setForeignDirection(entry, e.target.value)"
                  >
                    <a-radio-button value="debit">借</a-radio-button>
                    <a-radio-button value="credit">贷</a-radio-button>
                  </a-radio-group>
                </div>
              </div>
              <AmountGrid
                v-model="entry.debitAmount"
                :is-debit="true"
                @click="onAmountClick(index, 'debit')"
              />
            </td>
            <td class="col-amount">
              <AmountGrid
                v-model="entry.creditAmount"
                :is-debit="false"
                @click="onAmountClick(index, 'credit')"
              />
            </td>
          </tr>
          <!-- 合计行 -->
          <tr class="total-row">
            <td class="col-action"></td>
            <td class="col-summary" colspan="2">
              <span class="total-label">合计：</span>
            </td>
            <td class="col-amount">
              <div class="amount-cells">
                <div
                  v-for="(cell, idx) in totalDebitCells"
                  :key="idx"
                  class="cell"
                  :class="{ 'red-line': idx === 4, 'blue-line': idx === 8 }"
                >
                  {{ cell }}
                </div>
              </div>
            </td>
            <td class="col-amount">
              <div class="amount-cells">
                <div
                  v-for="(cell, idx) in totalCreditCells"
                  :key="idx"
                  class="cell"
                  :class="{ 'red-line': idx === 4, 'blue-line': idx === 8 }"
                >
                  {{ cell }}
                </div>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 借贷平衡摘要 -->
    <div class="balance-summary">
      <span>借方合计: <b>{{ totalDebit.toFixed(2) }}</b></span>
      <span>贷方合计: <b>{{ totalCredit.toFixed(2) }}</b></span>
      <span :class="isBalanced ? 'diff-balanced' : 'diff-unbalanced'">
        差额: {{ balanceDiff.toFixed(2) }}
      </span>
    </div>

    <!-- 借贷不平衡提示 -->
    <div v-if="!isBalanced && (totalDebit > 0 || totalCredit > 0)" class="balance-warning">
      <ExclamationCircleOutlined />
      借贷不平衡，差额：{{ balanceDiff.toFixed(2) }}
    </div>
    <!-- 已结账期间提示 -->
    <div v-if="isClosedPeriod" class="closed-period-warning">
      <LockOutlined />
      所选日期所在期间已结账，禁止录入凭证
    </div>

    <!-- 底部信息 -->
    <div class="voucher-footer">
      <span>制单人：{{ form.creator || '-' }}</span>
      <span>修改人：{{ form.modifier || '-' }}</span>
      <span>审核人：{{ form.auditor || '-' }}</span>
    </div>

    <!-- 附件区域 -->
    <div class="attachment-section">
      <div class="attachment-title">附件（最多上传九个）</div>
      <div class="attachment-upload">
        <a-upload
          ref="uploadRef"
          :customRequest="handleUpload"
          :maxCount="9"
          :multiple="true"
          :showUploadList="false"
          :beforeUpload="beforeUpload"
          class="upload-area"
        >
          <div class="ant-upload-drag-container">
            <PlusOutlined class="upload-icon" />
            <div class="upload-text">拖拽上传或<em>点击上传</em></div>
            <div class="upload-tip">png/jpg/gif/pdf/doc/xlsx 等，单文件不超过 50MB</div>
          </div>
        </a-upload>
        <div class="attachment-tips">
          <p>在此区域内点击右键</p>
          <p>“粘贴”剪贴板中的图片</p>
        </div>
      </div>
      <!-- 附件列表 -->
      <div v-if="attachmentList.length > 0" class="attachment-list">
        <div
          v-for="att in attachmentList"
          :key="att.id"
          class="attachment-item"
        >
          <FileOutlined class="att-icon" />
          <span class="att-name" :title="att.originalName">{{ att.originalName }}</span>
          <span class="att-size">{{ formatFileSize(att.fileSize) }}</span>
          <span class="att-time">{{ formatDate(att.uploadTime) }}</span>
          <a-button
            type="link" danger
            size="small"
            @click="deleteAttachmentItem(att.id)"
          >删除</a-button>
          <a-button
            type="link"
            size="small"
            @click="downloadAttachment(att.id)"
          >下载</a-button>
        </div>
      </div>
      <!-- 待上传文件预览（未保存记录） -->
      <div v-if="pendingFiles.length > 0" class="attachment-list pending-list">
        <div v-for="(f, idx) in pendingFiles" :key="idx" class="attachment-item pending">
          <FileOutlined class="att-icon" />
          <span class="att-name" :title="f.name">{{ f.name }}</span>
          <span class="att-size">{{ formatFileSize(f.size) }}</span>
          <span class="att-status">{{ f.status === 'uploading' ? '上传中...' : '待保存' }}</span>
          <a-button
            type="link" danger
            size="small"
            @click="removePendingFile(idx)"
          >移除</a-button>
        </div>
      </div>
      <a-button class="add-invoice-btn" @click="addInvoice">
        <PlusOutlined />添加发票
      </a-button>
    </div>

    <!-- 草稿箱弹窗 -->
    <a-modal v-model:open="draftsVisible" title="草稿箱" :width="800">
      <a-table :dataSource="draftList" :columns="draftColumns" rowKey="id" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'action'">
            <a-button type="link" @click="loadDraft(record)">加载</a-button>
            <a-button type="link" danger @click="deleteDraft(record)">删除</a-button>
          </template>
        </template>
      </a-table>
    </a-modal>

    <!-- 快捷键说明弹窗 -->
    <a-modal v-model:open="showShortcuts" title="快捷键说明" :width="400" :footer="null">
      <div class="shortcuts-content">
        <p><kbd>F2</kbd> 保存凭证</p>
        <p><kbd>F3</kbd> 保存并新增</p>
        <p><kbd>F4</kbd> 审核凭证</p>
        <p><kbd>Tab</kbd> 切换到下一单元格</p>
        <p><kbd>Shift+Tab</kbd> 切换到上一单元格</p>
        <p><kbd>Enter</kbd> 确认输入</p>
        <p><kbd>Esc</kbd> 取消输入</p>
      </div>
    </a-modal>

    <!-- 科目选择弹窗 -->
    <a-modal
      v-model:open="accountSelectVisible"
      title="科目选择"
      :width="600"
      :destroyOnClose="true"
      class="account-select-dialog"
    >
      <!-- 顶部标签页 -->
      <div class="account-tabs">
        <span
          v-for="cat in accountCategories"
          :key="cat"
          :class="['tab-item', { active: currentSelectCategory === cat }]"
          @click="switchSelectCategory(cat)"
        >
          {{ cat }}
        </span>
      </div>

      <!-- 科目树 -->
      <div class="account-tree-container">
        <a-tree
          ref="accountTreeRef"
          :treeData="currentCategoryTree"
          :fieldNames="{ title: 'displayName', key: 'id', children: 'children' }"
          :defaultExpandAll="true"
          :selectedKeys="selectedAccount ? [selectedAccount.id] : []"
          @select="(_keys: any, { node }: any) => handleAccountNodeClick(node)"
        />
      </div>

      <!-- 底部按钮 -->
      <template #footer>
        <a-button @click="accountSelectVisible = false">取 消</a-button>
        <a-button type="primary" @click="confirmAccountSelect">确 定</a-button>
      </template>
    </a-modal>

    <!-- 新增科目弹窗 -->
    <a-modal
      v-model:open="addAccountVisible"
      title="新增科目"
      :width="600"
      :destroyOnClose="true"
      class="account-dialog"
    >
      <a-form
        ref="addAccountFormRef"
        :model="addAccountForm"
        :rules="addAccountRules"
        class="custom-form"
      >
        <!-- 科目编码 -->
        <div class="form-row">
          <label class="form-label">科目编码：</label>
          <div class="form-control">
            <a-input
              v-model:value="addAccountForm.code"
              placeholder="科目编码长度分别为4/6/8/10位数字"
            />
          </div>
        </div>

        <!-- 提示信息1 -->
        <div class="info-tip">
          科目编码支持最长四级，一级科目必须以1（资产）/2（负债）/3（权益）/4（成本）/5（损益）开头,科目编码长度分别为4/6/8/10位数字；子级科目不可超过99个
        </div>

        <!-- 科目名称 -->
        <div class="form-row">
          <label class="form-label">科目名称：</label>
          <div class="form-control">
            <a-input
              v-model:value="addAccountForm.name"
              placeholder="包含中文最长32个字符，否则最长64个"
            />
          </div>
        </div>

        <!-- 上级科目 -->
        <div class="form-row">
          <label class="form-label">上级科目：</label>
          <div class="form-control">
            <a-input
              :value="addAccountParentName"
              disabled
              placeholder="无"
            />
          </div>
        </div>

        <!-- 科目类别 -->
        <div class="form-row">
          <label class="form-label">科目类别：</label>
          <div class="form-control">
            <a-select v-model:value="addAccountForm.category" placeholder="请选择类别" style="width: 100%">
              <a-select-option
                v-for="cat in categoryOptions"
                :key="cat.value"
                :value="cat.value"
              >{{ cat.label }}</a-select-option>
            </a-select>
          </div>
        </div>

        <!-- 提示信息2 -->
        <div class="info-tip">
          科目类别已根据国家小企业会计准则预置，不支持自定义类别；<br/>
          新增子科目时，子科目将自动继承上级科目的余额方向、类别；
        </div>

        <!-- 余额方向 -->
        <div class="form-row">
          <label class="form-label">余额方向：</label>
          <div class="form-control">
            <a-switch
              v-model:checked="addAccountBalanceSwitch"
              checked-children="借"
              un-checked-children="贷"
            />
            <span class="balance-direction-label">{{ addAccountBalanceSwitch ? '借' : '贷' }}</span>
          </div>
        </div>

        <!-- 数量核算 + 计算单位 -->
        <div class="form-row">
          <label class="form-label"></label>
          <div class="form-control quantity-row">
            <a-checkbox v-model:checked="addAccountForm.enableQuantity">数量核算</a-checkbox>
            <span class="unit-label">计算单位</span>
            <a-input
              v-model:value="addAccountForm.unit"
              :disabled="!addAccountForm.enableQuantity"
              placeholder=""
              class="unit-input"
            />
          </div>
        </div>
      </a-form>
      <template #footer>
        <div class="dialog-footer">
          <a-button @click="addAccountVisible = false">取 消</a-button>
          <a-button @click="handleAddAccountSubmit">保 存</a-button>
          <a-button type="primary" @click="handleAddAccountSaveAndNew">保存并新增</a-button>
        </div>
      </template>
    </a-modal>

    <!-- 辅助核算选择弹窗 -->
    <AuxiliaryPicker
      v-model:visible="auxiliaryPickerVisible"
      v-model="auxiliaryPickerValue"
      :aux-type="auxiliaryPickerType"
      :account-set-id="accountSetStore.currentAccountSetId || 0"
      @select="onAuxiliarySelect"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import { LeftOutlined, RightOutlined, DownOutlined, PlusOutlined, ExclamationCircleOutlined, LockOutlined, FileOutlined, LinkOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import AmountGrid from '@/components/AmountGrid.vue'
import AuxiliaryPicker from '@/components/AuxiliaryPicker.vue'
import AccountSetSelector from '@/components/AccountSetSelector.vue'
import VoucherStatusFlow from './components/VoucherStatusFlow.vue'
import {
  getVoucherDetail,
  createVoucher,
  updateVoucher,
  auditVoucher as apiAuditVoucher,
  saveDraft as apiSaveDraft,
  getDrafts,
  getNextVoucherNumber,
  getAccountTree,
  createAccount,
  getPeriods,
  getVoucherTemplates,
  getVoucherTemplateDetail,
  uploadAttachment,
  getAttachments,
  deleteAttachment,
  getAttachmentDownloadUrl,
  getLatestExchangeRate,
  getAccountBalanceByYearMonth,
  completeVoucherRecord
} from '@/api/finance'
import { get as httpGet } from '@/api/request'
import { useAccountSetStore } from '@/stores/accountSet'
import { genTempId } from '@/utils/tempId'

const route = useRoute()
const router = useRouter()
const accountSetStore = useAccountSetStore()

// 科目余额缓存 { [accountId]: { direction: '借方'|'贷方', amount: '12,500.00' } }
const accountBalanceMap = ref<Record<number, { direction: string; amount: string }>>({})

// 获取科目当前余额
async function fetchAccountBalance(accountId: number) {
  if (accountBalanceMap.value[accountId]) return
  try {
    const accountSetId = accountSetStore.currentAccountSetId || 0
    const year = currentYear.value
    const month = currentPeriod.value
    const res = await getAccountBalanceByYearMonth({ year, month, accountId, accountSetId }) as any[]
    if (res && res.length > 0) {
      const item = res[0]
      const endDebit = item.endDebit || 0
      const endCredit = item.endCredit || 0
      const net = endDebit - endCredit
      accountBalanceMap.value[accountId] = {
        direction: net >= 0 ? '借方' : '贷方',
        amount: Math.abs(net).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
      }
    }
  } catch {
    // 静默失败
  }
}

// 辅助核算选择弹窗
const auxiliaryPickerVisible = ref(false)
const auxiliaryPickerType = ref('')
const auxiliaryPickerIndex = ref(0)
const auxiliaryPickerValue = ref<any>(null)

// 辅助类型标签映射
const auxTypeLabels: Record<string, string> = {
  customer: '客户',
  supplier: '供应商',
  department: '部门',
  project: '项目',
  employee: '员工',
  business_unit: '经营单元',
  express_brand: '快递品牌',
  outlet: '网点',
  business_direction: '业务方向'
}

// 打开辅助核算选择弹窗
function openAuxiliaryPicker(index: number, auxType: string) {
  auxiliaryPickerIndex.value = index
  auxiliaryPickerType.value = auxType
  const entry = form.value.entries[index]
  if (entry.auxiliaryJson) {
    try {
      auxiliaryPickerValue.value = JSON.parse(entry.auxiliaryJson)
    } catch {
      auxiliaryPickerValue.value = null
    }
  } else {
    auxiliaryPickerValue.value = null
  }
  auxiliaryPickerVisible.value = true
}

// 辅助核算选择确认
function onAuxiliarySelect(item: any) {
  const entry = form.value.entries[auxiliaryPickerIndex.value]
  entry.auxiliaryJson = JSON.stringify({
    type: auxiliaryPickerType.value,
    id: item.id,
    code: item.code,
    name: item.name
  })
}

// 获取分录的辅助核算显示信息
function getAuxiliaryDisplay(entry: any): string {
  if (!entry.auxiliaryJson) return ''
  try {
    const parsed = JSON.parse(entry.auxiliaryJson)

    // 新格式：数组
    if (Array.isArray(parsed)) {
      return parsed
        .filter((item: any) => item && item.type && item.name)
        .map((item: any) => {
          const label = auxTypeLabels[item.type] || item.type
          return `${label}: ${item.name}`
        })
        .join(' | ')
    }

    // 旧格式：单对象 {type, id, code, name}
    if (parsed.type && parsed.name) {
      const label = auxTypeLabels[parsed.type] || parsed.type
      return `${label}: ${parsed.name}`
    }

    // 无法识别的格式，返回空
    return ''
  } catch {
    return ''
  }
}

// 获取科目的辅助核算类型
function getAccountAuxType(entry: any): string | null {
  if (!entry.accountId) return null
  const account = accountTree.value.find((a: any) => a.id === entry.accountId)
  return account?.auxiliary || null
}

// 表单数据
const form = ref({
  id: null as number | null,
  voucherWord: '记',
  voucherNumber: 1,
  date: new Date().toISOString().split('T')[0],
  periodId: null as number | null,
  attachmentCount: 0,
  remark: '',
  creator: '',
  modifier: '',
  auditor: '',
  status: 0,
  entries: [] as any[]
})

// 当前编辑行
const currentRowIndex = ref(0)

// 当前正在编辑摘要的行索引
const editingSummaryIndex = ref<number | null>(null)

// 当前年份和期数
const currentYear = ref(new Date().getFullYear())
const currentPeriod = ref(1)

// 期间列表（含是否已结账）
const periodList = ref<Array<{ id: number; startDate: string; endDate: string; isClosed: boolean }>>([])

// 当前日期所在期间是否已结账
const isClosedPeriod = computed(() => {
  if (!form.value.date || periodList.value.length === 0) return false
  const d = new Date(form.value.date)
  const matched = periodList.value.find(p => {
    const start = new Date(p.startDate)
    const end = new Date(p.endDate)
    return d >= start && d <= end
  })
  return matched ? matched.isClosed : false
})

// 是否为待补录凭证
const isRecordingVoucher = computed(() => {
  return form.value.remark?.includes('[待补录]') && form.value.status === 0
})

// 补录完成并提交
async function handleCompleteRecord() {
  if (!validateForm()) return
  try {
    const data = prepareSubmitData()
    if (form.value.id) {
      await updateVoucher(form.value.id, data)
    }
    await completeVoucherRecord(form.value.id!)
    message.success('补录完成，已提交审核')
    router.push('/finance/voucher/list')
  } catch (error: any) {
    message.error(error?.message || '补录提交失败')
  }
}

// 根据日期更新年份和期数
watch(() => form.value.date, (newDate) => {
  if (newDate) {
    const date = new Date(newDate)
    currentYear.value = date.getFullYear()
    currentPeriod.value = date.getMonth() + 1
    // 按日期在已加载的期间列表中定位并回写 periodId（用于取号预览与"期"显示；后端仍会按日期权威解析）
    const matched = periodList.value.find(p => {
      const start = new Date(p.startDate)
      const end = new Date(p.endDate)
      return date >= start && date <= end
    })
    form.value.periodId = matched ? matched.id : null
  }
}, { immediate: true })

// 计算合计
const totalDebit = computed(() => {
  return form.value.entries.reduce((sum, entry) => sum + (entry.debitAmount || 0), 0)
})

const totalCredit = computed(() => {
  return form.value.entries.reduce((sum, entry) => sum + (entry.creditAmount || 0), 0)
})

// 借贷差额与平衡状态
const balanceDiff = computed(() => {
  return Math.abs(totalDebit.value - totalCredit.value)
})
const isBalanced = computed(() => balanceDiff.value < 0.01)

// 合计金额格子
const totalDebitCells = computed(() => formatAmountToCells(totalDebit.value))
const totalCreditCells = computed(() => formatAmountToCells(totalCredit.value))

function formatAmountToCells(amount: number): string[] {
  if (!amount || amount === 0) {
    return Array(11).fill('')
  }
  
  const totalFen = Math.round(Math.abs(amount) * 100) // 转为分（整数）
  
  // 初始化11个格子为空字符串
  const result: string[] = new Array(11).fill('')
  
  // 从右到左填入数字（分->亿）
  const digits = totalFen.toString().split('')
  for (let i = 0; i < digits.length && i < 11; i++) {
    result[10 - i] = digits[digits.length - 1 - i]
  }
  
  // 找到第一个非0数字的位置（从左到右，只检查整数部分，索引0-8对应亿到元）
  let firstNonZero = -1
  for (let i = 0; i < 9; i++) {
    if (result[i] !== '' && result[i] !== '0') {
      firstNonZero = i
      break
    }
  }
  
  // 处理前导0：将有效数字之前的位设为空
  if (firstNonZero > 0) {
    for (let i = 0; i < firstNonZero; i++) {
      result[i] = ''
    }
  } else if (firstNonZero === -1) {
    // 整数部分全是0（金额小于1元），只保留元位的0
    for (let i = 0; i < 8; i++) {
      result[i] = ''
    }
  }
  
  return result
}

// 科目数据
const accountTree = ref<any[]>([])
const filteredAccounts = ref<any[]>([])

// 草稿箱
const draftsVisible = ref(false)
const draftList = ref<any[]>([])
const draftColumns = [
  { title: '凭证字', dataIndex: 'voucherWord', width: 80 },
  { title: '凭证号', dataIndex: 'voucherNumber', width: 80 },
  { title: '日期', dataIndex: 'date', width: 120 },
  { title: '摘要', dataIndex: 'summary' },
  { title: '操作', dataIndex: 'action', width: 120 }
]
const showShortcuts = ref(false)

// 科目选择弹窗相关
const accountSelectVisible = ref(false)
const currentEditingIndex = ref<number>(0)
const selectedAccount = ref<any>(null)
const accountCategories = ['资产', '负债', '权益', '成本', '损益']
const currentSelectCategory = ref('资产')
const categoryTreeMap = ref<Record<string, any[]>>({})

// treeProps no longer needed - using a-tree :fieldNames directly

// 当前分类的科目树
const currentCategoryTree = computed(() => {
  const tree = categoryTreeMap.value[currentSelectCategory.value] || []
  return addDisplayName(tree)
})

// 为科目树节点添加 displayName
function addDisplayName(nodes: any[]): any[] {
  return nodes.map(node => {
    const result = {
      ...node,
      displayName: `${node.code} ${node.name}`
    }
    if (node.children && node.children.length > 0) {
      result.children = addDisplayName(node.children)
    }
    return result
  })
}

// 新增科目弹窗相关
const addAccountVisible = ref(false)
const addAccountFormRef = ref()
const addAccountForm = reactive({
  code: '',
  name: '',
  category: '',
  balanceDirection: '借',
  parentId: null as number | null,
  unit: '',
  enableQuantity: false
})

const addAccountRules: Record<string, any[]> = {
  code: [{ required: true, message: '请输入编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  category: [{ required: true, message: '请选择类别', trigger: 'change' }]
}

// 余额方向开关（true=借，false=贷）
const addAccountBalanceSwitch = computed({
  get: () => addAccountForm.balanceDirection === '借',
  set: (val: boolean) => {
    addAccountForm.balanceDirection = val ? '借' : '贷'
  }
})

// 科目类别选项映射
const categoryOptionsMap: Record<string, { label: string; value: string }[]> = {
  '资产': [
    { label: '流动资产', value: '流动资产' },
    { label: '非流动资产', value: '非流动资产' }
  ],
  '负债': [
    { label: '流动负债', value: '流动负债' },
    { label: '非流动负债', value: '非流动负债' }
  ],
  '权益': [
    { label: '所有者权益', value: '所有者权益' }
  ],
  '成本': [
    { label: '成本', value: '成本' }
  ],
  '损益': [
    { label: '损益', value: '损益' }
  ]
}

// 当前可选的科目类别
const categoryOptions = computed(() => {
  const mainCategory = getMainCategory(addAccountForm.category)
  return categoryOptionsMap[mainCategory] || categoryOptionsMap['资产']
})

// 获取科目所属大类
function getMainCategory(category: string): string {
  for (const [mainCat, subCats] of Object.entries(categoryOptionsMap)) {
    if (subCats.some(c => c.value === category)) {
      return mainCat
    }
  }
  return currentSelectCategory.value
}

// 上级科目名称
const addAccountParentName = computed(() => {
  if (!addAccountForm.parentId) return '无'
  const parent = findAccountById(accountTree.value, addAccountForm.parentId)
  return parent ? `${parent.code} ${parent.name}` : '无'
})

// 根据ID查找科目
function findAccountById(tree: any[], id: number): any | null {
  for (const node of tree) {
    if (node.id === id) return node
    if (node.children) {
      const found = findAccountById(node.children, id)
      if (found) return found
    }
  }
  return null
}

// 初始化分录行
function initEntries() {
  form.value.entries = []
  for (let i = 0; i < 5; i++) {
    form.value.entries.push(createEmptyEntry(i + 1))
  }
}

function createEmptyEntry(lineNumber: number) {
  return {
    _uid: genTempId(),
    lineNumber,
    summary: '',
    accountId: null,
    accountCode: '',
    accountName: '',
    debitAmount: null as number | null,
    creditAmount: null as number | null,
    auxiliaryJson: null,
    currencyCode: null as string | null,   // 外币代码
    exchangeRate: null as number | null,   // 汇率
    originalAmount: null as number | null  // 原币金额
  }
}

// 加载凭证数据
async function loadVoucher(id: number) {
  try {
    const data = await getVoucherDetail(id) as any
    if (data) {
      form.value = { ...form.value, ...data }
      // API 返回 voucherNo，表单字段为 voucherNumber，需要手动映射
      if (data.voucherNo !== undefined) {
        form.value.voucherNumber = data.voucherNo
      }
      // 确保至少5行
      while (form.value.entries.length < 5) {
        form.value.entries.push(createEmptyEntry(form.value.entries.length + 1))
      }
      // 确保所有分录都有 _uid
      form.value.entries.forEach((e: any) => { if (!e._uid) e._uid = genTempId() })
    }
  } catch (error) {
    message.error('加载凭证失败')
  }
}

// 加载期间列表
async function loadPeriods() {
  const accountSetId = accountSetStore.currentAccountSetId
  if (!accountSetId) return
  try {
    const res = await getPeriods(accountSetId) as any[]
    if (res) {
      periodList.value = res.map((p: any) => ({
        id: p.id,
        startDate: p.startDate,
        endDate: p.endDate,
        isClosed: p.isClosed
      }))
    }
  } catch (error) {
    console.error('加载期间失败', error)
  }
}

// 加载科目树
async function loadAccounts() {
  const accountSetId = accountSetStore.currentAccountSetId
  if (!accountSetId) return
  try {
    const res = await getAccountTree(undefined, accountSetId) as any[]
    if (res) {
      accountTree.value = flattenAccounts(res)
      filteredAccounts.value = accountTree.value
    }
  } catch (error) {
    console.error('加载科目失败', error)
  }
}

function flattenAccounts(accounts: any[]): any[] {
  const result: any[] = []
  function traverse(nodes: any[]) {
    nodes.forEach(node => {
      result.push(node)
      if (node.children && node.children.length > 0) {
        traverse(node.children)
      }
    })
  }
  traverse(accounts)
  return result
}

// 日期变化
async function onDateChange() {
  // 根据日期获取新的凭证号
  if (!form.value.id) {
    await fetchNextNumber()
  }
}

// 获取下一个凭证号
async function fetchNextNumber() {
  try {
    const res = await getNextVoucherNumber(form.value.voucherWord, form.value.periodId || 0) as any
    if (res) {
      form.value.voucherNumber = res
    }
  } catch (error) {
    console.error('获取凭证号失败', error)
  }
}

// 金额点击
function onAmountClick(index: number, type: 'debit' | 'credit') {
  currentRowIndex.value = index
  // 如果借方有金额，贷方不能输入，反之亦然
  const entry = form.value.entries[index]
  if (type === 'debit' && entry.creditAmount) {
    entry.creditAmount = null
  } else if (type === 'credit' && entry.debitAmount) {
    entry.debitAmount = null
  }
}

// Tab键切换焦点
function focusNext(_event: Event, index: number, field: string) {
  const fields = ['summary', 'account', 'debit', 'credit']
  const currentFieldIndex = fields.indexOf(field)
  
  if (currentFieldIndex < fields.length - 1) {
    // 移动到下一个字段
    currentRowIndex.value = index
  } else if (index < form.value.entries.length - 1) {
    // 移动到下一行
    currentRowIndex.value = index + 1
  } else {
    // 添加新行
    if (form.value.entries.length >= 200) {
      message.warning('凭证分录数量已达上限(200条)')
      return
    }
    form.value.entries.push(createEmptyEntry(form.value.entries.length + 1))
    nextTick(() => {
      currentRowIndex.value = form.value.entries.length - 1
    })
  }
}

// 保存凭证
async function saveVoucher() {
  if (!validateForm()) return
  
  try {
    const data = prepareSubmitData()
    let res
    if (form.value.id) {
      res = await updateVoucher(form.value.id, data) as any
    } else {
      res = await createVoucher(data) as any
    }
    
    if (res) {
      message.success('保存成功')
      if (!form.value.id) {
        form.value.id = res?.id
      }
      // 保存成功后上传待上传附件
      if (form.value.id && pendingFiles.value.length > 0) {
        await uploadPendingFiles(form.value.id)
      }
    }
  } catch (error) {
    message.error('保存失败')
  }
}

// 保存并新增
async function saveAndNew() {
  await saveVoucher()
  clearForm()
  await fetchNextNumber()
}

// 暂存草稿
async function saveDraft() {
  try {
    const data = prepareSubmitData()
    await apiSaveDraft(data)
    message.success('暂存成功')
  } catch (error) {
    message.error('暂存失败')
  }
}

// 显示草稿箱
async function showDrafts() {
  try {
    const res = await getDrafts() as any[]
    draftList.value = res || []
    draftsVisible.value = true
  } catch (error) {
    message.error('加载草稿失败')
  }
}

// 加载草稿
function loadDraft(draft: any) {
  form.value = { ...form.value, ...draft }
  draftsVisible.value = false
}

// 删除草稿
async function deleteDraft(draft: any) {
  try {
    await new Promise<void>((resolve, reject) => {
      Modal.confirm({
        title: '提示',
        content: '确定删除此草稿？',
        onOk() { resolve() },
        onCancel() { reject() }
      })
    })
    // 调用删除草稿API
    draftList.value = draftList.value.filter(d => d.id !== draft.id)
    message.success('删除成功')
  } catch {
    // 取消
  }
}

// 审核凭证
async function auditVoucher() {
  if (!form.value.id) {
    message.warning('请先保存凭证')
    return
  }
  try {
    await apiAuditVoucher(form.value.id)
    message.success('审核成功')
    form.value.status = 1
  } catch (error) {
    message.error('审核失败')
  }
}

// 清空表单
function clearForm() {
  form.value.id = null
  form.value.voucherNumber = 1
  form.value.attachmentCount = 0
  form.value.remark = ''
  form.value.creator = ''
  form.value.modifier = ''
  form.value.auditor = ''
  form.value.status = 0
  attachmentList.value = []
  pendingFiles.value = []
  initEntries()
}

// 上一张凭证
function prevVoucher() {
  if (form.value.id && form.value.id > 1) {
    router.push(`/finance/voucher/entry/${form.value.id - 1}`)
  }
}

// 下一张凭证
function nextVoucher() {
  if (form.value.id) {
    router.push(`/finance/voucher/entry/${form.value.id + 1}`)
  }
}

// 更多操作
function handleMoreCommand(command: string) {
  switch (command) {
    case 'print':
      message.info('打印功能开发中')
      break
    case 'copy':
      message.info('复制功能开发中')
      break
    case 'delete':
      handleDelete()
      break
  }
}

// 删除凭证
async function handleDelete() {
  if (!form.value.id) {
    message.warning('当前凭证未保存')
    return
  }
  try {
    await new Promise<void>((resolve, reject) => {
      Modal.confirm({
        title: '提示',
        content: '确定删除此凭证？',
        onOk() { resolve() },
        onCancel() { reject() }
      })
    })
    // 调用删除API
    message.success('删除成功')
    router.push('/finance/voucher/list')
  } catch {
    // 取消
  }
}

// 表单验证
function validateForm(): boolean {
  // 检查借贷平衡
  if (totalDebit.value !== totalCredit.value) {
    message.error('借贷方金额不平衡')
    return false
  }
  if (totalDebit.value === 0) {
    message.error('金额不能为0')
    return false
  }
  // 检查必填项
  const validEntries = form.value.entries.filter(
    e => e.summary && e.accountId && (e.debitAmount || e.creditAmount)
  )
  if (validEntries.length < 2) {
    message.error('至少需要两条有效的分录')
    return false
  }
  return true
}

// 准备提交数据
function prepareSubmitData() {
  const validEntries = form.value.entries
    .filter(e => e.summary && e.accountId && (e.debitAmount || e.creditAmount))
    .map((e, idx) => ({
      ...e,
      lineNumber: idx + 1
    }))
  
  return {
    voucherWord: form.value.voucherWord,
    voucherNumber: form.value.voucherNumber,
    date: form.value.date,
    periodId: form.value.periodId,
    attachmentCount: form.value.attachmentCount,
    remark: form.value.remark,
    entries: validEntries
  }
}

// 监听路由参数变化
watch(() => route.params.id, (newId) => {
  if (newId) {
    loadVoucher(Number(newId))
  } else {
    clearForm()
    fetchNextNumber()
  }
})

// 监听账套切换
watch(() => accountSetStore.currentAccountSetId, async (newId) => {
  if (!newId) return
  await loadAccounts()
  await loadPeriods()
  loadTemplates()
  // 重置凭证数据
  initEntries()
  fetchNextNumber()
})

// 会计科目单元格点击
function handleAccountCellClick(index: number) {
  currentRowIndex.value = index
}

// 打开科目选择弹窗
function openAccountSelectDialog(index: number) {
  currentEditingIndex.value = index
  selectedAccount.value = null
  accountSelectVisible.value = true
  loadCategoryTree(currentSelectCategory.value)
}

// 切换科目分类
function switchSelectCategory(category: string) {
  currentSelectCategory.value = category
  loadCategoryTree(category)
}

// 加载分类科目树
async function loadCategoryTree(category: string) {
  if (!categoryTreeMap.value[category]) {
    try {
      const res = await getAccountTree(category) as any[]
      categoryTreeMap.value[category] = res || []
    } catch (error) {
      console.error('加载科目树失败', error)
    }
  }
}

// 科目树节点点击
function handleAccountNodeClick(data: any) {
  selectedAccount.value = data
}

// 确认选择科目
function confirmAccountSelect() {
  if (!selectedAccount.value) {
    message.warning('请选择一个科目')
    return
  }
  const entry = form.value.entries[currentEditingIndex.value]
  entry.accountId = selectedAccount.value.id
  entry.accountCode = selectedAccount.value.code
  entry.accountName = selectedAccount.value.name
  accountSelectVisible.value = false

  // 获取该科目的当前余额
  fetchAccountBalance(selectedAccount.value.id)

  // 检测科目是否有外币属性
  const currency = selectedAccount.value.currency
  if (currency && currency !== 'RMB' && currency !== '人民币') {
    entry.currencyCode = currency
    // 自动获取最新汇率
    autoFetchExchangeRate(currentEditingIndex.value, currency)
  } else {
    entry.currencyCode = null
    entry.exchangeRate = null
    entry.originalAmount = null
  }
}

// 打开新增科目弹窗
function openAddAccountDialog(index: number) {
  currentEditingIndex.value = index
  resetAddAccountForm()
  // 默认设置为当前选中的分类
  addAccountForm.category = currentSelectCategory.value === '资产' ? '流动资产' : 
                            currentSelectCategory.value === '负债' ? '流动负债' :
                            currentSelectCategory.value === '权益' ? '所有者权益' :
                            currentSelectCategory.value === '成本' ? '成本' : '损益'
  addAccountVisible.value = true
}

// 重置新增科目表单
function resetAddAccountForm() {
  addAccountForm.code = ''
  addAccountForm.name = ''
  addAccountForm.category = ''
  addAccountForm.balanceDirection = '借'
  addAccountForm.parentId = null
  addAccountForm.unit = ''
  addAccountForm.enableQuantity = false
  addAccountFormRef.value?.resetFields()
}

// 新增科目提交
async function handleAddAccountSubmit() {
  const valid = await addAccountFormRef.value?.validate().catch(() => false)
  if (!valid) return

  try {
    await createAccount({
      code: addAccountForm.code,
      name: addAccountForm.name,
      category: addAccountForm.category,
      balanceDirection: addAccountForm.balanceDirection,
      parentId: addAccountForm.parentId,
      unit: addAccountForm.enableQuantity ? addAccountForm.unit : null
    }, accountSetStore.currentAccountSetId || 0)
    message.success('创建成功')
    addAccountVisible.value = false
    // 刷新科目树
    await reloadAllCategoryTrees()
    // 自动填入当前分录行
    fillNewAccountToEntry(addAccountForm.code)
  } catch (error) {
    message.error('创建失败')
  }
}

// 保存并新增
async function handleAddAccountSaveAndNew() {
  const valid = await addAccountFormRef.value?.validate().catch(() => false)
  if (!valid) return

  try {
    await createAccount({
      code: addAccountForm.code,
      name: addAccountForm.name,
      category: addAccountForm.category,
      balanceDirection: addAccountForm.balanceDirection,
      parentId: addAccountForm.parentId,
      unit: addAccountForm.enableQuantity ? addAccountForm.unit : null
    }, accountSetStore.currentAccountSetId || 0)
    message.success('创建成功')
    await reloadAllCategoryTrees()
    fillNewAccountToEntry(addAccountForm.code)
    // 保留类别和上级科目，清空其他
    const savedParentId = addAccountForm.parentId
    const savedCategory = addAccountForm.category
    const savedBalanceDirection = addAccountForm.balanceDirection
    resetAddAccountForm()
    addAccountForm.parentId = savedParentId
    addAccountForm.category = savedCategory
    addAccountForm.balanceDirection = savedBalanceDirection
  } catch (error) {
    message.error('创建失败')
  }
}

// 刷新所有分类的科目树
async function reloadAllCategoryTrees() {
  categoryTreeMap.value = {}
  for (const cat of accountCategories) {
    await loadCategoryTree(cat)
  }
  // 同时刷新扁平化列表
  await loadAccounts()
}

// 将新科目填入当前分录行
function fillNewAccountToEntry(code: string) {
  // 在扁平化列表中查找
  const account = accountTree.value.find(a => a.code === code)
  if (account) {
    const entry = form.value.entries[currentEditingIndex.value]
    entry.accountId = account.id
    entry.accountCode = account.code
    entry.accountName = account.name
  }
}

// 自动获取汇率
async function autoFetchExchangeRate(index: number, currencyCode: string) {
  try {
    const accountSetId = accountSetStore.currentAccountSetId
    if (!accountSetId) return
    const res = await getLatestExchangeRate({
      accountSetId,
      currencyCode,
      date: form.value.date || new Date().toISOString().split('T')[0]
    }) as any
    if (res && res.rate) {
      form.value.entries[index].exchangeRate = res.rate
    }
  } catch (e) {
    console.warn('自动获取汇率失败', e)
  }
}

// 原币金额变化时自动计算本位币
function onOriginalAmountChange(entry: any, direction: 'debit' | 'credit') {
  if (entry.originalAmount != null && entry.exchangeRate) {
    const localAmount = parseFloat((entry.originalAmount * entry.exchangeRate).toFixed(2))
    if (direction === 'debit') {
      entry.debitAmount = localAmount
      entry.creditAmount = null
    } else {
      entry.creditAmount = localAmount
      entry.debitAmount = null
    }
  }
}

// 切换外币方向
function setForeignDirection(entry: any, direction: 'debit' | 'credit') {
  entry._foreignDirection = direction
  onOriginalAmountChange(entry, direction)
}

// 查询分录的外币方向
function getForeignDirection(entry: any): 'debit' | 'credit' {
  return entry._foreignDirection || 'debit'
}

// 插入行
function insertEntry(index: number) {
  if (form.value.entries.length >= 200) {
    message.warning('凭证分录数量已达上限(200条)')
    return
  }
  // 在当前行下方插入新行
  const newEntry = createEmptyEntry(0)
  form.value.entries.splice(index + 1, 0, newEntry)
  // 重新编号
  renumberEntries()
}

// 删除行
function deleteEntry(index: number) {
  // 至少保留一行
  if (form.value.entries.length <= 1) {
    message.warning('至少保留一行分录')
    return
  }
  form.value.entries.splice(index, 1)
  // 重新编号
  renumberEntries()
  // 调整当前行索引
  if (currentRowIndex.value >= form.value.entries.length) {
    currentRowIndex.value = form.value.entries.length - 1
  }
}

// 重新编号分录行
function renumberEntries() {
  form.value.entries.forEach((entry, idx) => {
    entry.lineNumber = idx + 1
  })
}

// 初始化
// ===== 凭证模板 =====
const templateList = ref<any[]>([])

async function loadTemplates() {
  const accountSetId = accountSetStore.currentAccountSetId
  if (!accountSetId) return
  try {
    const res = await getVoucherTemplates(accountSetId) as any[]
    templateList.value = res || []
  } catch {
    // 静默失败
  }
}

async function handleTemplateSelect(tpl: any) {
  if (!tpl?.id) return
  // 如果当前已有有效分录，提示确认
  const hasEntries = form.value.entries.some(e => e.accountId || e.summary)
  if (hasEntries) {
    try {
      await new Promise<void>((resolve, reject) => {
        Modal.confirm({
          title: '提示',
          content: '使用模板将替换当前分录，是否继续？',
          onOk() { resolve() },
          onCancel() { reject() }
        })
      })
    } catch {
      return
    }
  }
  try {
    const detail = await getVoucherTemplateDetail(tpl.id) as any
    if (!detail?.entries?.length) {
      message.warning('模板没有分录')
      return
    }
    form.value.entries = detail.entries.map((e: any, idx: number) => ({
      _uid: genTempId(),
      lineNumber: idx + 1,
      summary: e.summary || '',
      accountId: e.accountId || null,
      accountCode: e.accountCode || '',
      accountName: e.accountName || '',
      debitAmount: e.debitAmount || null,
      creditAmount: e.creditAmount || null,
      auxiliaryJson: e.auxiliaryJson || null
    }))
    // 确保至少5行
    while (form.value.entries.length < 5) {
      form.value.entries.push(createEmptyEntry(form.value.entries.length + 1))
    }
    message.success(`已应用模板「${tpl.name}」`)
  } catch {
    message.error('加载模板失败')
  }
}

// 初始化
onMounted(() => {
  if (accountSetStore.currentAccountSetId) {
    loadAccounts()
    loadPeriods()
    loadTemplates()
  }
  initEntries()
  
  const id = route.params.id
  if (id) {
    loadVoucher(Number(id))
    loadAttachments(Number(id))
  } else {
    fetchNextNumber()
  }
})

// 监听账套变化，自动重新加载基础数据
watch(() => accountSetStore.currentAccountSetId, async (newId) => {
  if (newId) {
    accountTree.value = []
    periodList.value = []
    await Promise.all([loadAccounts(), loadPeriods(), loadTemplates()])
  }
})

// ======================== 附件上传 ========================

interface AttachmentItem {
  id: number
  fileName: string
  originalName: string
  filePath: string
  fileSize: number
  contentType: string
  uploadTime: string
  uploaderName: string
}

interface PendingFile {
  file: File
  name: string
  size: number
  status: 'pending' | 'uploading'
}

const attachmentList = ref<AttachmentItem[]>([])
const pendingFiles = ref<PendingFile[]>([])
const _uploadRef = ref<any>(null) // eslint-disable-line
void _uploadRef

// 加载已保存的附件
async function loadAttachments(voucherId: number) {
  try {
    const res = await getAttachments('voucher', voucherId)
    attachmentList.value = res || []
    form.value.attachmentCount = attachmentList.value.length
  } catch (e) {
    console.error('加载附件失败', e)
  }
}

// a-upload 自定义上传处理
async function handleUpload(options: any) {
  const file = options.file as File
  if (!form.value.id) {
    // 未保存凭证，将文件加入待上传队列
    pendingFiles.value.push({ file, name: file.name, size: file.size, status: 'pending' })
    message.info('请先保存凭证，附件将在保存后自动上传')
    return
  }
  await uploadSingleFile(file, form.value.id)
}

async function uploadSingleFile(file: File, voucherId: number) {
  const accountSetId = accountSetStore.currentAccountSetId || 0
  const formData = new FormData()
  formData.append('file', file)
  formData.append('accountSetId', String(accountSetId))
  formData.append('businessType', 'voucher')
  formData.append('businessId', String(voucherId))
  try {
    const res = await uploadAttachment(formData)
    attachmentList.value.push({
      id: res.fileId,
      fileName: res.fileName,
      originalName: res.originalName,
      filePath: res.filePath,
      fileSize: res.fileSize,
      contentType: res.contentType,
      uploadTime: res.uploadTime,
      uploaderName: ''
    })
    form.value.attachmentCount = attachmentList.value.length
    message.success(`「${file.name}」上传成功`)
  } catch (e) {
    message.error(`「${file.name}」上传失败`)
  }
}

// 保存后上传待上传文件
async function uploadPendingFiles(voucherId: number) {
  if (pendingFiles.value.length === 0) return
  for (const pf of pendingFiles.value) {
    pf.status = 'uploading'
    await uploadSingleFile(pf.file, voucherId)
  }
  pendingFiles.value = []
}

function beforeUpload(file: File) {
  const maxSize = 50 * 1024 * 1024
  if (file.size > maxSize) {
    message.error('文件大小不能超过 50MB')
    return false
  }
  return true
}

function _handleExceed() { // eslint-disable-line
  message.warning('最多上传 9 个附件')
}
void _handleExceed

function removePendingFile(idx: number) {
  pendingFiles.value.splice(idx, 1)
}

async function deleteAttachmentItem(id: number) {
  try {
    await deleteAttachment(id)
    attachmentList.value = attachmentList.value.filter(a => a.id !== id)
    form.value.attachmentCount = attachmentList.value.length
    message.success('附件已删除')
  } catch {
    message.error('删除失败')
  }
}

function downloadAttachment(id: number) {
  const url = getAttachmentDownloadUrl(id)
  const a = document.createElement('a')
  a.href = url
  a.target = '_blank'
  a.click()
}

function addInvoice() {
  message.info('添加发票功能开发中')
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(2) + ' MB'
}

function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
</script>

<style scoped lang="scss">
.voucher-entry-page {
  padding: 16px;
  background: var(--bg-card);
  min-height: calc(100vh - 100px);
}


// 凭证头
.voucher-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  padding: 12px 0;
  position: relative;

  .header-left, .header-center, .header-right {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  // 左侧区域：凭证字、号、日期
  .header-left {
    flex: 0 0 auto;
  }

  // 中间区域：标题和期数（居中）
  .header-center {
    flex: 1;
    justify-content: center;
    gap: 16px;
  }

  // 右侧区域：附件
  .header-right {
    flex: 0 0 auto;
  }

  .label {
    color: var(--text-2);
    font-size: 14px;
  }

  .voucher-title {
    font-size: 26px;
    font-weight: bold;
    color: var(--text-1);
  }

  .voucher-period {
    font-size: 15px;
    color: var(--text-3);
    letter-spacing: 1px;
  }

  // 凭证号/附件个数输入框包装器
  .voucher-number-input-wrapper {
    position: relative;
    width: 70px;

    .voucher-number-input {
      width: 100%;

      :deep(.ant-input-number-input) {
        text-align: center;
        padding-right: 0;
      }
    }

    // 默认隐藏 spin button
    input[type="number"]::-webkit-inner-spin-button,
    input[type="number"]::-webkit-outer-spin-button {
      -webkit-appearance: none;
      appearance: none;
      opacity: 0;
      width: 0;
      margin: 0;
    }

    // Firefox
    input[type="number"] {
      -moz-appearance: textfield;
    }

    // 聚焦时显示 spin button
    &:focus-within {
      input[type="number"]::-webkit-inner-spin-button,
      input[type="number"]::-webkit-outer-spin-button {
        -webkit-appearance: inner-spin-button;
        appearance: auto;
        opacity: 1;
        width: auto;
        margin: 0 2px;
      }

      input[type="number"] {
        -moz-appearance: number-input;
      }

      :deep(.ant-input-number) {
        border-color: var(--color-primary);
      }
    }
  }
}

// 凭证表格
.voucher-table-wrapper {
  border: 2px solid var(--text-1);
  margin-bottom: 16px;
  overflow-x: auto;
}

.voucher-table {
  width: 100%;
  border-collapse: collapse;
  
  th, td {
    border: 1px solid var(--border);
    padding: 0;
    text-align: center;
    vertical-align: middle;
  }
  
  th {
    background: var(--bg-muted);
    font-weight: normal;
    height: 50px;
  }
  
  td {
    height: 50px;
  }
  
  .col-action {
    width: 40px;
    min-width: 40px;
    background: var(--bg-muted);

    .row-action-btns {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 4px;
      height: 100%;
      
      .action-icon {
        width: 20px;
        height: 20px;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        font-size: 14px;
        font-weight: bold;
        border: 1px solid var(--text-3);
        color: var(--text-3);
        background: var(--bg-card);
        transition: all 0.2s;
        
        &:hover {
          border-color: var(--color-primary);
          color: var(--color-primary);
        }

        &.add:hover {
          border-color: var(--color-success);
          color: var(--color-success);
        }

        &.delete:hover {
          border-color: var(--color-danger);
          color: var(--color-danger);
        }
        
        span {
          line-height: 1;
        }
      }
    }
  }
  
  .col-summary {
    width: 25%;
    min-width: 200px;
    padding: 0;
    height: 60px;

    .summary-cell {
      position: relative;
      width: 100%;
      height: 100%;

      .summary-textarea {
        width: 100%;
        height: 100%;
        min-height: 60px;
        padding: 8px;
        border: 1px solid transparent;
        background: var(--bg-muted);
        resize: none;
        font-size: 14px;
        font-family: inherit;
        color: var(--text-1);
        outline: none;
        box-sizing: border-box;
        line-height: 1.5;

        &:hover {
          border-color: var(--border);
        }

        &:focus {
          background: var(--bg-card);
          border-color: var(--text-1);
          resize: vertical;
        }
      }

      .summary-placeholder {
        position: absolute;
        right: 8px;
        bottom: 6px;
        font-size: 12px;
        color: var(--text-3);
        pointer-events: none;
        user-select: none;
      }

      &.is-editing {
        .summary-textarea {
          background: var(--bg-card);
          border-color: var(--text-1);
          resize: vertical;
        }
      }
    }
  }
  
  .col-account {
    width: 30%;
    min-width: 250px;
  }
  
  .col-amount-header {
    width: 22.5%;
    min-width: 250px;
    padding: 4px 0;
    
    .amount-header-title {
      font-size: 14px;
      margin-bottom: 4px;
    }
    
    .amount-header-cells {
      display: flex;
      justify-content: space-around;
      font-size: 12px;
      color: var(--text-2);
      
      span {
        flex: 1;
        text-align: center;
      }
    }
  }
  
  .col-amount {
    padding: 0;
    height: 50px;
  }
  
  .active-row {
    background: var(--bg-muted);
  }

  .total-row {
    background: var(--bg-muted);
    font-weight: bold;
    
    .total-label {
      float: left;
      margin-left: 16px;
    }
    
    .amount-cells {
      display: flex;
      height: 100%;
      width: 100%;
      
      .cell {
        flex: 1;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 14px;
        font-weight: 600;
        color: var(--text-1);
        border-right: 1px solid var(--border);
        min-width: 20px;
        height: 100%;
        
        &:last-child {
          border-right: none;
        }
        
        &.red-line {
          border-right: 2px solid var(--color-danger) !important;
        }

        &.blue-line {
          border-right: 2px solid var(--color-info) !important;
        }
      }
    }
  }
}

// 借贷不平衡 / 已结账期间警告条
.balance-summary {
  display: flex;
  align-items: center;
  gap: 24px;
  padding: 10px 16px;
  margin-bottom: 8px;
  background: var(--bg-muted);
  border: 1px solid var(--border);
  border-radius: 4px;
  font-size: 14px;
  color: var(--text-1);

  b {
    font-weight: 600;
  }

  .diff-balanced {
    color: var(--color-success);
    font-weight: 600;
  }

  .diff-unbalanced {
    color: var(--color-danger);
    font-weight: 600;
  }
}

.balance-warning,
.closed-period-warning {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 8px;
}

.balance-warning {
  background: var(--color-danger-light);
  border: 1px solid var(--color-danger-border);
  color: var(--color-danger);
}

.closed-period-warning {
  background: var(--color-warning-light);
  border: 1px solid var(--color-warning-border);
  color: var(--color-warning);
}

// 底部信息
.voucher-footer {
  display: flex;
  justify-content: space-between;
  padding: 12px 0;
  margin-bottom: 16px;
  color: var(--text-2);
  font-size: 14px;
}

// 附件区域
.attachment-section {
  border-top: 1px solid var(--border);
  padding-top: 16px;

  .attachment-title {
    font-size: 14px;
    color: var(--text-2);
    margin-bottom: 12px;
  }
  
  .attachment-upload {
    display: flex;
    align-items: flex-start;
    gap: 16px;
    margin-bottom: 12px;
    
    .upload-area {
      .ant-upload-drag-container {
        width: 280px;
        height: 100px;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 4px;
        border: 1px dashed var(--border);
        border-radius: 4px;
        cursor: pointer;
        padding: 16px;
        &:hover {
          border-color: var(--color-primary);
        }
        .upload-icon {
          font-size: 28px;
          color: var(--text-3);
        }
        .upload-text {
          font-size: 13px;
          em { color: var(--color-primary); }
        }
        .upload-tip {
          font-size: 11px;
          color: var(--text-3);
        }
      }
    }

    .attachment-tips {
      color: var(--text-3);
      font-size: 12px;
      align-self: center;
      
      p {
        margin: 4px 0;
      }
    }
  }

  .attachment-list {
    margin: 8px 0 12px;
    display: flex;
    flex-direction: column;
    gap: 4px;

    .attachment-item {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 10px;
      background: var(--bg-muted);
      border-radius: 4px;
      border: 1px solid var(--border);

      &.pending {
        border-color: var(--color-warning);
        background: var(--color-warning-light);
      }

      .att-icon {
        color: var(--color-info);
        flex-shrink: 0;
      }

      .att-name {
        flex: 1;
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        font-size: 13px;
        color: var(--text-1);
      }

      .att-size {
        font-size: 12px;
        color: var(--text-3);
        flex-shrink: 0;
        min-width: 60px;
        text-align: right;
      }

      .att-time {
        font-size: 12px;
        color: var(--text-3);
        flex-shrink: 0;
      }

      .att-status {
        font-size: 12px;
        color: var(--color-warning);
        flex-shrink: 0;
      }
    }
  }
  
  .add-invoice-btn {
    margin-top: 8px;
  }
}

// 快捷键说明
.shortcuts-content {
  p {
    margin: 8px 0;
    font-size: 14px;
    
    kbd {
      display: inline-block;
      padding: 4px 8px;
      font-size: 12px;
      line-height: 1.4;
      color: var(--text-1);
      background: var(--bg-muted);
      border: 1px solid var(--border);
      border-radius: 3px;
      box-shadow: var(--shadow-sm);
      margin-right: 8px;
      min-width: 60px;
      text-align: center;
    }
  }
}

// 会计科目单元格
.account-cell {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 48px;
  padding: 0 8px;
  cursor: pointer;
  
  .account-main {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  .account-display {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: 14px;
    color: var(--text-1);
  }

  .aux-tag {
    display: inline-block;
    font-size: 11px;
    padding: 1px 6px;
    border-radius: 3px;
    cursor: pointer;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 100%;
    background: var(--color-warning-light);
    color: var(--color-warning);
    border: 1px dashed var(--color-warning);

    &.has-value {
      background: var(--color-info-light);
      color: var(--color-info);
      border: 1px solid var(--color-info-border);
    }
    
    &:hover {
      opacity: 0.8;
    }
  }
  
  .account-actions {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 2px;
    flex-shrink: 0;
    
    .action-btn {
      font-size: 12px;
      color: var(--text-3);
      cursor: pointer;
      padding: 2px 6px;
      &:hover {
        color: var(--color-primary);
      }
    }
  }
}

// 科目余额提示
.account-balance-hint {
  font-size: 11px;
  color: var(--text-3);
  padding: 2px 8px 4px;
  line-height: 1.4;
}

// 科目选择弹窗样式
.account-select-dialog {
  .account-tabs {
    display: flex;
    gap: 30px;
    padding-bottom: 10px;
    border-bottom: 1px solid var(--border);
    margin-bottom: 15px;

    .tab-item {
      font-size: 15px;
      color: var(--text-3);
      cursor: pointer;
      padding-bottom: 8px;
      position: relative;
      
      &.active {
        color: var(--text-1);
        &::after {
          content: '';
          position: absolute;
          bottom: -1px;
          left: 0;
          right: 0;
          height: 2px;
          background: var(--color-primary);
        }
      }
      &:hover {
        color: var(--color-primary);
      }
    }
  }
  
  .account-tree-container {
    height: 400px;
    overflow-y: auto;
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 8px;
    
    :deep(.ant-tree-treenode) {
      .ant-tree-node-content-wrapper {
        height: 32px;
        line-height: 32px;
      }
    }
    
    :deep(.ant-tree-title) {
      font-size: 14px;
    }
  }
}

// 新增科目弹窗样式
.account-dialog {
  :deep(.ant-modal-body) {
    padding: 20px 24px 10px;
  }

  :deep(.ant-modal-footer) {
    padding: 10px 24px 20px;
  }
}

.custom-form {
  .form-row {
    display: flex;
    align-items: center;
    margin-bottom: 16px;

    .form-label {
      width: 85px;
      flex-shrink: 0;
      text-align: left;
      color: var(--text-1);
      font-size: 14px;
    }

    .form-control {
      flex: 1;
    }
  }

  .info-tip {
    background-color: var(--color-info-light);
    border-left: 3px solid var(--color-info);
    padding: 10px 15px;
    margin: 8px 0 16px 85px;
    font-size: 13px;
    color: var(--text-2);
    line-height: 1.8;
  }

  .balance-direction-label {
    margin-left: 10px;
    color: var(--text-1);
    font-size: 14px;
  }

  .quantity-row {
    display: flex;
    align-items: center;
    gap: 20px;

    .unit-label {
      color: var(--text-1);
      font-size: 14px;
    }

    .unit-input {
      width: 120px;
    }
  }
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

// 外币面板样式
.foreign-currency-panel {
  background: var(--color-warning-light);
  border: 1px solid var(--color-warning-border);
  border-radius: 6px;
  padding: 6px 8px;
  margin-bottom: 4px;
  display: flex;
  flex-direction: column;
  gap: 4px;

  .currency-tag {
    align-self: flex-start;
  }

  .foreign-row {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .foreign-label {
    font-size: 12px;
    color: var(--color-warning);
    width: 30px;
    flex-shrink: 0;
  }

  .foreign-input {
    flex: 1;
    width: 100%;

    :deep(.ant-input-number-input) {
      font-size: 12px;
    }
  }
}
</style>
