# 钉钉 H5 微应用部署配置指南

本文档面向运维 / 实施同学，描述将 STOTOP 移动端接入钉钉 H5 微应用所需完成的全部配置项。请在每次新增企业部署时按本指南逐项核对。

## 1. 钉钉开放平台注册

1. 登录 [钉钉开放平台](https://open.dingtalk.com/)
2. 进入 “应用开发” → “企业内部应用” → “H5 微应用”
3. 点击 “创建应用”，填写以下基本信息：
   - 应用名称：建议填写 `STOTOP`
   - 应用图标：使用项目 logo
   - 应用简介：一句话描述（如 “一站式财务 + 业务工作台”）
   - 应用首页地址：见下文 “应用配置”

## 2. 应用配置

### 2.1 基本配置
- **应用首页地址（移动端）**：`https://{domain}/mobile.html#/m/home`
- **PC 端首页地址**：`https://{domain}/workhub`
- **服务器出口 IP 白名单**：填写部署服务器的公网出口 IP（多 IP 用英文逗号分隔）

### 2.2 开发管理
- 开发模式：企业内部开发
- 开发环境：H5

> 注意：钉钉对应用首页地址有 https 强制要求，本地联调可使用钉钉提供的内网穿透或 ngrok。

## 3. JSAPI 权限申请

进入 “权限管理” → “JSAPI 权限申请”，至少申请下列权限：

| 权限点                                | 用途                       |
| ------------------------------------- | -------------------------- |
| `media.chooseImage`                   | 选择图片 / 拍照            |
| `media.upload`                        | 上传媒体文件               |
| `util.scan`                           | 扫码录单                   |
| `runtime.permission.requestAuthCode`  | 免登授权（获取 authCode）  |

申请后通常 1 ~ 2 个工作日内审核通过；审核期间已绑定为 “应用管理员” 的账号可正常调试。

## 4. 后端配置（appsettings.json）

在部署服务器的 `appsettings.json`（或 `appsettings.Production.json`）中填入以下节点：

```json
"DingTalk": {
  "AppKey": "从开放平台获取",
  "AppSecret": "从开放平台获取",
  "CorpId": "企业 CorpId",
  "AgentId": "H5 应用的 AgentId",
  "RobotWebhookUrl": "群机器人 Webhook 地址",
  "RobotSecret": "群机器人加签密钥"
}
```

> 上线前请将 AppSecret / RobotSecret 通过密钥管理服务或环境变量注入，不要直接提交到版本库。

应用启动后可通过管理后台 “通知设置 → 钉钉” 进行运行时校验（点击 “测试连接”），后端会调用钉钉 `gettoken` 接口验证凭证有效性。

## 5. 待办通知 detailUrl 配置

CardFlow 模块在创建钉钉待办时，`detailUrl` 默认使用 redirect 分流模板：

```
https://{domain}/redirect/card/{cardId}?orgId={orgId}
```

`/redirect/card/{cardId}` 由静态页 `web/redirect.html` 处理，会根据 `User-Agent` 自动分流：

- 移动设备 → `/mobile.html#/m/card/{cardId}?orgId={orgId}`
- PC 设备  → `/workhub/card/{cardId}`

若需要自定义跳转域名或路径，可在 “通知设置 → 钉钉 → detailUrl 模板” 中填写自定义模板，支持下列占位符：

| 占位符      | 含义                       |
| ----------- | -------------------------- |
| `{id}`      | 待办 ID（CfTodoItem.FID）  |
| `{cardId}`  | 卡片 ID（CfCard.FID）      |
| `{orgId}`   | 组织 ID（FOrgId）          |

默认推荐保持为空，使用系统内建的 redirect 分流模板。

## 6. 群机器人配置

群机器人用于推送系统级消息（异常告警、定时报表、上线通知等）：

1. 在目标钉钉群点击右上角设置 → “群机器人” → “自定义”
2. 安全设置选择 **加签**，记录生成的 `Secret`
3. 复制 Webhook URL（含 `access_token` 参数）
4. 在系统管理后台的 “通知设置 → 群机器人” 中粘贴 Webhook 与 Secret，或调用 `POST /api/mobile/bot/config` 接口写入

> 加签方式相比 IP 白名单更适合容器化 / 多副本部署。

## 7. CORS 配置

后端默认仅允许本地开发端口跨域。生产环境部署时，务必在 `Program.cs` 或环境变量中追加实际域名（包括 PC 域名与移动端域名，如果分开部署）。

钉钉 H5 容器在大多数场景下与目标域同源，理论上不会触发 CORS；但若使用了独立的 API 子域（如 `api.example.com`），需要单独配置。

## 8. 部署检查清单

| 项 | 描述 | 完成 |
| --- | --- | --- |
| 1 | `appsettings.json` 已填入正确的 DingTalk 配置 | ☐ |
| 2 | 服务器公网 IP 已添加到钉钉白名单 | ☐ |
| 3 | JSAPI 权限已申请并审核通过 | ☐ |
| 4 | H5 应用首页地址配置正确（`/mobile.html#/m/home`） | ☐ |
| 5 | Hangfire 定时任务已生效（访问 `/hangfire` 仪表板可看到 CardFlowTimeoutJob、PushRetryJob） | ☐ |
| 6 | 群机器人 Webhook 测试发送成功 | ☐ |
| 7 | redirect 页面 PC / 移动端分流正常（用 PC 浏览器与钉钉客户端分别打开 `https://{domain}/redirect/card/1?orgId=1` 验证） | ☐ |
| 8 | 通知设置后台 “测试连接” 返回成功 | ☐ |
| 9 | 钉钉客户端中打开 H5 微应用可完成免登并进入工作台 | ☐ |

## 9. 常见问题

- **打开微应用提示 “请在钉钉客户端中打开此链接”**：说明 redirect.html 检测到非钉钉 UA。直接在浏览器中访问会进入此分支，符合预期。
- **创建待办失败：用户未绑定 UnionId**：先在 “组织 → 身份源同步” 中触发一次钉钉通讯录同步，将 SysUser 与钉钉 UnionId 绑定。
- **钉钉 errcode=88 / 60011**：通常是 AppKey/AppSecret 不匹配或服务器 IP 未加白名单，按本指南第 1、2 步重新核对。
- **PC 端跳转到 redirect 后停留在加载界面**：检查 `redirect.html` 是否被 SPA fallback 拦截，确认部署网关已将 `/redirect/*.html` 直接返回静态文件。
