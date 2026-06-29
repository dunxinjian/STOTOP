<template>
  <div class="admin-config-center">
    <PageHeader title="配置中心" />
    <a-alert
      message="此页面汇集了各模块的管理配置入口，仅管理员可见。"
      type="info"
      show-icon
      style="margin-bottom: 16px"
    />

    <!-- 按模块分组展示 -->
    <a-row :gutter="[16, 16]">
      <a-col :span="12" v-for="group in configGroups" :key="group.module">
        <a-card :title="group.title" size="small">
          <a-list :data-source="group.items" size="small">
            <template #renderItem="{ item }">
              <a-list-item class="config-item" @click="router.push(item.route)">
                <a-list-item-meta :title="item.name" :description="item.description">
                  <template #avatar>
                    <component :is="item.icon" style="font-size: 18px; color: var(--text-2);" />
                  </template>
                </a-list-item-meta>
                <template #actions>
                  <RightOutlined style="color: #bbb;" />
                </template>
              </a-list-item>
            </template>
          </a-list>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { markRaw, type Component } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  RightOutlined,
  CloudDownloadOutlined,
  ScheduleOutlined,
  AccountBookOutlined,
  DatabaseOutlined,
  ControlOutlined,
  PartitionOutlined,
  SwapOutlined,
  BankOutlined,
  TransactionOutlined,
  FileTextOutlined,
  SnippetsOutlined,
  FunctionOutlined,
  AuditOutlined,
  FileProtectOutlined,
  ProfileOutlined,
  SortAscendingOutlined,
  FieldTimeOutlined,
  TagsOutlined,
  CalendarOutlined,
  ApartmentOutlined,
  SafetyCertificateOutlined,
  BarcodeOutlined,
  DollarOutlined,
  GiftOutlined,
  TeamOutlined,
  EnvironmentOutlined,
  BlockOutlined,
  HomeOutlined,
} from '@ant-design/icons-vue'

const router = useRouter()

interface ConfigItem {
  name: string
  route: string
  description: string
  icon: Component
}

interface ConfigGroup {
  module: string
  title: string
  items: ConfigItem[]
}

const configGroups: ConfigGroup[] = [
  {
    module: 'cardflow',
    title: 'CardFlow',
    items: [
      { name: '自动化下载', route: '/cardflow/automation', description: '配置自动化数据下载任务', icon: markRaw(CloudDownloadOutlined) },
      { name: '任务调度', route: '/cardflow/hangfire', description: 'Hangfire 后台任务监控', icon: markRaw(ScheduleOutlined) },
    ],
  },
  {
    module: 'finance',
    title: '财务管理',
    items: [
      { name: '科目管理', route: '/finance/accounts', description: '会计科目的新增、编辑和层级维护', icon: markRaw(AccountBookOutlined) },
      { name: '账套管理', route: '/finance/account-sets', description: '账套创建与规则配置', icon: markRaw(DatabaseOutlined) },
      { name: '账套规则', route: '/finance/account-set-rules', description: '账套级别的业务规则', icon: markRaw(ControlOutlined) },
      { name: '辅助核算设置', route: '/finance/auxiliary-setting', description: '辅助核算项目类型配置', icon: markRaw(PartitionOutlined) },
      { name: '汇率管理', route: '/finance/exchange-rate', description: '多币种汇率维护', icon: markRaw(SwapOutlined) },
      { name: '交易渠道配置', route: '/finance/bank-channels', description: '银行及支付渠道配置', icon: markRaw(BankOutlined) },
      { name: '银行流水', route: '/finance/bank-transactions', description: '银行流水导入与匹配', icon: markRaw(TransactionOutlined) },
      { name: '凭证手动规则', route: '/finance/voucher-rules', description: '手动凭证生成规则配置', icon: markRaw(FileTextOutlined) },
      { name: '凭证模板', route: '/finance/voucher-template', description: '凭证分录模板定义', icon: markRaw(SnippetsOutlined) },
      { name: '公式配置', route: '/finance/formula-config', description: '报表与结转公式维护', icon: markRaw(FunctionOutlined) },
      { name: '银行对账', route: '/finance/bank-reconciliation', description: '银行对账单匹配', icon: markRaw(AuditOutlined) },
      { name: '发票管理', route: '/finance/invoice-manage', description: '发票登记与匹配', icon: markRaw(FileProtectOutlined) },
      { name: '阿米巴报表模板配置', route: '/finance/amoeba/templates', description: '阿米巴损益报表模板', icon: markRaw(ProfileOutlined) },
      { name: '待分类管理', route: '/finance/amoeba/classify', description: '阿米巴科目分类映射', icon: markRaw(SortAscendingOutlined) },
    ],
  },
  {
    module: 'task',
    title: '工作任务',
    items: [
      { name: '调度管理', route: '/task/schedules', description: '任务定时调度配置', icon: markRaw(FieldTimeOutlined) },
      { name: '标签管理', route: '/task/tags', description: '任务标签的创建与维护', icon: markRaw(TagsOutlined) },
      { name: '考核周期', route: '/task/performance/periods', description: '绩效考核周期设置', icon: markRaw(CalendarOutlined) },
      { name: '维度配置', route: '/task/performance/dimensions', description: '绩效考核维度定义', icon: markRaw(ApartmentOutlined) },
    ],
  },
  {
    module: 'express',
    title: '快递管理',
    items: [
      { name: '审核规则', route: '/express/invoice/review-rules', description: '账单审核自动化规则', icon: markRaw(SafetyCertificateOutlined) },
      { name: '运单号管理', route: '/express/waybill-number', description: '运单号段分配与管理', icon: markRaw(BarcodeOutlined) },
      { name: '出港加收管理', route: '/express/surcharge', description: '出港附加费规则配置', icon: markRaw(DollarOutlined) },
      { name: '返利政策', route: '/express/policy-rebate', description: '返利政策规则定义', icon: markRaw(GiftOutlined) },
      { name: '业务代理', route: '/express/agent', description: '快递业务代理商管理', icon: markRaw(TeamOutlined) },
      { name: '快递网点', route: '/express/network-point', description: '网点基础信息维护', icon: markRaw(EnvironmentOutlined) },
      { name: '承包区', route: '/express/franchise-area', description: '承包区域划分与配置', icon: markRaw(BlockOutlined) },
      { name: '末端驿站', route: '/express/last-mile-station', description: '末端驿站信息管理', icon: markRaw(HomeOutlined) },
    ],
  },
]
</script>

<style scoped lang="scss">
.admin-config-center {
  padding: 0;
}

.config-item {
  cursor: pointer;
  transition: background-color 0.2s;

  &:hover {
    background-color: #f5f5f5;
  }
}
</style>
