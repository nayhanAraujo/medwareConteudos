import { Document, HeadingLevel, Packer, Paragraph } from 'docx'
import { htmlToContent } from 'docx/html'
import DOMPurify from 'dompurify'

export interface ExportPhrase { id: number; codigoFrase?: string; titulo?: string; frase?: string }

export async function exportPhraseDocx(group: string, phrases: ExportPhrase[]): Promise<Blob> {
  if (!phrases.length) throw new Error('O grupo selecionado não possui frases.')
  const { RTFJS } = await import('rtf.js')
  RTFJS.loggingEnabled(false)
  const children: ReturnType<typeof htmlToContent>[number][] = [
    new Paragraph({ text: group, heading: HeadingLevel.TITLE }),
    new Paragraph({ text: `${phrases.length} frases`, spacing: { after: 300 } })
  ]
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
      children.push(new Paragraph({ text: phrase.titulo || `Frase ${phrase.id}`, heading: HeadingLevel.HEADING_1, keepNext: true, spacing: { before: 280, after: 120 } }))
      children.push(new Paragraph({ text: `Código ${phrase.codigoFrase || phrase.id}`, keepNext: true, spacing: { after: 120 } }))
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
      default: { document: { run: { font: 'Arial', size: 22, color: '000000' } } },
      paragraphStyles: [
        { id: 'Title', name: 'Title', basedOn: 'Normal', run: { size: 36, bold: true, color: '000000' } },
        { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', run: { size: 28, bold: true, color: '000000' } }
      ]
    },
    sections: [{ properties: { page: { margin: { top: 1134, bottom: 1134, left: 1134, right: 1134 } } }, children }]
  }))
}
