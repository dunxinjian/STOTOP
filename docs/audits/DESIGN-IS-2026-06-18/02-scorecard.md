# 02 · 评分卡（每项 0–3）

1. Good design is **innovative** — Score: 2/3
   Evidence: Feed 式工作台 + J/K 导航 + 撤销/分级确认 + SignalR 实时推送（01·A/B）。
   Justification: 是对「活动流收件箱」成熟范式的明显改良，而非新范式，故 2 不 3。

2. Good design is **useful** — Score: 2/3
   Justification: 处理待办与发起流程都被直接支持，但相邻区（重复筛选、矛盾质量卡）增加步骤与噪音，故 2 不 3。

3. Good design is **aesthetic** — Score: 2/3
   Evidence: 令牌体系基本统一（01·B），但有裸 hex 泄漏 + 同维度两套标签。
   Justification: 可见层面整洁系统、不一致≤2 处明显项，故 2；裸 hex 多为代码层未直接刺眼。

4. Good design is **understandable** — Score: 1/3
   Evidence: 两套来源筛选标签不一致（审批↔OA）；两个质量部件读数矛盾（01·A/C）。
   Justification: 2–3 个控件含义不清/信号冲突，首次用户会困惑，落在「1」。

5. Good design is **unobtrusive** — Score: 2/3
   Justification: 常态下 chrome 安静，但空态下 6 chip + 完整筛选条盖过实际内容，取最差实例仍可保 2。

6. Good design is **honest** — Score: 2/3
   Evidence: 中栏「待处理 1」与右栏「暂无待处理质量问题」并显（01·C）。
   Justification: 存在一处 label→状态错配（非暗黑模式），按「≤1 处」记 2。

7. Good design is **long-lasting** — Score: 2/3
   Evidence: 整体克制、令牌驱动；emoji 图标 + 顶栏 blur 为轻度潮流标记（01·B）。
   Justification: 1–2 个轻度时代标记，记 2。

8. Good design is **thorough** — Score: 2/3
   Evidence: 空/载入/成功/错误/禁用/撤销/确认/延后归档齐备（01·B）；但卡片 focus/键盘态缺失。
   Justification: 仅 focus 一态偏糙，记 2。

9. Good design is **environmentally friendly** — Score: 2/3（估算）
   Evidence: 动效均门控、无空闲自动播放；双 5 分钟轮询 + SignalR（01·D）；JS 字节未测。
   Justification: 动效门控、资源中等，记 2。

10. Good design is **as little design as possible** — Score: 1/3
    Evidence: 同维度双筛选控件、三处并存导航面、两个质量部件（01·A/C）。
    Justification: 3–5 个可删/可合并元素，落在「1」。

---

**合计：18 / 30**（其中 #4 理解性、#10 克制 各 1 分，均为工作台承重维度）
