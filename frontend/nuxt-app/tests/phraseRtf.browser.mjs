import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { phraseHtmlToRtf } from '../utils/phraseRtf.ts'
const require = createRequire(import.meta.url)
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright')
const browser = await chromium.launch({ headless: true, channel: 'msedge' })
try {
  const page = await browser.newPage()
  await page.setContent('<div id="editor"></div><div id="roundtrip"></div>')
  await page.addScriptTag({ path: require.resolve('rtf.js/dist/RTFJS.bundle.js') })
  await page.addScriptTag({ content: `window.serialize = ${phraseHtmlToRtf.toString()}` })
  const result = await page.evaluate(async () => {
    RTFJS.loggingEnabled(false)
    const original = String.raw`{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0 Tahoma;}}{\colortbl;\red255\green0\blue0;}\pard\qc\f0\fs28\cf1\b Cora\'e7\'e3o\b0\i  normal\i0\ul  sublinhado\ulnone\par}`
    async function render(rtf, target) {
      const doc = new RTFJS.Document(Uint8Array.from(rtf, c => c.charCodeAt(0)).buffer)
      target.replaceChildren(...await doc.render())
    }
    const editor = document.querySelector('#editor')
    await render(original, editor)
    editor.querySelector('span').append(' alterado')
    const saved = window.serialize(editor)
    const target = document.querySelector('#roundtrip')
    await render(saved, target)
    return { saved, text: target.textContent, runs: [...target.querySelectorAll('span')].map(x => ({ text: x.textContent, font: x.style.fontFamily, size: x.style.fontSize, weight: x.style.fontWeight, color: x.style.color, italic: x.style.fontStyle, decoration: x.style.textDecoration })) }
  })
  assert.match(result.text, /Coração alterado/)
  assert.match(result.saved, /\\qc/)
  assert.ok(result.runs.some(x => x.weight === 'bold' && x.font.includes('Tahoma') && x.color === 'rgb(255, 0, 0)'))
  assert.ok(result.runs.some(x => x.italic === 'italic'))
  assert.ok(result.runs.some(x => x.decoration.includes('underline')))
  console.log('RTF: edição preservou acentos, fonte, cor, negrito, itálico, sublinhado e alinhamento.')
} finally { await browser.close() }
