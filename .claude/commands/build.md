---
description: 用 .slnf 工作区过滤器只编译单个模块及其依赖闭包，而非整个 WebAPI 图（大库 feedback loop 提速）。
argument-hint: [cardflow|express|finance|crm|task|core|dormitory]
---

按模块依赖闭包编译，**不要编译全图**。

- Windows（首选）：用 PowerShell 跑 `scripts/dev/build-filter.ps1 $ARGUMENTS`
- macOS/Linux：用 Bash 跑 `scripts/dev/build-filter.sh $ARGUMENTS`

不带参数时脚本会列出可用过滤器（cardflow / core / crm / dormitory / express / finance / task）。

编译失败 → 定位**首个**报错文件:行并修复，再重跑本命令。不要为了"绕过"而切换到全图 `dotnet build` 或改用 WebAPI.sln。
