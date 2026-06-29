# Express 快递业务模块设计文档

## 1. 模块职责与边界

### 1.1 核心职责

- **计费管理**：基于多层级报价体系，对出港运单执行自动化价格计算与成本核算
- **报价管理**：维护品牌×省份×重量段的报价矩阵，支持加收、佣金、共享别名等扩展配置
- **账单管理**：按批次/周期聚合计费结果，生成账单并驱动审核→确认→收款全生命周期
- **店铺与网点管理**：管理快递业务基础组织结构（品牌、店铺、网点、承包区、末端驿站）
- **运单号管理**：号段分配、客户余额、交易流水
- **政策返利**：阶梯返利规则配置与周期结算
- **预付款**：客户预付款余额管理与流水记录
- **报表分析**：利润、重量、流量多维分析

### 1.2 不负责的内容（明确边界）

| 边界外内容 | 归属模块 |
|---|---|
| 原始数据文件解析、暂存表清洗管道框架 | CardFlow |
| 财务凭证生成与会计账务 | Finance |
| 用户权限、角色、菜单管理 | System |
| 仓储基类、审计字段、多租户 | Core |
| CRM 客户主数据维护 | CRM |

### 1.3 与其他模块的依赖关系

```mermaid
graph LR
    Express -->|导入管道框架| CardFlow
    Express -->|凭证生成| Finance
    Express -->|权限校验| System
    Express -->|仓储/审计基类| Core
    CardFlow -->|STG暂存数据| Express
```

- **CardFlow**：Express 复用其导入管道框架（FileType注册、批次管理、进度通知），STG暂存表由CardFlow管道写入，Express的Agent从中读取（`STOTOP.Module.Express.csproj` 引用 `STOTOP.Module.CardFlow`）
- **Finance**：账单确认后可触发凭证生成
- **System**：接口鉴权与菜单权限
- **Core**：Entity基类、IRepository<T>、UnitOfWork

---

## 2. 数据库表设计

### 2.1 核心实体表清单

#### 基础数据（8张）

| 表名 | 说明 | 主键 | 关键字段 |
|---|---|---|---|
| EXP_Brand | 快递品牌 | GUID | Code, Name, IsActive |
| EXP_Shop | 店铺 | GUID | BrandId, Name, Code, ClientId, Status |
| EXP_Province | 省份（34个） | BIGINT | Name, Code, RegionGroup |
| EXP_Agent | 业务代理 | GUID | Name, Code, ShopId |
| EXP_NetworkPoint | 快递网点 | GUID | Name, Code, BrandId, Province |
| EXP_FranchiseArea | 承包区 | GUID | Name, NetworkPointId |
| EXP_LastMileStation | 末端驿站 | GUID | Name, NetworkPointId |
| EXP_Salesman | 业务员 | GUID | Name, Code, ShopId |

#### 报价体系（9张）

> 报价矩阵（重量段×省份×首续价）不落明细表，整体序列化存于 `EXP_Quotation.FMatrixJson`，因此无独立的"重量段定义""报价矩阵明细"表。

| 表名 | 说明 | 主键 | 关键字段 |
|---|---|---|---|
| EXP_Quotation | 快递报价（聚合根，含 FMatrixJson 矩阵） | BIGINT | BrandCode, PlanName, EffectiveDate, Status, MatrixJson |
| EXP_VolumeTier（EXP发件量阶梯） | 发件量阶梯 | BIGINT | ClientId, MinMonthlyVolume, QuotationId |
| EXP_QuotationSurcharge | 出港加收 | BIGINT | QuotationId, Name, Type |
| EXP_QuotationSurchargeConfig | 出港加收配置项 | BIGINT | SurchargeId, Key, Value |
| EXP_QuotationSurchargeDest | 出港加收目的地 | BIGINT | SurchargeId, ProvinceId |
| EXP_QuotationShop | 报价关联店铺 | BIGINT | QuotationId, ShopId |
| EXP_QuotationAlias | 报价共享别名 | BIGINT | QuotationId, AliasName |
| EXP_QuotationSurchargeLink | 加收关联（多对多） | BIGINT | QuotationId, SurchargeId |
| EXP_QuotationCommission | 佣金配置 | BIGINT | QuotationId, Role, Rate |

#### 成本体系（3张）

| 表名 | 说明 | 主键 | 关键字段 |
|---|---|---|---|
| EXP_CostItem | 成本项目（9种标准项） | BIGINT | Code, Name, Category |
| EXP_CostPlan | 成本方案 | BIGINT | Name, BrandId, EffectiveDate |
| EXP_CostDetail | 成本方案明细 | BIGINT | CostPlanId, CostItemId, ProvinceId, Price |

#### 运单与计费（5张）

| 表名 | 说明 | 主键 | 关键字段 |
|---|---|---|---|
| EXP_Waybill | 出港运单基础信息 | BIGINT | WaybillNo, ShopId, ProvinceId, Weight, BatchId |
| EXP_BillingResult | 出港运单计费结果 | BIGINT | WaybillId, PartyRole(1应收/2层级/3佣金), Amount |
| EXP_BillingResultDetail | 计费结果明细 | BIGINT | BillingResultId, CostItemId, Amount |
| EXP_WaybillHistory | 出港运单历史 | BIGINT | WaybillId, OperationType, Timestamp |
| EXP_BillingResultHistory | 计费结果历史 | BIGINT | BillingResultId, OperationType, Timestamp |

#### 账单与预付款（6张）

| 表名 | 说明 | 主键 | 关键字段 |
|---|---|---|---|
| EXP_Invoice | 出港账单 | GUID | ShopId, Period, Status, TotalAmount, TotalCost, TotalProfit |
| EXP_InvoiceReviewRule | 账单审核规则 | BIGINT | RuleName, Condition, AutoPass |
| EXP_InvoiceReviewLog | 账单审核日志 | BIGINT | InvoiceId, ReviewStatus, Reviewer, Remark |
| EXP_Prepayment | 预付款记录 | BIGINT | ShopId, Amount, Date |
| EXP_PrepaymentBalance | 预付款余额 | BIGINT | ShopId, Balance |
| EXP_PrepaymentTransaction | 预付款流水 | BIGINT | BalanceId, Type, Amount, Timestamp |

#### 政策返利（6张）

| 表名 | 说明 | 主键 | 关键字段 |
|---|---|---|---|
| EXP_PolicyRebate | 政策返利 | BIGINT | Name, BrandId, Period, Status |
| EXP_PolicyRebateStep | 返利阶梯 | BIGINT | PolicyRebateId, MinVolume, MaxVolume, Rate |
| EXP_PolicyRebateReward | 奖罚规则 | BIGINT | PolicyRebateId, Type, Condition, Amount |
| EXP_PolicyRebateCondition | 规则条件 | BIGINT | PolicyRebateId, Field, Operator, Value |
| EXP_PolicyRebateSettlement | 返利结算 | BIGINT | PolicyRebateId, ShopId, Period, SettledAmount |
| EXP_PolicyRebateSettlementDetail | 结算明细 | BIGINT | SettlementId, WaybillId, RebateAmount |

#### 运单号管理（3张）

| 表名 | 说明 | 主键 | 关键字段 |
|---|---|---|---|
| EXP_WaybillNumberPool | 运单号段 | BIGINT | BrandId, StartNo, EndNo, AllocatedCount |
| EXP_WaybillNumberTransaction | 运单号交易 | BIGINT | PoolId, ShopId, Quantity, Date |
| EXP_CustomerWaybillBalance | 客户运单号余额 | BIGINT | ShopId, BrandId, Balance |

#### 特殊配置（6张）

| 表名 | 说明 | 主键 | 关键字段 |
|---|---|---|---|
| EXP_CustomerRebate | 客户返利 | BIGINT | ShopId, Period, Rate |
| EXP_CustomerRebateStep | 客户返利阶梯 | BIGINT | CustomerRebateId, MinVolume, Rate |
| EXP_AvgWeightLimit | 均重上限 | BIGINT | ShopId, ProvinceId, MaxAvgWeight |
| EXP_DestinationRatio | 目的地占比 | BIGINT | ShopId, ProvinceId, Ratio |
| EXP_FeeReduction | 费用减免 | BIGINT | ShopId, CostItemId, ReductionType, Value |
| EXP_MonthlyAdjustment | 月度调整 | BIGINT | ShopId, Period, AdjustType, Amount |

#### 暂存表

| 表名 | 说明 | 主键 | 关键字段 |
|---|---|---|---|
| STG_STO_Outbound_TC | 申通出港数据(太仓) | BIGINT | BatchId, WaybillNo, F计算状态 |
| STG_JT_Transaction_TC | 极兔交易明细(太仓) | BIGINT | BatchId, WaybillNo, F计算状态 |

### 2.2 关键字段说明

- **PartyRole（参与方角色）**：1=应收（客户层）、2=层级应收（代理/网代等中间层）、3=佣金
- **F计算状态**：0=待计算、1=计算成功、2=计算失败待重试、3=需重算
- **Status（账单状态）**：未确认→待审核→已确认→已发送→已收款→已归档
- **ReviewStatus（审核状态）**：0=待审核、1=自动通过、2=人工通过、3=人工驳回、4=反审核

### 2.3 表间关系（ER图）

```mermaid
erDiagram
    EXP_Brand ||--o{ EXP_Shop : "品牌→店铺"
    EXP_Shop ||--o{ EXP_Waybill : "店铺→运单"
    EXP_Shop ||--o{ EXP_Invoice : "店铺→账单"
    EXP_Shop ||--o{ EXP_PrepaymentBalance : "店铺→预付余额"

    EXP_Quotation ||--o{ EXP_QuotationSurcharge : "报价→加收"
    EXP_Quotation ||--o{ EXP_QuotationShop : "报价→关联店铺"
    EXP_Quotation ||--o{ EXP_QuotationCommission : "报价→佣金"

    EXP_QuotationSurcharge ||--o{ EXP_QuotationSurchargeConfig : "加收→配置项"
    EXP_QuotationSurcharge ||--o{ EXP_QuotationSurchargeDest : "加收→目的地"

    EXP_Waybill ||--o{ EXP_BillingResult : "运单→计费结果"
    EXP_BillingResult ||--o{ EXP_BillingResultDetail : "计费结果→明细"

    EXP_Invoice ||--o{ EXP_InvoiceReviewLog : "账单→审核日志"

    EXP_CostPlan ||--o{ EXP_CostDetail : "成本方案→明细"
    EXP_CostItem ||--o{ EXP_CostDetail : "成本项目→明细"

    EXP_PolicyRebate ||--o{ EXP_PolicyRebateStep : "返利→阶梯"
    EXP_PolicyRebate ||--o{ EXP_PolicyRebateSettlement : "返利→结算"
    EXP_PolicyRebateSettlement ||--o{ EXP_PolicyRebateSettlementDetail : "结算→明细"
```

---

## 3. API 接口清单

### 3.1 计费管理

| 方法 | 路径 | 功能 |
|---|---|---|
| POST | /api/express/billing/execute | 执行计费（触发PricingAgent/CostAgent） |
| GET | /api/express/billing/results | 分页查询计费结果列表 |
| GET | /api/express/billing/results/{id} | 计费结果详情 |
| GET | /api/express/billing/results/by-waybill/{waybillId} | 按运单查询全部计费结果 |
| GET | /api/express/billing/errors | 异常运单统计（按异常类型聚合） |
| GET | /api/express/billing/errors/detail | 某类异常的运单明细（分页） |
| POST | /api/express/billing/retry | 触发异常运单重算 |

### 3.2 报价管理

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/quotations | 报价方案列表 |
| GET | /api/express/quotations/{id} | 报价方案详情 |
| POST | /api/express/quotations | 新建报价方案 |
| PUT | /api/express/quotations/{id} | 更新报价方案 |
| DELETE | /api/express/quotations/{id} | 删除报价方案 |
| POST | /api/express/quotations/{id}/copy | 复制报价方案 |
| GET | /api/express/quotations/template | 下载导入模板 |
| POST | /api/express/quotations/import | 导入报价数据 |
| GET | /api/express/quotations/by-shop | 按店铺名称查询关联报价（按业务对象分组） |
| GET | /api/express/quotations/{id}/shops | 报价关联店铺列表 |
| POST | /api/express/quotations/{id}/shops | 为报价添加关联店铺 |
| DELETE | /api/express/quotations/{id}/shops/{shopId} | 移除报价关联店铺 |
| POST | /api/express/quotations/{id}/shops/check-conflicts | 检查店铺关联冲突 |
| GET | /api/express/quotations/{id}/commissions | 报价佣金配置列表 |
| POST | /api/express/quotations/{id}/commissions | 保存佣金配置（新增或更新） |
| DELETE | /api/express/quotations/commissions/{commissionId} | 删除佣金配置 |
| GET | /api/express/quotations/{id}/change-logs | 报价变更日志 |
| GET | /api/express/quotations/{id}/aliases | 报价共享别名列表 |
| POST | /api/express/quotations/{id}/aliases | 添加报价共享别名 |
| DELETE | /api/express/quotations/aliases/{aliasId} | 删除报价共享别名 |

### 3.3 账单管理

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/invoice | 账单列表 |
| GET | /api/express/invoice/{id} | 账单详情 |
| POST | /api/express/invoice/generate | 手动生成账单 |
| PUT | /api/express/invoice/{id}/confirm | 确认账单 |
| PUT | /api/express/invoice/{id}/send | 发送账单 |
| PUT | /api/express/invoice/{id}/payment | 收款 |
| POST | /api/express/invoice/auto-generate | 触发自动出账任务 |

### 3.4 账单审核（挂在 invoice 控制器下）

| 方法 | 路径 | 功能 |
|---|---|---|
| POST | /api/express/invoice/{id}/review | 人工审核 |
| POST | /api/express/invoice/{id}/reverse-review | 反审核 |
| GET | /api/express/invoice/review-rules | 审核规则列表 |
| POST | /api/express/invoice/review-rules | 创建审核规则 |
| PUT | /api/express/invoice/review-rules/{id} | 更新审核规则 |
| DELETE | /api/express/invoice/review-rules/{id} | 删除审核规则 |

#### 账单对账（挂在 invoice 控制器下）

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/invoice/{id}/reconciliation | 对账详情 |
| POST | /api/express/invoice/{id}/reconciliation/confirm | 确认对账 |
| POST | /api/express/invoice/{id}/reconciliation/dispute | 提起对账异议 |
| POST | /api/express/invoice/{id}/reconciliation/dispute/resolve | 处理对账异议 |
| GET | /api/express/invoice/{id}/reconciliation/export | 导出对账明细（Excel） |

### 3.5 店铺管理

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/shops | 店铺列表 |
| GET | /api/express/shops/{name} | 店铺详情 |
| POST | /api/express/shops | 新建店铺 |
| PUT | /api/express/shops/{name} | 更新店铺 |
| DELETE | /api/express/shops/{name} | 删除店铺 |
| POST | /api/express/shops/assignments | 添加店铺归属 |
| DELETE | /api/express/shops/assignments/{assignmentId} | 删除店铺归属 |

> 待配置店铺列表与一键完成配置在质量中心（见 3.14：`quality-center/pending-shops` 与 `quality-center/pending-shops/complete`）。

### 3.6 网点管理

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/network-points | 网点列表 |
| GET | /api/express/network-points/{id} | 网点详情 |
| GET | /api/express/network-points/check-code | 校验网点编号是否存在 |
| POST | /api/express/network-points | 新建网点 |
| PUT | /api/express/network-points/{id} | 更新网点 |
| DELETE | /api/express/network-points/{id} | 删除网点 |

### 3.7 运单管理

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/waybills | 运单列表 |
| GET | /api/express/waybills/{id} | 运单详情 |
| GET | /api/express/waybills/by-no | 按运单号+品牌查询 |
| POST | /api/express/waybills/import | 导入运单 |
| POST | /api/express/waybills/discover-shops | 手动触发新店铺发现 |
| POST | /api/express/archive/execute | 手动触发归档 |
| GET | /api/express/archive/stats | 归档统计 |

### 3.8 运单号管理

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/waybill-number/pool | 号段列表 |
| POST | /api/express/waybill-number/pool | 创建号段 |
| POST | /api/express/waybill-number/allocate | 分配运单号 |
| POST | /api/express/waybill-number/return | 回收号段 |
| GET | /api/express/waybill-number/balance/{clientId} | 客户余额查询 |

### 3.9 附加费管理

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/price-surcharges | 附加费列表 |
| GET | /api/express/price-surcharges/{id} | 附加费详情 |
| POST | /api/express/price-surcharges | 新建附加费 |
| PUT | /api/express/price-surcharges/{id} | 更新附加费 |
| DELETE | /api/express/price-surcharges/{id} | 删除附加费 |
| PUT | /api/express/price-surcharges/{id}/toggle-active | 启用/停用切换 |

### 3.10 成本方案

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/cost-plans | 成本方案列表 |
| GET | /api/express/cost-plans/{id} | 成本方案详情 |
| POST | /api/express/cost-plans | 新建成本方案 |
| PUT | /api/express/cost-plans/{id} | 更新成本方案 |
| PUT | /api/express/cost-plans/{id}/activate | 启用方案 |
| PUT | /api/express/cost-plans/{id}/deactivate | 停用方案 |
| DELETE | /api/express/cost-plans/{id} | 删除成本方案 |
| GET | /api/express/cost-items | 成本项目主数据列表（9种标准项） |
| POST | /api/express/cost-items | 新建成本项目 |
| PUT | /api/express/cost-items/{id} | 更新成本项目 |

> 成本项、应用网点、关联店铺、时间段、矩阵、互斥配置等子资源均挂在 `cost-plans/{planId}/...` 下；另有 `GET /api/express/cost-plans/effective-cost` 计算运单有效成本、`GET /api/express/cities` 城市查询。

### 3.11 政策返利

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/policy-rebate | 返利政策列表 |
| GET | /api/express/policy-rebate/{id} | 返利政策详情 |
| POST | /api/express/policy-rebate | 新建返利政策 |
| PUT | /api/express/policy-rebate/{id} | 更新返利政策 |
| DELETE | /api/express/policy-rebate/{id} | 删除返利政策 |
| PUT | /api/express/policy-rebate/{id}/enable | 启用 |
| PUT | /api/express/policy-rebate/{id}/disable | 停用 |
| GET | /api/express/policy-rebate-settlement | 结算列表（分页） |
| GET | /api/express/policy-rebate-settlement/{id} | 结算详情 |
| POST | /api/express/policy-rebate-settlement/execute | 执行返利结算 |
| PUT | /api/express/policy-rebate-settlement/{id}/confirm | 确认结算 |
| PUT | /api/express/policy-rebate-settlement/{id}/write-off | 核销结算 |
| POST | /api/express/policy-rebate-settlement/simulate | 结算试算 |

### 3.12 预付款

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/prepayment/balance/{clientId} | 预付款余额查询 |
| GET | /api/express/prepayment/transaction | 流水记录查询 |
| POST | /api/express/prepayment/recharge | 充值 |

### 3.13 报表分析

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/report/flow-distribution | 流量分布 |
| GET | /api/express/report/flow-trend | 流量趋势 |
| GET | /api/express/report/weight-distribution | 重量段分布 |
| GET | /api/express/report/weight-trend | 均重趋势 |
| GET | /api/express/report/profit-by-client | 按客户毛利 |
| GET | /api/express/report/profit-by-shop | 按店铺毛利 |
| GET | /api/express/report/profit-trend | 毛利趋势 |
| GET | /api/express/report/profit-by-intermediary | 中间人视角毛利 |
| GET | /api/express/report/profit-by-salesman | 业务员提成视角毛利 |
| GET | /api/express/report/profit-by-weight-segment | 按重量段毛利 |
| GET | /api/express/report/profit-by-region | 按大区流量损益 |
| GET | /api/express/report/profit-by-province | 按省份流量损益 |
| GET | /api/express/report/filter-options | 筛选下拉选项 |
| GET | /api/express/report/dashboard | 综合看板 |

### 3.14 数据质量中心

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/quality-center/overview | 质量中心总览 |
| GET | /api/express/quality-center/pending-shops | 待配置店铺列表 |
| POST | /api/express/quality-center/pending-shops/complete | 一键完成店铺归属+报价校验 |
| GET | /api/express/quality-center/empty-shop-rows | 空店铺账号运单列表 |
| POST | /api/express/quality-center/empty-shop-rows/fill | 批量补填空店铺账号 |
| POST | /api/express/quality-center/empty-shop-rows/ignore | 批量忽略空店铺账号 |
| POST | /api/express/quality-center/rerun-billing | 重新计费 |
| GET | /api/express/quality-center/unrecognized-network-points | 未识别网点列表 |
| POST | /api/express/quality-center/associate-network-point | 关联网点 |
| POST | /api/express/quality-center/ignore-network-point-errors | 批量忽略网点错误 |
| GET | /api/express/quality-center/network-point-mismatches | 网点不一致记录 |
| POST | /api/express/quality-center/network-point-mismatches/ignore | 批量忽略网点不一致 |

### 3.15 基础数据

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/provinces | 省份列表（下拉用） |
| GET | /api/express/provinces/{id} | 省份详情 |
| POST | /api/express/provinces | 新建省份 |
| PUT | /api/express/provinces/{id} | 更新省份 |
| DELETE | /api/express/provinces/{id} | 删除省份 |
| GET | /api/express/provinces/regions | 大区名称列表 |
| PUT | /api/express/provinces/regions/rename | 重命名大区 |
| GET | /api/express/brands | 品牌列表 |
| GET | /api/express/brands/{code} | 品牌详情 |
| POST | /api/express/brands | 新建品牌 |
| PUT | /api/express/brands/{code} | 更新品牌 |
| DELETE | /api/express/brands/{code} | 删除品牌 |
| GET | /api/express/client-quotation-summary | 业务对象统一查询聚合（含报价数量统计） |
| GET | /api/express/salesmen | 业务员列表 |
| GET | /api/express/salesmen/{no} | 业务员详情 |
| POST | /api/express/salesmen | 新建业务员 |
| PUT | /api/express/salesmen/{no} | 更新业务员 |
| DELETE | /api/express/salesmen/{no} | 删除业务员 |
| GET | /api/express/salesmen/candidates | 可选 HR 员工候选人列表 |
| GET | /api/express/last-mile-stations | 末端驿站列表 |
| GET | /api/express/last-mile-stations/{code} | 末端驿站详情 |
| POST | /api/express/last-mile-stations | 新建末端驿站 |
| PUT | /api/express/last-mile-stations/{code} | 更新末端驿站 |
| DELETE | /api/express/last-mile-stations/{code} | 删除末端驿站 |
| GET | /api/express/franchise-areas | 承包区列表 |
| GET | /api/express/franchise-areas/{code} | 承包区详情 |
| GET | /api/express/franchise-areas/check-code | 校验承包区编号是否存在 |
| POST | /api/express/franchise-areas | 新建承包区 |
| PUT | /api/express/franchise-areas/{code} | 更新承包区 |
| DELETE | /api/express/franchise-areas/{code} | 删除承包区 |
| GET | /api/express/agents | 业务代理列表 |
| GET | /api/express/agents/{id} | 业务代理详情 |
| POST | /api/express/agents | 新建业务代理 |
| PUT | /api/express/agents/{id} | 更新业务代理 |
| DELETE | /api/express/agents/{id} | 删除业务代理 |

### 3.16 名称映射（脏数据归一）

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/network-point-aliases | 网点名称映射列表（分页） |
| POST | /api/express/network-point-aliases | 新增网点名称映射 |
| POST | /api/express/network-point-aliases/batch | 批量新增网点名称映射 |
| DELETE | /api/express/network-point-aliases/{id} | 删除网点名称映射 |
| GET | /api/express/salesman-aliases | 业务员名称映射列表（分页） |
| POST | /api/express/salesman-aliases | 新增业务员名称映射（源脏名→员工工号） |
| POST | /api/express/salesman-aliases/batch | 批量新增业务员名称映射 |
| DELETE | /api/express/salesman-aliases/{id} | 删除业务员名称映射 |

### 3.17 用户网点权限

| 方法 | 路径 | 功能 |
|---|---|---|
| GET | /api/express/network-permissions | 用户网点权限列表（分页） |
| GET | /api/express/network-permissions/{id} | 网点权限详情 |
| POST | /api/express/network-permissions | 创建用户网点权限 |
| DELETE | /api/express/network-permissions/{id} | 删除用户网点权限 |

---

## 4. 业务流程

### 4.1 计费Agent流程

#### PricingAgent（价格计算）

```mermaid
flowchart TD
    A[读取规则配置] --> B[验证配置完整性]
    B --> C[从STG表读取批次数据]
    C --> D{店铺预扫描}
    D -->|空店铺| E[标记status=3]
    D -->|新店铺| F[自动建档]
    D -->|待配置| G[跳过]
    D -->|正常| H[省份名称→ID转换]
    H --> I[幂等清除旧记录]
    I --> J[调用PricingEngine计算]
    J --> K[输出统计结果]
    K --> L[SuccessCount / ErrorCount / SkippedCount / PendingManualCount]
```

#### CostAgent（成本计算）

```mermaid
flowchart TD
    A[读取规则配置] --> B[从计费结果表读取已成功运单]
    B --> C[筛选PartyRole=1的记录]
    C --> D[加载CostPlanCache]
    D --> E[并行计算成本 Parallel.ForEach]
    E --> F[查询BillingResultId映射]
    F --> G[构建成本明细对象]
    G --> H[批量写入成本明细表-事务]
```

#### Agent协作关系

```mermaid
flowchart LR
    STG[STG暂存表] --> PA[PricingAgent]
    PA --> BR[EXP_BillingResult]
    BR --> CA[CostAgent]
    CA --> CD[EXP_CostDetail]
    PA -.->|独立并行| CA
```

> PricingAgent 和 CostAgent 完全独立，互不依赖，可并行执行。

### 4.2 数据导入管道

```mermaid
flowchart TD
    A[用户上传Excel/CSV] --> B[CardFlow管道接收]
    B --> C[Parser解析原始数据]
    C --> D[写入STG暂存表]
    D --> E[标记F计算状态=0]
    E --> F[触发计费Agent]
    F --> G{计算结果}
    G -->|成功| H[F计算状态=1]
    G -->|失败| I[F计算状态=2]
    G -->|需重算| J[F计算状态=3]
```

### 4.3 账单生命周期

```mermaid
stateDiagram-v2
    [*] --> 未确认
    未确认 --> 待审核 : 提交审核
    待审核 --> 已确认 : 自动通过/人工通过
    待审核 --> 人工审核中 : 触发人工审核
    人工审核中 --> 已确认 : 人工通过
    人工审核中 --> 未确认 : 人工驳回
    已确认 --> 未确认 : 反审核
    已确认 --> 已发送 : 发送账单
    已发送 --> 已收款 : 确认收款
    已收款 --> 已归档 : 归档
```

**账单聚合字段**：总单量、总重量、平均重量、总应收、总成本、总利润、均重追补、占比追补

### 4.4 店铺配置流程

```mermaid
flowchart TD
    A[PricingAgent发现新店铺] --> B[自动建档到EXP_Shop]
    B --> C[标记为待配置]
    C --> D[出现在质量中心-待处理店铺]
    D --> E[运营人员分配配置]
    E --> F{配置内容}
    F --> G[关联报价方案]
    F --> H[设置均重上限]
    F --> I[设置目的地占比]
    F --> J[设置费用减免]
    G --> K[店铺状态→正常]
    K --> L[后续运单正常计费]
```

### 4.5 业务对象层级链

```mermaid
flowchart LR
    KH[客户KH] --> DL[代理DL]
    DL --> WD[网代WD]
    WD --> YW[业务YW]
    YW --> CB[分部CB]
    CB --> YZ[营运YZ]
```

每层可独立配置：报价方案、均重上限、目的地占比、返利政策。

计费结果按 **PartyRole** 为每层生成独立记录：
- PartyRole=1：应收（客户层）
- PartyRole=2：层级应收（代理/网代等中间层）
- PartyRole=3：佣金
