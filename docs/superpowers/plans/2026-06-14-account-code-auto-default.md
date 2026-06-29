# 新增子科目编码自动默认 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 打开"新增子科目"弹窗时，把 `科目编码` 默认填为"上级科目下最大子编码 + 1"，用户可改。

**Architecture:** 纯前端方案。所需数据前端已有（`accountTree` 含完整嵌套 `children`，已有 `findAccountById` 取节点）。新增 2 个纯函数算编码，接入 `handleAddChild`（打开弹窗）与 `handleSaveAndAdd`（保存并新增续填）。后端/DTO/API 不动。

**Tech Stack:** Vue 3 `<script setup>` + TypeScript + ant-design-vue。无前端测试运行器——纯函数逻辑用一次性 Node 脚本断言验证，集成用 `npm run type-check` + `npm run build` + 手动冒烟。

**唯一改动文件:** `web/src/views/finance/AccountManage.vue`

---

## 关键事实（实现者须知）

- 编码规则（后端 `AccountService.CreateAsync` 已强校验，前端只负责给默认值）：子科目编码 = 上级编码 + 2 位数字（一/二/三/四级 = 4/6/8/10 位），同级 ≤ 99 个（后缀 01–99）。
- `handleAddChild(parent)` 收到的 `parent` 是 `flattenAccounts` 扁平化后的行，**已剥离 `children`**（见 `flatten` 里的 `const { children, ...rest } = node`）。要拿子科目必须用 `findAccountById(accountTree.value, parent.id)` 取回真实树节点再读 `.children`。
- `parent` 行带有 `.code` / `.category` / `.balanceDirection`（`...rest` 透传），可直接用。
- 范围：**仅新增子科目**。顶部"新增"一级科目（`handleAdd`，无上级）不自动填，保持手填。
- `handleSaveAndAdd` 同时服务"新增一级"和"新增子科目"两种续填；仅当有上级（`savedParentId` 真值）时才自动续填编码。

---

## Task 1: 验证纯函数逻辑（一次性 Node 断言，不入库）

先用可执行断言确认算法正确，再落到 `.vue`。此脚本临时存在，Task 2 删除、不提交。

**Files:**
- Create（临时）: `web/tmp-account-code-check.mjs`

- [ ] **Step 1: 写一次性验证脚本**

创建 `web/tmp-account-code-check.mjs`：

```js
// 把 2 位顺序号拼成完整子科目编码；非法或超 99（同级上限）返回空串
function buildChildCode(parentCode, suffix) {
  if (!Number.isInteger(suffix) || suffix < 1 || suffix > 99) return ''
  return parentCode + String(suffix).padStart(2, '0')
}

// 计算"上级科目下最大子编码 + 1"；无有效子科目时返回首个子编码（…01）
function computeNextChildCode(parentCode, children) {
  let maxSuffix = 0
  for (const child of children || []) {
    const childCode = String(child?.code ?? '')
    if (!childCode.startsWith(parentCode)) continue
    const tail = childCode.slice(parentCode.length)
    if (!/^\d{2}$/.test(tail)) continue
    const n = parseInt(tail, 10)
    if (n > maxSuffix) maxSuffix = n
  }
  return buildChildCode(parentCode, maxSuffix + 1)
}

let failed = 0
function eq(actual, expected, name) {
  const ok = actual === expected
  if (!ok) failed++
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}: got "${actual}", want "${expected}"`)
}

// computeNextChildCode
eq(computeNextChildCode('540104', []), '54010401', '无子科目→01')
eq(computeNextChildCode('540104', [{ code: '54010401' }, { code: '54010402' }]), '54010403', '连续→最大+1')
eq(computeNextChildCode('540104', [{ code: '54010403' }]), '54010404', '缺口→最大+1')
eq(computeNextChildCode('540104', [{ code: '54010405' }, { code: '54010402' }]), '54010406', '乱序→最大+1')
eq(computeNextChildCode('540104', [{ code: '54010409' }]), '54010410', '09→10补零')
eq(computeNextChildCode('540104', [{ code: '54010499' }]), '', '满99→留空')
eq(computeNextChildCode('540104', [{ code: '999999' }, { code: '5401040X' }, { code: '54010402' }]), '54010403', '脏数据跳过')

// buildChildCode 边界
eq(buildChildCode('540104', 0), '', 'suffix=0→空')
eq(buildChildCode('540104', 100), '', 'suffix=100→空')
eq(buildChildCode('540104', NaN), '', 'suffix=NaN→空')
eq(buildChildCode('540104', 1), '54010401', 'suffix=1→01')

// "保存并新增"的递增逻辑（基于已存编码）
const bump = (code) => buildChildCode(code.slice(0, -2), parseInt(code.slice(-2), 10) + 1)
eq(bump('54010401'), '54010402', 'bump 01→02')
eq(bump('54010409'), '54010410', 'bump 09→10')
eq(bump('54010499'), '', 'bump 99→空')

console.log(failed === 0 ? '\nALL PASS' : `\n${failed} FAILED`)
process.exit(failed === 0 ? 0 : 1)
```

- [ ] **Step 2: 运行并确认全部通过**

Run: `node web/tmp-account-code-check.mjs`
Expected: 末行输出 `ALL PASS`，退出码 0。

> 若有 FAIL：修脚本里的函数体直到全 PASS——Task 2 要把**完全相同**的函数体搬进 `.vue`。

---

## Task 2: 落地两个纯函数 + 接入 handleAddChild

把验证过的函数搬进组件并接到"新增子科目"。函数与首个调用点同一次提交，避免 `vue-tsc` 报未使用符号。

**Files:**
- Modify: `web/src/views/finance/AccountManage.vue`（在 `findAccountById` 之后新增函数；改 `handleAddChild`）
- Delete（临时）: `web/tmp-account-code-check.mjs`

- [ ] **Step 1: 新增两个纯函数**

在 `findAccountById` 函数之后（约 512 行，`const formRules` 之前）插入：

```ts
// 把 2 位顺序号拼成完整子科目编码；非法或超 99（同级上限）返回空串
function buildChildCode(parentCode: string, suffix: number): string {
  if (!Number.isInteger(suffix) || suffix < 1 || suffix > 99) return ''
  return parentCode + String(suffix).padStart(2, '0')
}

// 计算"上级科目下最大子编码 + 1"；无有效子科目时返回首个子编码（…01）
function computeNextChildCode(parentCode: string, children: any[]): string {
  let maxSuffix = 0
  for (const child of children || []) {
    const childCode = String(child?.code ?? '')
    if (!childCode.startsWith(parentCode)) continue
    const tail = childCode.slice(parentCode.length)
    if (!/^\d{2}$/.test(tail)) continue
    const n = parseInt(tail, 10)
    if (n > maxSuffix) maxSuffix = n
  }
  return buildChildCode(parentCode, maxSuffix + 1)
}
```

- [ ] **Step 2: 接入 handleAddChild**

把现有 `handleAddChild`（约 600–607 行）：

```ts
// 新增子科目
function handleAddChild(parent: any) {
  isEdit.value = false
  resetForm()
  formData.parentId = parent.id
  formData.category = parent.category
  formData.balanceDirection = parent.balanceDirection
  dialogVisible.value = true
}
```

改为：

```ts
// 新增子科目
function handleAddChild(parent: any) {
  isEdit.value = false
  resetForm()
  formData.parentId = parent.id
  formData.category = parent.category
  formData.balanceDirection = parent.balanceDirection
  // 默认编码 = 上级下最大子编码 + 1（扁平行已剥离 children，需回树取真实节点）
  const parentNode = findAccountById(accountTree.value, parent.id)
  formData.code = computeNextChildCode(parent.code, parentNode?.children ?? [])
  dialogVisible.value = true
}
```

- [ ] **Step 3: 删除临时验证脚本**

Run: `git rm -f --ignore-unmatch web/tmp-account-code-check.mjs; if (Test-Path web/tmp-account-code-check.mjs) { Remove-Item web/tmp-account-code-check.mjs }`
（脚本从未入库，直接删文件即可；上面命令对"已入库/未入库"都安全。）

- [ ] **Step 4: 类型检查通过**

Run: `cd web; npm run type-check`
Expected: 无错误（exit 0）。若报 `computeNextChildCode`/`buildChildCode` 未使用，说明 Step 2 未接上——回查。

- [ ] **Step 5: 提交**

```bash
git add web/src/views/finance/AccountManage.vue
git commit -m "feat: 新增子科目时默认带出最大子编码+1

打开新增子科目弹窗时，科目编码默认填为上级下最大子编码+1
（无子科目→…01，满99→留空），用户可改。仅前端 AccountManage.vue。

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 接入 handleSaveAndAdd（保存并新增续填）

"保存并新增"成功后，下一条默认编码 = 刚保存编码顺序号 + 1。基于已存编码递增，不依赖 `loadData()` 的异步刷新；仅有上级时续填（顶级新增保持留空）。

**Files:**
- Modify: `web/src/views/finance/AccountManage.vue`（改 `handleSaveAndAdd`，约 674–698 行）

- [ ] **Step 1: 改 handleSaveAndAdd**

把现有：

```ts
    await createAccount(buildPayload(), accountSetId)
    message.success('创建成功')
    loadData()
    const savedParentId = formData.parentId
    const savedCategory = formData.category
    const savedBalanceDirection = formData.balanceDirection
    resetForm()
    formData.parentId = savedParentId
    formData.category = savedCategory
    formData.balanceDirection = savedBalanceDirection
```

改为：

```ts
    await createAccount(buildPayload(), accountSetId)
    message.success('创建成功')
    loadData()
    const savedParentId = formData.parentId
    const savedCategory = formData.category
    const savedBalanceDirection = formData.balanceDirection
    const savedCode = formData.code
    resetForm()
    formData.parentId = savedParentId
    formData.category = savedCategory
    formData.balanceDirection = savedBalanceDirection
    // 续填下一条默认编码 = 刚保存编码顺序号 + 1（基于已存编码递增，不等异步刷新的树）
    // 仅子科目续填；顶级新增（无上级）保持留空，与既有行为一致
    if (savedParentId && savedCode.length >= 2) {
      formData.code = buildChildCode(savedCode.slice(0, -2), parseInt(savedCode.slice(-2), 10) + 1)
    }
```

- [ ] **Step 2: 类型检查通过**

Run: `cd web; npm run type-check`
Expected: 无错误（exit 0）。

- [ ] **Step 3: 构建通过**

Run: `cd web; npm run build`
Expected: 构建成功，无类型/编译错误。

- [ ] **Step 4: 提交**

```bash
git add web/src/views/finance/AccountManage.vue
git commit -m "feat: 保存并新增子科目时续填下一条默认编码

保存成功后基于刚存编码顺序号+1续填（满99留空），便于连续录入。
仅子科目续填，顶级新增保持留空。

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: 手动冒烟验证（可选，建议）

自动门禁（Node 断言 + type-check + build）已过；此步在真实页面确认体验。

- [ ] **Step 1: 起本地服务**

用 restart-dev 技能拉起前后端（端口 9000/9001），或既有 `scripts/dev`。

- [ ] **Step 2: 在科目管理页验证**

1. 选一个**有子科目**的科目，点行内"新增子科目"——编码框应自动出现"最大子编码+1"。
2. 选一个**无子科目**的末级科目，点"新增子科目"——编码框应出现"上级编码+01"。
3. 在弹窗点"保存并新增"——编码框应自动跳到下一个顺序号。
4. 顶部"新增"（一级科目）——编码框应**保持空**（不受影响）。
5. 点已有科目"编辑"——编码框 disabled、显示原值（不受影响）。

> 发现问题回到 Task 1 的纯函数补断言、再修。

---

## Self-Review（写计划者已核对）

- **Spec 覆盖**：computeNextChildCode（最大+1 / 无子科目→01 / 满99→留空 / 脏数据跳过）= Task 1+2；handleAddChild 接入 = Task 2；handleSaveAndAdd 续填 = Task 3；"仅子科目、顶级不填"= Task 2/3 的范围约束 + Task 4.4 冒烟。无遗漏。
- **占位符**：无 TODO/TBD，所有步骤含完整代码与确切命令。
- **类型/命名一致**：`buildChildCode` / `computeNextChildCode` 在 Task 1 验证、Task 2 落地、Task 3 复用，签名与命名一致；`findAccountById` 为既有函数。
- **YAGNI**：不加测试框架、不做后端接口、不做缺口回填、不动顶级新增。
