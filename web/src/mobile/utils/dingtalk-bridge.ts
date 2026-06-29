/**
 * 钉钉 JSAPI 桥接层 — 开发环境自动 mock
 */
import * as dd from 'dingtalk-jsapi'

const isDingTalk = /DingTalk/.test(navigator.userAgent)

/** 原生文件选择器 fallback */
function nativeFilePicker(accept: string): Promise<string[]> {
  return new Promise((resolve) => {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = accept
    input.multiple = true
    input.onchange = () => {
      const files = Array.from(input.files || [])
      // 返回本地 blob URL 作为 filePaths
      resolve(files.map(f => URL.createObjectURL(f)))
    }
    input.click()
  })
}

export const bridge = {
  /** 获取免登授权码 */
  async requestAuthCode(): Promise<string> {
    if (isDingTalk) {
      const res = await (dd as any).requestAuthCode({ clientId: '' })
      return res.code || res.result?.code || ''
    }
    // 开发环境 mock
    console.warn('[DingTalk Bridge] Mock: requestAuthCode')
    return 'dev-mock-auth-code'
  },

  /** 选择图片 */
  async chooseImage(opts?: { count?: number }): Promise<string[]> {
    if (isDingTalk) {
      const res = await (dd as any).chooseImage({
        count: opts?.count || 9,
        sourceType: ['camera', 'album'],
      })
      return res.filePaths || []
    }
    // 回退到原生 input
    return nativeFilePicker('image/*')
  },

  /** 上传图片获取 mediaId */
  async uploadImage(filePath: string): Promise<string> {
    if (isDingTalk) {
      const res = await (dd as any).uploadImage({ filePath, showProgress: true })
      return res.mediaId
    }
    // mock: 返回 fake mediaId
    console.warn('[DingTalk Bridge] Mock: uploadImage')
    return 'mock-media-id-' + Date.now()
  },

  /** 扫码 */
  async scan(): Promise<{ type: string; text: string }> {
    if (isDingTalk) {
      const res = await (dd as any).scan({ type: 'all' })
      return { type: res.type || 'unknown', text: res.text || '' }
    }
    // mock: 返回测试数据
    console.warn('[DingTalk Bridge] Mock: scan')
    return { type: 'qrCode', text: 'stotop://card/123' }
  },

  /** 设置标题 */
  async setTitle(title: string): Promise<void> {
    if (isDingTalk) {
      ;(dd as any).setNavigationTitle?.({ title })
    }
    document.title = title
  },

  /** 关闭页面 */
  async closePage(): Promise<void> {
    if (isDingTalk) {
      ;(dd as any).closePage?.()
    } else {
      window.history.back()
    }
  },
}
