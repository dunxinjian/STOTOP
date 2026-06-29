## 阶段3：主页工作台重设计

> 前置假定：阶段0 已在 `:root` 定义全部权威 CSS 令牌（`--color-*`/`--text-*`/`--bg-*`/`--border*`/`--biz-*`/`--radius-*`/`--shadow-*`/`--font-*`/`--space-*`）；阶段1 已完成全局去蓝。本阶段所有颜色/间距/字号一律引用 `var(--令牌)`，不得新增硬编码十六进制色或像素间距（栅格尺寸、动画位移等纯布局值除外）。
> 验证命令统一用 PowerShell 语法（`;` 串联、`$null`）。ripgrep 在仓库根执行。

---

### Task 1: theme.ts 权威色值对齐（success/error/info/sidebar）

**Files:**
- Modify `web/src/stores/theme.ts` (52-78 `defaultThemeConfig`)

- [ ] **Step 1: 读取并确认当前默认值。** 已确认 `web/src/stores/theme.ts` 第 52-78 行 `defaultThemeConfig`：`colorSuccess: '#52C41A'`、`colorError: '#FF4D4F'`、`colorInfo: '#13C2C2'`、`colorWarning: '#E6A700'`、`sidebarBgColor: '#e4e7ef'`。`colorPrimary: '#FF6700'` 维持不变（权威主色经 antd 算法派生，hover/active 由阶段0 CSS 令牌承担）。

- [ ] **Step 2: 改 colorSuccess。** 当前 `  colorSuccess: '#52C41A',` → 目标 `  colorSuccess: '#2BA471',`。

- [ ] **Step 3: 改 colorError 与 colorInfo。** 当前两行：
```ts
  colorError: '#FF4D4F',
  colorInfo: '#13C2C2',
```
→ 目标：
```ts
  colorError: '#E5484D',
  colorInfo: '#3A6FB0',
```
（`colorWarning: '#E6A700'` 已与权威值一致，保留不动。）

- [ ] **Step 4: 改 sidebarBgColor。** 当前 `  sidebarBgColor: '#e4e7ef',` → 目标 `  sidebarBgColor: '#EDEEF1',`。

- [ ] **Step 5: 静态验证旧青色信息色已无引用。** 运行 `rg -n "#13C2C2|#13c2c2" web/src/stores/theme.ts`，期望输出 0 行（命令无 stdout，退出码 1 视为通过）。再运行 `rg -n "'#FF4D4F'|'#52C41A'" web/src/stores/theme.ts`，期望 0 行。

- [ ] **Step 6: 提交。** `git add web/src/stores/theme.ts; git commit -m "阶段3-1：theme 默认色对齐权威令牌（成功绿/危险红/信息蓝/侧栏底）`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 2: WorkItemCard 来源/优先级配置色改令牌

**Files:**
- Modify `web/src/views/workhub/WorkItemCard.vue` (401-430 `sourceConfig`/`priorityConfig`)

- [ ] **Step 1: 确认当前来源配置。** 已确认 401-412 行 `sourceConfig` 中颜色为硬编码：`oa #1677ff`、`quality #fa541c`、`task #52c41a`、`datacenter #722ed1`、`cardflow #1677ff`、`contract #7B5B3A`、`points #d4b106`、`finance #faad14`、`system #595959`、`workflow #13c2c2`。模板 25-33 行以 `sourceColor + '15'`/`+ '18'` 拼接透明底，故 color 值必须是可拼 8 位 alpha 的十六进制或改为 rgba——令牌为 CSS 变量无法做字符串拼接，需改造为运行时读取。

- [ ] **Step 2: 将 sourceConfig 颜色改为 CSS 变量引用串。** 当前：
```ts
const sourceConfig: Record<WorkItem['source'], { label: string; color: string; icon: any }> = {
  oa: { label: 'OA审批', color: '#1677ff', icon: AuditOutlined },
  quality: { label: '质量异常', color: '#fa541c', icon: WarningOutlined },
  task: { label: '任务', color: '#52c41a', icon: CheckSquareOutlined },
  datacenter: { label: 'CardFlow', color: '#722ed1', icon: ImportOutlined },
  cardflow: { label: 'CardFlow审批', color: '#1677ff', icon: AuditOutlined },
  contract: { label: '合同', color: '#7B5B3A', icon: FileTextOutlined },
  points: { label: '积分', color: '#d4b106', icon: TrophyOutlined },
  finance: { label: '财务', color: '#faad14', icon: DollarOutlined },
  system: { label: '系统', color: '#595959', icon: SettingOutlined },
  workflow: { label: '工作流', color: '#13c2c2', icon: CheckSquareOutlined },
}
```
→ 目标（color 改为权威业务令牌的 `var(...)` 引用；审批/CardFlow 走 `--biz-approval`，运单/datacenter 走 `--biz-waybill`，工作流/任务/系统无专属业务色则用语义色或中性 `--text-3`）：
```ts
const sourceConfig: Record<WorkItem['source'], { label: string; color: string; icon: any }> = {
  oa: { label: 'OA审批', color: 'var(--biz-approval)', icon: AuditOutlined },
  quality: { label: '质量异常', color: 'var(--biz-quality)', icon: WarningOutlined },
  task: { label: '任务', color: 'var(--color-success)', icon: CheckSquareOutlined },
  datacenter: { label: '运单', color: 'var(--biz-waybill)', icon: ImportOutlined },
  cardflow: { label: 'CardFlow审批', color: 'var(--biz-approval)', icon: AuditOutlined },
  contract: { label: '合同', color: 'var(--biz-contract)', icon: FileTextOutlined },
  points: { label: '积分', color: 'var(--biz-points)', icon: TrophyOutlined },
  finance: { label: '财务', color: 'var(--biz-finance)', icon: DollarOutlined },
  system: { label: '系统', color: 'var(--text-3)', icon: SettingOutlined },
  workflow: { label: '工作流', color: 'var(--color-success)', icon: CheckSquareOutlined },
}
```
（同时把 datacenter 的 label `'CardFlow'` 修正为 `'运单'`，与统计栏 `sourceLabelMap` 口径一致。）

- [ ] **Step 3: 改造模板里的 alpha 拼接为 color-mix。** 模板第 25 行 `:style="{ background: sourceColor + '15' }"` 与第 33 行 `:style="{ background: sourceColor + '18', color: sourceColor }"` 因 color 现为 `var(...)` 串无法再 `+ '15'` 拼接。当前 25 行：
```html
    <div class="source-icon-wrap" :style="{ background: sourceColor + '15' }">
```
→ 目标：
```html
    <div class="source-icon-wrap" :style="{ background: `color-mix(in srgb, ${sourceColor} 9%, transparent)` }">
```
当前 33 行：
```html
        <span class="source-tag" :style="{ background: sourceColor + '18', color: sourceColor }">
```
→ 目标：
```html
        <span class="source-tag" :style="{ background: `color-mix(in srgb, ${sourceColor} 12%, transparent)`, color: sourceColor }">
```
（`color-mix` 在当前 stat-item 样式 930-933 行已被项目使用，浏览器支持已确认。同时修浅底对比度：来源标签底从 18%→12% 不致过深，文字用纯业务色保证 AA。）

- [ ] **Step 4: 改 priorityConfig 颜色为令牌。** 当前 419-424：
```ts
const priorityConfig = {
  urgent: { label: '紧急', color: '#ff4d4f', tagColor: 'error' },
  high: { label: '高', color: '#fa8c16', tagColor: 'warning' },
  normal: { label: '普通', color: '#1890ff', tagColor: 'processing' },
  low: { label: '低', color: '#8c8c8c', tagColor: 'default' },
} as const
```
→ 目标（color 用于左侧 priority-bar 内联 `background`，改 `var(...)`；tagColor 保留 antd 语义名，antd 色已由 Task 1 主题对齐）：
```ts
const priorityConfig = {
  urgent: { label: '紧急', color: 'var(--color-danger)', tagColor: 'error' },
  high: { label: '高', color: 'var(--color-warning)', tagColor: 'warning' },
  normal: { label: '普通', color: 'var(--color-primary)', tagColor: 'processing' },
  low: { label: '低', color: 'var(--text-3)', tagColor: 'default' },
} as const
```

- [ ] **Step 5: 构建验证。** `cd web; npm run build`，期望 vite build 成功（`✓ built in` 字样），无新增报错。

- [ ] **Step 6: 提交。** `git add web/src/views/workhub/WorkItemCard.vue; git commit -m "阶段3-2：WorkItemCard 来源/优先级色挂 --biz-*/--color-* 令牌`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 3: WorkItemCard 降信息密度（标题/摘要/元信息/间距）

**Files:**
- Modify `web/src/views/workhub/WorkItemCard.vue` (647-744 卡片内容/标题/摘要/底部 SCSS)

- [ ] **Step 1: 卡片内容纵向间距换令牌。** 当前 647-654 `.card-content`：
```scss
.card-content {
  flex: 1;
  padding: 14px 18px 14px 14px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
}
```
→ 目标（gap 8px→`--space-sm8` 令牌化、padding 走令牌组合）：
```scss
.card-content {
  flex: 1;
  padding: var(--space-md12) var(--space-lg16) var(--space-md12) var(--space-md12);
  display: flex;
  flex-direction: column;
  gap: var(--space-sm8);
  min-width: 0;
}
```

- [ ] **Step 2: 标题降到 14/500。** 当前 685-693 `.card-title`：
```scss
.card-title {
  flex: 1;
  font-size: 15px;
  font-weight: 600;
  color: $text-primary;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
```
→ 目标：
```scss
.card-title {
  flex: 1;
  font-size: var(--font-base);
  font-weight: 500;
  color: var(--text-1);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
```

- [ ] **Step 3: 摘要色换 --text-2。** 当前 703-710 `.card-summary`：
```scss
.card-summary {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.56);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  line-height: 1.5;
}
```
→ 目标：
```scss
.card-summary {
  font-size: var(--font-sm2);
  color: var(--text-2);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  line-height: 1.5;
}
```

- [ ] **Step 4: 元信息（时间/截止/流程号）换 --text-3。** 当前 695-700 `.card-time` 用 `$text-placeholder`、728-744 `.deadline` 用 `$text-secondary`、781-786 `.process-no` 用 `$text-secondary`。三处颜色统一为 `var(--text-3)`：
  - `.card-time` 695-700：`color: $text-placeholder;` → `color: var(--text-3);`
  - `.deadline` 733：`color: $text-secondary;` → `color: var(--text-3);`，且 `&.overdue` 736-739 `color: #ff4d4f;` → `color: var(--color-danger);`
  - `.process-no` 784：`color: $text-secondary;` → `color: var(--text-3);`

- [ ] **Step 5: 底部行间距换令牌。** 当前 712-718 `.card-footer` `gap: 8px; margin-top: 4px;` → `gap: var(--space-sm8); margin-top: var(--space-xs4);`。当前 720-726 `.card-meta` `gap: 8px;` → `gap: var(--space-sm8);`。当前 746-751 `.card-actions` `gap: 6px;` → `gap: var(--space-2xs2)*?`（6px 无精确令牌，改 `gap: var(--space-sm8);` 统一为 8 或保留 6px 作为按钮间距纯布局值——本步选保留 `gap: 6px;` 不动，避免视觉跳变）。仅改 `.card-footer` 与 `.card-meta` 两处。

- [ ] **Step 6: 标题行 gap 令牌化。** 当前 657-662 `.card-header` `gap: 7px;`（非 4 基数）→ `gap: var(--space-sm8);`，向 8px 对齐 4 基数刻度。

- [ ] **Step 7: 构建验证。** `cd web; npm run build`，期望成功。

- [ ] **Step 8: 提交。** `git add web/src/views/workhub/WorkItemCard.vue; git commit -m "阶段3-3：WorkItemCard 降信息密度（标题14/500、摘要--text-2、元信息--text-3、间距令牌化）`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 4: WorkItemCard 次要操作 hover 显现 + 限制可见标签 + 链接/按钮去硬编码

**Files:**
- Modify `web/src/views/workhub/WorkItemCard.vue` (114-133 模板「...」下拉、746-778 card-actions/more-actions、893-928 related-links 行/separator/link SCSS)

- [ ] **Step 1: 次要操作（更多下拉按钮）默认弱化、hover 显现。** 「...」更多按钮当前 770-777 `.more-actions-btn` 始终 `color: $text-secondary`。改为卡片非 hover 态降透明、卡片 hover 时显现。当前 770-777：
```scss
  .more-actions-btn {
    padding: 0 6px;
    color: $text-secondary;

    &:hover {
      color: $text-primary;
    }
  }
```
→ 目标：
```scss
  .more-actions-btn {
    padding: 0 6px;
    color: var(--text-3);
    opacity: 0;
    transition: opacity 0.15s ease, color 0.15s ease;

    &:hover {
      color: var(--text-1);
    }
  }
```
并在 `.work-item-card` hover 时显现：在 600-604 `.work-item-card:hover` 块内追加一行 `.more-actions-btn { opacity: 1; }`（注意 `.more-actions-btn` 在 `.card-actions` 内，需写为 `:hover .more-actions-btn`）。当前 600-604：
```scss
  &:hover {
    border-color: rgba(255, 103, 0, 0.18);
    box-shadow: 0 8px 18px rgba(18, 31, 53, 0.08);
    transform: translateY(-1px);
  }
```
→ 目标：
```scss
  &:hover {
    border-color: var(--color-primary-border);
    box-shadow: var(--shadow-md);
    transform: translateY(-1px);

    .more-actions-btn {
      opacity: 1;
    }
  }
```
（顺带把 hover 边框/阴影硬编码改令牌。注意：键盘可达性——`.more-actions-btn:focus-visible` 也应显现，在 `.more-actions-btn` 内追加 `&:focus-visible { opacity: 1; }`。）

- [ ] **Step 2: 限制关联链接可见数量。** 模板 46-78 行 `related-links-row` 用 `v-for` 渲染全部 `item.relatedLinks`，无上限。新增 computed 仅取前 3 条。在 `<script setup>` 末尾（585 行 `handleWithdraw` 之后、`</script>` 之前）追加：
```ts
// 关联链接最多展示 3 条，超出折叠为「+N」
const MAX_VISIBLE_LINKS = 3
const visibleLinks = computed(() => (props.item.relatedLinks || []).slice(0, MAX_VISIBLE_LINKS))
const hiddenLinkCount = computed(() => Math.max(0, (props.item.relatedLinks?.length || 0) - MAX_VISIBLE_LINKS))
```
然后模板 46-77 把 `v-for="(link, idx) in item.relatedLinks"`（47 行）改为 `v-for="(link, idx) in visibleLinks"`，把 76 行 `v-if="idx < item.relatedLinks!.length - 1"` 改为 `v-if="idx < visibleLinks.length - 1"`。在 77 行 `</template>` 之后、78 行 `</div>` 之前插入溢出提示：
```html
        <span v-if="hiddenLinkCount > 0" class="related-link link-more">+{{ hiddenLinkCount }}</span>
```

- [ ] **Step 3: 关联链接色去硬编码。** 当前 904-918 `.related-link`：
```scss
.related-link {
  font-size: 13px;
  color: rgba(0, 0, 0, 0.45);
  cursor: pointer;
  transition: color 0.2s;

  &:hover {
    color: #1677ff;
  }

  &.disabled {
    color: rgba(0, 0, 0, 0.25);
    cursor: not-allowed;
  }
}
```
→ 目标（hover 由蓝改主色，去蓝；常态/禁用走 text 令牌）：
```scss
.related-link {
  font-size: var(--font-sm2);
  color: var(--text-3);
  cursor: pointer;
  transition: color 0.2s;

  &:hover {
    color: var(--color-primary);
  }

  &.disabled {
    color: var(--text-disabled);
    cursor: not-allowed;
  }
}
```
当前 925-928 `.link-separator` `color: rgba(0, 0, 0, 0.25);` → `color: var(--text-disabled);`。当前 930-936 `.related-link-summary` `color: rgba(0, 0, 0, 0.65);` → `color: var(--text-2);`。

- [ ] **Step 4: 主按钮色去硬编码。** 当前 759-768 `.card-actions :deep(.ant-btn-primary)`：
```scss
  :deep(.ant-btn-primary) {
    background: #ff6700;
    border-color: #ff6700;

    &:hover,
    &:focus {
      background: #ff8533;
      border-color: #ff8533;
    }
  }
```
→ 目标：
```scss
  :deep(.ant-btn-primary) {
    background: var(--color-primary);
    border-color: var(--color-primary);

    &:hover,
    &:focus {
      background: var(--color-primary-hover);
      border-color: var(--color-primary-hover);
    }
  }
```
当前 752-757 `:deep(.ant-btn-sm)` `border-radius: 6px;` → `border-radius: var(--radius-md);`。

- [ ] **Step 5: 卡片容器与多选态去硬编码。** 当前 591-598 `.work-item-card` `border-radius: 8px;` → `border-radius: var(--radius-lg);`；`box-shadow: 0 1px 2px rgba(18,31,53,0.04);` → `box-shadow: var(--shadow-sm);`；`border: 1px solid rgba(18,31,53,0.07);` → `border: 1px solid var(--border);`。当前 610-613 `.work-item-card--multi-selected` `background-color: #e6f4ff; border-color: #91caff;`（蓝）→ 改主色浅底：`background-color: var(--color-primary-light); border-color: var(--color-primary-border);`。

- [ ] **Step 6: di-extra-icon / di-error 等导入增强残留色去硬编码。** 当前 837-840 `.di-extra-icon` `color: #52c41a;` → `color: var(--color-success);`；842-849 `.di-error` `color: #ff4d4f;` → `color: var(--color-danger);`。模板 156 行 `a-progress :stroke-color="'#52c41a'"` 改为 `:stroke-color="'var(--color-success)'"`（注意：antd Progress 的 strokeColor 接受 CSS 颜色串，CSS 变量在 SVG stroke 上下文可用；若 preview 验证发现进度条变黑，回退为权威成功色 `'#2BA471'`）。

- [ ] **Step 7: 给 link-more 加样式。** 在 `.link-separator`（925 行）之后追加：
```scss
.link-more {
  color: var(--text-3);
  font-weight: 500;
}
```

- [ ] **Step 8: 构建验证。** `cd web; npm run build`，期望成功。

- [ ] **Step 9: 静态验证本文件去蓝去硬编码。** 运行 `rg -n "#1677ff|#ff6700|#ff8533|#e6f4ff|#91caff|#52c41a|#ff4d4f|rgba\(0, 0, 0, 0\.45\)" web/src/views/workhub/WorkItemCard.vue`，期望 0 行（仅 Step 6 进度条若回退保留 `#2BA471` 不在此清单）。

- [ ] **Step 10: 提交。** `git add web/src/views/workhub/WorkItemCard.vue; git commit -m "阶段3-4：WorkItemCard 次要操作hover显现+限制可见标签+链接/按钮去硬编码`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 5: WorkHubCenter Tab 徽章优先级分层 + 页底统一 --bg-page

**Files:**
- Modify `web/src/views/workhub/WorkHubCenter.vue` (420-425 全部徽章、503-508 待办徽章、728-733 通知徽章、822-828 容器、872-879 tab-content、850-854 ink-bar)

- [ ] **Step 1: 容器页底统一 --bg-page。** 当前 822-828 `.workhub-center`：
```scss
.workhub-center {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  background: #f4f6f8;
}
```
→ 目标 `background: var(--bg-page);`。

- [ ] **Step 2: 全部 Tab 徽章——通知/混合，用中性信息色。** 当前 420-425（「全部」Tab 徽章，绿色 `#52c41a`）：
```html
          <a-badge
            v-if="allBadge > 0"
            :count="allBadge"
            :number-style="{ backgroundColor: '#52c41a', fontSize: '10px', minWidth: '16px', height: '16px', lineHeight: '16px' }"
            class="tab-badge"
          />
```
→ 目标（「全部」是聚合，用中性 `--color-info`/蓝；徽章颜色与优先级分层对应：急=danger、待办=primary、通知/聚合=info）：
```html
          <a-badge
            v-if="allBadge > 0"
            :count="allBadge"
            :number-style="{ backgroundColor: 'var(--color-info)', fontSize: '10px', minWidth: '16px', height: '16px', lineHeight: '16px' }"
            class="tab-badge"
          />
```

- [ ] **Step 3: 待办 Tab 徽章用主色。** 当前 503-508 `backgroundColor: '#1677ff'` → `backgroundColor: 'var(--color-primary)'`（待办=进行中工作=主色 primary）。

- [ ] **Step 4: 通知 Tab 徽章用信息色。** 当前 728-733 `backgroundColor: '#ff4d4f'`（红）→ `backgroundColor: 'var(--color-info)'`。说明：红色 danger 应保留给「急」语义（紧急工作项/超时），通知未读量并非紧急，降为信息色 info，避免徽章红色泛滥稀释紧急信号。

- [ ] **Step 5: ink-bar 去硬编码。** 当前 850-854 `:deep(.ant-tabs-ink-bar)` `background: #ff6700;` → `background: var(--color-primary);`。

- [ ] **Step 6: tab-content 滚动条与 nav 去硬编码。** 当前 837-843 `:deep(.ant-tabs-nav)` `border-bottom: 1px solid #e8edf3;` → `border-bottom: 1px solid var(--border);`（背景 `rgba(255,255,255,0.92)` 半透明保留，blur 效果不破坏）。

- [ ] **Step 7: 构建验证。** `cd web; npm run build`，期望成功。

- [ ] **Step 8: 提交。** `git add web/src/views/workhub/WorkHubCenter.vue; git commit -m "阶段3-5：WorkHubCenter Tab徽章优先级分层（急=danger/待办=primary/通知=info）+页底--bg-page`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 6: WorkHubCenter 统计栏色挂令牌 + 紧急摘要/新消息横幅概念收敛

**Files:**
- Modify `web/src/views/workhub/WorkHubCenter.vue` (55-62 statsItems、181-200 sourceColorMap、529-552 模板紧急摘要行、634-641 新消息横幅、908-992 统计/紧急 SCSS、1052-1089 横幅 SCSS)

- [ ] **Step 1: statsItems 颜色挂业务令牌。** 当前 55-62：
```ts
const statsItems = computed(() => [
  { key: 'oa', label: '审批', count: hub.stats.value.approval, color: '#1677ff', icon: AuditOutlined },
  { key: 'quality', label: '异常', count: hub.stats.value.alert, color: '#fa541c', icon: WarningOutlined },
  { key: 'task', label: '任务', count: hub.stats.value.task, color: '#52c41a', icon: CheckSquareOutlined },
  { key: 'datacenter', label: '运单', count: hub.stats.value.reminder, color: '#722ed1', icon: ImportOutlined },
  { key: 'contract', label: '合同', count: hub.stats.value.notification, color: '#7B5B3A', icon: FileTextOutlined },
  { key: 'points', label: '积分', count: 0, color: '#d4b106', icon: TrophyOutlined },
])
```
→ 目标（color 改 `var(...)`，用于 `--stat-color` 内联变量，see 模板 520 行 `:style="{ '--stat-color': stat.color }"`，color-mix 已用此变量）：
```ts
const statsItems = computed(() => [
  { key: 'oa', label: '审批', count: hub.stats.value.approval, color: 'var(--biz-approval)', icon: AuditOutlined },
  { key: 'quality', label: '异常', count: hub.stats.value.alert, color: 'var(--biz-quality)', icon: WarningOutlined },
  { key: 'task', label: '任务', count: hub.stats.value.task, color: 'var(--color-success)', icon: CheckSquareOutlined },
  { key: 'datacenter', label: '运单', count: hub.stats.value.reminder, color: 'var(--biz-waybill)', icon: ImportOutlined },
  { key: 'contract', label: '合同', count: hub.stats.value.notification, color: 'var(--biz-contract)', icon: FileTextOutlined },
  { key: 'points', label: '积分', count: 0, color: 'var(--biz-points)', icon: TrophyOutlined },
])
```
注意：模板 930-933 行 `.stat-item.active` 用 `color-mix(in srgb, var(--stat-color, $color-primary) 8%, white)`，`--stat-color` 现为 `var(--biz-*)` 串嵌套，`color-mix` 支持嵌套 `var()`，但 fallback `$color-primary`（SCSS 编译期常量）保留即可。

- [ ] **Step 2: sourceColorMap 改令牌（混合列表头像底色）。** 当前 181-190 `sourceColorMap` 全硬编码，模板 460 行用 `(sourceColorMap[item.data.source] || '#595959') + '18'` 拼 alpha。同 Task 2 模式改造：把 map 值改为 `var(...)`，模板 459-461 行的 `+ '18'` 改 color-mix。当前 460 行：
```html
                  :style="{ backgroundColor: (sourceColorMap[item.data.source] || '#595959') + '18', color: sourceColorMap[item.data.source] || '#595959' }"
```
→ 目标：
```html
                  :style="{ backgroundColor: `color-mix(in srgb, ${sourceColorMap[item.data.source] || 'var(--text-3)'} 12%, transparent)`, color: sourceColorMap[item.data.source] || 'var(--text-3)' }"
```
map 改为：
```ts
const sourceColorMap: Record<string, string> = {
  oa: 'var(--biz-approval)',
  quality: 'var(--biz-quality)',
  task: 'var(--color-success)',
  datacenter: 'var(--biz-waybill)',
  contract: 'var(--biz-contract)',
  points: 'var(--biz-points)',
  finance: 'var(--biz-finance)',
  system: 'var(--text-3)',
}
```

- [ ] **Step 3: 收敛概念重复——紧急摘要行与新消息横幅。** 现状：`urgent-summary-row`（模板 529-552）展示「N 项紧急 · N 项即将超时」，`new-items-banner`（634-641）展示「有 N 条新消息，点击查看」。两者均为「顶部一行可点击提示条」，视觉与位置重复。收敛策略：保留二者但视觉语义分离——紧急摘要是状态聚合（恒显），新消息横幅是增量推送（仅 newItemsCount>0 时显）。让二者样式向同一「提示条」基类靠拢以消除随机差异。当前 956-967 `.urgent-summary-row`：
```scss
.urgent-summary-row {
  display: flex;
  align-items: center;
  min-height: 34px;
  padding: 0 12px;
  margin-bottom: 10px;
  font-size: 13px;
  color: rgba(0, 0, 0, 0.65);
  border: 1px solid rgba(255, 77, 79, 0.16);
  border-radius: 8px;
  background: rgba(255, 77, 79, 0.045);
}
```
→ 目标（间距令牌化、色改 danger 令牌的 color-mix 浅底，与横幅一致用 8px 圆角令牌）：
```scss
.urgent-summary-row {
  display: flex;
  align-items: center;
  min-height: 34px;
  padding: 0 var(--space-md12);
  margin-bottom: var(--space-sm8);
  font-size: var(--font-sm2);
  color: var(--text-2);
  border: 1px solid color-mix(in srgb, var(--color-danger) 22%, transparent);
  border-radius: var(--radius-lg);
  background: var(--color-danger-light);
}
```
当前 969-987 `.urgent-dot` `background: #ff4d4f;`→`background: var(--color-danger);`；`.urgent-link, .expiring-link` `color: #cf1322;` → `color: var(--color-danger-text);`，`&:hover color: #ff4d4f;` → `color: var(--color-danger);`；`.separator` `color: rgba(0,0,0,0.25);` → `color: var(--text-disabled);`。

- [ ] **Step 4: 新消息横幅去硬编码、间距令牌化。** 当前 1053-1071 `.new-items-banner`：
```scss
.new-items-banner {
  display: flex;
  align-items: center;
  gap: 6px;
  background: #fff7f0;
  border: 1px solid rgba(255, 103, 0, 0.24);
  border-radius: 8px;
  padding: 8px 12px;
  margin-bottom: 8px;
  font-size: 13px;
  color: #d94f00;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;

  &:hover {
    background: #fff1e8;
  }
}
```
→ 目标：
```scss
.new-items-banner {
  display: flex;
  align-items: center;
  gap: var(--space-2xs2);
  background: var(--color-primary-light);
  border: 1px solid var(--color-primary-border);
  border-radius: var(--radius-lg);
  padding: var(--space-sm8) var(--space-md12);
  margin-bottom: var(--space-sm8);
  font-size: var(--font-sm2);
  color: var(--color-primary-active);
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;

  &:hover {
    background: color-mix(in srgb, var(--color-primary) 14%, transparent);
  }
}
```
当前 1085-1088 `.banner-close:hover` `background: rgba(22,119,255,0.1);`（蓝）→ `background: color-mix(in srgb, var(--color-primary) 10%, transparent);`。

- [ ] **Step 5: 统计栏卡片去硬编码。** 当前 917-921 `.stat-item` `background: $bg-card; box-shadow: 0 1px 2px rgba(18,31,53,0.04); border: 1px solid rgba(18,31,53,0.06);` → `background: var(--bg-card); box-shadow: var(--shadow-sm); border: 1px solid var(--border);`。当前 925-927 `:hover` `box-shadow: 0 8px 18px rgba(18,31,53,0.08);` → `box-shadow: var(--shadow-md);`。当前 901-906 `.stats-bar` `gap: 10px; margin-bottom: 12px;` → `gap: var(--space-sm8); margin-bottom: var(--space-md12);`。

- [ ] **Step 6: 构建验证。** `cd web; npm run build`，期望成功。

- [ ] **Step 7: 提交。** `git add web/src/views/workhub/WorkHubCenter.vue; git commit -m "阶段3-6：WorkHubCenter 统计栏挂--biz-*+紧急摘要/新消息横幅概念收敛`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 7: WorkHubCenter 残余蓝色/批量栏/混合列表/空状态去硬编码

**Files:**
- Modify `web/src/views/workhub/WorkHubCenter.vue` (1008-1031 批量栏、1100-1104 选中态、1124-1160 空状态、1188-1222 mixed-item、1316-1374 通知项/未读点)

- [ ] **Step 1: 批量操作栏去蓝。** 当前 1008-1031 `.batch-action-bar` `background: #f0f5ff; border: 1px solid #adc6ff;` 与 `.batch-info color: #1677ff;` 全蓝。→ 目标改主色浅底：`background: var(--color-primary-light); border: 1px solid var(--color-primary-border);`；`.batch-info color: var(--color-primary);`。

- [ ] **Step 2: 选中态去硬编码。** 当前 1100-1104 `:deep(.work-item-card.work-item-card--selected)`：
```scss
  background: rgba(255, 103, 0, 0.045);
  box-shadow: inset 3px 0 0 0 #ff6700, 0 8px 18px rgba(18, 31, 53, 0.08);
  border-color: rgba(255, 103, 0, 0.22);
```
→ 目标：
```scss
  background: var(--color-primary-light);
  box-shadow: inset 3px 0 0 0 var(--color-primary), var(--shadow-md);
  border-color: var(--color-primary-border);
```

- [ ] **Step 3: 空状态去硬编码。** 当前 1124-1135 `.empty-state` `background: $bg-card; border-radius: 8px; border: 1px dashed rgba(18,31,53,0.12);` → `background: var(--bg-card); border-radius: var(--radius-lg); border: 1px dashed var(--border-strong);`。当前 1137-1149 `.empty-icon` `color: #ff6700; background: rgba(255,103,0,0.08);` → `color: var(--color-primary); background: var(--color-primary-light);`。当前 1151-1155 `.empty-title` `color: $text-primary;` → `color: var(--text-1);`；1157-1160 `.empty-desc color: $text-secondary;` → `color: var(--text-3);`。空状态顺序确认：模板 674-684 行空状态已在「工作项列表」之后、「延后/归档区」之前，顺序正确（统计栏→紧急摘要→筛选→横幅→列表/空状态→延后→归档），无需调整 DOM 顺序。

- [ ] **Step 4: 混合列表 hover/未读去蓝去橙硬编码。** 当前 1200-1221 `.mixed-item` `&:hover { background: #fff7f0; border-color: rgba(255,103,0,0.18); }`、`&.mixed-notification.is-unread { background: rgba(255,77,79,0.04); }`、`&.mixed-conversation { background: rgba(82,196,26,0.02); }`。→ hover 改 `background: var(--color-primary-light); border-color: var(--color-primary-border);`；未读通知底 `background: var(--color-info-light);`；conversation 底 `background: color-mix(in srgb, var(--color-success) 4%, transparent);`。1197 行 `border: 1px solid rgba(18,31,53,0.05);` → `border: 1px solid var(--border);`。

- [ ] **Step 5: 通知项与未读点去硬编码。** 当前 1312-1322 `.notification-item:hover background: #fff7f0;` → `background: var(--color-primary-light);`；`&.is-unread background: rgba(24,144,255,0.04);`（蓝）→ `background: var(--color-info-light);`。当前 1367-1374 `.unread-dot background: #ff4d4f;`（红，2 处：1367 与模板内）→ 改 `background: var(--color-info);`（未读≠紧急，与 Task 5 通知徽章 info 口径一致）。1308-1309 `.notification-item border: 1px solid rgba(18,31,53,0.05);` → `var(--border)`。

- [ ] **Step 6: 加载更多/无更多/loading 文字去 SCSS 旧变量为令牌（可选对齐）。** 当前 1113-1121 `.load-more-btn color: $color-primary;` → `color: var(--color-primary);`；`.no-more-text color: $text-secondary;` → `color: var(--text-3);`。1176-1177 `.loading-state color: $text-secondary; font-size: $font-size-sm;` → `color: var(--text-3); font-size: var(--font-sm);`。

- [ ] **Step 7: 构建验证。** `cd web; npm run build`，期望成功。

- [ ] **Step 8: 静态验证整文件去蓝去硬编码。** 运行 `rg -n "#1677ff|#1890ff|#ff6700|#fff7f0|#f0f5ff|#adc6ff|#ff4d4f|#52c41a|#722ed1|#fa541c|#d4b106|#7B5B3A|rgba\(24, 144, 255" web/src/views/workhub/WorkHubCenter.vue`，期望 0 行。

- [ ] **Step 9: 提交。** `git add web/src/views/workhub/WorkHubCenter.vue; git commit -m "阶段3-7：WorkHubCenter 批量栏/选中态/空状态/混合列表/通知项去蓝去硬编码`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 8: WorkHubRecentVisits 移除装饰条纹 + 统一两个列表样式

**Files:**
- Modify `web/src/views/workhub/WorkHubRecentVisits.vue` (10-20 模板工作带、158-199 工作带 SCSS、201-302 chips/frequent SCSS)

- [ ] **Step 1: 移除「仓配工作带」装饰条纹块。** 模板 10-20 行整块为非交互装饰（`aria-hidden` dock-bars 条纹 + 文案，无点击入口）。决策：移除——它无路由入口、纯视觉噪声，且条纹与右栏其余内容风格冲突。删除模板 10-20 行整个 `<div class="warehouse-workband">...</div>`：
```html
      <div class="warehouse-workband">
        <div>
          <strong>仓配工作带</strong>
          <span>运单、入库、异常与结算入口集中处理</span>
        </div>
        <div class="dock-bars" aria-hidden="true">
          <span style="height: 18px"></span>
          <span style="height: 28px"></span>
          <span style="height: 22px"></span>
        </div>
      </div>
```
删除后 panel-body 直接以「最近访问」section-label（23 行）开头。

- [ ] **Step 2: 删除工作带相关 SCSS。** 删除 158-199 行 `.warehouse-workband { ... }` 与 `.dock-bars { ... }` 两个完整规则块（含其内 `strong`/`span`/`span` 嵌套）。

- [ ] **Step 3: 统一「最近访问」与「常用功能」为一致行式列表。** 现状：「最近访问」是 pill 芯片横向 flex-wrap（模板 24-35、SCSS 201-249 `.pages-chips`/`.page-chip`），「常用功能」是纵向行列表（模板 41-55、SCSS 258-302 `.frequent-list`/`.frequent-item`）。两者结构不一致。决策：将「最近访问」也改为行式列表，与常用功能统一。模板 24-35 行：
```html
      <div class="pages-chips" v-if="recommendationStore.recentPages.length > 0">
        <div
          v-for="item in recommendationStore.recentPages"
          :key="item.path"
          class="page-chip"
          @click="router.push(item.path)"
        >
          <component :is="getPageIcon(item.path)" class="chip-icon" />
          <span class="chip-name">{{ getDisplayTitle(item.title) }}</span>
          <span class="chip-time">{{ formatRelativeTime(item.lastVisitTime) }}</span>
        </div>
      </div>
```
→ 目标（复用 frequent-list/frequent-item 结构，尾部展示相对时间而非访问次数）：
```html
      <div class="frequent-list" v-if="recommendationStore.recentPages.length > 0">
        <div
          v-for="item in recommendationStore.recentPages"
          :key="item.path"
          class="frequent-item"
          @click="router.push(item.path)"
        >
          <component :is="getPageIcon(item.path)" class="frequent-icon" />
          <span class="frequent-name">{{ getDisplayTitle(item.title) }}</span>
          <span class="frequent-meta">{{ formatRelativeTime(item.lastVisitTime) }}</span>
        </div>
      </div>
```

- [ ] **Step 4: 删除废弃 pill 样式、统一行式样式与令牌。** 删除 201-249 行 `.pages-chips`/`.page-chip`/`.chip-icon`/`.chip-name`/`.chip-time` 五个规则块（已被 frequent 结构替代）。当前 264-277 `.frequent-item`：
```scss
.frequent-item {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 38px;
  padding: 8px 10px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;

  &:hover {
    background: rgba(22, 119, 255, 0.05);
  }
}
```
→ 目标（hover 去蓝改主色浅底、间距令牌化）：
```scss
.frequent-item {
  display: flex;
  align-items: center;
  gap: var(--space-sm8);
  min-height: 38px;
  padding: var(--space-sm8) var(--space-md12);
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: all 0.2s ease;

  &:hover {
    background: var(--color-primary-light);
  }
}
```
当前 294-302 `.visit-count`（访问次数 pill，橙色硬编码）`background: rgba(255,103,0,0.08); color: #ff6700;` → 改令牌；并新增 `.frequent-meta`（Step 3 引入的相对时间）样式。在 `.visit-count` 块后追加：
```scss
.frequent-meta {
  font-size: var(--font-xs);
  color: var(--text-3);
  flex-shrink: 0;
}
```
`.visit-count` 改：
```scss
.visit-count {
  font-size: var(--font-xs);
  background: var(--color-primary-light);
  color: var(--color-primary);
  padding: 2px var(--space-sm8);
  border-radius: var(--radius-pill);
  font-weight: 500;
  flex-shrink: 0;
}
```

- [ ] **Step 5: 面板头/分隔线/滚动条去硬编码。** 当前 108 行 `.recent-visits-panel background: #fff;` → `background: var(--bg-card);`。118 行 `.panel-header border-bottom: 1px solid #edf0f4;` → `border-bottom: 1px solid var(--border);`，120 行渐变背景 `linear-gradient(180deg,#fff,#fbfcfd)` → `background: var(--bg-card);`（去渐变）。124-127 `.panel-header__title color: $text-primary;` → `color: var(--text-1);`。152-155 `.section-label--top border-top: 1px solid #edf0f4;` → `border-top: 1px solid var(--border);`，144-149 `.section-label color: $text-secondary;` → `color: var(--text-2);`。279-283 `.frequent-icon color: $text-secondary;` → `color: var(--text-3);`，285-292 `.frequent-name color: $text-regular;` → `color: var(--text-1);`。

- [ ] **Step 6: 构建验证。** `cd web; npm run build`，期望成功。

- [ ] **Step 7: 静态验证去蓝去条纹。** 运行 `rg -n "warehouse-workband|dock-bars|rgba\(22, 119, 255|#f6f9ff|#ff6700|#edf0f4" web/src/views/workhub/WorkHubRecentVisits.vue`，期望 0 行。

- [ ] **Step 8: 提交。** `git add web/src/views/workhub/WorkHubRecentVisits.vue; git commit -m "阶段3-8：右栏移除仓配工作带装饰条纹+最近访问/常用功能统一行式列表`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 9: QualitySummaryCard + QualityAlertBar 去硬编码 + 概念区分

**Files:**
- Modify `web/src/views/workhub/QualitySummaryCard.vue` (94-204 SCSS)
- Modify `web/src/views/workhub/QualityAlertBar.vue` (75-139 SCSS)

- [ ] **Step 1: QualitySummaryCard 容器去橙渐变。** 当前 95-109 `.quality-summary-card` 背景为橙色渐变 `linear-gradient(135deg, rgba(255,103,0,0.08), ...)`，hover 同样橙渐变 + `box-shadow inset ... rgba(255,103,0,0.12)`。→ 改纯卡片底 + 中性 hover：
```scss
.quality-summary-card {
  border-bottom: 1px solid var(--border);
  cursor: pointer;
  transition: background 0.2s, box-shadow 0.2s;
  background: var(--bg-card);
}

.quality-summary-card:hover {
  background: var(--bg-muted);
}
```

- [ ] **Step 2: 标题/箭头/图标色去硬编码。** 当前 118-133：`.card-header__icon color: #fa541c;` → `color: var(--biz-quality);`；`.card-header__title color: #262626;` → `color: var(--text-1);`；`.card-header__arrow color: #bfbfbf;` → `color: var(--text-3);`。

- [ ] **Step 3: 指标卡片与指标色去硬编码。** 当前 152-163 `.metric-item` `background: rgba(255,255,255,0.78); border: 1px solid rgba(18,31,53,0.06); box-shadow: 0 1px 2px rgba(18,31,53,0.04);` → `background: var(--bg-card); border: 1px solid var(--border); box-shadow: var(--shadow-sm);`，`border-radius: 8px` → `var(--radius-lg)`。当前 170-180 三个指标图标色：`.pending color: #faad14;` → `color: var(--color-warning);`；`.new color: #1890ff;`（蓝）→ `color: var(--color-info);`；`.overdue color: #ff4d4f;` → `color: var(--color-danger);`。当前 187-196 `.metric-value color: #262626;` → `color: var(--text-1);`，`&.has-warning color: #ff4d4f;` → `color: var(--color-danger);`。198-203 `.metric-label color: #8c8c8c;` → `color: var(--text-3);`；147-150 `.empty-text color: #999;` → `color: var(--text-3);`。

- [ ] **Step 4: QualityAlertBar 质量色去硬编码 + 区分概念。** 现状 QualityAlertBar（告警条，模板 57-72）与 QualitySummaryCard（右栏概览卡）口径重叠：前者 `getQualityDashboardStats`（pending/overdue/todayNew），后者 `getWorkHubQualitySummary`（pendingTotal/todayNew/overdueWarning）。二者分处中栏顶部与右栏，语义为「全局告警条 vs 右栏管理概览」，保留二者但在 QualityAlertBar 顶部注释口径区分。先去色：当前 78-91 `.quality-alert-bar`：
```scss
.quality-alert-bar {
  display: flex;
  align-items: center;
  height: 40px;
  background: rgba(250, 84, 28, 0.075);
  border-left: 3px solid #fa541c;
  border-radius: 0 8px 8px 0;
  margin: 8px 18px 0 18px;
  padding: 0 14px;
  flex-shrink: 0;
  font-size: 13px;
  color: $text-regular;
  box-shadow: 0 1px 2px rgba(250, 84, 28, 0.06);
}
```
→ 目标：
```scss
.quality-alert-bar {
  display: flex;
  align-items: center;
  height: 40px;
  background: color-mix(in srgb, var(--biz-quality) 8%, transparent);
  border-left: 3px solid var(--biz-quality);
  border-radius: 0 var(--radius-lg) var(--radius-lg) 0;
  margin: var(--space-sm8) var(--space-lg16) 0 var(--space-lg16);
  padding: 0 var(--space-md12);
  flex-shrink: 0;
  font-size: var(--font-sm2);
  color: var(--text-2);
  box-shadow: var(--shadow-sm);
}
```

- [ ] **Step 5: QualityAlertBar 图标/数字/标题/分隔去硬编码。** 当前 100-126：`.alert-icon color: #fa541c;` → `color: var(--biz-quality);`；`.alert-title color: $text-primary;` → `color: var(--text-1);`；`.alert-center color: $text-secondary;` → `color: var(--text-2);`；`.alert-num color: #fa541c;` → `color: var(--biz-quality);`；`.alert-dot color: $text-placeholder;` → `color: var(--text-disabled);`。128-138 `.alert-right color: $color-primary;` → `color: var(--color-primary);`。

- [ ] **Step 6: 构建验证。** `cd web; npm run build`，期望成功。

- [ ] **Step 7: 静态验证。** 运行 `rg -n "#fa541c|#faad14|#1890ff|#ff4d4f|#262626|#bfbfbf|#999|rgba\(250, 84, 28|rgba\(255, 103, 0" web/src/views/workhub/QualitySummaryCard.vue web/src/views/workhub/QualityAlertBar.vue`，期望 0 行。

- [ ] **Step 8: 提交。** `git add web/src/views/workhub/QualitySummaryCard.vue web/src/views/workhub/QualityAlertBar.vue; git commit -m "阶段3-9：质量概览卡/告警条去硬编码挂--biz-quality+口径注释区分`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 10: index.vue 三栏容器背景统一令牌

**Files:**
- Modify `web/src/views/workhub/index.vue` (137-194 SCSS)

- [ ] **Step 1: 三栏根容器去浅橙渐变 + 统一 --bg-page。** 当前 138-145 `.workhub-three-col`：
```css
.workhub-three-col {
  display: flex;
  height: calc(100vh - 48px);
  overflow: hidden;
  background:
    linear-gradient(180deg, rgba(255, 103, 0, 0.035), rgba(255, 255, 255, 0) 180px),
    #f4f6f8;
}
```
→ 目标（去顶部浅橙渐变光晕，统一页底）：
```css
.workhub-three-col {
  display: flex;
  height: calc(100vh - 48px);
  overflow: hidden;
  background: var(--bg-page);
}
```

- [ ] **Step 2: 左栏背景与分隔线令牌化。** 当前 147-153 `.workhub-left` `background: #f7f8fa; border-right: 1px solid #e8edf3;` → `background: var(--bg-page); border-right: 1px solid var(--border);`。

- [ ] **Step 3: 右栏背景与分隔线令牌化。** 当前 165-174 `.workhub-right` `background: #fff; border-left: 1px solid #e8edf3;` → `background: var(--bg-card); border-left: 1px solid var(--border);`。

- [ ] **Step 4: 构建验证。** `cd web; npm run build`，期望成功。

- [ ] **Step 5: 静态验证。** 运行 `rg -n "#f4f6f8|#f7f8fa|#e8edf3|rgba\(255, 103, 0, 0\.035\)" web/src/views/workhub/index.vue`，期望 0 行。

- [ ] **Step 6: 提交。** `git add web/src/views/workhub/index.vue; git commit -m "阶段3-10：工作台三栏容器背景统一--bg-page/--bg-card去浅橙渐变`<换行>`<换行>`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"`。

---

### Task 11: 逐屏 preview 截图验证 + 运行时令牌验证

**Files:**
- 无文件改动（验证任务；如发现回归再回到对应 Task 修复）

- [ ] **Step 1: 启动 preview。** 用 `preview_start` 启动前端（或确认 dev 服务已在 9000 端口运行）。导航到工作台主页路由（`/` 或 workhub 入口）。

- [ ] **Step 2: 待办 Tab 全屏截图。** `preview_screenshot` 截待办 Tab。核对：(a) 统计栏六色为 --biz-* 业务色非蓝；(b) WorkItemCard 标题 14px/500、摘要灰阶 --text-2、来源标签浅底对比清晰；(c) 更多「...」按钮非 hover 态隐藏，hover 卡片后显现；(d) Tab 徽章：待办=橙(primary)、通知=蓝(info)、全部=蓝(info)；(e) 页底为 --bg-page 浅灰，无浅橙光晕。

- [ ] **Step 3: 通知 Tab 与全部 Tab 截图。** 切到「通知」「全部」Tab 各截图，核对未读底色为 info 浅蓝、未读点为 info 色（非红）、混合列表头像底色为业务色。

- [ ] **Step 4: 右栏默认态截图。** 确保中栏无选中项使右栏显示 QualitySummaryCard + WorkHubRecentVisits。核对：(a) 仓配工作带条纹块已消失；(b) 「最近访问」与「常用功能」均为行式列表样式一致；(c) 质量概览卡背景为纯白非橙渐变、指标色 warning/info/danger；(d) hover 行底色为主色浅底非蓝。

- [ ] **Step 5: 质量告警条截图（若有数据）。** 若 `pendingCount>0`，核对中栏顶部 QualityAlertBar 左边框/图标/数字为 --biz-quality 砖红橙、非旧 #fa541c。

- [ ] **Step 6: 运行时令牌验证（改 --color-primary 看全局变色）。** 用 `preview_eval` 执行 `document.documentElement.style.setProperty('--color-primary', '#0000FF')`，再 `preview_screenshot`。期望：Tab ink-bar、待办徽章、主按钮、选中态边条、新消息横幅、链接 hover 全部变蓝——证明这些处确实走 `var(--color-primary)` 而非硬编码。验证后执行 `document.documentElement.style.removeProperty('--color-primary')` 还原。

- [ ] **Step 7: 全仓静态回归。** 运行 `rg -n "#1677ff|#1890ff|#13c2c2" web/src/views/workhub`，期望 0 行（确认 workhub 目录无残留 Ant 蓝/青）。再运行 `rg -n "#1677ff" web/src --glob '!**/tokens*'`，记录全局剩余命中数作为后续阶段基线（本阶段仅保证 workhub 清零）。

- [ ] **Step 8: 停止 preview。** `preview_stop`。本任务无提交（如 Step 2-6 发现回归，回到对应 Task 修复并重新提交后再复验）。

---

## 概念收敛与口径说明（设计决策备查）

- **统计栏 vs 紧急摘要行 vs 新消息横幅**：三者均在待办 Tab 顶部。统计栏=按来源分类的恒显计数入口（点击筛选）；紧急摘要行=跨来源的紧急/超时聚合（点击设优先级筛选）；新消息横幅=SignalR 实时增量推送（仅 newItemsCount>0 显，点击 flush）。三者语义正交，Task 6 保留全部但统一为同一「提示条」视觉基类（圆角 --radius-lg、间距令牌、危险/主色浅底），消除随机视觉差异即为「收敛」，不删除功能。
- **QualityAlertBar vs QualitySummaryCard**：前者中栏顶部全员可见的紧急告警条（仅有待处理时显）；后者右栏管理角色的质量概览卡（恒显、可跳看板）。二者数据源不同（`getQualityDashboardStats` vs `getWorkHubQualitySummary`），保留并在 Task 9 去色统一为 --biz-quality 砖红橙，口径以注释区分。
- **未读=info 而非 danger**：Task 5/7 将通知未读徽章与未读点由红 `#ff4d4f` 改 `--color-info` 蓝，把红色 danger 语义独占给「急」（紧急工作项/超时），避免红点泛滥稀释真正紧急信号——这是 Tab 徽章优先级分层（急=danger/待办=primary/通知=info）的一致延伸。