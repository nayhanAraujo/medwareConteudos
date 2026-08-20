#!/usr/bin/env node
/**
 * Bridge: imagem + regras LaudosUX → HTML ou TXT modo texto via Cursor Composer 2.5
 *
 * Usage:
 *   node convert.mjs <imagePath> [mimeType] [format]
 *
 * format: html (default) | modoTexto
 *
 * Env:
 *   CURSOR_API_KEY (required)
 *   CURSOR_MODEL (default: composer-2.5)
 *   CURSOR_USE_FAST (default: true)
 *   CURSOR_REPO_ROOT (default: ../ from this script = backend/)
 *   CURSOR_OUTPUT_FORMAT (optional override for format)
 */
import { readFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { Agent, CursorAgentError } from '@cursor/sdk'

const __dirname = dirname(fileURLToPath(import.meta.url))

function fail(message, code = 1) {
  process.stderr.write(String(message) + '\n')
  process.exitCode = code
  setImmediate(() => process.exit(code))
}

function isAuthError(err) {
  const msg = String(err?.message ?? err ?? '')
  return (
    err?.name === 'AuthenticationError' ||
    /invalid user api key/i.test(msg) ||
    /invalid api key/i.test(msg) ||
    /unauthorized/i.test(msg)
  )
}

function normalizeFormat(value) {
  const v = String(value ?? 'html').trim().toLowerCase()
  if (v === 'modotexto' || v === 'modo-texto' || v === 'modo_texto' || v === 'txt' || v === 'text') {
    return 'modoTexto'
  }
  return 'html'
}

function extractHtml(text) {
  if (!text || typeof text !== 'string') return ''

  const fenced = text.match(/```(?:html)?\s*([\s\S]*?)```/i)
  if (fenced?.[1]) {
    return fenced[1].trim()
  }

  const styleIdx = text.search(/<style[\s>]/i)
  if (styleIdx >= 0) {
    return text.slice(styleIdx).trim()
  }

  return text.trim()
}

function extractText(text) {
  if (!text || typeof text !== 'string') return ''

  const fenced = text.match(/```(?:txt|text)?\s*([\s\S]*?)```/i)
  if (fenced?.[1]) {
    return fenced[1].trim()
  }

  return text.trim()
}

function buildPromptHtml(fileName) {
  return [
    'Você é um especialista em scripts HTML do sistema LaudosUX (Medware).',
    'Analise a imagem anexada (layout de laudo médico) e gere o HTML correspondente.',
    '',
    'OBRIGATÓRIO: leia e siga estritamente o arquivo manual_scripts_html_UX.md na raiz do repositório (cwd).',
    '',
    'Regras essenciais do output:',
    '- Fragmento HTML apenas (sem <!DOCTYPE>, <html>, <head>, <body>, <meta>, <title>, <link>)',
    '- Ordem: <style> → <div id="containerHtml"> → <script>',
    '- Layout em duas colunas (flexbox/grid), sem tabelas de layout',
    '- Sem CSS inline (style="")',
    '- Classes seguras (laudo-, campo-, secao-); evitar .container, .button, .input',
    '- Impressão: <div class="d-flex">, ats="imprimir", percent, indexImpressao, ids Collapse*',
    '- Campos: id VR_*, class campoMedida, descricaoMedida, unidadeMedida, referencia',
    '- Script com function iniciarFuncoes() e chamada iniciarFuncoes() no final',
    '',
    `Nome do arquivo de origem: ${fileName}`,
    '',
    'Responda SOMENTE com o HTML LaudosUX completo. Sem markdown, sem explicações.'
  ].join('\n')
}

function buildPromptModoTexto(fileName) {
  return [
    'Você é um especialista em modelos LaudosUX em modo texto (tipoScript = 3).',
    'Analise a imagem anexada (layout de laudo médico) e gere o arquivo TXT (DSL) para importação.',
    '',
    'OBRIGATÓRIO: leia e siga estritamente o arquivo AGENTE-MODELOS-MODO-TEXTO.md na raiz do repositório (cwd).',
    '',
    'Regras essenciais do output TXT:',
    '- Uma linha = um campo ou uma etiqueta de seção',
    '- Etiquetas: [NOME DA SEÇÃO]',
    '- Labels: *Descrição (VAR)',
    '- Numéricos: Descrição (VAR): 0.0  unidade (M: min a max) (F: min a max)',
    '- Listas: Descrição (VAR) = op1, op2 [Lista] ou [Única] ou [Múltipla]',
    '- Fórmulas: Código: if(<<VR_VAR>> > 0) { ... } else { \'\' }',
    '- NÃO use prefixo VR_ no nome entre parênteses do TXT',
    '- NÃO inclua campo LAUDODESCRITIVO (o importador adiciona automaticamente)',
    '- Organize seções pensando na divisão em duas colunas (primeira metade coluna 1, depois coluna 2)',
    '- UTF-8, português',
    '',
    `Nome do arquivo de origem: ${fileName}`,
    '',
    'Responda SOMENTE com o TXT puro. Sem markdown, sem explicações, sem fences ```.'
  ].join('\n')
}

async function main() {
  const imagePath = process.argv[2]
  const mimeType = process.argv[3] || 'image/png'
  const format = normalizeFormat(process.env.CURSOR_OUTPUT_FORMAT || process.argv[4] || 'html')

  if (!imagePath) {
    fail('Usage: node convert.mjs <imagePath> [mimeType] [format]')
  }

  const apiKey = process.env.CURSOR_API_KEY?.trim()
  if (!apiKey) {
    fail('CURSOR_API_KEY não definida.')
  }

  const modelId = process.env.CURSOR_MODEL?.trim() || 'composer-2.5'
  const useFast = (process.env.CURSOR_USE_FAST ?? 'true').toLowerCase() !== 'false'
  const repoRoot = resolve(
    process.env.CURSOR_REPO_ROOT?.trim() || resolve(__dirname, '..')
  )

  const absoluteImage = resolve(imagePath)
  let imageBytes
  try {
    imageBytes = await readFile(absoluteImage)
  } catch (err) {
    fail(`Falha ao ler imagem: ${err.message}`)
  }

  const base64 = imageBytes.toString('base64')
  const fileName = absoluteImage.split(/[/\\]/).pop() || 'image'

  const model = {
    id: modelId,
    ...(useFast ? { params: [{ id: 'fast', value: 'true' }] } : {})
  }

  const prompt = format === 'modoTexto' ? buildPromptModoTexto(fileName) : buildPromptHtml(fileName)

  let agent
  let skipDispose = false
  try {
    agent = await Agent.create({
      apiKey,
      model,
      local: { cwd: repoRoot }
    })

    const run = await agent.send({
      text: prompt,
      images: [{ data: base64, mimeType }]
    })

    const result = await run.wait()

    if (result.status === 'error') {
      const errMsg = result.error?.message || `Run do agente falhou (id=${result.id}).`
      fail(errMsg, 2)
    }

    const rawText = result.result ?? ''

    if (format === 'modoTexto') {
      const text = extractText(rawText)
      if (!text || !text.includes('[')) {
        fail('Resposta do agente não contém TXT modo texto válido (etiqueta [SEÇÃO] ausente).', 2)
      }
      process.stdout.write(JSON.stringify({
        format: 'modoTexto',
        text,
        runId: result.id,
        model: modelId
      }))
      return
    }

    const html = extractHtml(rawText)
    if (!html || !html.includes('containerHtml')) {
      fail('Resposta do agente não contém HTML LaudosUX válido (containerHtml ausente).', 2)
    }

    process.stdout.write(JSON.stringify({
      format: 'html',
      html,
      runId: result.id,
      model: modelId
    }))
  } catch (err) {
    if (isAuthError(err)) {
      skipDispose = true
      process.stderr.write(
        'AUTH_ERROR: Chave Cursor inválida ou sem permissão para o Agent SDK. ' +
          'Gere uma User API Key em https://cursor.com/dashboard/integrations e configure com: ' +
          'dotnet user-secrets set "Cursor:ApiKey" "<nova-chave>"\n'
      )
      process.exitCode = 3
      return
    }
    if (err instanceof CursorAgentError) {
      fail(`CursorAgentError: ${err.message} (retryable=${err.isRetryable})`, 1)
    }
    fail(err?.stack || err?.message || String(err), 1)
  } finally {
    if (skipDispose) return
    try {
      if (agent && typeof agent[Symbol.asyncDispose] === 'function') {
        await agent[Symbol.asyncDispose]()
      }
    } catch {
      // ignore dispose races after auth/network failures
    }
  }
}

main()
