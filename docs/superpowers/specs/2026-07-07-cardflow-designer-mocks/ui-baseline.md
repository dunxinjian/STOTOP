# CardFlow 设计器 UI 保真基准表（ui-baseline）

> 真源：`mock-shared.css` + 设计稿 C11（规格与令牌）/ D3（边界态）。
> 用途：每个 UI 任务收尾时 `preview_inspect` 抽查 ≥5 项属性与本表比对，偏差 >1px 即修。
> 色彩规则：mock 具体色值一律按语义映射到项目令牌（表③）；**唯一豁免是色调**，尺寸/字号/间距/圆角/投影逐值照抄。

## ① 组件尺寸表

| 组件 | 类（实现） | 属性 | 期望值 | mock 源 |
|---|---|---|---|---|
| 节点卡片 | `.cfd-node` | width | 340px（画布标准；抽屉内示意 300px 由调用方覆盖） | C11 标注图 |
| | | border-radius | 10px | .fnode |
| | | border | 1px solid 边线令牌 | .fnode |
| | | box-shadow | `--shadow-card`（≈mock 0 2px 8px .09） | .fnode |
| 节点头部 | `.cfd-node__head` | padding | 10px 13px | .fnode .nh |
| | | 总高 | 46px（26 图标+2×10 内距） | C11 |
| | | gap | 9px | .nh |
| 节点图标 | `.cfd-node__icon` | size / radius | 26×26 / 7px | .ni |
| | | font | 13px / 700 | .ni |
| 节点标题 | `.cfd-node__title` | font | 13.5px / 600 | .nt |
| 节点摘要区 | `.cfd-node__body` | padding / font / 行距 | 9px 13px / 12.5px / gap 5px | .nb |
| | 摘要键列 | width | 56px | .nb .kv .kk |
| 连接线 | `.cfd-connector` | w×h | 2×20px | .connector |
| "+" 按钮 | `.cfd-plus` | size / border / font | 26px 圆 / 1.5px / 17px | .plus |
| | margin | 上下 5px | .plus |
| | hover | scale 1.18 + 实心主色（120ms） | C2/D9 |
| 分支列 | `.cfd-branch-col` | max-width / gap | 250px / 20px（列间） | .bcol/.branchcols |
| 分支头 | `.cfd-branch-head` | border-top / radius / padding | 3px 主色顶线 / 9px / 10px 12px | .bhead |
| | 兜底列 | border-top 灰、无删除按钮 | .bhead.default |
| 优先级徽标 | `.cfd-branch-prio` | font / padding / radius | 11px / 0 6px / 4px | .prio |
| 条件文本块 | `.cfd-branch-cond` | font / padding / radius | 12px mono / 6px 9px / 6px | .bhead .cond |
| 抽屉 | AntD Drawer 覆盖 | width | 400px（<1440 屏 360px，D5） | .drawer |
| 抽屉头 | `.cfd-drawer-head` | padding | 13px 18px | .dhd |
| 抽屉 Tab | AntD a-tabs 覆盖 | 项 padding / font | 10px 12px / 13px（选中 600） | .dtabs |
| 抽屉体 | `.cfd-drawer-body` | padding | 16px 18px | .dbd |
| 权限胶囊 | PermissionTri 组件 | 项 padding / font / radius | 2px 9px / 11.5px / 5px | .tri（已实现，组件 scoped） |
| 配置列表 | `.cfd-list` / `.cfd-list__row` | 行 padding / font / radius | 8px 12px / 12.5px / 外框 8px | .fplist/.fprow |
| 选项卡列表 | `.cfd-opts` / `.cfd-opts__item` | 项 padding / font / radius | 10px 13px / 13px / 外框 8px | .optlist/.opt |
| 条件组容器 | `.cfd-cgroup` | border / radius / padding | 1px 主色淡边 / 9px / 12px | .cgroup |
| 或分隔 | `.cfd-orsep` | 徽标 padding / radius | 1px 12px / 12px + 两侧 1px 线 | .orsep/.orbadge |
| 成员胶囊 | `.cfd-member` | padding / radius / 头像 | 3px 12px 3px 4px / 18px / 22px 圆 | .mem/.av |
| 保存胶囊 | SaveStateChip 组件 | padding / font / radius | 2px 10px / 12px / 14px | .chip（已实现） |
| 徽标 tag | 沿用 AntD a-tag 小号 | font / padding | 12px / 1px 8px（radius 4px） | .tag |

## ② 字号阶梯表（设计器域）

| 用途 | font-size | weight | 对应令牌/变量 |
|---|---|---|---|
| 微角标 / 优先级徽标 | 11px | 400/600 | `--font-xs` |
| 权限胶囊 / 锁定说明 | 11.5px | 400/600 | 字面值（阶梯半档，mock 特有） |
| 辅注 / 徽标 / 条件 mono | 12px | 400 | `--font-sm` |
| 正文次级 / 列表行 / 摘要 | 12.5px | 400 | 字面值（半档） |
| 控件文本 / Tab / 菜单 | 13px | 400/600 | `--font-sm2` |
| 节点标题 / 表单值 | 13.5px | 600 | 字面值（半档） |
| 面板标题 | 14px | 600 | `--font-base` |
| 弹层标题 | 14.5px | 600/700 | 字面值（半档） |

> 半档字号（11.5/12.5/13.5/14.5）为 mock 特有阶梯，UI 保真硬约束要求照抄，仅在 `cardflow-designer.scss` 与设计器组件内出现，不外溢到其他模块。字重仅 400/600/700。

## ③ mock 色 → 项目令牌映射表

| mock 值 | 语义 | 项目令牌 |
|---|---|---|
| `#1677ff`（主蓝/审批节点/选中/链接） | 主色 | `var(--color-primary)`（预设派生，橙/板岩蓝） |
| `#e6f0ff` blue-1 | 主色浅底 | `var(--color-primary-light)` |
| `#bcdaff` blue-2 | 主色描边 | `var(--color-primary-border)` |
| `#722ed1` purple（自动节点） | 自动处理 | `var(--color-flow-auto)` / `-light` / `-border` |
| `#fa8c16` orange（抄送节点） | 抄送 | `var(--color-flow-cc)` / `-light` / `-border` |
| `#52c41a` green（结束/命中/成功） | 成功 | `var(--color-success)` / `-light` / `-text` / `-border` |
| `#faad14`/`#fffbe6` gold（警告/兜底提示） | 警告 | `var(--color-warning)` / `-light` / `-text` / `-border` |
| `#ff4d4f`/`#fff1f0` red（错误/敏感/危险） | 危险 | `var(--color-danger)` / `-light` / `-text` / `-border` |
| `#8c8c8c`（发起节点图标/mute 文本） | 三级文本 | `var(--text-3)` |
| `#1f1f1f` ink / `#595959` sub / `#bfbfbf` faint | 文本层级 | `var(--text-1)` / `--text-2` / `--text-disabled` |
| `#f0f0f0` line / `#e8e8e8` line2 | 边线 | `var(--border-faint)` / `var(--border)` |
| `#f0f2f5` bg / `#fff` card | 表面 | `var(--bg-page)` / `var(--bg-card)` |
| `0 2px 8px rgba(0,0,0,.09)` | 卡片投影 | `$shadow-card` |
| `0 6px 22px rgba(0,0,0,.13)` | 弹层投影 | `$shadow-dropdown`（就近归并） |
| `0 10px 26px rgba(0,0,0,.16)` | 拖拽抬升 | `var(--shadow-lift)` |

## 核对流程（每个 UI 任务固定收尾）

1. spec 目录直接浏览器打开对应 mock 屏，与实现页并排；
2. `preview_inspect` 抽查该任务核心元素 ≥5 项属性（width/height/padding/border-radius/font-size/box-shadow）比对本表；
3. 布局结构（栏位/分区/元素顺序）逐块目检一致；偏差 >1px 即修（色调差异豁免）。
