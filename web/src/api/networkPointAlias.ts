// 网点名称映射 API
import { get, post, del } from './request'
import type { PagedResult } from './express'

export interface NetworkPointAliasDto {
  id: number
  name: string
  networkPointCode?: string
  networkPointName?: string
  createdTime?: string
}

export interface NetworkPointAliasQueryRequest {
  keyword?: string
  networkPointCode?: string
  pageIndex: number
  pageSize: number
}

export interface CreateNetworkPointAliasRequest {
  name: string
  networkPointCode: string
}

export interface BatchCreateNetworkPointAliasRequest {
  items: CreateNetworkPointAliasRequest[]
}

export function getNetworkPointAliasList(params: NetworkPointAliasQueryRequest): Promise<PagedResult<NetworkPointAliasDto>> {
  return get('/express/network-point-aliases', params)
}

export function createNetworkPointAlias(data: CreateNetworkPointAliasRequest): Promise<any> {
  return post('/express/network-point-aliases', data)
}

export function deleteNetworkPointAlias(id: number): Promise<any> {
  return del(`/express/network-point-aliases/${id}`)
}

export function batchCreateNetworkPointAliases(data: BatchCreateNetworkPointAliasRequest): Promise<any> {
  return post('/express/network-point-aliases/batch', data)
}
