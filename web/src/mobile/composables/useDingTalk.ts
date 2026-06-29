import * as dd from 'dingtalk-jsapi'
import { getJsapiSignature } from '@shared/api/auth'

let jsapiReady = false

/** 初始化 JSAPI 鉴权 */
export async function initJsapi() {
  if (jsapiReady) return

  const url = location.href.split('#')[0]
  const config = await getJsapiSignature(url)

  await new Promise<void>((resolve, reject) => {
    ;(dd as any).config({
      agentId: config.agentId,
      corpId: config.corpId,
      timeStamp: config.timeStamp,
      nonceStr: config.nonceStr,
      signature: config.signature,
      jsApiList: ['chooseImage', 'uploadImage', 'scan', 'requestAuthCode'],
      onSuccess: () => {
        jsapiReady = true
        resolve()
      },
      onFail: (err: any) => {
        console.error('[JSAPI] config 失败:', err)
        reject(err)
      },
    })
  })
}

/** 拍照/选择图片 */
export async function chooseImage(maxCount = 9): Promise<string[]> {
  await initJsapi()
  const res = await (dd as any).chooseImage({
    count: maxCount,
    sourceType: ['camera', 'album'],
  })
  return res.filePaths || []
}

/** 上传图片到钉钉临时存储获取 mediaId */
export async function uploadImage(filePath: string): Promise<string> {
  await initJsapi()
  const res = await (dd as any).uploadImage({
    filePath,
    showProgress: true,
  })
  return res.mediaId
}

/** 扫码 */
export async function scanCode(): Promise<{ type: string; text: string }> {
  await initJsapi()
  const res = await (dd as any).scan({ type: 'all' })
  return { type: res.type || 'unknown', text: res.text || '' }
}
