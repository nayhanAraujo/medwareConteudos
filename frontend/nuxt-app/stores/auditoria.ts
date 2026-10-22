import { defineStore } from 'pinia'
import type { AuditCatalog, AuditDetail, AuditEvent } from '~/utils/auditoria'

interface AuditReader { get<T>(path: string): Promise<T> }
const message = (error: unknown) => error instanceof Error ? error.message : 'Não foi possível consultar a auditoria.'

export const useAuditoriaStore = defineStore('auditoria', {
  state: () => ({
    rows: [] as AuditEvent[], nextCursor: null as string | null, loading: false, error: '',
    catalog: { modules: [], actions: [], results: [] } as AuditCatalog, catalogError: '',
    detailId: null as string | null, detail: null as AuditDetail | null, detailLoading: false, detailError: '',
    listRequest: 0, detailRequest: 0, catalogRequest: 0
  }),
  actions: {
    async load(api: AuditReader, query: Record<string, string>, cursor?: string) {
      const request = ++this.listRequest
      this.loading = true; this.error = ''; this.nextCursor = null
      try {
        const params = new URLSearchParams({ ...query, take: '50' })
        if (cursor) params.set('cursor', cursor)
        const response = await api.get<{ data: AuditEvent[]; nextCursor: string | null }>(`/api/web/auditoria?${params}`)
        if (request !== this.listRequest) return
        this.rows = response.data; this.nextCursor = response.nextCursor
      } catch (error) {
        if (request === this.listRequest) { this.rows = []; this.error = message(error) }
      } finally { if (request === this.listRequest) this.loading = false }
    },
    async loadCatalog(api: AuditReader) {
      const request = ++this.catalogRequest
      this.catalogError = ''
      try {
        const response = await api.get<{ data: AuditCatalog }>('/api/web/auditoria/filtros')
        if (request === this.catalogRequest) this.catalog = response.data
      } catch (error) { if (request === this.catalogRequest) this.catalogError = message(error) }
    },
    async openDetail(api: AuditReader, id: string) {
      const request = ++this.detailRequest
      this.detailId = id; this.detail = null; this.detailLoading = true; this.detailError = ''
      try {
        const response = await api.get<{ data: AuditDetail }>(`/api/web/auditoria/${encodeURIComponent(id)}`)
        if (request === this.detailRequest) this.detail = response.data
      } catch (error) { if (request === this.detailRequest) this.detailError = message(error) }
      finally { if (request === this.detailRequest) this.detailLoading = false }
    },
    closeDetail() { this.detailRequest++; this.detailId = null; this.detail = null; this.detailError = ''; this.detailLoading = false },
    clear() {
      this.listRequest++; this.detailRequest++; this.catalogRequest++
      this.rows = []; this.nextCursor = null; this.error = ''; this.loading = false
      this.closeDetail(); this.catalog = { modules: [], actions: [], results: [] }; this.catalogError = ''
    }
  }
})
