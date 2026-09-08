export interface PacoteDto {
  codPacote: number
  nome: string
  descricao?: string
}

export interface ScriptMrdDto {
  codScriptMrd: number
  nomeArquivo: string
  padrao: boolean
  ordem?: number
}

export interface ScriptVersionFileDto {
  codArquivo: number
  tipo: string
  caminho?: string
  nomeArquivo: string
  dataUpload?: string
  usuarioUpload?: string
}

export interface ScriptVersionMrdDto {
  codVersaoMrd: number
  nomeArquivo: string
  padrao: boolean
  ordem?: number
}

export interface ScriptVersionHistoryDto {
  tipoAlteracao: string
  descricao?: string
  usuario?: string
  dataAlteracao?: string
}

export interface VersaoDto {
  codVersao: number
  numeroVersao: string
  dataCriacao?: string
  ativo?: string
  aprovado?: string
  observacoes?: string
  usuarioResponsavel?: string
  descricaoAlteracoes?: string
  aprovadoPor?: string
}

export interface VersaoCreateMetaDto {
  nomeScript: string
  descricaoScript?: string
  sistema: string
  linguagem?: string
  nomePacote?: string
  proximaVersao: string
  versoesExistentes: VersaoDto[]
}

export interface ScriptVersionDetailDto {
  codVersao: number
  codScriptLaudo: number
  numeroVersao: string
  nomeScript: string
  descricaoScript?: string
  sistema: string
  linguagem?: string
  nomePacote?: string
  descricaoAlteracoes?: string
  alteracoesInterface?: string
  alteracoesCodigo?: string
  dataCriacao?: string
  usuarioResponsavel?: string
  ativo?: string
  aprovado?: string
  aprovadoPor?: string
  dataAprovacao?: string
  observacoes?: string
  temArquivoJson: boolean
  temArquivoDll: boolean
  imagens: ScriptVersionFileDto[]
  pdfs: ScriptVersionFileDto[]
  mrdList: ScriptVersionMrdDto[]
  historico: ScriptVersionHistoryDto[]
}

export interface ScriptListItem {
  codScriptLaudo: number
  codPacote?: number
  nome: string
  descricao?: string
  linguagem?: string
  sistema: string
  aprovado: number
  ativo: number
  temArquivoJson: boolean
  nomePacote?: string
  caminhoAzure?: string
  temArquivoDll: boolean
  temArquivoMrd: boolean
  criadoPor?: string
  aprovadoPor?: string
  dataVerificacao?: string | null
  linkTeste?: string
  ultimaVersao?: string
  mrdList: ScriptMrdDto[]
  variaveis?: { variavel: string; nome: string }[]
  imagensDisplay: { caminho: string; nomeArquivo: string }[]
  pdfsDisplay?: { caminho: string; nomeArquivo: string }[]
}

export interface PagedScripts {
  data: ScriptListItem[]
  page: number
  totalPages: number
  totalItems: number
}

export interface ScriptSpecificMapping {
  scriptId: number
  specialtyId: number
  specialtyName?: string
}

export interface TransferPackageInfo {
  scriptCount: number
  destinationMapped: boolean
  specificMappings: ScriptSpecificMapping[]
}

export interface TransferPackageResult {
  transferredCount: number
  destinationMapped: boolean
  specificRuleScriptIds: number[]
}

export interface VersaoFormFiles {
  arquivo_json?: File | null
  arquivo_dll?: File | null
  arquivos_mrd?: FileList | null
  imagens?: FileList | null
  pdfs?: FileList | null
  mrd_padrao_versao_idx?: number
  mrd_padrao?: number
  mrd_excluir?: number[]
}

export function useScriptsApi() {
  const api = useApi()

  const getPacotes = () => api.get<{ data: PacoteDto[] }>('/api/web/scripts/pacotes')

  const listScripts = (q: Record<string, string | number | undefined>) => {
    const params = new URLSearchParams()
    Object.entries(q).forEach(([k, v]) => {
      if (v !== undefined && v !== '') params.set(k, String(v))
    })
    return api.get<PagedScripts>(`/api/web/scripts?${params}`)
  }

  const getScript = (id: number) => api.get<{ data: Record<string, unknown> }>(`/api/web/scripts/${id}`)

  const verificarNome = (nome: string, excludeId?: number) =>
    api.post<{ exists: boolean }>('/api/web/scripts/verificar-nome', { nome, excludeId })

  const createScript = (form: FormData) =>
    api.postForm<{ codScriptLaudo: number }>('/api/web/scripts', form)

  const updateScript = (id: number, form: FormData) =>
    api.putForm<{ success: boolean }>(`/api/web/scripts/${id}`, form)

  const validatePackageTransfer = (body: { pacoteOrigem: number; pacoteDestino: number; codigosScripts: number[] }) =>
    api.post<{ data: TransferPackageInfo }>('/api/web/scripts/transferir-pacote/validar', body)

  const transferPackage = (body: { pacoteOrigem: number; pacoteDestino: number; codigosScripts: number[] }) =>
    api.post<{ data: TransferPackageResult }>('/api/web/scripts/transferir-pacote', body)

  const toggleAtivo = (id: number) => api.post(`/api/web/scripts/${id}/toggle-ativo`)
  const toggleAprovacao = (id: number) => api.post(`/api/web/scripts/${id}/toggle-aprovacao`)

  const getVariaveis = (id: number) =>
    api.get<{ data: { codVariavel: number; nome: string; sigla: string; formula: string; normalidade: string; associada: boolean }[]; nomeScript: string }>(
      `/api/web/scripts/${id}/variaveis`
    )

  const saveVariaveis = (id: number, codVariaveis: number[]) =>
    api.post(`/api/web/scripts/${id}/variaveis`, { codVariaveis })

  const getMrd = (id: number) =>
    api.get<{ data: ScriptMrdDto[]; nomeScript: string; sistema: string }>(`/api/web/scripts/${id}/mrd`)

  const addMrd = (id: number, sistema: string, files: FileList | File[]) => {
    const fd = new FormData()
    fd.append('sistema', sistema)
    Array.from(files).forEach((f) => fd.append('arquivos_mrd', f))
    return api.postForm(`/api/web/scripts/${id}/mrd`, fd)
  }

  const deleteMrd = (codScriptMrd: number) => api.del(`/api/web/scripts/mrd/${codScriptMrd}`)
  const deleteScriptImage = (codArquivo: number) => api.del(`/api/web/scripts/anexos/${codArquivo}`)

  const listVersoes = (id: number, filters?: { numeroVersao?: string; aprovado?: string; ativo?: string }) => {
    const params = new URLSearchParams()
    if (filters?.numeroVersao) params.set('numeroVersao', filters.numeroVersao)
    if (filters?.aprovado) params.set('aprovado', filters.aprovado)
    if (filters?.ativo) params.set('ativo', filters.ativo)
    const q = params.toString()
    return api.get<{ data: VersaoDto[] }>(`/api/web/scripts/${id}/versoes${q ? `?${q}` : ''}`)
  }

  const getVersaoCreateMeta = (id: number) =>
    api.get<{ data: VersaoCreateMetaDto }>(`/api/web/scripts/${id}/versoes/meta`)

  const getVersao = (id: number, codVersao: number) =>
    api.get<{ data: ScriptVersionDetailDto }>(`/api/web/scripts/${id}/versoes/${codVersao}`)

  function buildVersaoFormData(
    body: {
      numeroVersao?: string
      descricaoAlteracoes: string
      alteracoesInterface?: string
      alteracoesCodigo?: string
      observacoes?: string
      usuarioResponsavel?: string
    },
    files?: VersaoFormFiles
  ) {
    const fd = new FormData()
    if (body.numeroVersao) fd.append('numero_versao', body.numeroVersao)
    fd.append('descricao_alteracoes', body.descricaoAlteracoes)
    if (body.alteracoesInterface) fd.append('alteracoes_interface', body.alteracoesInterface)
    if (body.alteracoesCodigo) fd.append('alteracoes_codigo', body.alteracoesCodigo)
    if (body.observacoes) fd.append('observacoes', body.observacoes)
    if (body.usuarioResponsavel) fd.append('usuario_responsavel', body.usuarioResponsavel)
    if (files?.arquivo_json) fd.append('arquivo_json', files.arquivo_json)
    if (files?.arquivo_dll) fd.append('arquivo_dll', files.arquivo_dll)
    if (files?.arquivos_mrd) Array.from(files.arquivos_mrd).forEach((f) => fd.append('arquivos_mrd', f))
    if (files?.imagens) Array.from(files.imagens).forEach((f) => fd.append('imagens', f))
    if (files?.pdfs) Array.from(files.pdfs).forEach((f) => fd.append('pdfs', f))
    if (files?.mrd_padrao_versao_idx != null)
      fd.append('mrd_padrao_versao_idx', String(files.mrd_padrao_versao_idx))
    if (files?.mrd_padrao != null) fd.append('mrd_padrao', String(files.mrd_padrao))
    files?.mrd_excluir?.forEach((id) => fd.append('mrd_excluir_list', String(id)))
    return fd
  }

  const createVersao = (
    id: number,
    body: Parameters<typeof buildVersaoFormData>[0],
    files?: VersaoFormFiles
  ) => api.postForm<{ codVersao: number }>(`/api/web/scripts/${id}/versoes`, buildVersaoFormData(body, files))

  const updateVersao = (
    codVersao: number,
    body: Parameters<typeof buildVersaoFormData>[0],
    files?: VersaoFormFiles
  ) => api.putForm<{ success: boolean }>(`/api/web/scripts/versoes/${codVersao}`, buildVersaoFormData(body, files))

  const ativarVersao = (codVersao: number) => api.post(`/api/web/scripts/versoes/${codVersao}/ativar`)
  const aprovarVersao = (codVersao: number) => api.post(`/api/web/scripts/versoes/${codVersao}/aprovar`)
  const excluirVersao = (codVersao: number) => api.del(`/api/web/scripts/versoes/${codVersao}`)
  const excluirVersaoAnexo = (codArquivo: number) => api.del(`/api/web/scripts/versoes/anexos/${codArquivo}`)

  const downloadVersaoArquivo = (codVersao: number, tipo: 'json' | 'dll' | 'mrd', filename: string) =>
    download(`/api/web/scripts/versoes/${codVersao}/exportar/${tipo}`, filename)

  const downloadVersaoMrd = (codVersao: number, codVersaoMrd: number, filename: string) =>
    download(`/api/web/scripts/versoes/${codVersao}/exportar-mrd?codVersaoMrd=${codVersaoMrd}`, filename)

  const downloadVersaoMrdZip = (codVersao: number, filename: string) =>
    download(`/api/web/scripts/versoes/${codVersao}/exportar-mrd-zip`, filename)

  const downloadVersaoAnexo = (codArquivo: number, filename: string) =>
    download(`/api/web/scripts/versoes/anexos/${codArquivo}/download`, filename)

  const download = (path: string, filename: string) =>
    api.getBlob(path).then((blob) => {
      const a = document.createElement('a')
      a.href = URL.createObjectURL(blob)
      a.download = filename
      a.click()
      URL.revokeObjectURL(a.href)
    })

  const getEmails = () => api.get<{ emails: string }>('/api/web/scripts/emails-notificacao')
  const saveEmails = (emails: string) => api.post('/api/web/scripts/emails-notificacao', { emails })

  const getAprovar = (token: string) => api.get<{ success: boolean; data?: Record<string, unknown>; status?: string }>(
    `/api/web/scripts/aprovar/${token}`
  )

  const postAprovar = (token: string, acao: 'aprovar' | 'rejeitar') =>
    api.post(`/api/web/scripts/aprovar/${token}`, { acao })

  const sendImagesEmail = (id: number, email: string, sistema: string) =>
    api.post(`/api/web/scripts/${id}/send-images-email`, { email, sistema })

  const solicitarAprovacao = (
    id: number,
    body: { nomeScript: string; linkTeste: string; emails: string[]; mensagem?: string }
  ) => api.post(`/api/web/scripts/${id}/solicitar-aprovacao`, body)

  return {
    getPacotes,
    listScripts,
    getScript,
    verificarNome,
    createScript,
    updateScript,
    validatePackageTransfer,
    transferPackage,
    toggleAtivo,
    toggleAprovacao,
    getVariaveis,
    saveVariaveis,
    getMrd,
    addMrd,
    deleteMrd,
    deleteScriptImage,
    listVersoes,
    getVersaoCreateMeta,
    getVersao,
    createVersao,
    updateVersao,
    ativarVersao,
    aprovarVersao,
    excluirVersao,
    excluirVersaoAnexo,
    downloadVersaoArquivo,
    downloadVersaoMrd,
    downloadVersaoMrdZip,
    downloadVersaoAnexo,
    download,
    getEmails,
    saveEmails,
    getAprovar,
    postAprovar,
    sendImagesEmail,
    solicitarAprovacao
  }
}
