/**
 * 临时ID生成器，用于前端 v-for :key 绑定
 * 当列表项没有天然唯一ID时使用
 */
let _uid = 0

export function genTempId(): string {
  return `__tmp_${++_uid}`
}
