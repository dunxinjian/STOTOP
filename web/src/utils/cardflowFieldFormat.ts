import type { SchemaFieldDefinition } from '@/types/cardflow'

/** 卡片字段值 → 显示字符串的单一真源。根治 PC/移动只读端对结构化字段（user/org/account/…）输出 [object Object]。 */
export function formatFieldDisplayValue(field: SchemaFieldDefinition, val: any): string {
  if (val === null || val === undefined || val === '') return '-'
  switch (field.type as string) {
    case 'money':
    case 'amount':
      return formatMoneyValue(val) || '-'
    case 'number':
      return String(val)
    case 'date':
      return formatDateValue(val)
    case 'enum':
      return formatEnumValue(field, val)
    case 'user':
    case 'org':
      return typeof val === 'object' ? (val.name || val.label || val.title || '-') : String(val)
    case 'account':
      return formatAccountValue(val)
    case 'auxiliary':
      return formatAuxiliaryValue(val)
    case 'bankAccount':
      return formatBankAccountValue(val)
    case 'cardRef':
      return typeof val === 'object'
        ? ([val.cardNumber || val.targetCardNumber, val.title].filter(Boolean).join(' ') || '-')
        : String(val)
    case 'voucherRef':
      return typeof val === 'object' ? (val.voucherNo || val.voucherNumber || '-') : String(val)
    case 'file':
      return Array.isArray(val)
        ? (val.length ? val.map((f: any) => f?.name || f?.fileName || '文件').join('、') : '-')
        : '-'
    default:
      return typeof val === 'object' ? (val.name || val.label || JSON.stringify(val)) : String(val)
  }
}

export function formatMoneyValue(val: any): string {
  const num = Number(val)
  if (isNaN(num) || val === null || val === undefined || val === '') return ''
  return `¥${num.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

export function formatDateValue(val: any): string {
  if (!val) return ''
  const d = new Date(val)
  if (isNaN(d.getTime())) return String(val)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** 枚举显示：兼容 string[] 与 { label, value }[]（存码显名） */
export function formatEnumValue(field: SchemaFieldDefinition, val: any): string {
  const options = (field.options || []) as any[]
  for (const opt of options) {
    if (opt && typeof opt === 'object' && opt.value === val) return opt.label ?? String(val)
  }
  return String(val)
}

export function formatAccountValue(val: any): string {
  if (!val) return '-'
  if (typeof val === 'string') return val || '-'
  const code = val.code || val.accountCode
  const name = val.name || val.accountName
  if (code && name) return `${code} ${name}`
  return name || code || '-'
}

export function formatAuxiliaryValue(val: any): string {
  if (!val) return '-'
  if (typeof val === 'string') return val || '-'
  if (val.code && val.name) return `${val.code} ${val.name}`
  return val.name || val.code || '-'
}

export function formatBankAccountValue(val: any): string {
  if (!val) return '-'
  if (typeof val === 'string') return val || '-'
  const accountNo = val.accountNo || val.bankAccountNo
  const bankName = val.bankName
  const accountName = val.accountName
  return [accountNo, bankName, accountName].filter(Boolean).join(' · ') || '-'
}
