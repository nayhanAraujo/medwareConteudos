import { Document, Packer } from 'docx'
import { htmlToContent } from 'docx/html'
import DOMPurify from 'dompurify'
import { zipSync } from 'fflate'

export interface ExportPhrase { id: number; codigoFrase?: string; titulo?: string; frase?: string }

export async function exportPhraseDocxZip(phrases: ExportPhrase[]): Promise<Blob> {
  if (!phrases.length) throw new Error('O grupo selecionado não possui frases.')
  const files: Record<string, Uint8Array> = {}
  for (const phrase of phrases) {
    const title = (phrase.titulo || 'Frase').replace(/[<>:"/\\|?*\x00-\x1f]/g, '_').trim().slice(0, 120).replace(/[. ]+$/, '') || 'Frase'
    const name = `${phrase.id} - ${title}.docx`
    if (files[name]) throw new Error('A lista de frases mudou durante a exportação. Tente novamente.')
    const document = await exportPhraseDocx([phrase])
    files[name] = new Uint8Array(await document.arrayBuffer())
  }
  // DOCX files are already compressed; store them without recompressing.
  const zip = zipSync(files, { level: 0 })
  return new Blob([new Uint8Array(zip)], { type: 'application/zip' })
}

export async function exportPhraseDocx(phrases: ExportPhrase[]): Promise<Blob> {
  if (!phrases.length) throw new Error('O grupo selecionado não possui frases.')
  const { RTFJS } = await import('rtf.js')
  RTFJS.loggingEnabled(false)
  const children: ReturnType<typeof htmlToContent>[number][] = []
  for (const phrase of phrases) {
    try {
      const value = phrase.frase || ''
      const container = document.createElement('div')
      if (/^\s*\{\\rtf/i.test(value)) {
        if (/\\(?:pict|object|trowd)\b/.test(value)) throw new Error('contém imagens ou tabelas, ainda não suportadas na exportação')
        const ansi = value.replace(/[^\x00-\xff]/g, ch => `\\u${ch.charCodeAt(0) > 32767 ? ch.charCodeAt(0) - 65536 : ch.charCodeAt(0)}?`)
        const rtf = new RTFJS.Document(Uint8Array.from(ansi, ch => ch.charCodeAt(0)).buffer)
        container.append(...await rtf.render())
      } else {
        const paragraph = document.createElement('p')
        paragraph.textContent = value
        paragraph.style.whiteSpace = 'pre-wrap'
        container.append(paragraph)
      }
      if (container.querySelector('img,table,svg,object')) throw new Error('contém elementos não suportados na exportação')
      const warnings: string[] = []
      const content = htmlToContent(DOMPurify.sanitize(container.innerHTML), { onWarning: warning => warnings.push(JSON.stringify(warning)) })
      if (warnings.length) throw new Error('a formatação contém recursos não suportados pelo conversor DOCX')
      children.push(...content)
    } catch (reason) {
      throw new Error(`Não foi possível exportar a frase "${phrase.titulo || phrase.id}": ${reason instanceof Error ? reason.message : reason}`)
    }
  }
  return Packer.toBlob(new Document({
    styles: {
      default: { document: { run: { font: 'Arial', size: 22, color: '000000' } } }
    },
    sections: [{ properties: { page: { margin: { top: 1134, bottom: 1134, left: 1134, right: 1134 } } }, children }]
  }))
}
