# 保险管理模块 设计文档

## 1. 模块职责与边界

### 核心职责
- 双引擎保险管理（商业保险 + 共保基金）
- 多业务对象关联（三轮车、人员、机动车、财产）— 通过 BusinessType + RelatedObjectId 通用关联
- 保单全生命周期（有效→过期→退保）
- 完整理赔流程（出险登记→理赔申请→审批→赔付）
- 共保基金缴费管理（生成→催缴→确认）
- 自定义审批工作流（模块内部 InsApprovalConfig + InsApprovalRecord + SettlementService 实现多环节配置与流转，不依赖 OA）
- 到期预警

### 不负责的内容
- 具体审批流执行（理赔审批由本模块内部实现，非通用工作流）
- 财务付款结算对接（由 Finance 模块负责）

### 依赖关系
- **Core / Infrastructure** → 基础实体、仓储、组织隔离、权限与多租户
- **Finance** → 理赔拨付的财务对接

## 2. 数据库表设计

### 表清单

| 表名 | 中文说明 | 主键 | 关键字段 |
|------|---------|------|---------|
| INS保险公司 | 保险公司主数据 | FID (BIGINT IDENTITY) | FUID, F组织ID, F公司名称, F公司编码(组织内UNIQUE), F公司类型 |
| INS保单 | 保单核心表 | FID (BIGINT IDENTITY) | FUID, F业务类型, F关联对象ID, F保险大类(1商业/2共保), F保险公司ID(FK), F共保基金ID(FK), F保单号, F保费, F保额, F生效/到期日期, F保险状态 |
| INS出险记录 | 出险事故登记 | FID (BIGINT IDENTITY) | FUID, F保单ID(FK), F出险编号(UNIQUE), F出险日期, F事故类型, F预估/实际损失金额, F责任划分, F出险状态 |
| INS理赔记录 | 理赔流程表 | FID (BIGINT IDENTITY) | FUID, F出险ID(FK), F保单ID(FK), F理赔编号(UNIQUE), F理赔类型, F定损/理赔/自付金额, F理赔状态, F当前环节ID(FK) |
| INS共保基金 | 共保基金主表 | FID (BIGINT IDENTITY) | FUID, F组织ID, F基金名称, F基金编码(组织内UNIQUE), F累计缴费/赔付/余额, F缴费标准, F免赔额, F赔付上限 |
| INS共保基金缴费 | 基金缴费明细 | FID (BIGINT IDENTITY) | FUID, F基金ID(FK), F保单ID(FK), F缴费编号, F缴费金额, F缴费周期, F缴费状态 |
| INS理赔审批配置 | 审批环节配置 | FID (BIGINT IDENTITY) | FUID, F组织ID, F环节序号(组织内UNIQUE), F环节名称, F环节编码, F审批人类型, F可驳回 |
| INS理赔审批记录 | 审批流水记录 | FID (BIGINT IDENTITY) | FUID, F理赔ID(FK), F环节配置ID(FK), F审批人ID, F审批动作, F审批意见 |

### ER关系

```mermaid
erDiagram
    INS保险公司 ||--o{ INS保单 : "承保"
    INS共保基金 ||--o{ INS保单 : "参保"
    INS保单 ||--o{ INS出险记录 : "出险"
    INS出险记录 ||--o{ INS理赔记录 : "理赔"
    INS保单 ||--o{ INS理赔记录 : "保单理赔"
    INS共保基金 ||--o{ INS共保基金缴费 : "缴费"
    INS理赔审批配置 ||--o{ INS理赔记录 : "当前环节"
    INS理赔审批配置 ||--o{ INS理赔审批记录 : "环节配置"
    INS理赔记录 ||--o{ INS理赔审批记录 : "审批流水"
```

## 3. API 接口清单

### 保单管理 (InsPolicyController)

| 方法 | 路径 | 功能 |
|------|------|------|
| GET | /api/insurance/policies | 保单列表（分页） |
| GET | /api/insurance/policies/{id} | 保单详情 |
| POST | /api/insurance/policies | 创建保单 |
| PUT | /api/insurance/policies/{id} | 更新保单 |
| GET | /api/insurance/policies/expiring | 即将到期保单（默认30天） |
| GET | /api/insurance/policies/by-object | 按业务对象查保单 |

### 出险管理 (InsClaimController)

| 方法 | 路径 | 功能 |
|------|------|------|
| GET | /api/insurance/claims | 出险记录列表 |
| GET | /api/insurance/claims/{id} | 出险记录详情 |
| POST | /api/insurance/claims | 创建出险记录 |
| PUT | /api/insurance/claims/{id} | 更新出险记录 |
| PUT | /api/insurance/claims/{id}/close | 结案 |

### 保险公司 (InsCompanyController)

| 方法 | 路径 | 功能 |
|------|------|------|
| GET | /api/insurance/companies | 保险公司列表 |
| GET | /api/insurance/companies/{id} | 保险公司详情 |
| POST | /api/insurance/companies | 创建保险公司 |
| PUT | /api/insurance/companies/{id} | 更新保险公司 |
| PUT | /api/insurance/companies/{id}/status | 切换启用/停用状态 |

### 共保基金 (InsCoInsuranceFundController)

| 方法 | 路径 | 功能 |
|------|------|------|
| GET | /api/insurance/funds | 基金列表 |
| GET | /api/insurance/funds/{id} | 基金详情 |
| POST | /api/insurance/funds | 创建基金 |
| PUT | /api/insurance/funds/{id} | 更新基金 |
| POST | /api/insurance/funds/{id}/contributions/generate | 生成缴费记录 |
| PUT | /api/insurance/funds/contributions/{id}/confirm | 确认缴费 |
| GET | /api/insurance/funds/{fundId}/contributions | 缴费记录列表 |

### 审批配置 (InsApprovalConfigController)

| 方法 | 路径 | 功能 |
|------|------|------|
| GET | /api/insurance/approval-config | 审批环节配置列表 |
| POST | /api/insurance/approval-config | 创建审批环节 |
| PUT | /api/insurance/approval-config/{id} | 更新审批环节 |
| PUT | /api/insurance/approval-config/{id}/status | 切换启用/停用状态 |
| PUT | /api/insurance/approval-config/reorder | 调整审批环节排序 |

### 理赔结算 (InsSettlementController)

| 方法 | 路径 | 功能 |
|------|------|------|
| GET | /api/insurance/settlements | 理赔记录列表（分页） |
| GET | /api/insurance/settlements/{id} | 理赔详情 |
| POST | /api/insurance/settlements | 创建理赔记录 |
| PUT | /api/insurance/settlements/{id} | 更新理赔记录 |
| PUT | /api/insurance/settlements/{id}/submit | 提交审批 |
| PUT | /api/insurance/settlements/{id}/review | 审核（按环节通过/驳回） |
| PUT | /api/insurance/settlements/{id}/approve | 审批通过 |
| PUT | /api/insurance/settlements/{id}/pay | 拨付 |
| GET | /api/insurance/settlements/pending-my | 待我审批列表 |
| GET | /api/insurance/settlements/{id}/approval-history | 审批历史 |

### 报表 (InsReportController)

| 方法 | 路径 | 功能 |
|------|------|------|
| GET | /api/insurance/reports/premium-summary | 保费汇总报表 |
| GET | /api/insurance/reports/claim-analysis | 出险分析报表 |
| GET | /api/insurance/reports/settlement-analysis | 赔付分析报表 |
| GET | /api/insurance/reports/fund-balance | 基金收支报表 |
| GET | /api/insurance/reports/overview | 综合看板 |

## 4. 业务流程

### 理赔审批流程

```mermaid
stateDiagram-v2
    [*] --> 已登记: 出险报案
    已登记 --> 处理中: 开始处理
    处理中 --> 理赔中: 发起理赔
    理赔中 --> 审批中: 提交审批
    审批中 --> 已赔付: 审批通过+拨付
    审批中 --> 已驳回: 审批驳回
    已驳回 --> 审批中: 重新提交
    已赔付 --> 已结案: 结案
    处理中 --> 已关闭: 无需理赔/关闭
```

### 共保基金缴费流程

```mermaid
flowchart TD
    A[管理员创建共保基金] --> B[设定缴费标准/周期]
    B --> C[生成缴费记录 POST funds/{id}/contributions/generate]
    C --> D[待缴费状态]
    D --> E{按期缴费?}
    E -->|是| F[确认缴费 PUT contributions/{id}/confirm]
    F --> G[更新基金余额]
    E -->|否| H[逾期标记]
    H --> I[催缴通知]
```
