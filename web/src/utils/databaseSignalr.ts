import * as signalR from '@microsoft/signalr'

/**
 * 创建并返回数据库进度 Hub 连接（/hubs/database-progress）
 * 每次调用都是独立实例，便于在保留初始化完成后销毁
 *
 * 【未统一到 signalr.ts 标准工厂的原因】
 * 1. 数据库初始化是短生命周期连接，使用简单固定间隔重连 [0, 2s, 5s] 即可，
 *    无需标准工厂的指数退避 + 手动重连兜底策略
 * 2. 调用方需要获取 connectionId 传给后端以定向推送进度，标准工厂不暴露此能力
 * 3. 返回原始 HubConnection 而非 SignalRManager，匹配 DatabaseSetup.vue 的用法
 */
export function createDatabaseProgressConnection(): signalR.HubConnection {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/database-progress', {
      accessTokenFactory: () => localStorage.getItem('stotop_token') || ''
    })
    .withAutomaticReconnect([0, 2000, 5000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()
  return conn
}

/**
 * 启动连接并返回 connectionId
 */
export async function startAndGetConnectionId(conn: signalR.HubConnection): Promise<string> {
  await conn.start()
  const id = conn.connectionId
  if (!id) throw new Error('无法获取 SignalR connectionId')
  return id
}

/**
 * 停止并销毁连接
 */
export async function stopDatabaseConnection(conn: signalR.HubConnection): Promise<void> {
  if (conn.state !== signalR.HubConnectionState.Disconnected) {
    await conn.stop()
  }
}
