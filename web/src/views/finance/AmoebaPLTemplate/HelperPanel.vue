<template>
  <aside
    class="amoeba-helper-panel"
    :class="{ 'amoeba-helper-panel--collapsed': collapsed }"
    :style="{ width: collapsed ? '40px' : `${width}px` }"
  >
    <!-- 折叠条 -->
    <div v-if="collapsed" class="amoeba-helper-panel__rail" @click="$emit('update:collapsed', false)">
      <a-tooltip title="展开助手栏" placement="left">
        <DoubleLeftOutlined class="amoeba-helper-panel__rail-icon" />
      </a-tooltip>
      <div class="amoeba-helper-panel__rail-text">助手</div>
    </div>

    <!-- 展开面板 -->
    <template v-else>
      <div class="amoeba-helper-panel__head">
        <div class="amoeba-helper-panel__title">上下文助手</div>
        <a-tooltip title="收起" placement="bottom">
          <a-button type="text" size="small" @click="$emit('update:collapsed', true)">
            <DoubleRightOutlined />
          </a-button>
        </a-tooltip>
      </div>
      <a-tabs v-model:active-key="activeTab" size="small" :tab-bar-gutter="6" class="amoeba-helper-panel__tabs">
        <a-tab-pane key="field" tab="字段说明">
          <div class="amoeba-helper-panel__body">
            <template v-if="focusedFieldKey && fieldDocs[focusedFieldKey]">
              <h4>{{ fieldDocs[focusedFieldKey].title }}</h4>
              <div v-html="fieldDocs[focusedFieldKey].content"></div>
            </template>
            <a-empty v-else description="点击表单字段，查看完整说明" :image="emptyImage" />
          </div>
        </a-tab-pane>
        <a-tab-pane key="logic" tab="取数逻辑">
          <div class="amoeba-helper-panel__body amoeba-helper-panel__body--scroll">
            <div class="amoeba-helper-panel__logic-intro">
              <p style="margin:0 0 8px;color:var(--text-2);font-size:12px">报表聚合时，各数据源按以下流程取数并汇入损益项：</p>
            </div>
            <a-collapse :bordered="false" default-active-key="billing">
              <a-collapse-panel key="billing" header="① 计费结果 (billing)">
                <p><strong>数据表</strong>：EXP出港运单_计费结果</p>
                <p><strong>核心字段</strong>：F应收金额 · F归属网点编号 · F品牌编码</p>
                <p><strong>聚合方式</strong>：</p>
                <ul>
                  <li>金额合计 (amount) — 运费收入</li>
                  <li>运单计数 (waybill_count) — 件量指标</li>
                  <li>重量合计 (weight) — 重量指标</li>
                </ul>
                <p><strong>范围控制</strong>：已计价 (priced) / 全部 (all)</p>
                <p style="color:var(--color-warning);font-size:12px">⚠️ 日期筛选必须用 F运单日期，非 F计费日期</p>
              </a-collapse-panel>
              <a-collapse-panel key="voucher" header="② 凭证 (voucher)">
                <p><strong>数据表</strong>：FIN凭证 + FIN凭证分录</p>
                <p><strong>匹配优先级</strong>（从高到低）：</p>
                <ol>
                  <li><strong>数据源天然归属</strong> — billing 生成的凭证直接归入出港</li>
                  <li><strong>科目编码前缀</strong> — 最长前缀优先匹配</li>
                  <li><strong>摘要关键词</strong> — 凭证摘要包含关键词</li>
                  <li><strong>人工分类</strong> — 手动归类</li>
                  <li><strong>未匹配池</strong> — 触发报警机制</li>
                </ol>
                <div style="background:var(--color-success-light);border-radius:4px;padding:6px 8px;margin-top:8px;font-size:12px;border:1px solid var(--color-success-border)">
                  <strong>独占规则</strong>：一条凭证只能匹配一个损益项，匹配后自动解除其他历史匹配关系。
                </div>
              </a-collapse-panel>
              <a-collapse-panel key="estimate" header="③ 暂估数据 (estimate)">
                <p>从暂估入库数据取数，与凭证走同一匹配管道。</p>
                <p><strong>特点</strong>：支持辅助核算过滤，不预绑定损益项。</p>
              </a-collapse-panel>
              <a-collapse-panel key="depreciation" header="④ 资产折旧 (depreciation)">
                <p>从固定资产卡片自动计算折旧金额。</p>
                <p><strong>方向</strong>：固定归入“综合”方向，参与分摊。</p>
              </a-collapse-panel>
              <a-collapse-panel key="allocation" header="⑤ 分摊 (allocation)">
                <p>综合方向的 10 项成本汇入分摊池，按件量比例分配：</p>
                <ul>
                  <li>出港份额 = 综合池 × (出港件量 / 总件量)</li>
                  <li>进港份额 = 综合池 × (进港件量 / 总件量)</li>
                </ul>
                <p style="color:var(--text-3);font-size:12px">无需配置取数规则，系统自动计算。</p>
              </a-collapse-panel>
              <a-collapse-panel key="formula" header="⑥ 公式 (formula)">
                <p>纯公式计算项，引用其他损益项进行运算。</p>
                <p><strong>语法</strong>：<code>${'${项目名称}'}</code> + - 运算符</p>
                <p style="color:var(--color-warning);font-size:12px">⚠️ 未配置公式时金额恒为 0</p>
              </a-collapse-panel>
            </a-collapse>
          </div>
        </a-tab-pane>
        <a-tab-pane key="coverage" tab="覆盖率">
          <div class="amoeba-helper-panel__body amoeba-helper-panel__body--scroll">
            <slot name="coverage">
              <div class="amoeba-helper-panel__guide">
                <h4>数据覆盖率</h4>
                <p>覆盖率反映模板配置对实际财务数据的匹配完整度：已匹配的凭证分录数 ÷ 全部凭证分录数。</p>
                <table style="width:100%;border-collapse:collapse;font-size:12px;margin:8px 0">
                  <tr style="background:var(--bg-muted)"><th style="padding:4px 8px;border:1px solid var(--border)">覆盖率</th><th style="padding:4px 8px;border:1px solid var(--border)">状态</th><th style="padding:4px 8px;border:1px solid var(--border)">建议</th></tr>
                  <tr><td style="padding:4px 8px;border:1px solid var(--border);text-align:center">≥ 95%</td><td style="padding:4px 8px;border:1px solid var(--border)"><span style="color:var(--color-success)">🟢 优秀</span></td><td style="padding:4px 8px;border:1px solid var(--border)">无需处理</td></tr>
                  <tr><td style="padding:4px 8px;border:1px solid var(--border);text-align:center">≥ 80%</td><td style="padding:4px 8px;border:1px solid var(--border)"><span style="color:var(--color-warning)">🟡 良好</span></td><td style="padding:4px 8px;border:1px solid var(--border)">检查未匹配项</td></tr>
                  <tr><td style="padding:4px 8px;border:1px solid var(--border);text-align:center">< 80%</td><td style="padding:4px 8px;border:1px solid var(--border)"><span style="color:var(--color-danger)">🔴 需关注</span></td><td style="padding:4px 8px;border:1px solid var(--border)">补充关联科目/关键词</td></tr>
                </table>
                <div style="background:var(--color-success-light);border:1px solid var(--color-success-border);border-radius:4px;padding:8px;margin:10px 0;font-size:12px">
                  <strong>如何提高覆盖率：</strong>
                  <ol style="margin:4px 0 0;padding-left:18px">
                    <li>为 data 节点补充<strong>关联科目</strong>（覆盖更多科目前缀）</li>
                    <li>添加<strong>摘要关键词</strong>（兜底匹配无科目命中的凭证）</li>
                    <li>使用<strong>人工分类</strong>处理特殊凭证</li>
                  </ol>
                </div>
                <p style="color:var(--text-3);font-size:12px">💡 点击顶栏「覆盖率检查」按钮可实时计算当前模板的覆盖率并查看未匹配科目明细。</p>
              </div>
            </slot>
          </div>
        </a-tab-pane>
        <a-tab-pane key="reference" tab="引用关系">
          <div class="amoeba-helper-panel__body amoeba-helper-panel__body--scroll">
            <template v-if="references && references.length">
              <div class="amoeba-helper-panel__ref-hint">
                本项被以下 {{ references.length }} 个项目的公式引用，删除前请审视：
              </div>
              <a-list size="small" :data-source="references">
                <template #renderItem="{ item }">
                  <a-list-item>
                    <span class="amoeba-helper-panel__ref-name">{{ item.name }}</span>
                    <a-tag size="small">{{ item.direction || '综合' }}</a-tag>
                  </a-list-item>
                </template>
              </a-list>
            </template>
            <template v-else>
              <div class="amoeba-helper-panel__guide">
                <h4>公式引用关系</h4>
                <p>当其他损益项的公式中引用了当前项目名称时，此处会显示引用方列表。</p>
                <div style="background:var(--color-info-light);border:1px solid var(--color-info-border);border-radius:4px;padding:8px;margin:8px 0;font-size:12px">
                  <strong>引用示例：</strong>
                  <p style="margin:4px 0 0">若「出港毛利」的公式为：</p>
                  <code style="display:block;margin:4px 0;background:var(--bg-card);padding:4px 6px;border-radius:2px">${'${出港收入合计}'} - ${'${出港直接成本合计}'}</code>
                  <p style="margin:4px 0 0">则选中「出港收入合计」时，此处会显示「出港毛利」作为引用方。</p>
                </div>
                <div style="background:var(--color-warning-light);border:1px solid var(--color-warning-border);border-radius:4px;padding:8px;margin:8px 0;font-size:12px">
                  <strong>⚠️ 删除保护：</strong>
                  <ul style="margin:4px 0 0;padding-left:18px">
                    <li>被引用的项目<strong>不建议直接删除</strong>，否则引用方公式将求值为 0</li>
                    <li>重命名项目后，引用方公式需同步更新</li>
                    <li>选中任意项目即可实时查看其被引用情况</li>
                  </ul>
                </div>
                <p style="color:var(--text-3);font-size:12px">💡 模板内所有项目名称必须全局唯一，以确保公式引用准确无歧义。</p>
              </div>
            </template>
          </div>
        </a-tab-pane>
      </a-tabs>
    </template>
  </aside>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { Empty } from 'ant-design-vue'
import { DoubleLeftOutlined, DoubleRightOutlined } from '@ant-design/icons-vue'

const props = defineProps<{
  collapsed: boolean
  width?: number
  focusedFieldKey?: string
  references?: Array<{ id: number; name: string; direction?: string }>
  defaultTab?: string
}>()

defineEmits<{
  (e: 'update:collapsed', val: boolean): void
}>()

const emptyImage = Empty.PRESENTED_IMAGE_SIMPLE
const activeTab = ref(props.defaultTab || 'field')

// 字段说明文档库
const fieldDocs: Record<string, { title: string; content: string }> = {
  nodeRole: {
    title: '节点角色',
    content: `
      <p>决定节点在模板树中的行为与计算参与方式：</p>
      <table style="width:100%;border-collapse:collapse;font-size:12px;margin:8px 0">
        <tr style="background:var(--bg-muted)"><th style="padding:4px 8px;border:1px solid var(--border);text-align:left">角色</th><th style="padding:4px 8px;border:1px solid var(--border);text-align:left">行为</th><th style="padding:4px 8px;border:1px solid var(--border);text-align:center">参与汇总</th></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>分组 (group)</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">分组容器，金额=子项自动求和</td><td style="padding:4px 8px;border:1px solid var(--border);text-align:center">✅</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>数据 (data)</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">实际取数项，需配置数据源</td><td style="padding:4px 8px;border:1px solid var(--border);text-align:center">✅</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>公式 (formula)</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">公式计算项，需配置公式</td><td style="padding:4px 8px;border:1px solid var(--border);text-align:center">❌</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>指标 (indicator)</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">KPI 指标（票量、均重等）</td><td style="padding:4px 8px;border:1px solid var(--border);text-align:center">❌</td></tr>
      </table>
      <p style="color:var(--text-3);font-size:12px">💡 汇总规则：group 金额 = Σ子级 data + Σ子级 group（不含 formula 和 indicator）</p>
    `,
  },
  itemType: {
    title: '项目类型（已融合为节点角色）',
    content: `
      <p style="color:var(--color-warning);font-size:12px">⚠️ 项目类型已统一为「节点角色」体系，请参考节点角色说明。</p>
      <ul>
        <li><strong>收入</strong> → 对应 data 角色，数据源为 billing 或 voucher</li>
        <li><strong>成本</strong> → 对应 data 角色，数据源为 voucher / estimate / depreciation</li>
        <li><strong>利润</strong> → 对应 formula 角色，配置公式如 <code>\${收入} - \${成本}</code></li>
        <li><strong>板块</strong> → 对应 group 角色，自动汇总子项金额</li>
        <li><strong>指标</strong> → 对应 indicator 角色，非金额类数据</li>
      </ul>
    `,
  },
  dataSourceType: {
    title: '数据源类型',
    content: `
      <p>data 节点必须配置数据源，决定金额从哪里取：</p>
      <table style="width:100%;border-collapse:collapse;font-size:12px;margin:8px 0">
        <tr style="background:var(--bg-muted)"><th style="padding:4px 8px;border:1px solid var(--border);text-align:left">数据源</th><th style="padding:4px 8px;border:1px solid var(--border);text-align:left">说明</th></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>计费结果</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">从出港运单计费表聚合（金额/件量/重量）</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>凭证</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">从凭证分录按科目 + 关键词匹配</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>暂估数据</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">暂估入库数据，支持辅助核算过滤</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>资产折旧</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">固定资产自动折旧，归入综合方向</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>手工填报</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">用户手动录入金额</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)"><strong>计算公式</strong></td><td style="padding:4px 8px;border:1px solid var(--border)">通过公式引用其他项目计算</td></tr>
      </table>
      <p style="color:var(--text-3);font-size:12px">💡 分摊项无需配置数据源，由系统按件量比例自动分配。</p>
    `,
  },
  billingFilter: {
    title: 'Billing 聚合配置',
    content: `
      <p>当数据源为"计费结果"时，通过聚合参数控制取数方式：</p>
      <table style="width:100%;border-collapse:collapse;font-size:12px;margin:8px 0">
        <tr style="background:var(--bg-muted)"><th style="padding:4px 8px;border:1px solid var(--border)">聚合方式</th><th style="padding:4px 8px;border:1px solid var(--border)">范围</th><th style="padding:4px 8px;border:1px solid var(--border)">典型用途</th></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)">金额 (amount)</td><td style="padding:4px 8px;border:1px solid var(--border)">已计价</td><td style="padding:4px 8px;border:1px solid var(--border)">出港计费收入</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)">件量 (waybill_count)</td><td style="padding:4px 8px;border:1px solid var(--border)">已计价</td><td style="padding:4px 8px;border:1px solid var(--border)">出港计价件量</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)">件量 (waybill_count)</td><td style="padding:4px 8px;border:1px solid var(--border)">全部</td><td style="padding:4px 8px;border:1px solid var(--border)">出港全部件量</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)">重量 (weight)</td><td style="padding:4px 8px;border:1px solid var(--border)">已计价</td><td style="padding:4px 8px;border:1px solid var(--border)">出港计价总重</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)">重量 (weight)</td><td style="padding:4px 8px;border:1px solid var(--border)">全部</td><td style="padding:4px 8px;border:1px solid var(--border)">出港全量总重</td></tr>
      </table>
      <p style="color:var(--color-warning);font-size:12px">⚠️ 日期筛选必须使用 <code>F运单日期</code> 字段，不可用 F计费日期。</p>
    `,
  },
  accountCodes: {
    title: '关联科目',
    content: `
      <p>配置凭证分录匹配的科目编码，系统按<strong>最长前缀优先</strong>算法匹配：</p>
      <div style="background:var(--color-success-light);border:1px solid var(--color-success-border);border-radius:4px;padding:8px;margin:8px 0;font-size:12px">
        <strong>匹配优先级（从高到低）：</strong>
        <ol style="margin:4px 0 0;padding-left:18px">
          <li>数据源天然归属（billing → 出港收入）</li>
          <li>科目编码前缀匹配 ← <em>本配置</em></li>
          <li>摘要关键词匹配</li>
          <li>人工分类</li>
          <li>默认归入未匹配池</li>
        </ol>
      </div>
      <p><strong>独占规则</strong>：一条凭证只能匹配一个损益项，匹配后自动解除其他历史匹配。</p>
      <p style="color:var(--text-3);font-size:12px">💡 输入 <code>5001*</code> 可批量选中所有 5001 开头的科目。</p>
    `,
  },
  auxiliaryFilter: {
    title: '辅助核算过滤',
    content: `
      <p>为关联科目独立配置辅助核算过滤条件，精确筛选凭证数据。</p>
      <p><strong>支持的过滤维度：</strong></p>
      <ul>
        <li>客户 (customer)</li>
        <li>网点 (outlet)</li>
        <li>经营单元 (business_unit)</li>
        <li>快递品牌 (express_brand)</li>
        <li>部门 (department)</li>
        <li>业务方向 (business_direction)</li>
      </ul>
      <p style="color:var(--text-3);font-size:12px">💡 每个关联科目可配置不同的辅助核算过滤，互不影响。</p>
    `,
  },
  summaryKeywords: {
    title: '摘要关键词',
    content: `
      <p>当凭证的科目编码未匹配到任何损益项时，系统会检查凭证<strong>摘要文本</strong>中是否包含此处的关键词。</p>
      <div style="background:var(--color-warning-light);border:1px solid var(--color-warning-border);border-radius:4px;padding:8px;margin:8px 0;font-size:12px">
        <strong>规则说明：</strong>
        <ul style="margin:4px 0 0;padding-left:18px">
          <li>多个关键词为<strong>"或"</strong>关系，匹配任一即归入本项</li>
          <li>优先级低于科目编码匹配</li>
          <li>仅在数据源为"凭证"时生效</li>
        </ul>
      </div>
    `,
  },
  direction: {
    title: '方向归属（Tab）',
    content: `
      <p>损益项的方向由其所属的<strong>顶级 Tab (depth=0 group)</strong> 隐式决定：</p>
      <table style="width:100%;border-collapse:collapse;font-size:12px;margin:8px 0">
        <tr style="background:var(--bg-muted)"><th style="padding:4px 8px;border:1px solid var(--border)">Tab</th><th style="padding:4px 8px;border:1px solid var(--border)">含义</th><th style="padding:4px 8px;border:1px solid var(--border)">分摊</th></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)">🚀 出港</td><td style="padding:4px 8px;border:1px solid var(--border)">出港业务的收入与直接成本</td><td style="padding:4px 8px;border:1px solid var(--border)">接收分摊份额</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)">📦 进港</td><td style="padding:4px 8px;border:1px solid var(--border)">进港业务的收入与直接成本</td><td style="padding:4px 8px;border:1px solid var(--border)">接收分摊份额</td></tr>
        <tr><td style="padding:4px 8px;border:1px solid var(--border)">🏢 综合</td><td style="padding:4px 8px;border:1px solid var(--border)">无法归属的共用成本</td><td style="padding:4px 8px;border:1px solid var(--border)">作为分摊池</td></tr>
      </table>
      <p style="color:var(--text-3);font-size:12px">💡 综合成本池按「出港件量 : 进港件量」比例自动分摊。</p>
    `,
  },
  formula: {
    title: '计算公式',
    content: `
      <p>公式引用其他损益项进行运算，<strong>必须</strong>使用 <code>\${}</code> 包裹项目名称。</p>
      <div style="background:var(--color-info-light);border:1px solid var(--color-info-border);border-radius:4px;padding:8px;margin:8px 0;font-size:12px">
        <p style="margin:0"><strong>✅ 正确写法：</strong></p>
        <code style="display:block;margin-top:4px">\${出港收入合计} - \${出港直接成本合计} - \${分摊出港份额}</code>
      </div>
      <div style="background:var(--color-danger-light);border:1px solid var(--color-danger-border);border-radius:4px;padding:8px;margin:8px 0;font-size:12px">
        <p style="margin:0"><strong>❌ 错误写法：</strong></p>
        <code style="display:block;margin-top:4px">出港收入合计 - 出港直接成本合计</code>
        <p style="margin:4px 0 0;color:var(--text-3)">裸名称无法解析为项目引用，会导致求值为 0</p>
      </div>
      <p><strong>关键约束：</strong></p>
      <ul>
        <li>引用名称必须与目标项 FItemName <strong>完全一致</strong></li>
        <li>支持 + - 运算和跨 Tab 引用</li>
        <li>模板内所有项目名称必须<strong>全局唯一</strong></li>
      </ul>
    `,
  },
  perUnitMode: {
    title: '单票均模式',
    content: `
      <p>控制单票均值的计算方式：</p>
      <ul>
        <li><strong>金额 ÷ 票量</strong>：本项金额除以总票数，得出单票均值</li>
        <li><strong>不计算</strong>：不参与单票均列的显示</li>
      </ul>
      <p style="color:var(--text-3);font-size:12px">💡 总票数来源于用户手动填报（非自动统计），作为单票均计算的分母。</p>
    `,
  },
  unit: {
    title: '单位',
    content: `
      <p>报表中金额列的显示单位，常用选项：</p>
      <ul>
        <li><strong>元</strong>：默认，适用于金额类项目</li>
        <li><strong>票</strong>：适用于件量类指标</li>
        <li><strong>kg</strong>：适用于重量类指标</li>
        <li><strong>%</strong>：适用于比率类指标</li>
      </ul>
    `,
  },
}

watch(
  () => props.defaultTab,
  (val) => {
    if (val) activeTab.value = val
  }
)

// 点击字段标签时切回"字段说明"Tab：
// defaultTab 始终为 'field' 时同值赋值不触发上面的 watch，须按 focusedFieldKey 变化联动
watch(
  () => props.focusedFieldKey,
  (val) => {
    if (val) activeTab.value = 'field'
  }
)
</script>

<style scoped lang="scss">
.amoeba-helper-panel {
  flex-shrink: 0;
  background: var(--bg-muted);
  border-left: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  transition: width 0.2s;
  height: 100%;
  overflow: hidden;

  &--collapsed {
    background: var(--bg-muted);
    cursor: pointer;
  }

  &__rail {
    height: 100%;
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 12px 0;
    gap: 12px;
    color: var(--text-2);

    &:hover {
      background: var(--color-info-light);
      color: var(--color-primary);
    }
  }

  &__rail-icon {
    font-size: 16px;
  }

  &__rail-text {
    writing-mode: vertical-rl;
    font-size: 12px;
    letter-spacing: 4px;
    user-select: none;
  }

  &__head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 8px 12px;
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
  }

  &__title {
    font-weight: 600;
    font-size: 13px;
    color: var(--text-1);
  }

  &__tabs {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;

    :deep(.ant-tabs-nav) {
      margin: 0 12px;
      padding-top: 4px;
    }

    :deep(.ant-tabs-content-holder) {
      flex: 1;
      min-height: 0;
      overflow: hidden;
    }

    :deep(.ant-tabs-content) {
      height: 100%;
    }

    :deep(.ant-tabs-tabpane) {
      height: 100%;
    }
  }

  &__body {
    padding: 12px 16px;
    height: 100%;
    overflow-y: auto;
    font-size: 13px;
    line-height: 1.6;

    h4 {
      margin: 0 0 8px;
      font-size: 14px;
      font-weight: 600;
      color: var(--text-1);
    }

    :deep(p) {
      margin: 6px 0;
    }

    :deep(ul),
    :deep(ol) {
      margin: 4px 0 8px;
      padding-left: 20px;
    }

    :deep(code) {
      background: var(--bg-muted);
      padding: 1px 4px;
      border-radius: 2px;
      font-family: 'Consolas', monospace;
      font-size: 12px;
    }

    &--scroll {
      overflow-y: auto;
    }
  }

  &__ref-hint {
    color: var(--color-warning);
    margin-bottom: 8px;
    padding: 6px 8px;
    background: var(--color-warning-light);
    border-left: 3px solid var(--color-warning);
    font-size: 12px;
  }

  &__ref-name {
    font-weight: 500;
  }

  &__guide {
    h4 {
      margin: 0 0 10px;
      font-size: 14px;
      font-weight: 600;
      color: var(--text-1);
    }

    p {
      margin: 6px 0;
      font-size: 13px;
      line-height: 1.6;
    }
  }
}
</style>
