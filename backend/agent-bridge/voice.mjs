#!/usr/bin/env node
/**
 * Voice laudo bridge — interpretação por voz e exportação LaudosUX
 *
 * Usage:
 *   node voice.mjs apply <payload.json>
 *   node voice.mjs bootstrap <payload.json>
 *   node voice.mjs export <payload.json>
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

function extractJson(text) {
  if (!text || typeof text !== 'string') return null
  const fenced = text.match(/```(?:json)?\s*([\s\S]*?)```/i)
  const candidate = fenced?.[1]?.trim() ?? text.trim()
  try {
    return JSON.parse(candidate)
  } catch {
    const start = candidate.indexOf('{')
    const end = candidate.lastIndexOf('}')
    if (start >= 0 && end > start) {
      try {
        return JSON.parse(candidate.slice(start, end + 1))
      } catch {
        return null
      }
    }
    return null
  }
}

function buildPromptApply(transcript, camposScriptJson, mode, sourceFileName, intent) {
  const isBuild = (intent ?? 'build').toLowerCase() === 'build'
  const fromScratch = (mode ?? 'FromScratch').toLowerCase().includes('scratch')

  const buildLines = [
    'Você é especialista em modelos LaudosUX (JSON Studio camposScript).',
    'Leia AGENTE-LAUDO-POR-VOZ.md e AGENTE-MODELOS-MODO-TEXTO.md (seção JSON Studio) na cwd.',
    '',
    '## Intent: BUILD — criar modelo a partir da fala',
    '',
    'O médico descreveu em voz natural (transcript abaixo) o laudo desejado.',
    fromScratch
      ? 'Sessão DO ZERO: não há imagem nem modelo prévio. Gere o camposScript COMPLETO apenas a partir da fala.'
      : 'Gere ou reconstrua o camposScript completo conforme a descrição falada.',
    '',
    'Regras BUILD:',
    '- Extraia TODAS as seções mencionadas (ex.: dados do paciente, câmaras esquerda).',
    '- Extraia TODOS os campos com unidade (mm, cm, kg, m²) e normalidades M/F quando indicadas.',
    '- Corrija erros prováveis de STT (ex.: "via seda" → via de saída do VE, "anel aust" → anel aórtico).',
    '- Normalidade única "28.5 a 35.9" → aplique em M e F ou conforme contexto clínico.',
    '- Inclua fórmulas quando citadas (ex.: superfície corporal calculada).',
    '- NÃO inclua LAUDODESCRITIVO.',
    '- IDs únicos incrementais; coluna 1 salvo indicação contrária.',
    '',
    'Ignore o JSON vazio abaixo se fromScratch — substitua pelo modelo gerado.',
  ]

  const editLines = [
    'Você é especialista em modelos LaudosUX (JSON Studio camposScript).',
    'Leia AGENTE-LAUDO-POR-VOZ.md e AGENTE-MODELOS-MODO-TEXTO.md na cwd.',
    '',
    '## Intent: EDIT — alterar modelo existente',
    '',
    'Aplique SOMENTE os comandos de edição da fala ao JSON existente.',
    'Suporte: adicionar/remover/mover campos, criar seções, normalidades M/F, unidades, listas, fórmulas.',
    'Preserve campos não mencionados.',
    '',
    'Estado atual (JSON):',
    camposScriptJson,
  ]

  const tail = [
    '',
    'Fala / transcript do usuário (fonte principal):',
    transcript,
    '',
    'Responda SOMENTE JSON válido:',
    '{ "camposScript": [ ... ], "summary": "...", "warnings": [] }',
    'Preferir retornar camposScript como array no objeto raiz (não string escapada).',
    isBuild
      ? 'summary deve indicar quantos campos/seções foram criados (ex.: "Modelo criado com 8 campos em 2 seções.").'
      : 'summary deve descrever o que foi alterado.',
  ]

  return [...(isBuild ? buildLines : editLines), ...tail].join('\n')
}

function buildPromptBootstrap(imagePath, fileName) {
  return [
    'Analise a imagem do laudo e gere JSON Studio (camposScript) inicial.',
    'Siga AGENTE-LAUDO-POR-VOZ.md e AGENTE-MODELOS-MODO-TEXTO.md (seção JSON Studio).',
    '',
    `Arquivo: ${fileName}`,
    `Imagem: ${imagePath}`,
    '',
    'Inclua seções, campos numéricos com unidade e normalidades quando visíveis.',
    'NÃO inclua LAUDODESCRITIVO.',
    '',
    'Responda SOMENTE JSON: { "camposScript": [ ... ] }'
  ].join('\n')
}

function buildPromptExport(camposScriptJson, format) {
  if (format === 'modoTexto') {
    return [
      'Converta o JSON Studio abaixo para TXT modo texto (DSL LaudosUX).',
      'Siga AGENTE-MODELOS-MODO-TEXTO.md — uma linha por campo, [SEÇÃO], sem LAUDODESCRITIVO.',
      '',
      camposScriptJson,
      '',
      'Responda SOMENTE o TXT puro, sem markdown.'
    ].join('\n')
  }

  return [
    'Converta o JSON Studio abaixo para HTML LaudosUX (fragmento).',
    'Siga manual_scripts_html_UX.md — style, containerHtml, script com iniciarFuncoes().',
    '',
    camposScriptJson,
    '',
    'Responda SOMENTE HTML, sem markdown.'
  ].join('\n')
}

async function runAgent(prompt, images = []) {
  const apiKey = process.env.CURSOR_API_KEY?.trim()
  if (!apiKey) fail('CURSOR_API_KEY não definida.')

  const modelId = process.env.CURSOR_MODEL?.trim() || 'composer-2.5'
  const useFast = (process.env.CURSOR_USE_FAST ?? 'true').toLowerCase() !== 'false'
  const repoRoot = resolve(process.env.CURSOR_REPO_ROOT?.trim() || resolve(__dirname, '..'))

  const agent = await Agent.create({
    apiKey,
    model: { id: modelId, ...(useFast ? { params: [{ id: 'fast', value: 'true' }] } : {}) },
    local: { cwd: repoRoot }
  })

  try {
    const run = await agent.send({ text: prompt, images })
    const result = await run.wait()
    if (result.status === 'error') {
      fail(result.error?.message || 'Agent error', 2)
    }
    return result.result ?? ''
  } finally {
    try {
      if (typeof agent[Symbol.asyncDispose] === 'function') await agent[Symbol.asyncDispose]()
    } catch {
      // ignore
    }
  }
}

function normalizeCamposScriptJson(parsed, fallback) {
  if (!parsed) return fallback
  if (typeof parsed.camposScriptJson === 'string') {
    return parsed.camposScriptJson
  }
  if (parsed.camposScript) {
    return JSON.stringify({ camposScript: parsed.camposScript })
  }
  if (parsed.camposScriptJson?.camposScript) {
    return JSON.stringify(parsed.camposScriptJson)
  }
  return fallback
}

async function cmdApply(payload) {
  const prompt = buildPromptApply(
    payload.transcript,
    payload.camposScriptJson,
    payload.mode,
    payload.sourceFileName,
    payload.intent
  )
  const raw = await runAgent(prompt)
  const parsed = extractJson(raw)
  const camposScriptJson = normalizeCamposScriptJson(parsed, payload.camposScriptJson)
  process.stdout.write(JSON.stringify({
    camposScriptJson,
    summary: parsed?.summary ?? 'Alterações aplicadas pelo agente.',
    warnings: parsed?.warnings ?? []
  }))
}

async function cmdBootstrap(payload) {
  const imagePath = resolve(payload.imagePath)
  const imageBytes = await readFile(imagePath)
  const base64 = imageBytes.toString('base64')
  const prompt = buildPromptBootstrap(imagePath, payload.fileName)
  const raw = await runAgent(prompt, [{ data: base64, mimeType: payload.mimeType || 'image/png' }])
  const parsed = extractJson(raw)
  const json = parsed?.camposScript
    ? JSON.stringify({ camposScript: parsed.camposScript })
    : JSON.stringify(parsed ?? { camposScript: [] })
  process.stdout.write(JSON.stringify({ camposScriptJson: json }))
}

async function cmdExport(payload) {
  const prompt = buildPromptExport(payload.camposScriptJson, payload.format)
  const raw = await runAgent(prompt)
  let content = raw.trim()
  if (payload.format === 'modoTexto') {
    const fenced = content.match(/```(?:txt|text)?\s*([\s\S]*?)```/i)
    if (fenced?.[1]) content = fenced[1].trim()
  } else {
    const fenced = content.match(/```(?:html)?\s*([\s\S]*?)```/i)
    if (fenced?.[1]) content = fenced[1].trim()
    const styleIdx = content.search(/<style[\s>]/i)
    if (styleIdx >= 0) content = content.slice(styleIdx)
  }
  process.stdout.write(JSON.stringify({ content, format: payload.format }))
}

async function main() {
  const command = process.argv[2]
  const payloadPath = process.argv[3]
  if (!command || !payloadPath) {
    fail('Usage: node voice.mjs <apply|bootstrap|export> <payload.json>')
  }

  let payload
  try {
    payload = JSON.parse(await readFile(payloadPath, 'utf8'))
  } catch (err) {
    fail(`Payload inválido: ${err.message}`)
  }

  try {
    if (command === 'apply') await cmdApply(payload)
    else if (command === 'bootstrap') await cmdBootstrap(payload)
    else if (command === 'export') await cmdExport(payload)
    else fail(`Comando desconhecido: ${command}`)
  } catch (err) {
    if (err instanceof CursorAgentError) fail(`CursorAgentError: ${err.message}`)
    fail(err?.stack || err?.message || String(err))
  }
}

main()
