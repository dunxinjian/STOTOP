import type { CardComponentDefinition, CardHeaderConfig, SchemaFieldDefinition } from '@/types/cardflow'

export interface CardSchemaPayload {
  fields: SchemaFieldDefinition[]
  components: CardComponentDefinition[]
  header?: CardHeaderConfig
}

export function defaultCardHeaderConfig(): CardHeaderConfig {
  return {
    titleMode: 'flowName',
    titleText: '',
    titleFieldKey: null,
    titleTemplate: '',
    subtitleMode: 'flowCode',
    subtitleText: '',
    subtitleFieldKey: null,
    subtitleTemplate: '',
    showSubtitle: true,
    showStatus: false,
    align: 'left',
  }
}

export function normalizeCardHeaderConfig(header?: Partial<CardHeaderConfig> | null): CardHeaderConfig {
  return {
    ...defaultCardHeaderConfig(),
    ...(header || {}),
  }
}

export function parseCardSchemaPayload(json?: string | null): CardSchemaPayload {
  if (!json) return { fields: [], components: [], header: defaultCardHeaderConfig() }
  try {
    const parsed = JSON.parse(json)
    if (Array.isArray(parsed)) return { fields: parsed, components: [], header: defaultCardHeaderConfig() }
    if (parsed && typeof parsed === 'object') {
      return {
        fields: Array.isArray(parsed.fields) ? parsed.fields : [],
        components: Array.isArray(parsed.components) ? parsed.components : [],
        header: normalizeCardHeaderConfig(parsed.header),
      }
    }
  } catch {
    // Keep callers resilient to older or partially saved draft payloads.
  }
  return { fields: [], components: [], header: defaultCardHeaderConfig() }
}

export function parseCardSchemaFields(json?: string | null): SchemaFieldDefinition[] {
  return parseCardSchemaPayload(json).fields
}

export function parseCardSchemaHeader(json?: string | null): CardHeaderConfig {
  return parseCardSchemaPayload(json).header || defaultCardHeaderConfig()
}

export interface DetailTableSchema {
  detailTableKey: string
  label?: string
  columns: SchemaFieldDefinition[]
}

/**
 * 解析明细 schema 为完整的多表结构：兼容 legacy 裸数组 / { fields } / { tables:[...] } 三形态。
 * 编辑器仅编辑 default 表，但保存时须原样透传其余表，故解析必须保留全部表——单表取值走 parseDetailSchemaFields。
 */
export function parseDetailSchema(json?: string | null): DetailTableSchema[] {
  if (!json) return []
  try {
    const parsed = JSON.parse(json)
    if (Array.isArray(parsed)) {
      return [{ detailTableKey: 'default', label: '明细', columns: parsed }]
    }
    if (parsed && typeof parsed === 'object' && Array.isArray(parsed.tables)) {
      return parsed.tables
        .filter((table: any) => table && typeof table === 'object')
        .map((table: any) => ({
          detailTableKey: typeof table.detailTableKey === 'string' ? table.detailTableKey : 'default',
          label: typeof table.label === 'string' ? table.label : undefined,
          columns: Array.isArray(table.columns) ? table.columns : [],
        }))
    }
    if (parsed && typeof parsed === 'object' && Array.isArray(parsed.fields)) {
      return [{ detailTableKey: 'default', label: '明细', columns: parsed.fields }]
    }
  } catch {
    // Keep callers resilient to older or partially saved draft payloads.
  }
  return []
}

export function parseDetailSchemaFields(json?: string | null): SchemaFieldDefinition[] {
  const tables = parseDetailSchema(json)
  if (tables.length === 0) return []
  const defaultTable = tables.find(table => table.detailTableKey === 'default') || tables[0]
  return defaultTable.columns
}
