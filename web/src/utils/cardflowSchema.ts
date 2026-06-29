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

export function parseDetailSchemaFields(json?: string | null): SchemaFieldDefinition[] {
  if (!json) return []
  try {
    const parsed = JSON.parse(json)
    if (Array.isArray(parsed)) return parsed
    if (parsed && typeof parsed === 'object' && Array.isArray(parsed.tables)) {
      const defaultTable = parsed.tables.find((table: any) => table?.detailTableKey === 'default')
      const table = defaultTable || parsed.tables[0]
      return Array.isArray(table?.columns) ? table.columns : []
    }
    if (parsed && typeof parsed === 'object' && Array.isArray(parsed.fields)) {
      return parsed.fields
    }
  } catch {
    // Keep callers resilient to older or partially saved draft payloads.
  }
  return []
}
