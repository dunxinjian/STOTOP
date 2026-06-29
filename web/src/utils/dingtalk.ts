// 钉钉 JSAPI 集成工具
declare const dd: any

/**
 * 判断当前是否在钉钉环境中
 */
export function isDingTalkEnv(): boolean {
  return /DingTalk/i.test(navigator.userAgent)
}

/**
 * 初始化钉钉 JSAPI
 */
export async function initDingTalk(corpId: string) {
  if (!isDingTalkEnv()) return
  try {
    dd.config({
      agentId: '',
      corpId,
      timeStamp: Date.now().toString(),
      nonceStr: Math.random().toString(36).slice(2),
      signature: '',
      jsApiList: [
        'biz.navigation.setTitle',
        'biz.navigation.close',
        'runtime.permission.requestAuthCode',
        'device.notification.toast',
      ],
    })
    await new Promise<void>((resolve, reject) => {
      dd.ready(() => resolve())
      dd.error((err: any) => {
        console.error('钉钉 JSAPI 初始化失败:', err)
        reject(err)
      })
    })
  } catch (e) {
    console.error('initDingTalk error:', e)
  }
}

/**
 * 设置钉钉导航栏标题
 */
export function setDingTalkTitle(title: string) {
  if (!isDingTalkEnv()) return
  try {
    dd.biz.navigation.setTitle({ title })
  } catch { /* ignore */ }
}

/**
 * 关闭钉钉页面
 */
export function closeDingTalkPage() {
  if (!isDingTalkEnv()) return
  try {
    dd.biz.navigation.close()
  } catch { /* ignore */ }
}

/**
 * 获取钉钉免登授权码
 */
export async function getDingTalkAuthCode(corpId?: string): Promise<string | null> {
  if (!isDingTalkEnv()) return null
  return new Promise((resolve) => {
    dd.runtime.permission.requestAuthCode({
      corpId: corpId || '',
      onSuccess: (result: any) => resolve(result.code),
      onFail: () => resolve(null),
    })
  })
}

/**
 * 钉钉 Toast 提示
 */
export function showDingTalkToast(text: string, type: 'success' | 'error' = 'success') {
  if (!isDingTalkEnv()) return
  try {
    dd.device.notification.toast({
      icon: type,
      text,
      duration: 2,
    })
  } catch { /* ignore */ }
}
