import { defineStore } from 'pinia'
import type { QuoteHeader, QuoteDetail, QuoteDraft } from '~/utils/orcamentos'

interface Reader { get<T>(path: string): Promise<T>; post<T>(path: string, data: unknown): Promise<T>; put<T>(path: string, data: unknown): Promise<T> }
const base = '/api/web/orcamentos'
export const useOrcamentosStore = defineStore('orcamentos', {
  state: () => ({ rows: [] as QuoteHeader[], nextBefore: null as string | null, detail: null as QuoteDetail | null, loading: false, saving: false, error: '', sequence: 0, listSequence: 0 }),
  actions: {
    async list(api: Reader, query: Record<string, string>) {
      const seq = ++this.listSequence; this.loading = true; this.error = ''
      try { const r = await api.get<{ data: QuoteHeader[]; nextBefore: string | null }>(`${base}?${new URLSearchParams(query)}`); if (seq === this.listSequence) { this.rows = r.data; this.nextBefore = r.nextBefore } }
      catch (e) { if (seq === this.listSequence) { this.rows = []; this.nextBefore = null; this.error = e instanceof Error ? e.message : 'Falha na consulta.' } }
      finally { if (seq === this.listSequence) this.loading = false }
    },
    async load(api: Reader, id: string) {
      const seq = ++this.sequence; this.loading = true; this.error = ''; this.detail = null
      try { const r = await api.get<{ data: QuoteDetail }>(`${base}/${encodeURIComponent(id)}`); if (seq === this.sequence) this.detail = r.data }
      catch (e) { if (seq === this.sequence) this.error = e instanceof Error ? e.message : 'Falha na consulta.' }
      finally { if (seq === this.sequence) this.loading = false }
    },
    async save(api: Reader, id: string, version: string, draft: QuoteDraft, refreshPolicy = false, refreshClient = false) {
      this.saving = true; this.error = ''; const seq = ++this.sequence
      try { const r = await api.put<{ data: QuoteDetail }>(`${base}/${encodeURIComponent(id)}`, { version, draft, refreshPolicy, refreshClient }); if (seq === this.sequence) this.detail = r.data; return r.data }
      catch (e) { if (seq === this.sequence) this.error = e instanceof Error ? e.message : 'Não foi possível salvar.'; throw e }
      finally { if (seq === this.sequence) this.saving = false }
    },
    clear() { this.sequence++; this.listSequence++; this.rows = []; this.nextBefore = null; this.detail = null; this.error = ''; this.loading = false; this.saving = false }
  }
})
