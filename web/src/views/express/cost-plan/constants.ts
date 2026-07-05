// 成本方案共享常量。

/**
 * 结算重量环节选项（成本项按此环节取计费重量）。
 * 单一真源：CostPlanEdit 与 CostItemToolbar 均从此导入，避免两处硬编码漂移。
 */
export const WEIGHT_STAGE_OPTIONS: { value: number; label: string }[] = [
  { value: 1, label: '揽收称重' },
  { value: 2, label: '揽收体积重' },
  { value: 3, label: '中心操作称重' },
  { value: 4, label: '中心操作体积重' },
  { value: 5, label: '目的操作称重' },
  { value: 6, label: '目的操作体积重' },
]
