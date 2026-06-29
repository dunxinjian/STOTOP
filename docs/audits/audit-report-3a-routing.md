## CardFlow「节点路由」领域缺陷评审报告

**一句话结论**：路由的求值引擎本身基本正确，但**配置面（UI/预演）与运行时（求值上下文/退回/发布门禁）系统性脱节**——组织链/角色维度是隐性死功能、空条件被当 catch-all、预演与运行时三处分叉、退回按排序号而非真实路由图、发布门禁不查环/不查字段——导致「按条件送到对的下一节点」「配置易上手」「运行时与预览一致」三项核心意图均被实质性破坏。

**分级计数**（去重合并后共 16 条）：
- 严重 1 条
- 高 6 条
- 中 8 条
- 低 2 条

按类别：正确性/完整性 bug 7 条 · 设计缺陷 5 条 · UX 摩擦 3 条 · 智能化机会 1 条（见尾部）

> 去重说明：原 23 条经合并为 16 条。合并项：①「OrgChain/角色四字段永不赋值」(运行时) 与「ConditionBuilder org+inOrgChain UI 诱导」(前端) 同根，合为 #1；②「预演与运行时三处分叉」(后端) 与「PathPreviewPanel 写死五字段+三处分叉」(前端) 同根，合为 #5；③「AuditSnapshot 取值口径不一致」探针1/探针3 两条同根，合为 #11；④「画布恒画『默认顺序』线误导」探针2/探针3 两条同根，合为 #9。

---

### 一、正确性 / 完整性 bug

#### 1. 组织链/角色/关系四字段运行时永不赋值，相关路由条件恒判 false（UI 还主动诱导配置） 【严重】【数据完整性 + 设计缺陷】
- 位置：`src/STOTOP.Module.CardFlow/Services/ConditionEvaluationContextBuilder.cs:28`（构造点）；`ConditionRuleEvaluator.cs:164-169/299-311`（读取点）；`web/src/components/cardflow/ConditionBuilder.vue:96-102/384-391`（UI 入口）
- 问题：`BuildAsync` 构造上下文时只填 CardData/SourceContext/Initiator/InitiatorOrg/CurrentStageResult/DetailSummary，**从不给 OrgChain/RoleCodes/RoleNames/Relations 赋值**（全 src 无任何写入点），但求值器明确读取这四字段当真源。`inOrgChain`、`roles.code contains`、`roles.name`、`relations.*` 条件因空集合恒判 false。前端 `operatorMap.org` 还专门提供「属于组织链」操作符并渲染「组织链编码或组织ID」输入框，把死功能当活功能卖给配置者。
- 影响：凡按组织链/角色配置的路由分支运行时永远不命中，卡片静默走默认/兜底或判流程结束。零报错、零提示，属「配了等于没配」的最难排查缺陷，直接污染业务路由。
- 建议：二选一并三方对齐——(a) 接通：注入 `AuthService.GetUserRoleCodesAsync` 填角色、用 `ApproverResolver.ResolveOrgChainAsync` 同源逻辑从 FOrgId 上溯填 OrgChain、按业务填 Relations；(b) 删除：从 `operatorMap.org` + 模板输入分支 + 求值器 `inorgchain`/orgChain/roles/relations 分支整体移除，并在保存校验拒绝引用这些字段。接通前至少给 org+inOrgChain 加「当前版本暂不支持」禁用提示。
- 置信度：高（证据链闭合，已逐字核对构造点与读取点）

#### 2. 非默认规则的空条件被当作「默认匹配」，变成隐形 catch-all 【高】【正确性 bug】
- 位置：`src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:13`；前端 `RouteRuleCardEditor.vue:44-49`；落库 `FlowDefinitionService.SaveRouteRulesAsync` / 校验 `ValidateRouteRulesAsync:733`
- 问题：`Evaluate` 开头 `if (IsNullOrWhiteSpace(conditionJson)) return Match("空条件默认匹配")`。前端在 `conditions.length===0` 时回写 `conditionJson:null`，后端原样落库，发布校验**不拦截非默认规则空条件**（`ValidateRouteRulesAsync` 仅有「默认分支不得带条件」的对称校验，缺「非默认分支必须带条件」的对偶校验）。运行时 `StageRouteResolver:65-87` 遍历到该规则即 `Matched=true` 立刻 break。
- 影响：配置者新建一条尚未填条件的分支（或误删条件），上线后成为「无条件命中」的最高优先级 catch-all（若 FPriority/FID 较小），截胡所有真实条件规则与默认分支，卡片全部涌向它。静默错路由，行为上难以察觉。
- 建议：非默认规则的空/null conditionJson 返回 `Matched=false`（永不命中）；「空条件默认匹配」语义只保留给真正的默认分支；`SaveRouteRulesAsync`/`ValidateRouteRulesAsync` 增加「非默认规则必须有非空 conditions」校验。
- 置信度：高

#### 3. 空条件组 `conditions:[]` 恒匹配（与 #2 同根，嵌套空组可达） 【中】【正确性 bug】
- 位置：`src/STOTOP.Module.CardFlow/Services/ConditionRuleEvaluator.cs:73`；前端 `ConditionBuilder.vue:177-181`（addNestedGroup 推空组）
- 问题：`EvaluateGroup` 内 `if (childResults.Count == 0) return Match("空条件组默认匹配")`。真正可达路径是「顶层非空、内含一个 `addNestedGroup` 留空的 `{logic:'and',conditions:[]}`」：整段序列化，递归到内层空组即 Match。`or` 逻辑下空组令整组恒真。（注：原指控「顶层空组序列化为非空 JSON」措辞有误——顶层空组 length===0 写 null，走 :14 早返回；不影响结论。）
- 影响：配置者拖出空条件组当占位，规则即变 catch-all；嵌套空组在 or 下污染父组。属求值语义陷阱。
- 建议：空条件组返回 `Matched=false`，或保存校验拒绝空组；至少 or 逻辑下空组不贡献 true。
- 置信度：高

#### 4. 带 TypeErrors 的命中被跳过，优先级语义被破坏（类型笔误被「吃掉」走错分支） 【中】【正确性 bug】
- 位置：`src/STOTOP.Module.CardFlow/Services/StageRouteResolver.cs:81`（同型 guard 见 `CardFlowPathPreviewService.cs:164`、`DynamicStagePolicyResolver.cs:101`）
- 问题：选中逻辑 `if (evaluation.Matched && evaluation.TypeErrors.Count == 0)`。当高优先级规则的某 or 兄弟条件类型不兼容（`CompareOrdered` :273 / `BetweenValue` :336 塞 TypeErrors）时，即使该规则经 or 真命中也不 break，结果让位给更低优先级规则或默认分支。`FFailurePolicyJson` 字段从未被任何路由代码读取。（注：单叶 gt/between 类型错时本就 Matched=false，`TypeErrors.Count==0` 子句真正改变行为的是 or 组场景。）
- 影响：条件配置中的类型笔误不暴露为错误，而被吞掉变成走错分支，且高优先级让位低优先级，优先级序被打乱。排障极难（Candidates 已记 TypeErrors 但卡片照常前进）。
- 建议：TypeErrors 非空时进入显式失败/挂起态或回退 LegacyFallback 并审计告警；至少 Reason 标注「规则X因类型错误被忽略」；接通 `FFailurePolicyJson` 配置 fail-fast/fall-through。
- 置信度：高

#### 5. 预演与运行时上下文三处分叉 + 预演表单写死五字段——「预演通过」≠「上线走对」 【高】【设计缺陷】
- 位置：`src/STOTOP.Module.CardFlow/Services/CardFlowPathPreviewService.cs:255`（BuildPreviewContext）；运行时 `ConditionEvaluationContextBuilder.cs:44-47`；前端 `web/src/components/cardflow/designer/PathPreviewPanel.vue:13/42-48`
- 问题：(a) **InitiatorOrg 键名分叉**——预演写 `["orgId"]`，运行时写 `["id"]`，两侧必有一侧失配；(b) **DetailSummary 缺失**——预演 `BuildPreviewContext` 完全不构造，任何 `detailSummary.amount/rowCount` 条件预演恒 `Exists=false`，运行时却可能命中（exists/notExists 类算子下 notExists 预演反而恒真，加剧方向不可预测）；(c) **来源匹配单/双键**——运行时按 `FFromStageKey OR FFromStageDefinitionId`（`StageRouteResolver.cs:48-49`），预演只按 `FFromStageKey`（:108），仅靠 DefinitionId 关联的边预演漏看；(d) **预演表单写死** `amount/feeType/hasExpenseRequest/hasLoan/cardStatus` 五项，与当前流程实际 schema 无关，绝大多数流程预演时引用字段根本不存在、条件恒不匹配。
- 影响：「配完即验」承诺被系统性破坏。按组织路由（键名失配）、大额报销路由（DetailSummary 缺失）、仅 DefinitionId 关联的边、以及非这五字段的任何流程——预演结果与真实路径无关，配置者据此发布会被误导。
- 建议：预演表单按当前 `cardSchema` 动态生成字段（复用 ConditionBuilder 字段/类型元数据）；后端抽出共享纯函数 `BuildContext(cardData, details, source, initiator, org)` 供预演与运行时共用，统一 InitiatorOrg 键为 `id`、补建 DetailSummary、来源匹配补 DefinitionId 双键。
- 置信度：高

#### 6. 退回目标/作废下游纯按 FSortOrder，与运行时按路由图选路是两套拓扑 【高】【正确性 bug】
- 位置：`src/STOTOP.Module.CardFlow/Services/ReturnToStageRuntime.cs:26`（ResolvePreviousTarget）/:60（ResolveSpecifiedTarget）/:96（SupersedeDownstreamCompletedStages）
- 问题：退回目标/指定退回/作废下游三处全部以 `FSortOrder` 为唯一拓扑依据，完全不读 `CfStageRouteRule`。但正向流转 `StageRouteResolver` 按路由规则跳（`FToStageKey` 定位），可跳过中间 sortOrder 节点、可让小 sortOrder 节点排后。两套口径在条件路由非线性时必然背离。`CfRouteDecisionSnapshot` 已持久化真实路径却未被退回逻辑利用。无任何「规则模式非线性路由+退回」组合测试覆盖。
- 影响：退回后再正向前进可能走到与原路径不同的分支，使按 sortOrder 的作废与新路径不匹配（作废了仍会经过的节点、或漏作废实际跳过的）。退回语义在带条件分支流程上不可靠，造成正确性/数据问题。
- 建议：退回目标基于「本轮实际已完成实例的真实前驱链」——由 `CfRouteDecisionSnapshot`（FFromStageKey→FToStageKey, 本轮 FRound）反推真实有向路径，退回目标=路径上的上一人工节点，作废下游=路径上该目标之后的实例，与正向路由共用同一拓扑口径。
- 置信度：高

#### 7. 线性兜底分支用旧 ConditionEvaluator，同一 JSON 条件可访问字段集不一致 【中】【一致性漂移】
- 位置：`src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:1672`；`ConditionEvaluator.cs:28-34`（降级仅塞 CardData）
- 问题：无 active 路由规则时走线性兜底，调旧 `ConditionEvaluator`，对 JSON 条件降级时**只塞 CardData**，不填 DetailSummary/SourceContext/Initiator/InitiatorOrg/CurrentStageResult。同一份流程定义，仅因「是否配了任意 active 规则」即在规则模式/线性兜底间切换，引用 `detailSummary.*`/`source.*`/`initiator.*` 的 JSON 条件可见字段集不同、求值结果可逆转。属已知 5 套 evaluator 未收敛的具体落点。
- 影响：行为不可预测（纯 card.* 条件不受影响，故影响面有限）。
- 建议：兜底路径也通过 `ConditionEvaluationContextBuilder.BuildAsync` 构造完整上下文；长期废弃旧 ConditionEvaluator 字符串 DSL 分支，统一到条件树求值器。
- 置信度：高

---

### 二、设计缺陷

#### 8. 发布校验不查环/不可达/终端节点，非法环流程可发布上线（同仓 Orchestration 却查环，口径不对称） 【高】【设计缺陷】
- 位置：`src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:733`（ValidateRouteRulesAsync）；对照 `OrchestrationEngineService.cs:625/668-689`（Kahn 拓扑查环）
- 问题：`ValidateRouteRulesAsync` 仅校验来源节点存在、单默认分支+优先级不重、目标节点存在，**无任何图级校验**（环检测/可达性/非终端节点必须有出边）。运行时 `StageRouteResolver` 也无防环，遇无出边节点按 :58 直接判「流程结束」提前 CompleteCard。唯一环检测只在设计器预演（口径还与运行时不同源，用户未必跑）。
- 影响：可发布含 A→B→A 环、不可达节点、或非终端节点无出边的流程。auto 节点环会自动反复 re-route（每步写一条 CfRouteDecisionSnapshot、状态机错乱、生命周期无法正确终止）；无出边节点导致本该继续的卡片被提前完成。属生命周期级缺陷。
- 建议：`ValidateRouteRulesAsync` 增加图级校验——①从起点做可达性遍历，用 Kahn/DFS 检测环并报「流程存在环：A→B→A」；②校验每个非终端节点至少一条出边或显式标记终端；③报告不可达节点。直接复用 `OrchestrationEngineService` 拓扑算法保持口径一致。
- 置信度：高

#### 9. 画布对每对相邻节点恒画「默认顺序」虚线边，但运行时有规则即忽略线性——心智模型背离 【中】【UX 摩擦】
- 位置：`web/src/components/cardflow/designer/FlowStateCanvas.vue:47`（linearEdges）；运行时 `StageRouteResolver.cs:31-36/:58`
- 问题：`linearEdges` 对每对相邻节点无条件生成 `label='默认顺序'` 虚线边（即便来源节点已配 active 规则），与条件边叠加显示。但运行时一旦判定该版本存在任意 active 规则即整版进入规则模式，完全不按 sortOrder 线性流转，无出边规则的节点直接判流程结束。
- 影响：配置者误以为没配条件边的节点仍按线性边往下走，实际只要全流程有一条规则该节点就提前终止。与 #8「发布不查终端无出边」叠加放大。（注：`RuleHealthPanel.vue:95-109` 已有同场景「死路节点」advisory 告警同屏呈现，故为「误导线+正确告警」双信号不一致，非无防护静默陷阱，定级降为中。）
- 建议：版本存在 active 规则时，linearEdges 隐藏/置灰并标注「已启用条件路由，线性顺序不生效」；对无出边规则节点画布高亮告警；将 RuleHealthPanel 死路检查升级为发布阻断（见 #13）。
- 置信度：高

#### 10. 两份决策快照口径冲突：resolver 自带快照被丢弃，入库另起 ContextBuilder 二次查库重算 【中】【设计缺陷】
- 位置：`src/STOTOP.Module.CardFlow/Services/FlowEngineService.cs:1843`（WriteRouteDecisionSnapshotAsync）；`StageRouteResolver.cs:130-139`（FinalizeResult）
- 问题：`FinalizeResult` 已序列化一份 `DecisionSnapshotJson`（含 source+全候选）随 routeResult 返回，但 `WriteRouteDecisionSnapshotAsync` **弃之不用**，改 `new ConditionEvaluationContextBuilder` 二次查库重建上下文，再生成第二份只含脱敏 ConsumedFields 的快照入库。结果：①入库快照与 resolver 那份口径不同，`routeResult.DecisionSnapshotJson` 全 src 无消费者（纯算浪费+排障误导）；②每次流转多一轮 CfCardDetail 明细聚合 + FDataJson 解析查询。
- 影响：维护者读 resolver 代码会以为入库的是那份快照，实际是另一份；外加每次路由多一轮明细/卡片查询的性能浪费。
- 建议：让 resolver 把已构建的 `ConditionEvaluationContext` 回传给引擎复用，或删除 FinalizeResult 里无人用的 DecisionSnapshotJson——无论哪种均消除二次查库。
- 置信度：高

#### 11. AuditSnapshot.ResolveField 与求值器 ResolveField 取值口径不一致 【中】【一致性漂移】
- 位置：`src/STOTOP.Module.CardFlow/Services/AuditSnapshotPolicyService.cs:69`；对照 `ConditionRuleEvaluator.cs:174-184/191-212`
- 问题：两处各写一份 root→source 映射且不一致：(1) 求值器支持 `card/carddata`、`source/sourcecontext` 别名，快照版只认精确 `card`/`source`（无 sourcecontext 别名）；(2) 求值器用 `TryReadDictionaryPath` 按 `.` 逐层下钻支持嵌套，快照版 `source.TryGetValue(key)` 单层不下钻；(3) 求值器整段大小写不敏感，快照依赖字典自身 comparer（因均为 OrdinalIgnoreCase 单键查找实际无碍，最弱一条）。
- 影响：用 `sourcecontext.x` 别名或嵌套路径（`card.parent.child`）的条件，求值时命中，写快照取值返回 null → fields 标 present=false，审计/复盘看到「没取到值却命中了」，误导排障。
- 建议：`AuditSnapshotPolicyService.ResolveField` 复用求值器同一 `ResolveField/TryReadDictionaryPath`（抽共享静态 FieldResolver），杜绝两份口径漂移。
- 置信度：高

#### 12. ConditionBuilder 操作符表与 schema 字段类型未对齐（number 死代码 + 五种引用型字段降级 text + 金额无 between） 【中】【一致性漂移】
- 位置：`web/src/components/cardflow/ConditionBuilder.vue:45`（operatorMap）；模板 :343-351（number 值输入死分支）；对照 `SchemaFieldEditor.vue:46-57`
- 问题：(a) `operatorMap.number` 是死代码（schema 永不产出 number 类型）；(b) cardRef/account/auxiliary/bankAccount/voucherRef 五种业务类型 `getOperators` 回退 text，落到「等于/包含/开头是」+裸文本框，对会计科目/银行账户毫无意义；(c) money/number 表没有 between，而求值器支持 between（`ConditionRuleEvaluator.cs:134/313`），金额区间可视化配不出，被迫拆成两条 gt/lt。
- 影响：业务引用类字段只能当字符串比，易配错（科目用「包含」比内部编码）；金额区间体验粗糙。属中等摩擦+轻度死代码。
- 建议：为五种引用型字段增专用操作符集（至少 eq/neq/empty/notEmpty）+ 对应值选择器（复用 AccountSelector/AuxiliarySelector）；money/number 表补 between；删除或对接 number 口径使键集与 schema 类型一一对应。
- 置信度：高

---

### 三、UX 摩擦 / 数据完整性

#### 13. 发布校验只查路由「缺默认分支/空条件」，不查条件引用字段是否存在或类型是否匹配 【中】【数据完整性】
- 位置：`web/src/views/cardflow/FlowDefinitionEditPage.vue:1504`（validateCardFlow2Config）；对照 `RuleHealthPanel.vue:171/175/117`
- 问题：RuleHealthPanel 已算出 error 级问题（字段不存在 :171、类型不匹配 :175、循环路径 :117），但**没有任何东西把它们接进发布门禁**。发布校验对路由只查空条件、来源/目标存在、缺默认分支。条件引用已删字段或对文本字段用数值比较均可顺利发布。
- 影响：配置者删字段/改类型后，引用该字段的路由条件运行时静默失效（恒不匹配或被跳过，落默认分支），RuleHealthPanel 标红却不拦发布，问题带入生产。健康检查与发布门禁脱节，削弱「配完即验」。
- 建议：把 RuleHealthPanel 的 error 级项纳入 `validateForPublish` 阻断条件，或发布对话框汇总展示并要求确认；字段删除时联动检测被引用的路由条件并提示。
- 置信度：高

#### 14. 克隆/复制版本时默认分支与优先级约束不在克隆期校验，可静默搬运非法配置 【中】【数据完整性】
- 位置：`src/STOTOP.Module.CardFlow/Services/FlowDefinitionService.cs:677`（CloneRouteRulesAsync）；三条克隆路径草稿自动克隆 :913 / 版本克隆 :1007 / 模板复制 :1199
- 问题：三条克隆路径均复用 `SaveRouteRulesAsync`，只做 key 命中校验，不校验单默认/优先级（那是 `ValidateRouteRulesAsync` 职责，仅 PublishAsync 跑）。源版本若历史存在双默认/优先级冲突，克隆出的草稿同样带非法配置且草稿态长存。disabled 源规则被原样克隆，而 ValidateRouteRulesAsync 只查 active，disabled 规则的悬空目标永不被发现。比指控更甚：`CopyCurrentVersionToTemplateAsync` 生成的是 published 版本却跳过校验，可直接绕过。
- 影响：非法/悬空（disabled）路由配置可经克隆链静默扩散到多个新流程/模板，active 非法配置下次发布才暴露、disabled 悬空目标永不暴露。数据完整性渐进漂移。
- 建议：克隆完成后对目标版本跑一次与发布同口径的 `ValidateRouteRulesAsync`（或至少校验单默认/优先级），非法即提示；对 disabled 路由克隆时一并校验其 to 目标存在。
- 置信度：高

#### 15. 画布连线不区分 next/branch 手柄，从任一连接点拖出的边都生成同质「条件分支」 【低】【UX 摩擦】
- 位置：`web/src/components/cardflow/designer/FlowStateCanvas.vue:176`（handleConnect）；连线回调 `FlowDefinitionEditPage.vue:409-421`
- 问题：节点渲染了 next（底，默认）/branch（右，条件）手柄，routeEdges 也按 isDefault 选句柄渲染，但 `handleConnect` **丢弃了 VueFlow Connection 自带的 sourceHandle 字段**，一律建 routeName='条件分支'、isDefault 仅由「是否已存在默认分支」隐式决定。
- 影响：用户从底部「默认」手柄拖线系统也不认，「从默认口连=默认边」预期落空，需事后到规则卡手动开默认开关。打磨级摩擦（边能正常创建）。
- 建议：`handleConnect` 读取 `connection.sourceHandle`，branch→条件分支、next→默认分支（已有默认则提示替换），让手柄语义贯通到生成的边。
- 置信度：高

#### 16. 规则卡 isDefault 开关清空已配条件无确认，而逐条删除却有 popconfirm——防护强弱倒置 【低】【UX 摩擦】
- 位置：`web/src/components/cardflow/designer/RouteRuleCardEditor.vue:44/47/67-69`；对照 `ConditionBuilder.vue:282-296/427-435`（删除有 popconfirm）；`FlowDefinitionEditPage.vue:1276`（buildRouteRequests 已对 isDefault 强制 null）
- 问题：打开 isDefault 开关经 patch→`next.conditionJson=null` 静默清空整组已配条件，**无任何「将丢弃已配条件」确认**；而 ConditionBuilder 删除单条条件却有 popconfirm。破坏性更大的动作防护更弱。
- 影响：误触默认开关会丢掉整棵条件树（仅靠页面级 history），与逐条删除有 popconfirm 形成体验落差。边缘但真实的数据丢失摩擦（保存前未持久化）。
- 建议：开启 isDefault 且已有非空条件时弹确认「切换为默认分支将清空当前条件，是否继续」；或前端保留 conditionJson 仅在保存时按 isDefault 落 null（`buildRouteRequests` 现状已证可行），便于来回切换不丢配置。
- 置信度：高

---

### 智能化机会（衍生）
- **路由解释与字段命名空间智能提示**：求值器支持 7+ 命名空间（card/detailSummary/source/initiator/initiatorOrg/currentStageResult/orgChain/roles），但 ConditionBuilder 字段下拉只枚举 schema 卡片字段（`ConditionBuilder.vue:309`），「大额报销按明细合计金额分流」（意图③明细聚合驱动卡片流转的核心场景）在可视化里**根本配不出**，逼配置者手改 conditionJson，而 `parseCondition` 对非法 JSON 静默清空原内容。建议：字段下拉增加分组选项（卡片字段/明细聚合/发起信息/当前节点结果），每类预置正确的 type→操作符映射；手改 JSON 解析失败时保留原文本+错误定位而非静默清空。这是把求值器既有能力转化为「可上手智能配置」的最高价值缺口（实质为高优先级 UX 摩擦，归此处因兼具智能化提升空间）。

---

### 该领域设计评判（意图②④兑现度）

逐项对照「按条件把卡片送到对的下一节点、配置易上手、删改不悬空、运行时与预览一致」：

- **「按条件送到对的下一节点」——部分破坏**。求值引擎本身（比较/类型/嵌套下钻/逻辑组）实现正确，但四类静默错路由叠加：组织链/角色维度恒 false（#1）、空条件/空组变 catch-all（#2/#3）、类型笔误被吞改走低优先级（#4）、退回按排序号而非真实路由图（#6）。条件能「送」，但在非线性/组织维度/误配场景下**送错且无声**。

- **「配置易上手」——明显落空**。UI 暴露了运行时不支持的 inOrgChain（#1），最有价值的明细聚合路由配不出（智能化机会），引用型字段降级字符串比（#12），手柄语义不贯通（#15），破坏性动作防护倒置（#16）。配置者形成的心智模型与系统实际能力多处错位。

- **「删改不悬空」——有缺口**。发布门禁不查字段存在/类型/环/终端（#8/#13），克隆链静默搬运非法与 disabled 悬空配置（#14）。删字段、配环、克隆旧错都能带病上线。

- **「运行时与预览一致」——系统性破坏**。预演与运行时三处分叉 + 预演表单写死五字段（#5），画布「默认顺序」线与运行时纯规则流转背离（#9），两份决策快照口径冲突（#10/#11），线性兜底与规则模式字段集不一致（#7）。这是该领域最集中的问题域——**「配完即验」承诺当前对绝大多数真实流程不成立**。

**总评**：路由引擎内核是健康的（求值算子正确、有候选/快照/审计意识），但围绕它的「配置面—预演—发布—退回—审计」五个外环全部存在与运行时脱节的口径漂移，根因高度集中在 **多套上下文构造/求值/取值逻辑各写一份、未收敛为单一真源**（ContextBuilder vs BuildPreviewContext vs ConditionEvaluator vs AuditSnapshot.ResolveField；StageRouteResolver 路由图 vs ReturnToStageRuntime 的 sortOrder；resolver 快照 vs 入库快照）。冻结决策方面：#1（OrgChain/inOrgChain）标记 `isKnownFrozen=true`——但这并非合理冻结，而是「UI 已上线、运行时未接通」的危险半成品，应优先「接通或下架并加禁用提示」而非维持现状。
