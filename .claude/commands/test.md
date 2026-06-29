---
description: 运行 tests/ 下 xUnit 测试项目（自动发现），可用 filter 子串收敛到单模块，避免跑全图。
argument-hint: [Finance|cardflow|Task|Dormitory|Express|...]
---

运行后端测试，**优先带 filter 收敛**。

- Windows（首选）：`scripts/dev/test-dotnet.ps1 $ARGUMENTS`
- macOS/Linux：`scripts/dev/test-dotnet.sh $ARGUMENTS`

说明：
- filter 是子串，匹配相对路径或项目名（如 `Finance` / `cardflow`）。
- **无参会跑全部**测试项目；其中 `CardFlow.Tests` / `WebAPI.Tests` 引用 WebAPI 会拉入近全图、较慢——日常迭代请带 filter。
- 脚本自动发现 `tests/` 下全部 `*.Tests.csproj`，新增测试项目零改动纳入。

失败时：读出失败用例名与断言信息 → 定位到具体被测代码 → 修复 → 重跑**同一 filter**，不要扩大范围。
