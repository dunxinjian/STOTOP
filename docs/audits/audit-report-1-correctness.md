# CardFlow 卡片流程 11 维度审计报告

## 一句话总体结论

CardFlow 主链在「点亮费用报销流」后存在 **一组真实可触发的 P0/P1 财务数据完整性与组织隔离缺陷**：跨批次去重因 Excel 列名 vs DB 列名错配 + STG 表无唯一约束而对所有种子规则静默失效，重复财务行可直入 STG 并被下游计费/凭证重复计数；凭证创建无事务、去重键不含规则组/不排除红冲、自动凭证桥两条撤销路径口径不一；模板克隆/发现链在组织隔离上被多处打穿（信任客户端 OrgId、FillOrgId 覆盖、GetTemplates 不限 FOrgId=0）；CfStaging/CfBatchRow/CardFlowBatchController 三处缺组织校验或权限网关导致跨组织读写与无门禁影子入口。审批引擎侧 orsign 单人拒绝整卡退回、countersign 转办后永久卡死、组织链/角色路由运行期恒空属功能正确性破坏。脱敏对已完成卡片整体失效属可达的敏感字段泄露。

## 缺陷分级计数（去重后 35 条）

- **P0：2 条**（去重列名错配致重复财务行入库 / STG 无唯一约束兜底不可达 —— 同一缺陷的两层，合并表述）
- **P1：13 条**
- **P2：14 条**
- **P3：6 条**
- 其中标记为「已知有意推迟（isKnownDeferred=true）但带新增具体后果」：4 条（委托未接通、CfImport 撤销不红冲、SignalR 双 Hub、编排引擎 API 未隔离、桶B 设计器入口、移动端源上下文契约死文件——见专节）

---

## 一、P0 / P1 必修

### 1. 跨批次去重对所有种子规则静默失效，重复财务行直入 STG（含无唯一约束兜底）【P0｜data-integrity｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/AutoPlugin/Implementations/ExcelInputPlugin.cs:469`（主路径）；`src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs:571`（STG_出港运费 无 UNIQUE 索引）；兜底入口 `ExcelInputPlugin.cs:611`
- **问题**：`crossBatchDedupFields` 在种子里配的是 Excel 列名（"运单号"/"审批编号"），而 DataTable 只用 `DbColumn`（F运单号/F审批编号）建列。`dt.Columns.Contains(field)` 恒 false → `crossBatchDedupActive=false`，整段跨批次去重被静默禁用（仅一条 warning）。叠加 STG_出港运费 只有普通 INDEX、无任何 UNIQUE，BulkCopy 写重复运单不抛 2601/2627，唯一冲突『重查-剔除-重试』兜底永不进入。
- **影响**：同一运单/审批编号在重复上传/重跑批次时被重复写入 STG（FID3002 出港运费、FID3003 费用支出两张财务取数源表），直接喂给下游计费与派件量聚合及自动凭证，造成财务重复计数，DB 层零兜底。
- **违反的设计意图**：意图1（事件/异常驱动——重复数据应被识别为异常并阻断后续计费）。
- **建议修复**：在 `ParseConfigAsync` 或去重前用 `_columnMappings` 把 `_crossBatchDedupFields` 由 ExcelColumn→DbColumn 归一化（与 cs:619 keyFields 兜底逻辑一致），或统一改种子写 DB 列名；同时为 `STG_出港运费(F运单号, FOrgId)` 等业务键建唯一索引让兜底生效。两者至少落地其一，建议都做。

### 2. 凭证创建无事务，AddAsync 逐条自动提交——中途失败留下半截/不平衡凭证【P1｜concurrency-transaction｜置信度 high】
- **位置**：`src/STOTOP.Infrastructure/Repositories/Repository.cs:28`；`VoucherService.cs:289-313`；`AutoVoucherHandler.cs:336/451`
- **问题**：`Repository.AddAsync` 每次调用立即 `SaveChangesAsync`（非工作单元）。`VoucherService.CreateAsync` 先提交凭证头再循环提交分录，全程无 `BeginTransaction`/`TransactionScope`。任一分录写入异常（科目查询、连接抖动、约束冲突、`EnableRetryOnFailure` 重试耗尽）→ 已提交的头+前几条分录永久留库，形成借贷不平衡/只有头无分录的脏凭证；`AutoVoucherHandler` 在 foreach 组循环里逐张调用，单张失败仅 `failedGroups++` 不回滚。
- **影响**：直接污染财务账的潜在数据损坏路径（非每次必现，仅运行期故障命中写入窗口时）。同样非原子模式存在于 `UpdateAsync/SaveDraft/Reverse`。
- **违反的设计意图**：none（财务记账原子性通用约定）。
- **建议修复**：用 `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)` 包裹 `BeginTransactionAsync`，把凭证头+全部分录纳入单一事务（因启用了执行策略，直接 BeginTransaction 跨多次 SaveChanges 会抛 InvalidOperationException）；或去掉 `Repository.AddAsync` 即时提交、末尾一次性 SaveChanges。覆盖 Create/Update/SaveDraft/Reverse。

### 3. 自动凭证去重键不含规则组ID：GroupBy 为空 + 多规则组同日，第二组凭证被误判重复跳过【P1｜data-integrity｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/Handlers/AutoVoucherHandler.cs:284`（去重查询 287-296，键计算 679-695，DataScopeId 兜底 332）
- **问题**：GroupBy 为空分支按 (规则组 × 业务日期) 拆多张凭证，但 `ComputeBusinessKeyV2` 只取 KeyFields+batchId、不含 ruleGroupId。当 KeyFields 不含区分规则组的字段（如仅网点/日期）时，多个规则组得到相同 businessKey；第一组写入占用 `F数据作用域ID` 后，后续规则组去重命中被 skip。`DataScopeId` 含 ruleGroupId 的兜底仅在 businessKey 为 null（未配 KeyFields）时生效——配了 KeyFields 反而触发该 bug。
- **影响**：本应生成的财务凭证整张静默丢失、对应金额未入账（仅 LogInformation 无告警），下游 VoucherExplainService 按 businessKey 反查也会因键碰撞误判已生成。「GroupBy 为空 + 多规则组按行路由」正是 V2 引擎核心设计场景，命中概率非边缘。
- **违反的设计意图**：意图1（异常驱动；凭证拆分粒度与去重粒度须一致）。
- **建议修复**：`ComputeBusinessKeyV2` 在 GroupBy 为空多规则组分支把 ruleGroupId 纳入键起始（keyParts 首段加 ruleGroupId），保证去重粒度与凭证拆分粒度一致。

### 4. 经导入工作台(CfImportController)撤销批次不红冲凭证，留下财务残留【P1｜data-integrity｜置信度 high｜已知推迟 3c】
- **位置**：`src/STOTOP.Module.CardFlow/Controllers/CfImportController.cs:1735`（RevokeCfBatchAsync 1735-1755）；对照 `BatchLifecycleService.cs:192-204`
- **问题**：两条撤销路径行为不一致——`CardFlowBatchController.Revoke` → `BatchLifecycleService.RevokeBatchAsync` 会级联取消卡片并对 FDataJson 的 voucherXxxRef 红冲（CreateReversalAsync）；而 CfImportController 的 `DeleteBatch(mode=revoke)` 走私有 `RevokeCfBatchAsync`，仅翻转 FIsRevoked/FStatus + 递增版本 + 推送，无任何凭证红冲、无级联取消卡片。两入口都对终端用户开放。
- **影响**：经导入工作台撤销后，已生成的 FIN凭证 仍为有效记账，账面残留+重复入账风险。**残留范围比文档描述更广**：批次级 AutoVoucherHandler 按 [F批次ID] 生成的凭证（DataScopeId=businessKey）两条撤销路径其实都不红冲（BatchLifecycleService 只扫卡片 FDataJson 的 voucherXxxRef）。
- **违反的设计意图**：意图1（撤销应触发对账面的反向事件，却被静默吞掉）。
- **建议修复**：让 `RevokeCfBatchAsync` 复用 `IBatchLifecycleService.RevokeBatchAsync`（统一含红冲+级联取消），或 PreCheck 对有未红冲有效凭证的批次禁止经导入工作台软删；彻底闭环还需让红冲覆盖按 CF凭证记录/`FIN凭证.F数据作用域ID` 关联的批次级凭证。

### 5. RejectAsync 不区分审批模式，orsign 单人拒绝即整卡退回；IsStageReturned 写好却 0 调用【P1｜correctness-bug｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:701`（非 returnToStage 分支 751-765）；`ApprovalModeHandler.cs:33-47`（正确语义无人用）
- **问题**：RejectAsync 校验通过后无条件把节点+卡片置 returned，不读 `FApprovalMode`。系统支持 orsign（或签）模式（退回语义应为全部 rejected 才退回，任一 approved 即通过），但全代码库 0 处调用已实现的 `IsStageReturned`。对照 ApproveAsync 通过侧正确调用了 `IsStageCompleted`，退回侧明显遗漏对称调用。
- **影响**：orsign 节点中任一处理人点拒绝就把整张卡片打回发起人，违反或签语义。countersign 因 Any(rejected) 巧合一致无害，真正受损的是 orsign。
- **违反的设计意图**：意图2（节点应按其审批模式正确判定通过/退回）。
- **建议修复**：标记 assignee=rejected 并 SaveChanges 后，重查全部 assignee 调用 `IsStageReturned(FApprovalMode, allAssignees)` 判定；未达退回条件时保持节点 active 或按 IsStageCompleted 推进，而非无条件整卡退回。

### 6. countersign 节点转办/加签后永久卡死——transferred/cancelled/waiting 残留破坏 All(approved)【P1｜correctness-bug｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/ApprovalModeHandler.cs:20`；完成判定取数无状态过滤 `FlowEngineService.cs:624-629`；转办置残留态 `TransferAsync FlowEngineService.cs:1337`
- **问题**：countersign 完成判定 `All(approved)` 取该节点全部 assignee（不按状态过滤）。转办把原处理人置 transferred 并新增 pending；其余人 approved 后，残留的 transferred 使 `All(approved)` 恒 false → 节点永久无法完成。sequential 有 `IsIgnoredSequentialStatus` 显式忽略 cancelled/transferred，countersign 无同等豁免。加签 before 模式把原 assignee 置 "waiting"，若未被正确 reactivate（SuspendedAssigneeIds 漏记）同样卡死，属同根因次要风险。
- **影响**：会签节点确定性永久卡死，无用户侧自救路径（RecallAsync 要求所有 assignee 仍 pending，此时 transferred 已不满足），需人工介入数据。
- **违反的设计意图**：意图2（状态机必须能到达终态）。
- **建议修复**：countersign 完成/退回判定仅基于活跃集合——先过滤 transferred/cancelled/waiting，再判 `active.Count>0 && active.All(approved)` / `Any(rejected)`。

### 7. 组织链/角色字段族 + inOrgChain 算子运行期恒空，相关路由条件永不命中【P1｜design-gap｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:161`（ResolveField 164-169，IsInOrgChain 299-311）；上下文默认空 `ConditionEvaluationContext.cs:10-13`；`ConditionEvaluationContextBuilder.BuildAsync` 与 `CardFlowPathPreviewService.BuildPreviewContext` 均未赋值
- **问题**：评估器把 field=orgChain / roles.code / roles.name / relations.* 及 operator=inOrgChain 当一等公民支持，但运行期/预演上下文构造器从不给这四个集合赋值（默认 new() 空）。任何按组织链/角色路由或 inOrgChain 写的规则运行期恒为 false，静默落默认分支，无报错。缺口面不止路由：`DynamicStagePolicyResolver.cs:93-100` 与 `FlowEngineService.cs:1843` 走同一 BuildAsync，含动态阶段策略。单测手工填 OrgChain 绕过真实构造器，属「测试掩盖生产 bug」。
- **影响**：前后端均宣称支持的「按组织链/角色路由」运行期永久失效且无告警，可致审批静默走错分支。
- **违反的设计意图**：意图2（按条件路由）、意图4（规则简单可用）。
- **建议修复**：在 `ConditionEvaluationContextBuilder` 内按 card.FInitiatorId/FOrgId 填充 RoleCodes/RoleNames/OrgChain（复用 `ApproverResolver.ResolveOrgChainAsync` 与 `AuthService.GetUserRoleCodesAsync`）；预演侧用 request.InitiatorId/OrgId。短期至少在命中这些字段/算子时产 TypeError 或告警。

### 8. SaveAsTemplate 新建模板分支：FillOrgId 把 FOrgId=0 改写为当前组织，模板落不到全局且触发唯一索引冲突【P1｜correctness-bug｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:1105`（CloneFlowDefinitionAsync `FOrgId=request.OrgId??0` 在 945）；`src/STOTOP.Infrastructure/Data/STOTOPDbContext.cs` FillOrgIdForNewEntities（State=Added 且 FOrgId==0 覆盖为 CurrentOrgId）；唯一索引 `CfFlowDefinitionConfiguration.cs:32`
- **问题**：新建模板分支传 OrgId=0 意图建全局模板，但 SaveChanges 时 FillOrgId 把 FOrgId 覆盖成调用方组织（认证请求 CurrentOrgId 恒非空）。后果：(a) 不是真正全局模板；(b) 新模板 FFlowCode 与源相同、FOrgId 又被改成与源相同的当前组织 → 触发唯一索引 (FFlowCode,FOrgId) 冲突，抛 `DbUpdateException`（非 InvalidOperationException，控制器 catch 不到）→ 500。且「更新已存在模板」分支始终按 FOrgId==0 查不到 → 死路，**save-as-template 功能全程不可用**。
- **影响**：保存为模板首次创建直接 500 失败，功能不可用。
- **违反的设计意图**：意图4（模板复用要简单可用）。
- **建议修复**：克隆保存后显式置 `newDefinition.FOrgId=0` 再 SaveChanges，或给 FillOrgId 加「克隆为模板」白名单/标志位跳过覆盖（如 V22 种子用原生 SQL 写 FOrgId=0）；并把控制器 catch 扩到 DbUpdateException。

### 9. CloneFlowDefinitionAsync 直接信任客户端 request.OrgId 作为新流程 FOrgId，无授权校验 → 跨组织写越权【P1｜security｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:945`（源读取 927-929 用 IgnoreQueryFilters）；Controller `/clone`、`/templates/{id}/clone-to-org` 仅 [Authorize]
- **问题**：`FOrgId=request.OrgId??0` 直接作为新建流程组织归属，非 0 时 FillOrgId 不覆盖；控制器与前端均把 orgId 作为可选入参透传，不校验是否属调用方授权组织。源读取又用 IgnoreQueryFilters 可读任意组织/模板。
- **影响**：任意已认证用户可传 OrgId=他人组织，把整套流程定义+版本+节点克隆进无权组织，污染对方流程库（卡片流转配置主链）；可读任意源、写任意目标的跨租户写越权。
- **违反的设计意图**：项目约定 FOrgId 组织隔离。
- **建议修复**：服务端忽略客户端 OrgId，统一用 CurrentOrgId 作为克隆目标；仅全局模板创建由服务内部置 0；若确需指定目标组织须复用 OrgContextService 归属校验。

### 10. GetTemplatesAsync 仅按 FIsTemplate 过滤（不限 FOrgId=0），org 绑定模板跨组织泄露【P1｜data-integrity/security｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:1019-1021`（IgnoreQueryFilters().Where(FIsTemplate && published)，无 FOrgId==0）；对照查重处 1052-1054 有 FOrgId==0
- **问题**：取模板故意绕过组织过滤以取全局模板，但 Where 未约束 FOrgId==0。SaveAsTemplate 的 else（首次建模板）主路径经 FillOrgId 必然把 FOrgId 盖成操作者组织，落库即 org 绑定的 FIsTemplate=true 行，随后被 GetTemplates 无差别下发给所有组织。
- **影响**：组织 A 看见组织 B 的流程定义模板（含 FAllowedRolesJson/FTriggerConfigJson 等敏感配置），「从模板创建」列表上组织隔离被实际打穿，属跨租户信息泄露。
- **违反的设计意图**：组织隔离约定 + 意图4（模板列表应只呈现全局模板）。
- **建议修复**：GetTemplatesAsync 的 Where 增加 `&& x.FOrgId==0`；并从根本确保模板写入时 FOrgId 真正落 0（见第 8 条），否则即便加了 FOrgId==0 过滤，新建模板也因被盖 org 而完全发现不到。

### 11. 跨批次去重比对只取前两个字段，配 3+ 去重字段时过度去重（误删合法行）【P1｜data-integrity｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/AutoPlugin/Implementations/ExcelInputPlugin.cs:586`（586-592）；`QueryExistingByFieldsAsync cs:1082-1084`
- **问题**：去重存在性判断硬编码只比对 `_crossBatchDedupFields[0]` 与 [1] 的 (string,string) 元组；JOIN 虽用全部 fields，但 reader 只读 0、1 两列。配 3+ 字段时，仅第 3+ 字段不同的两行被判同键、第二行 `continue` 跳过。
- **影响**：去重粒度被强制降为前两列，静默丢合法数据（无异常，仅 crossBatchDupCount++）。当前 seed 去重字段≤1 未激活，但 2026-06-17 派件量接入 design spec 明确要求三个去重字段，属「已埋好、下一个特性即触发」的潜伏型缺陷。
- **违反的设计意图**：意图1（异常驱动；去重粒度应符合配置意图）。
- **建议修复**：改用全字段拼成的复合 key（`string.Join('', vals)`）做 HashSet 比对，`QueryExistingByFieldsAsync` 同步读全部列；或配置层限制最多 2 个去重字段并校验。

### 12. 脱敏仅在「当前节点 active 且 V2」时生效；已完成/非活动卡片回传原始 FDataJson 与明细原值【P1｜security｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/CardService.cs:137`（默认原值 137，脱敏分支条件 194/204，覆盖 219-226，明细 ToCardDetailRowDto 始终原值 648）；前端裸渲染 `web/src/views/cardflow/CardDetailPage.vue:73-86`、`web/src/views/cardflow-mobile/MobileCardApprovalPage.vue:247-254`
- **问题**：GetByIdAsync 先无条件 `DataJson=card.FDataJson`，仅当存在 FStatus=="active" 的当前阶段实例且 Version==2 时才用 Redacted 覆盖。三种情况下 masked/hidden 字段以原值回传：①卡片已完成/作废（无 active 阶段）；②当前阶段 V1（Version!=2）；③阶段实例非 active。前端不做二次脱敏。
- **影响**：审批结束后查看历史卡片（最常见场景）时，银行卡号/手机号等被节点配置为 masked/hidden 的敏感字段原值直达浏览器，属可达的越权读取。
- **违反的设计意图**：意图4（字段权限/脱敏按节点展示）。
- **建议修复**：脱敏与「是否当前活动节点」解耦——对非活动/已完成卡片采用流程级默认（或合并所有节点 hidden/masked 取并集）脱敏画像后再回传；不要把 FDataJson 原值作为 DTO 默认值。

### 13. CardFlowBatchController.GetRows 按 batchId 直查 CfBatchRow，跨组织泄露导入原始数据【P1｜security｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Controllers/CardFlowBatchController.cs:185`（CfBatchRow 非 IOrgScoped，`CfBatchRow.cs:9`）；对照 UpdateRow 有归属预检 228 行
- **问题**：GetRows 直接 `_db.Set<CfBatchRow>().Where(r=>r.FBatchId==id)`，CfBatchRow 是 BaseEntity 无全局组织过滤器，且未像 UpdateRow 先按组织过滤加载 CfBatch 校验归属。同控制器 GetById/GetList 查 CfBatch（过滤器生效）是安全的，凸显 GetRows 漏归属校验。
- **影响**：任一登录用户传他组织 batchId 即可分页读出每行 FDataJson（导入原始业务明细，未脱敏），典型 IDOR。
- **违反的设计意图**：FOrgId 组织隔离 + 意图4。
- **建议修复**：GetRows 先 `var batch = _db.Set<CfBatch>().FirstOrDefaultAsync(b=>b.FID==id)`（依赖 CfBatch 过滤器），null 则返回「批次不存在」再查行；或给 CfBatchRow 加 FOrgId 并实现 IOrgScoped。

### 14. CardFlowBatchController 整类无 [RequirePermission]，任何登录用户即可上传/改暂存行/确认/撤销批次【P1｜security｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Controllers/CardFlowBatchController.cs:18`（11 端点全无 RequirePermission，Revoke 347）；对照 `CfImportController.cs:1137` 同类操作均有 RequirePermission
- **问题**：类级仅 [Authorize]，upload/列表/rows/PATCH 改行/exclude/restore/confirm（触发质检+fanout）/DELETE 撤销（含凭证红冲级联）全无权限网关。功能等价的 CfImportController 每端点都加了 ImportUpload/ImportProcess/UploadCenter。
- **影响**：仅需登录（不需任何 CardFlow 权限）即可经此并行路由上传触发批次、改暂存行、确认进入 fanout、撤销批次，与 CfImportController 权限矩阵完全脱节（横向越权/Broken Access Control）。破坏性操作受批次状态校验约束，故非未认证 RCE 级别。
- **违反的设计意图**：none（权限网关一致性）。
- **建议修复**：各端点补齐与 CfImportController 一致的 RequirePermission（上传=ImportUpload，rows改/exclude/restore/confirm/revoke=ImportProcess，查询=UploadCenter）；若与 CfImportController 重叠属遗留应直接下线避免无门禁影子入口。

---

## 二、P2 建议修

### 15. WithdrawAsync 误把顺签 waiting 态当作「已审批」，阻断无人审批时的合法撤回【P2｜correctness-bug｜置信度 high】
- **位置**：`FlowEngineService.cs:937`（930-938）；`SequentialApprovalRuntime.cs:18-24`
- **问题**：撤回守卫 `assignees.Any(a=>a.FStatus!="pending")`，但 sequential 首环节初始态为 [pending, waiting, waiting...]，waiting 命中 → 误判已审批。
- **影响**：顺签卡片在首环节无人处理时发起人无法撤回（仅可用性受损，不污染数据）。
- **建议修复**：守卫改为 `Any(a=>a.FStatus is "approved" or "rejected" or "transferred")`，把 pending 与 waiting 都视为未审批。

### 16. VoidAsync 可作废已完成卡片并释放已锁定预算，且权限仅认发起人与注释不符【P2｜data-integrity｜置信度 high】
- **位置**：`FlowEngineService.cs:1083`（守卫 1083-1087，作废+Release 1102-1124）；对照完成时 Lock+Consume 2132-2133
- **问题**：仅拦 voided 不拦 completed；completed 卡片已 Lock+Consume，作废只做 Release 不做反向冲销 → 预算台账与卡片/余额状态脱节（locked 占用被错误释放；consumed 被查询过滤部分自保护）。注释「发起人或管理员」但代码仅放行发起人、无 IsAdmin。
- **影响**：账实不符（无静默资损），需该 completed 卡片发起人本人主动作废（非常规操作）。
- **建议修复**：明确允许作废状态集合（仅 draft/active/returned），completed 走红冲，补 IsAdmin 放行。

### 17. fixedUsers 取人不去重且不校验用户有效性——重复 userId 致会签死锁、停用/不存在用户致节点停滞【P2｜correctness-bug｜置信度 high】
- **位置**：`ApproverResolver.cs:51`（ResolveConfiguredUsers 59-76）；对照 ResolveUserIdsAsync 382-394 有 Distinct + FStatus==1
- **问题**：fixedUsers 走捷径绕过 Distinct 与 SysUser.FStatus==1 校验。重复 userId → 会签两条同 FUserId pending，ApproveAsync FirstOrDefault 只置一条 → All(approved) 永不成立死锁；停用/删除用户 → 节点无人能推进。
- **影响**：需配置数据错误为前提，单卡流程卡死/停滞，可人工恢复。
- **建议修复**：ResolveConfiguredUsers 解析出的 userId 回流 ResolveUserIdsAsync 统一 Distinct+FStatus==1 校验（兜底分支 ApplyFallbackAsync:324 同理）。

### 18. OR 组内任一子条件类型错误毒化整条已命中路由边【P2｜correctness-bug｜置信度 high】
- **位置**：`StageRouteResolver.cs:81`；`ConditionRuleEvaluator.EvaluateGroup 76-85`；CompareOrdered 273；预演 `CardFlowPathPreviewService.cs:164`
- **问题**：选边门禁 `Matched && TypeErrors.Count==0`，但 EvaluateGroup 把子结果 TypeErrors 无条件聚合，Matched 只按 Any/All 算。OR 组里子A 命中但子B 报 TypeError（如对字符串字段用 gt）→ 组 Matched=true 但 TypeErrors 非空 → 本应命中的边被静默丢弃。
- **影响**：依赖配置错误才暴露的潜伏正确性缺陷，TypeError 在候选诊断/预演可观测，非完全静默。
- **建议修复**：EvaluateGroup 令 TypeErrors 与短路对齐（OR 命中丢弃未命中子项 TypeErrors）；或门禁仅看 Matched、TypeError 作告警。需运行时+预演+DynamicStagePolicyResolver 四处同步。

### 19. PathPreview 上下文键名与缺失明细汇总与运行时口径不一致致预演误判【P2｜contract-mismatch｜置信度 high】
- **位置**：`CardFlowPathPreviewService.cs:255`（255-258）；运行期 `ConditionEvaluationContextBuilder.cs:44-47/65`
- **问题**：(1) 运行期 initiatorOrg.id=card.FOrgId，预演 initiatorOrg.orgId=request.OrgId，键名互斥必有一侧错；(2) 预演不构造 DetailSummary，凡以 detailSummary.* 作路由的规则预演按空字段求值。
- **影响**：仅限草稿路径预演诊断工具，作者看到与真实流转分叉的预测路径，不污染运行期。
- **建议修复**：统一 initiatorOrg 键名（建议 id），预演侧按 request 构造同结构 DetailSummary，至少补 id 别名。

### 20. 批内业务主键重复被计为 failRows 而非 skippedRows，污染批次失败统计【P2｜correctness-bug｜置信度 high】
- **位置**：`ExcelInputPlugin.cs:538`（538-542）；对照跨批次重复 594/676 走 skippedRows
- **问题**：批内 keyFields 主键重复时 `failRows++`，日志却说「已跳过」。同类「重复」被分到 fail/skip 两个计数桶。
- **影响**：FFailedRows 被正常去重抬高，UI/告警误报失败；不破坏已导入数据。
- **建议修复**：批内主键重复改 `skippedRows++`，与跨批次去重口径统一。

### 21. ReadHeaders 与实际解析表头切分逻辑不一致（截断 vs Column{i} 占位），重复表头静默覆盖【P2｜contract-mismatch｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/Import/ExcelParserService.cs:238`（ReadHeaders TakeWhile 截断 122-125，ParseExcel 不截断 241-243/255-265）
- **问题**：匹配端遇空列名 TakeWhile 截断，解析端不截断改 Column{i} 保留后续列（注释「遇空列停止」与实现矛盾）。表头中部空列使两端列集合分叉；行字典 `dict[header]=cellValue` 同名表头静默覆盖丢数据。
- **影响**：条件性数据正确性问题——空列在尾部时两端基本收敛；同名覆盖需导出 Excel 恰好同名列头。
- **建议修复**：统一表头切分策略（匹配与解析共用同一 ReadHeaders 结果）；重复表头检测时报错/加后缀去重。

### 22. 红冲后去重仍命中原凭证：撤销过的批次重跑无法重建凭证，账面永久缺失【P2｜data-integrity｜置信度 medium】
- **位置**：`AutoVoucherHandler.cs:287`（去重查询无 F已撤销 过滤）；`CardFlowVoucherBridge.cs:110-116`；`VoucherService.cs:698-713`
- **问题**：去重查询只按 F数据作用域ID 匹配，不过滤 FIsRevoked。原凭证红冲后仅打撤销标记、businessKey 不变，冲销凭证未设 FDataScopeId。对同一既有批次（同 BatchId）重跑凭证生成时，键一致命中撤销原凭证被永久跳过。注：businessKey 以 batchId 为首段（批次内唯一），真正「重新导入」（新 BatchId）不触发，故触发面较窄。
- **影响**：原+冲销凭证净额为零又拒绝重建，对应业务金额从账上消失。VoucherRevokeHandler 的红冲凭证还拷贝 FDataScopeId，命中概率更高。
- **建议修复**：去重查询追加 `AND ([F已撤销]=0 OR [F已撤销] IS NULL)`，并同时排除红冲凭证；或撤销时清空/改写原凭证 F数据作用域ID。

### 23. 借贷平衡校验容差 0.001 与 Finance 端严格相等口径不一致，含分以下精度时写入抛异常【P2｜contract-mismatch｜置信度 high】
- **位置**：`AutoVoucherHandler.cs:273`（273/377，金额无 Round 544-553）；`VoucherService.cs:858-864` 严格相等
- **问题**：CardFlow 端 `Abs(debit-credit)>0.001m` 判平衡且金额不 Round；下游 ValidateVoucher 严格 `!=`。源数据带 2 位以上小数（残差落 (0,0.001m]）时 CardFlow 判平衡通过 → ValidateVoucher 不相等 → 抛 InvalidOperationException → 459 行 catch 成 failedGroups。
- **影响**：偶发性凭证生成失败、原因隐晦（仅落 warnings），单组失败不影响其它组。
- **建议修复**：每条分录 `Math.Round(amount,2,AwayFromZero)` 后累加比较，统一两端口径。

### 24. 崩溃恢复对新建批次无 FCreatedTime 兜底 + 无在途锁，IIS 重叠回收下双跑【P2｜concurrency-transaction｜置信度 high】
- **位置**：`BatchJobProcessorService.cs:78`（78-86）；创建只设 FStatus/FCreatedTime `BatchTriggerService.cs:117-118`；首写 FUpdatedTime `FlowEngineService.cs:371-372`
- **问题**：恢复扫描 FStatus∈{0,2,4} 且 (FUpdatedTime==null OR <now-10min)；新建批次 FUpdatedTime 保持 null，对 null 分支恒命中且不受 -10min 约束。IIS 应用池重叠回收使新旧 worker 各执行一次 RecoverPendingBatchesAsync，对同一批次各入队各自内存 Channel → 双进程同时处理。入队前无状态占位/锁。
- **影响**：窄竞态（刚建+首节点未完+两进程都先于状态推进观测），命中即财务卡片/凭证双跑（下游插件非幂等时重复生成），零护栏。
- **建议修复**：恢复条件加 FCreatedTime>cutoff 兜底；入队前轻量在途占位/乐观锁 CAS；长期改 Hangfire 等带去重持久队列。

### 25. 批次级链恢复重跑：插件成功后、FCurrentBatchStageOrder 持久化前崩溃 → 同一节点重复执行【P2｜data-integrity｜置信度 medium】
- **位置**：`FlowEngineService.cs:370`（游标推进 371，TransitionBatchStatus/SaveChanges 379/383）；插件副作用 300-360
- **问题**：游标推进与节点状态/执行记录持久化在插件成功后才一起落库。崩溃落在「插件副作用已生效但本次 SaveChanges 未提交」窗口时游标不前移，恢复重跑从同一节点重执行。卡片 fan-out 有 draft 幂等护栏，生成凭证/写明细类批次级插件若非幂等则重复。
- **影响**：续跑幂等性完全外包给各插件，框架层无去重；常见配置下 Pricing/Cost/配 KeyFields 的 AutoVoucher 有自带去重，缺陷不外显。
- **建议修复**：把「插件执行+置12+游标推进」包进单一事务；或引入按 FBatchId+节点 的执行幂等键（续跑检查 CfPluginExecution.FStatus==12 即跳过）。

### 26. CardFlowTimeoutJob 注入 INotificationDispatcher 却从不调用，节点超时无持久通知/无自动升级【P2｜design-gap｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Jobs/CardFlowTimeoutJob.cs:15`（注入 15、赋值 26、广播 108-127，dispatcher 零调用）
- **问题**：超时仅置 FIsTimeout + 写 CfActionLog + 向 SignalR 组瞬时广播。注入的 dispatcher 全程未调用，`if(FIsTimeout) continue` 使广播严格一次性——那一刻无客户端连在组上则提醒永久丢失，无持久待办、无重发、无升级转派。
- **影响**：超时本应是可持久消费的异常事件，退化为一次性瞬时广播，违背事件/异常驱动。
- **建议修复**：标记超时处调用 dispatcher 生成持久待办/通知，或落为可被流程引擎消费的异常事件触发升级；若确不需要则删依赖。

### 27. V22 模板审批节点硬编码 userId:1 管理员，clone-to-org 原样复制，目标组织审批被指派给非本组织用户【P2｜design-gap｜置信度 high】
- **位置**：`src/STOTOP.WebAPI/Data/Seeders/CardFlowSeeder.cs:1456`（节点 5031/5032 fixedUsers userId:1，1467 同理）；克隆逐字复制 `FlowDefinitionService.cs:992/993`
- **问题**：V22 费用报销全局模板两个人工节点硬编码处理人 userId:1；CloneFlowDefinitionAsync `FAssigneeConfigJson=src...` 原样复制无按目标组织重绑。
- **影响**：任意组织克隆出的费用报销流审批固定派给全局 userId=1（通常不属目标组织），形成跨组织固定处理人指派，各组织改不掉直到手工编辑。需主动克隆触发，指派给管理员非泄露数据。
- **建议修复**：模板 fixedUsers 改 role/initiator-dept 等可随组织解析的策略，或 clone-to-org 时对 fixedUsers 做目标组织内重映射/置空并提示补配。

### 28. CardFlow 实时推送部分失效：BatchNotifier 独有事件发往 CardFlowHub，前端只连 ProgressHub【P2｜contract-mismatch｜置信度 high｜双轨退化】
- **位置**：`src/STOTOP.Module.CardFlow/Services/BatchNotifier.cs:23`（Clients.All 23/39/58）；`Program.cs:574/578`；前端 `useUploadCenter.ts:395`/`useBatchSync.ts:855`
- **问题**：BatchPipelineStarted/PluginStatusChanged/BatchProgressUpdate 经 `IHubContext<CardFlowHub>` 推送，前端用 `/hubs/progress`(ProgressHub) 连接并 SubscribeImportBatch（组 import-{batchId}），两条不同 Hub 连接。这三个 BatchNotifier 独有、ProgressHub 无等价名的事件客户端永远收不到。注：存在并行的 SignalRProgressNotifier(ProgressHub) 链覆盖 BatchStatusChanged/OnAutoPlugin* 等大部分功能，故非「全部失效」。
- **影响**：初始 autoPluginTrail 初始化/插件级状态依赖 15s 轮询补偿，可感知的实时性退化。
- **建议修复**：统一推送与订阅到同一 Hub 同一组（BatchNotifier 改用 IHubContext<ProgressHub> 推 import-{batchId}，或前端改连 /hubs/cardflow 并 SubscribeBatch），统一事件名消除双轨。

### 29. 节点流转创建的待办从不设 pushChannel 也不调 DispatchCreateTodoAsync，DingTalk 外推事实失效【P2｜design-gap｜置信度 high】
- **位置**：`FlowEngineService.cs:2562`（创建待办无 pushChannel/无后续 Dispatch；转办 1367、顺签 678、抄送 1408、动态 1288/1830 同）；`NotificationDispatcher.cs:31-36` 空 channel 置 skipped；唯一调 Dispatch 的是催办 UrgeAsync 1434
- **问题**：所有 CreateTodoAsync 调用点不传 pushChannel 且不调 DispatchCreateTodoAsync。正常审批流转产生的待办永远 FPushStatus=pending、FPushChannel=null，DingTalkChannel 常规流程从不触发；PushRetryJob 只重试 failed，连重试入口都进不去。
- **影响**：CfTodoItem 仍正常写库、系统内待办可用，缺失的仅外部 DingTalk 推送（功能已点亮但通道未接线）。
- **建议修复**：创建后按 NotificationSettingsService.DingtalkEnabled 解析默认 pushChannel 并在进入/转办/顺签处补 DispatchCreateTodoAsync；或在 TodoService.CreateTodoAsync 内统一解析+派发。

### 30. 审批/驳回事务内发起 DingTalk 远程 HTTP，事务跨网络 IO 持锁【P2｜concurrency-transaction｜置信度 high】
- **位置**：`FlowEngineService.cs:666`（事务 706，DispatchComplete 666/2629/2656，DispatchDelete 958/1117/1362/2669）；`DingTalkChannel.cs:119`；`NotificationDispatcher.cs:70/92/114`
- **问题**：ApproveAsync/RejectAsync 全程包在事务内；待办有 FExternalTodoId 时 Dispatch 进入 DingTalkChannel 做同步远程 HTTP（CreateClient 默认 100s），在持有卡片/节点行锁、事务未提交时进行。NotificationDispatcher 还共用同一 scoped DbContext 中途 SaveChanges。
- **影响**：钉钉慢/超时把整个审批事务拖住、放大锁竞争甚至死锁/超时回滚。触发需配钉钉+有 FExternalTodoId+钉钉端慢，不致数据损坏。
- **建议修复**：事务内只改 CfTodoItem 本地状态，提交后再异步派发外部待办（入队 PushRetryJob/后台 Channel）。

### 31. CfStagingController/StagingService Raw SQL 路径不做组织隔离，可跨组织读改删暂存业务数据【P2｜security｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/Staging/StagingService.cs:625`（BuildWhereClause 无 FOrgId）；EF 路径 ApplyFilters 345；控制器 `CfStagingController.cs:30`
- **问题**：StagingService 全部读/改/删只按 FBatchId/FID/状态/字段过滤，不带 FOrgId。**修正范围**：5 张 EF 映射 STG 表受 IOrgScoped 全局过滤器保护（STG 实体声明 FOrgId 但未实现 IOrgScoped——EF 路径仍被全局过滤器覆盖）；真实暴露面仅限走 Raw SQL 回退路径的非 EF 动态表，目前实际只有 STG_出港运费 一张表，且在 [Authorize]+RequirePermission(Staging) 之后——属单表跨组织 IDOR（读/删/改），非平台级、非 P0。
- **影响**：他组织 STG_出港运费 暂存行可被跨组织读/删/改。
- **建议修复**：BuildWhereClause 及各 Raw SQL 路径统一参数化追加 `AND [FOrgId]=@orgId`（取 IOrgContextAccessor.CurrentOrgId）；UpdateRecordInRawTableAsync 把 FOrgId/F账套ID/F批次ID 列入禁改白名单。（EF 路径已被全局过滤器覆盖，无需额外改。）

### 32. 上传落盘使用未净化的原始 file.FileName 拼路径，存在路径穿越写文件风险【P2｜security｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Controllers/CfImportController.cs:87`（Path.Combine(absolutePath, file.FileName)）；CompleteChunkUpload 629 用 meta.FileName；对照 CardFlowBatchController 用 Guid safeName
- **问题**：Upload 把客户端可控 file.FileName 直接 Path.Combine 落盘，CopyToAsync 在任何安全校验前（SecureFileUploadValidator 只在 SecurityCheckPlugin 内、文件已落盘后才跑）。filename 携带 ..\ 或绝对路径分量可写到上传目录外。受 [RequirePermission(ImportUpload)] 保护，故为已认证且持上传权限用户可触发，非匿名 RCE。
- **影响**：路径穿越任意写文件（如站点可执行目录）。
- **建议修复**：落盘前 `Path.GetFileName` 去目录分量 + 白名单/保留名校验（SecureFileUploadValidator.ValidateFileName 应在控制器入口先跑），或用服务端 Guid safeName 落盘；分片端点对 meta.FileName 同样处理。

### 33. AutoPluginFactory 为 Singleton 却用根 ServiceProvider 解析 Scoped 插件，卡片级 auto 节点产生 captive DbContext【P2｜concurrency-transaction｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/AutoPlugin/AutoPluginFactory.cs:31`（单参 Create 用根容器）；注册 `CardFlowModuleExtensions.cs:196` Singleton 捕获根 sp；卡片级调用 `FlowEngineService.cs:2210` 单参重载；批次级 297 用带 scopedProvider 重载（正确）
- **问题**：单参 Create 用捕获的根容器 GetRequiredService 解析 Scoped 插件（依赖 Scoped STOTOPDbContext）。生产默认不开 ValidateScopes，从根容器创建一个生命周期=应用级、跨请求共享、永不释放的 DbContext。
- **影响**：DbContext 非线程安全（卡片推进存在并发），可触发「second operation on this context」异常、跨卡片/批次脏跟踪状态、连接泄漏。批次级第 297 行已正确、仅卡片级第 2210 行遗漏。
- **建议修复**：卡片级改调 `Create(pluginCode, _serviceProvider)`（FlowEngineService 已持 scoped provider），或工厂内 CreateScope 并绑定到插件执行；Program.cs 显式开 ValidateScopes=true 让此类问题启动期暴露。

### 34. 移动端「发起+源上下文」契约仅存于孤儿文件 CardFillForm.vue，源上下文链前端无落地【P2｜dead-code-or-risk｜置信度 high】
- **位置**：`web/src/views/cardflow-mobile/CardFillForm.vue:56`（buildCreateCardPayload，读 route.params.flowId 这一不存在的 param）；活跃路由 `/m/cardflow/fill/:id`→`MobileCardFillPage.vue:54/154`（按 cardId 取数）；契约测试 `cardflow-source-context-contract.test.mjs:81`；PC 侧 `web/src/views/workhub/TriggerActionPanel.vue:135`
- **问题**：唯一携带 sourceModule/sourceType/sourceId/returnUrl/initialDataJson/sourceTitle 的构造逻辑在无路由、无引用的死文件 CardFillForm.vue 里。活跃路由映射到 MobileCardFillPage（把 :id 当 cardId、需卡片已存在、不传源上下文）。FlowSelectPage/CardApprovalView/CardDetailView 同为孤儿。契约测试断言死文件 → 假绿。PC 侧 createCard 也只传 {flowDefinitionId,orgId,dataJson:'{}'}。后端契约完整（CfCard/CardService/CardFlowSourceContextVerifier 已落地），属纯前端断链。
- **影响**：CRM→CardFlow 源上下文发起契约前端无活跃实现，且测试制造误判。
- **建议修复**：确定移动端发起真实入口，把 buildCreateCardPayload 源上下文构造迁入 MobileCardFillPage（或新增按 flowId 创建入口），修正 FlowSelectPage 跳转语义（flowId vs cardId）；删除 4 个孤儿文件或恢复路由；契约测试改断言活跃文件。

---

## 三、P3 隐患

### 35. amountMatrix 金额分级两端均闭且无重叠校验，边界值归属由配置顺序静默决定【P3｜design-gap｜置信度 high】
- **位置**：`ApproverResolver.cs:241`（239-248）
- **问题**：命中判定 `(amount<min)||(amount>max)` 跳过，即闭区间 [min,max]。相邻段 [0,1000]/[1000,5000] 时 amount=1000 双命中，按数组顺序取首个，无重叠/缺口校验。最坏 fail-closed（落兜底/抛错），不放行。
- **建议修复**：约定半开区间 [min,max)（命中改 `amount<max`），发布期对 ranges 做有序/无重叠/无缺口校验。

### 36. 字段缺失时 neq 恒判 false，缺失值「不等于」语义反直觉【P3｜correctness-bug｜置信度 high】
- **位置**：`ConditionRuleEvaluator.cs:119`（119-124 早返回在 op switch 前）
- **问题**：resolved.Exists==false 时除 exists/notExists/empty/notEmpty 外对所有算子（含 neq）返回 Matched=false，与多数引擎「缺失字段不等于任何具体值为真」相悖。注：路由引擎有专用 FIsDefault 默认分支机制，配置者有正确工具表达「默认放行」，引擎从不让卡片静默卡死。
- **建议修复**：对缺失字段单独处理 neq（缺失→不等于任何具体期望值→true），或文档明确「缺失字段对所有比较算子均不匹配」要求显式用 notExists 兜底。

### 37. 配置项 dateFields 被解析端完全忽略，日期列拿原始字符串/Excel 序列号入库【P3｜contract-mismatch｜置信度 high】
- **位置**：`ExcelInputPlugin.cs:697`（ParseConfigAsync 无 dateFields，string 原样写入 516-518）；种子 `CardFlowSeeder.cs:803`
- **问题**：种子配 dateFields 但解析端从不读取、无 date 解析分支。注：目标列是 datetime2（StgExpenseRecord.cs:26/33），SQL 隐式转换使 xls/xlsx 合法日期落库收敛同值，裸序列号会触发 BulkCopy 转换异常而非静默写入——故属死配置/契约不一致的低优先级清理，非数据破坏。
- **建议修复**：ParseConfigAsync 读取 dateFields 做统一日期解析（兼容 FromOADate/常见文本）建强类型列或归一 ISO，或删除该死配置键。

### 38. 条件分录行无兜底时未命中行被单边静默丢弃，依赖平衡校验兜底【P3｜design-gap｜置信度 high】
- **位置**：`AutoVoucherMatchingEngineV2.cs:421`（条件/兜底分行 418-419，第一轮 422-448，第二轮 457-472，借贷独立 275-282）
- **问题**：某方向只配条件分录行无兜底时，未命中任何条件值的行既不被接纳也不 consumed，从该方向静默消失；借贷独立分配可致单边。平衡校验是有效安全网（让整组失败而非生成错凭证），但缺明确诊断指向「某方向有行未被接纳」。
- **建议修复**：AssignRowsToEntryLines 末尾检测某方向存在未被接纳的行并显式上报；或配置校验阶段要求使用条件行的方向必须含一条兜底行。

### 39. BatchLifecycleService.RevokeBatchAsync 无事务，凭证红冲(跨服务)与批次软删非原子【P3｜concurrency-transaction｜置信度 high】
- **位置**：`BatchLifecycleService.cs:177`（188-222 无 BeginTransaction，红冲 try/catch 仅 LogError 373-384，单点提交 222）
- **问题**：逐卡片 CreateReversalAsync（外部服务各自提交）→ 改卡片/CfBatchRow → 置 FIsRevoked，末尾一次 SaveChanges 无事务包裹。红冲部分成功后 SaveChanges 失败 → 「批次未标记撤销+部分凭证已红冲」中间态。红冲失败被静默吞（批次可显示已撤销而部分未红冲）。重入有部分保护（cancelled 跳过、FIsRevoked 防重）。
- **建议修复**：本地 DbContext 改动用执行策略+事务包裹；红冲失败聚合并在关键失败时回滚/抛出而非静默吞；跨服务写入需补偿/对账。

### 40. BatchNotifier 用 Clients.All 推送，无组织隔离（跨组织信息泄露，潜伏）【P3｜security｜置信度 high】
- **位置**：`src/STOTOP.Module.CardFlow/Services/BatchNotifier.cs:23`（23/39/58 均 Clients.All）；对照 BatchLifecycleService 用 org_{FOrgId} 组
- **问题**：三方法 Clients.All 广播，payload 含 batchId/插件名/处理行数，无 orgId/组过滤。当前主链不可达（前端连 ProgressHub，CardFlowHub 无客户端，见第 28 条）；一旦前端接回 /hubs/cardflow 即暴露跨组织泄露。注：全仓无任何 AddToGroupAsync("org_..."),「对照组已隔离」前提本身也不成立，修复时需一并补 org_ 订阅端逻辑。
- **建议修复**：改 `Clients.Group($"batch-{batchId}")` 或 `org_{orgId}`（需把 orgId 透传进 IBatchNotifier），与订阅组对齐。

---

## 四、已知有意推迟项的具体后果（isKnownDeferred=true）

这些核心冻结/推迟属设计文档确认的有意决策，但残留部分带来新增具体后果，应在 V2 接通前处理边界：

1. **委托(代审批)整套未接通审批主链**（`DelegationService.cs:21`，已知 V2，**P2**）：核心未接通是 D1 确认的 V2 推迟项。残留后果——DelegationController CRUD 端点仍 [Authorize] 在线 + 前端「委托管理」路由可见，用户能创建/管理 active 但**零效力**的委托记录（受托人调审批会被「您不是当前节点处理人」拒），形成误导性活功能面。建议：V2 前下线入口或响应标注「暂未生效(V2)」。

2. **CfImportController 撤销不红冲凭证**（已在 P1 第 4 条，3c 已知分歧）：文档已识别撤销双份问题但聚焦实现收敛，**未提及补红冲与权限网关**；残留的具体后果是财务账面残留+重复入账，且第 14 条揭示该并行路由还整类无权限——两条已知/新发现叠加放大风险。

3. **冻结的编排引擎仍经 OrchestrationController 全量接通**（`OrchestrationController.cs:18`，桶B 冻结，**P3**）：冻结=保留后端代码+前端隐藏入口。残留后果——后端 Controller 无开关/无 [Obsolete]/无授权降级仍活跃，认证用户调 `/api/orchestration/instances` 即可经 StartAsync→TriggerCardFlowNodeAsync 在 CfCard 写 FOrchestrationInstanceId 接通冻结引擎；而 TriggerCardFlowNodeAsync 仍是占位实现（节点永远 running、实例挂死）。建议：API 层加门/410 下线，或 StartAsync 抛 NotSupported。

4. **桶B 设计器入口未摘除 + 移动端源上下文死文件**（`FlowDefinitionEditPage.vue:173` 桶B 入口 **P3**；第 34 条移动端契约 **P2**）：冻结=前端路由/菜单摘掉。残留后果——FlowDefinitionEditPage 仍渲染 cardView 步骤与 DynamicApprovalPolicyEditor/StageComponentViewEditor/CardComponentConfigDrawer 无 V2 门禁，用户落入零数据/不通电的冻结能力。建议：用 V2 feature flag 门禁隐藏入口（标签订正：该 spec 无「阶段3e」，应属阶段1「摘前端冗余入口」遗漏）。

5. **DingTalk SignalR 双 Hub**（已在 P2 第 28 条，已知）：CardFlowHub 注释自称「迁自 ProgressHub」但二者并存导致双轨，部分实时事件丢失。

---

## 五、按 4 条设计意图维度的兑现度小结

- **意图1（系统基于事件/异常驱动）— 兑现度低**：重复数据未被识别为异常反而静默入库（去重失效 P0）；撤销应触发反向红冲事件却被静默吞（CfImport 撤销 P1）；凭证去重键碰撞致凭证整张静默丢失（P1）；超时本应是可持久消费的异常事件却退化为一次性广播（P2）。异常/事件链多处断裂或被静默吞。

- **意图2（环节按顺序与条件正确处理，状态机可达终态）— 兑现度中下**：orsign 单人拒绝整卡退回（P1）、countersign 转办后永久卡死无自救（P1）、按组织链/角色路由运行期恒空（P1）、OR 组 TypeError 毒化路由边（P2）、fixedUsers 死锁/停滞（P2）等多处破坏「按模式正确判定」与「可达终态」。核心线性主链点亮但模式分支与条件路由的边角语义大量未兑现。

- **意图3（信息流转=卡片在各环节流转并通知处理人）— 兑现度中**：系统内待办流转可用，但外部 DingTalk 推送主链未接线（P2）、委托改变处理人未接通（V2 推迟 P2）、移动端源上下文发起契约前端无落地+测试假绿（P2）、实时推送双 Hub 部分失效（P2）。流转「能走」但「通知/外部触达/源上下文携带」多处未通电。

- **意图4（卡片信息按节点以恰当内容/形式展示，规则设计简单可用）— 兑现度中下**：脱敏对已完成/非 active/V1 卡片整体失效致敏感字段泄露（P1，最严重）；模板复用链多处被组织隔离打穿+save-as-template 全程不可用（P1×3）；amountMatrix 边界语义不可预期（P3）、缺失字段 neq 反直觉（P3）、明细 schema 解析口径漂移（已并入相关项）、冻结设计器入口未摘（P3）。展示/脱敏与模板规则的「按节点恰当 + 简单可用」承诺在终态卡片与跨组织场景上未兑现。

