# 新增子科目：科目编码自动默认

- 日期：2026-06-14
- 范围：仅前端 `web/src/views/finance/AccountManage.vue`
- 入口：仅"新增子科目"（有上级科目的场景）；顶部"新增"一级科目保持手填

## 背景与目标

科目管理页新增子科目时，`科目编码` 输入框当前为空，用户需手动按"上级编码 + 2 位顺序号"拼写。
目标：打开"新增子科目"弹窗时，自动把编码默认填为 **上级科目下所有子科目中最大编码 + 1**，用户可改。

## 编码规则（既有约束，后端 `AccountService.CreateAsync` 已强校验）

- 子科目编码 = 上级编码 + 2 位数字，逐级递增 2 位（一/二/三/四级 = 4/6/8/10 位）。
- 同一上级下子科目不超过 99 个（2 位后缀 01–99）。

## 方案：前端纯计算

新增子科目所需数据前端已具备——`accountTree` 含完整嵌套 `children`，且已有 `findAccountById(tree, id)` 可取得上级节点。
打开弹窗时在前端算出默认编码并填入 `formData.code`，**无后端改动、无额外请求**。
并发下若编码被他人占用，后端 `CreateAsync` 既有"科目编码已存在"校验兜底，用户重载后重试即可——对一个默认值便利功能可接受。

（备选：后端新增 `GET /api/account/next-code` 接口。为一个默认值多加接口 + 一次往返，数据前端已有，判定为过度设计，不采用。）

## 详细设计

### 1. 新增纯函数 `computeNextChildCode(parentCode, children)`

- 对每个子科目：取编码去掉 `parentCode` 前缀后的 2 位后缀，转数字。
- 防御：跳过编码不以 `parentCode` 开头、或后缀非纯数字的脏数据。
- 求最大后缀 `maxSuffix`（无有效子科目时视为 0）。
- `next = maxSuffix + 1`。
- 若 `next > 99` → 返回空串 `''`（同级已满，不填非法编码）。
- 否则返回 `parentCode + String(next).padStart(2, '0')`。

纯函数、无副作用，便于推理与（如需）单测。

### 2. 接入 `handleAddChild(parent)`

`flattenAccounts` 在扁平化时把 `children` 剥掉了，传入的 `parent` 不含 children。
故用 `findAccountById(accountTree.value, parent.id)` 取回真实节点，读其 `children`，调用 `computeNextChildCode(parent.code, children)`，结果赋给 `formData.code`。

### 3. 接入 `handleSaveAndAdd()`（保存并新增）

保存成功后，下一条默认编码 = 刚保存编码的后缀 +1。
因 `loadData()` 为异步刷新且未 await，直接基于"刚成功保存的编码"递增比等树刷新更可靠；同样在后缀超 99 时留空。

## 边界规则

| 情况 | 行为 |
|------|------|
| 上级无子科目 | `上级编码 + "01"`（如 540104 → 54010401） |
| 已有子科目 | 最大后缀 +1（如 …01、…02 → …03） |
| 子科目编码有缺口 | 仍取"最大 + 1"（如仅有 …03 → …04），符合需求原话 |
| 后缀已达/将超 99 | 留空，不填非法编码 |
| 编码框 | 仍可编辑（仅给默认值）；编辑科目时 `isEdit` 下编码本就 disabled，不受影响 |

## 改动范围

- 仅 `web/src/views/finance/AccountManage.vue`：新增 1 个纯函数 + 修改 `handleAddChild`、`handleSaveAndAdd` 2 个已有函数。
- 后端、DTO、API：不改动。

## 非目标（YAGNI）

- 顶部"新增"一级科目的自动编码。
- 缺口回填（找最小可用号）——需求明确为"最大 + 1"。
- 后端 next-code 接口。
