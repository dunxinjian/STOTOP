---
description: 用 rule-reviewer 子代理按 design/21-dev-rules.md 审查当前未提交改动的规约符合性。
---

对当前未提交改动启动 **rule-reviewer** 子代理（Agent 工具，subagent_type: `rule-reviewer`）做规约审查。

让该子代理：
- 审查范围 = `git diff` + `git diff --staged` + 新增未跟踪文件（`git status --porcelain` 里的 `??`）。
- 按本项目约定（@CLAUDE.md / @design/21-dev-rules.md）逐项核对，**只审是否违反既定约定**，不做泛泛风格挑刺。
- 输出按 🔴硬约束违规 / 🟡建议 / ✅通过 三档，每条带 `文件:行` 与修复建议。

收到子代理结论后，把 🔴 项整理成一份可执行的修复清单交给我确认。
