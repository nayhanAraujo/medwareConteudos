import { defineStore } from 'pinia'
import type { ConversionRecord } from '~/types/conversion'

export const useConversionStore = defineStore('conversion', {
  state: () => ({
    current: null as ConversionRecord | null,
    history: [] as ConversionRecord[]
  }),

  actions: {
    setCurrent(record: ConversionRecord) {
      this.current = record
      const exists = this.history.findIndex(h => h.id === record.id)
      if (exists >= 0) {
        this.history[exists] = record
      } else {
        this.history.unshift(record)
      }
    },

    updateHtml(id: string, html: string) {
      if (this.current?.id === id) {
        this.current.html = html
      }
      const item = this.history.find(h => h.id === id)
      if (item) item.html = html
    },

    getById(id: string) {
      return this.history.find(h => h.id === id) ?? null
    }
  }
})
