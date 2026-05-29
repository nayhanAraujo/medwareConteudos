import type { ScriptFormModel } from '~/components/scripts/ScriptForm.vue'

export function buildScriptFormData(
  model: ScriptFormModel,
  files: Record<string, File | FileList | null>
): FormData {
  const fd = new FormData()
  fd.append('nome', model.nome)
  fd.append('codpacote', String(model.codpacote))
  fd.append('descricao', model.descricao || '')
  fd.append('linguagem', model.linguagem || '')
  fd.append('caminho_projeto', model.caminho_projeto || '')
  fd.append('caminho_azure', model.caminho_azure || '')
  fd.append('link_teste', model.link_teste || '')
  fd.append('sistema', model.sistema)
  fd.append('aprovado', model.aprovado ? 'true' : 'false')
  fd.append('ativo', model.ativo ? 'true' : 'false')
  fd.append('aprovado_por', model.aprovado_por || '')
  fd.append('criado_por', model.criado_por || '')

  const json = files.arquivo_json
  if (json instanceof File) fd.append('arquivo_json', json)
  const dll = files.arquivo_dll
  if (dll instanceof File) fd.append('arquivo_dll', dll)
  const mrd = files.arquivos_mrd
  if (mrd instanceof FileList) Array.from(mrd).forEach((f) => fd.append('arquivos_mrd', f))
  const imgs = files.imagens
  if (imgs instanceof FileList) Array.from(imgs).forEach((f) => fd.append('imagens', f))
  const pdfs = files.pdfs
  if (pdfs instanceof FileList) Array.from(pdfs).forEach((f) => fd.append('pdfs', f))
  return fd
}
