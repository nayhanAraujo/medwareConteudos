import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { readFile, mkdir } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
const require = createRequire(import.meta.url)
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright')
const { unzipSync, strFromU8 } = require('fflate')
const base = process.env.PHRASE_TEST_BASE_URL || 'http://localhost:3000'
const browser = await chromium.launch({ headless: true, channel: 'msedge' })
const context = await browser.newContext({ acceptDownloads: true })
const user = { codusuario: 1, nome: 'Teste', role: 'admin' }
await context.addCookies([{ name: 'mdw_token', value: 'isolated-test', url: base }, { name: 'mdw_user', value: encodeURIComponent(JSON.stringify(user)), url: base }])
await context.addInitScript(user => { localStorage.setItem('mdw_token', 'isolated-test'); localStorage.setItem('mdw_user', JSON.stringify(user)) }, user)
const requests = []
const rtf = String.raw`{\rtf1\ansi\ansicpg1252{\fonttbl{\f0 Tahoma;}}{\colortbl;\red255\green0\blue0;}\pard\qc\f0\fs28\cf1\b Cora\'e7\'e3o\b0\i  normal\i0\ul  sublinhado\ulnone\par}`
await context.route('**/api-dotnet/**', async route => {
  const url = new URL(route.request().url())
  const path = url.pathname
  let data = {}
  if (path.endsWith('/assistente/grupos')) data = { items: [{ CODGRUPO: 7, GRUPO: 'Ecocardiograma', STATUS: -1 }], page: 1, pageSize: 200, total: 1 }
  else if (path.endsWith('/assistente/frases')) {
    requests.push(Object.fromEntries(url.searchParams))
    const page = Number(url.searchParams.get('page')) || 1
    const size = Number(url.searchParams.get('pageSize')) || 20
    const count = size === 100 ? (page === 1 ? 100 : 1) : 20
    data = { items: Array.from({ length: count }, (_, i) => ({ CODFRASE: (page - 1) * size + i + 1, CODIGO: `E${i}`, TITULO: `Frase ${(page - 1) * size + i + 1}`, FRASE: rtf, CODGRUPO: 7, STATUS: -1 })), page, pageSize: size, total: 101 }
  }
  await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ success: true, data, total: data.total }) })
})
try {
  const page = await context.newPage()
  page.on('pageerror', error => console.log('Page error:', error.message))
  await page.goto(`${base}/assistente/frases`)
  const button = page.getByRole('button', { name: 'Exportar DOCX em ZIP' })
  await button.waitFor()
  await page.getByPlaceholder('Pesquisar frases...').fill('filtro ignorado')
  const downloadEvent = page.waitForEvent('download').catch(async error => { console.log(await page.locator('.swal2-container').innerText().catch(() => 'No alert')); throw error })
  await button.click()
  const download = await downloadEvent
  assert.equal(download.suggestedFilename(), 'Ecocardiograma.zip')
  const zip = unzipSync(await readFile(await download.path()))
  assert.equal(Object.keys(zip).length, 101)
  assert.ok(Object.keys(zip).every(name => name.endsWith('.docx')))
  const xml = strFromU8(unzipSync(zip['1 - Frase 1.docx'])['word/document.xml'])
  const lastXml = strFromU8(unzipSync(zip['101 - Frase 101.docx'])['word/document.xml'])
  for (const document of [xml, lastXml]) {
    assert.ok(!document.includes('Ecocardiograma'))
    assert.ok(!document.includes('Frase 1'))
    assert.ok(!document.includes('Código'))
    assert.ok(!document.includes('1 frase'))
    const text = [...document.matchAll(/<w:t(?:\s[^>]*)?>(.*?)<\/w:t>/g)].map(match => match[1]).join('')
    assert.equal(text, 'Coração normal sublinhado')
  }
  assert.ok(xml.includes('Coração'))
  assert.ok(xml.includes('w:b') && xml.includes('w:i') && xml.includes('w:u'))
  assert.match(xml, /w:color w:val="ff0000"/i)
  assert.ok(xml.includes('Tahoma') && xml.includes('w:jc w:val="center"'))
  assert.ok(requests.some(r => r.page === '2' && r.pageSize === '100' && !r.search && r.groupId === '7'))
  const output = new URL('../../../.codex-build/phrase-docx/', import.meta.url)
  await mkdir(output, { recursive: true })
  await page.screenshot({ path: fileURLToPath(new URL('desktop.png', output)), fullPage: true })
  await page.setViewportSize({ width: 390, height: 844 })
  await page.screenshot({ path: fileURLToPath(new URL('mobile.png', output)), fullPage: true })
  console.log('ZIP: 101 DOCX individuais de duas páginas; isolamento das frases, RTF e download validados.')
} finally { await browser.close() }
