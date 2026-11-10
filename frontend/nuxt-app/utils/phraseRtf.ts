// Serialize the rendered styles, including inherited RTF font and color runs.
export function phraseHtmlToRtf(root: HTMLElement): string {
  const fonts: string[] = []
  const colors: string[] = []
  const escape = (text: string) => Array.from(text).map(ch => {
    if (/[\\{}]/.test(ch)) return `\\${ch}`
    if (ch === '\n') return '\\line '
    if (ch === '\t') return '\\tab '
    return Array.from({ length: ch.length }, (_, i) => {
      const code = ch.charCodeAt(i)
      return code > 127 ? `\\u${code > 32767 ? code - 65536 : code}?` : String.fromCharCode(code)
    }).join('')
  }).join('')
  function index(list: string[], value: string) {
    if (!list.includes(value)) list.push(value)
    return list.indexOf(value)
  }
  function walk(node: Node): string {
    if (node.nodeType === 3) return escape(node.textContent || '')
    if (!(node instanceof HTMLElement)) return ''
    if (node.tagName === 'BR') return '\\line '
    if (node.matches('img,table,svg,object')) throw new Error('Este RTF contém imagens ou tabelas. A edição visual desse conteúdo ainda não é suportada.')
    const style = getComputedStyle(node)
    const font = style.fontFamily.split(',')[0].trim().replace(/["']/g, '') || 'Arial'
    const rgb = style.color.match(/\d+/g)?.slice(0, 3).join(',') || '0,0,0'
    const color = index(colors, rgb) + 1
    const size = Math.round(parseFloat(style.fontSize) * 1.5) || 20
    const align = { center: '\\qc', right: '\\qr', justify: '\\qj' }[style.textAlign] || '\\ql'
    const block = ['block', 'list-item'].includes(style.display)
    const controls = `\\f${index(fonts, font)}\\fs${size}\\cf${color}`
      + (Number(style.fontWeight) >= 600 || style.fontWeight === 'bold' ? '\\b' : '\\b0')
      + (style.fontStyle === 'italic' ? '\\i' : '\\i0')
      + (style.textDecorationLine.includes('underline') ? '\\ul' : '\\ulnone')
      + (style.textDecorationLine.includes('line-through') ? '\\strike' : '\\strike0')
      + (style.verticalAlign === 'super' ? '\\super' : style.verticalAlign === 'sub' ? '\\sub' : '\\nosupersub')
    const content = Array.from(node.childNodes).map(walk).join('')
    return `{${block ? `\\pard${align}` : ''}${controls} ${content}${block ? '\\par' : ''}}`
  }
  const body = Array.from(root.childNodes).map(walk).join('')
  const fontTable = fonts.map((font, i) => `{\\f${i} ${escape(font)};}`).join('')
  const colorTable = colors.map(rgb => { const [r, g, b] = rgb.split(','); return `\\red${r}\\green${g}\\blue${b};` }).join('')
  return `{\\rtf1\\ansi\\ansicpg1252\\deff0\\uc1{\\fonttbl${fontTable || '{\\f0 Arial;}'}}{\\colortbl;${colorTable}}${body}}`
}
