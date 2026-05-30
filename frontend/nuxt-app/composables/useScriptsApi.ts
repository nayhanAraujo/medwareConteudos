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

  const listVersoes = (id: number) =>
    api.get<{ data: { codVersao: number; numeroVersao: string; dataCriacao?: string; ativo?: string }[] }>(
      `/api/web/scripts/${id}/versoes`
    )

  const getVersao = (id: number, codVersao: number) =>
    api.get<{ data: ScriptVersionDetailDto }>(`/api/web/scripts/${id}/versoes/${codVersao}`)

  const createVersao = (id: number, body: { numeroVersao: string; observacoes?: string; criadoPor?: string }) =>
    api.post<{ codVersao: number }>(`/api/web/scripts/${id}/versoes`, body)

  const ativarVersao = (codVersao: number) => api.post(`/api/web/scripts/versoes/${codVersao}/ativar`)

  const exportUrl = (path: string) => {
    const config = useRuntimeConfig()
    const auth = useAuthStore()
    return `${config.public.apiBase}${path}${path.includes('?') ? '&' : '?'}token=${encodeURIComponent(auth.token)}`
  }

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
    toggleAtivo,
    toggleAprovacao,
    getVariaveis,
    saveVariaveis,
    getMrd,
    addMrd,
    deleteMrd,
    listVersoes,
    getVersao,
    createVersao,
    ativarVersao,
    download,
    getEmails,
    saveEmails,
    getAprovar,
    postAprovar,
    sendImagesEmail,
    solicitarAprovacao
  }
}
