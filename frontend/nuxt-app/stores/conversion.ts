import { defineStore } from 'pinia'
import type { ConversionFormat, ConversionRecord } from '~/types/conversion'

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

    updateContent(id: string, format: ConversionFormat, content: string) {
      if (this.current?.id === id) {
        this.current.format = format
        if (format === 'modoTexto' || format === 'jsonStudio') {
          this.current.text = content
        } else {
          this.current.html = content
        }
      }
      const item = this.history.find(h => h.id === id)
      if (item) {
        item.format = format
        if (format === 'modoTexto' || format === 'jsonStudio') {
          item.text = content
        } else {
          item.html = content
        }
      }
    },

    updateHtml(id: string, html: string) {
      this.updateContent(id, 'html', html)
    },

    getById(id: string) {
      return this.history.find(h => h.id === id) ?? null
    }
  }
})
