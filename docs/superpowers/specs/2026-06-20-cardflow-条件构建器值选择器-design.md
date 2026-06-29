# CardFlow 条件构建器值选择器（enum/人员/组织）设计稿

> 日期：2026-06-20　状态：设计已确认（用户拍板：组织用 a-tree-select、缓存 composable、inOrgChain 也换选择器、取值用 id）
> 上游：CardFlow 三轮审计 §五速赢「条件构建器 enum 值下拉补 options」+ ②阻断 #2「enum/人员/组织值下拉空、选不出值（财务分流配不出还不报错）」+ #12（引用型字段降级）。本轮做 enum + 人员/组织三类。
> 本文为方案文档，**不修改代码**；结论带真实 file:line（2026-06-20 复核）。

---

## 0. 背景与缺陷（已核实现状）

`ConditionBuilder.vue` 的「值输入」按字段类型/运算符动态渲染，但三类是空的、选不出值：
- **enum**：in 多选（[:354](../../../web/src/components/cardflow/ConditionBuilder.vue:354)）、eq/neq 单选（[:366](../../../web/src/components/cardflow/ConditionBuilder.vue:366)）的 `a-select` **无任何 `:options`/`<a-select-option>`**。
- **user / org**：eq/neq 的 `a-select`（[:403-410](../../../web/src/components/cardflow/ConditionBuilder.vue:403)）同样无 options；org+inOrgChain（[:384](../../../web/src/components/cardflow/ConditionBuilder.vue:384)）降级为自由文本 `a-input`。

根因：`FieldOption{key,label,type}`（[:7-11](../../../web/src/components/cardflow/ConditionBuilder.vue:7)）**不带 enum 选项**；人员/组织列表也从未拉取注入。

### 已核实的关键事实
- **enum 选项源**：`SchemaFieldDefinition.options?: string[]`（[types/cardflow.ts:898](../../../web/src/types/cardflow.ts:898)）。4 个 designer 条件构建器各自 `props.fields.map(f => ({key,label,type}))` 丢了它；`props.fields` 即 `state.cardSchema`（`SchemaFieldDefinition[]`，带 `.options`）。
- **4 个 builder**（都喂 ConditionBuilder）：`RouteRuleCardEditor.vue:21`、`StageDefinitionEditor.vue:665`、`DynamicApprovalPolicyEditor.vue:46`、`FlowGroupConnectionEditor.vue:87`。
- **人员/组织数据源**：`getUserList(params)`→`/system/users`（`UserItem{id:number,name:string,...}`）；`getOrganizationTree()`→`/system/organizations/tree`（`OrgTreeNode{id:number,name:string,code,parentId?,children?:OrgTreeNode[]}`）。`useOrgContextStore.organizations` 只是当前用户**可切换**组织，非全量，不用。
- **取值语义（用户拍板：用 id）**：enum→选项字符串；user→用户 `id`（匹配 user 字段存的 id / `initiator.id`）；org→组织 `FID`（匹配 org 字段 id / `initiatorOrg.id` / 刚接通的 `OrgChain` 放的 FID）。求值器 `ConditionRuleEvaluator.CompareEquality` 数值/字符串通配，id/串作 value 均能命中（`inOrgChain` 用组织 FID，命中 OrgChain 的 FID 字符串）。
- **ConditionBuilder 递归**（嵌套组）+ 4 builder 复用 → 人员/组织列表用**模块级缓存一次性拉**，避免 N 次请求。
- 前端无测试运行器；验证 `vite build` + 用户 9001 live。

---

## 1. 目标 / 非目标

### 目标
1. enum 值下拉（in/eq-neq）渲染该字段 `options`，选得出值。
2. user 值控件 `a-select`（可搜索）渲染全量用户（id→姓名）。
3. org 值控件 `a-tree-select` 渲染组织树（value=FID），**eq/neq 与 inOrgChain 都用它**（替掉 inOrgChain 自由文本），与后端 OrgChain 放 FID 一致。
4. 人员/组织列表用缓存 composable 全局拉一次。

### 非目标（明确不做）
- ❌ 其它引用型字段（cardRef/account/auxiliary/bankAccount/voucherRef，审计 #12）专用选择器——本轮只 enum/user/org 三类。
- ❌ money/number 区间 between、operatorMap.number 死代码、enum {label,value} 双模型收敛（#12/3f）——各自后续。
- ❌ 不改求值器/后端、不改 `SchemaFieldDefinition`、不动条件运算符表语义（只补值控件）。

---

## 2. 设计

### 2.1 缓存 composable `useConditionRefData`（交付物 1，新建）

新建 `web/src/composables/useConditionRefData.ts`：模块级单例缓存 + 懒加载（全局只拉一次，递归/多 builder 共享）：

```ts
import { ref } from 'vue'
import { getUserList, getOrganizationTree } from '@/api/system'

export interface UserOption { value: number; label: string }
export interface OrgTreeOption { value: number; title: string; children?: OrgTreeOption[] }

const users = ref<UserOption[]>([])
const orgTreeData = ref<OrgTreeOption[]>([])
let loaded = false
let loadingPromise: Promise<void> | null = null

function mapOrgTree(nodes: any[]): OrgTreeOption[] {
  return (nodes || []).map(n => ({
    value: n.id,
    title: n.name,
    children: n.children && n.children.length ? mapOrgTree(n.children) : undefined,
  }))
}

export function useConditionRefData() {
  function ensureLoaded(): Promise<void> {
    if (loaded) return Promise.resolve()
    if (loadingPromise) return loadingPromise
    loadingPromise = (async () => {
      try {
        const [userRes, orgRes] = await Promise.all([
          getUserList({ page: 1, pageSize: 1000 }),
          getOrganizationTree(),
        ])
        const userItems: any[] = Array.isArray(userRes) ? userRes : (userRes?.items || [])
        users.value = userItems.map(u => ({ value: u.id, label: u.name }))
        const orgNodes: any[] = Array.isArray(orgRes) ? orgRes : (orgRes?.items || [])
        orgTreeData.value = mapOrgTree(orgNodes)
      } catch {
        // 失败列表留空、不抛；标记已加载避免反复重试
      } finally {
        loaded = true
      }
    })()
    return loadingPromise
  }
  return { users, orgTreeData, ensureLoaded }
}
```

> 防御性 `Array.isArray(res) ? res : res?.items`（沿用 orgContext store 口径，应对分页/数组两种返回）。`getUserList` 取大 pageSize 拉全量供选择器（用户量大时 a-select 开 `show-search` 前端过滤）。

### 2.2 `ConditionBuilder` 渲染真选择器（交付物 2）

- `FieldOption` 加 `options?: string[]`（[:7-11](../../../web/src/components/cardflow/ConditionBuilder.vue:7)）。
- `<script setup>`：`import { onMounted } from 'vue'`（现仅 computed/watch）；`import { useConditionRefData } from '@/composables/useConditionRefData'`；`const { users, orgTreeData, ensureLoaded } = useConditionRefData(); onMounted(() => ensureLoaded())`。
- 加助手 `function getFieldOptions(fieldKey: string): string[] { return props.fields.find(f => f.key === fieldKey)?.options || [] }`。
- 值控件模板（[:332-421](../../../web/src/components/cardflow/ConditionBuilder.vue:332)）改动：
  - **enum + in**（:354 多选）：补 `<a-select-option v-for="opt in getFieldOptions(field)" :key="opt" :value="opt">{{ opt }}</a-select-option>`。
  - **enum + eq/neq**（:366 单选）：同上补 options。
  - **user**（新，替原 :403 user 分支）：`v-else-if="getFieldType(field)==='user'"` → `<a-select show-search option-filter-prop="label" :options="users" .../>`（value=用户 id）。
  - **org**（新，替原 :384 inOrgChain `a-input` 与 :403 org 分支）：`v-else-if="getFieldType(field)==='org'"` → `<a-tree-select :tree-data="orgTreeData" show-search tree-node-filter-prop="title" :value="..." @change="..." placeholder="选择组织" .../>`（value=组织 FID；eq/neq 与 inOrgChain 共用）。
  - text/money/number/date/default 分支不变。
- 递归子 ConditionBuilder 不需传列表（composable 模块级共享）。

### 2.3 4 个 builder 透传 enum 选项（交付物 3）

各 builder 的 `computed<FieldOption[]>` mapper 加 `options: field.options`：
- `RouteRuleCardEditor.vue:21`、`StageDefinitionEditor.vue:665`、`DynamicApprovalPolicyEditor.vue:46`、`FlowGroupConnectionEditor.vue:87`。
- 形如 `props.fields.map(f => ({ key: f.key, label: f.label || f.key, type: f.type, options: f.options }))`。
- 计划阶段逐一核对：其 `props.fields` 确为带 `.options` 的 `SchemaFieldDefinition[]`；若某 builder 的 fields 已是无 options 的 FieldOption，则 `field.options` 为 undefined、enum 该处仍空（无回归，非本轮目标）。

---

## 3. 兼容性与风险

- **纯前端**：不改求值器/后端/schema；不动运算符表语义；只补值控件 + 透传 enum 选项 + 一个缓存 composable。
- **取值变化（修正方向）**：enum/user/org 由「空、选不出」变「选得出、存 id/选项串」。inOrgChain 由自由文本改组织树选 FID——与后端 OrgChain 放 FID 一致（org-chain 接通），更不易错。**存量已配条件**：原 inOrgChain 自由文本存的是字符串值，新控件存 FID（number），二者求值都走 CompareEquality 数值/串通配，存量字符串 FID 仍命中；存量 enum/user/org 空值不受影响。
- **缓存 composable**：模块级单例、懒加载、失败留空不抛、`loaded` 防重试；递归/多 builder 共享一次请求。
- **a-tree-select / a-select 选项量**：用户/组织量大时 `show-search` 前端过滤；getUserList 取大 pageSize（计划阶段确认后端上限，必要时分页/远程搜索——本轮先全量+前端搜索）。
- 风险中低：ConditionBuilder 值控件局部改 + 一个新 composable + 4 处一行；改动集中、无跨端契约变化。

---

## 4. 验证（前端无测试运行器）

- **`vite build` 绿**（`cd web && npm run build`=`vite build`，不链 vue-tsc）——确认无引入语法/类型错。
- **改动文件 stylelint 0 / 无裸 hex 回潮**（值控件改在模板，新 composable 无 `<style>`）。
- **用户 9001 live 逐项验**（不自起 vite）：
  1. enum 字段条件 → 值下拉出该字段的 options，eq 单选 / in 多选都能选。
  2. user 字段条件 → 值 a-select 出全量用户、可搜索、选中存 id。
  3. org 字段条件（eq/neq 与「属于组织链」）→ 值 a-tree-select 出组织树、可搜索、选中存 FID。
  4. 配好的 enum/org 分流条件，用预演或真实流转验证命中正确（org 选祖先 → inOrgChain 命中）。

---

## 5. 任务分解预览（供 writing-plans）
1. `useConditionRefData` composable（缓存拉 getUserList + getOrganizationTree、映射 users/orgTreeData）。
2. ConditionBuilder：`FieldOption+options`、`getFieldOptions` 助手、`onMounted ensureLoaded`、enum/user/org 三类值控件渲染（org 用 a-tree-select、含 inOrgChain）。
3. 4 个 builder mapper 各加 `options: field.options`（逐一核对 props.fields 类型）。
4. 收尾：`vite build` 绿；交用户 live 验四项。

> 前端无 TDD，inline 执行 + build 检查点 + 用户 live。转 writing-plans 细化。
