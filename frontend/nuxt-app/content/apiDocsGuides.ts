export const docsGuides: Record<string, { eyebrow: string; title: string; intro: string; sections: { title: string; text: string; code?: string }[] }> = {
  'inicio-rapido': {
    eyebrow: 'PRIMEIROS PASSOS', title: 'Da primeira chamada à integração.', intro: 'Use o endereço do ambiente selecionado e siga os três passos abaixo.',
    sections: [
      { title: '01 · Obtenha seu token', text: 'Envie a senha de parceiro fornecida pela Medware. O servidor retorna o JWT; não é necessário conhecer a chave de assinatura.', code: 'POST /apiconteudos/v1/token\nContent-Type: application/json\n\n{"senha": "SUA_SENHA_DE_PARCEIRO"}' },
      { title: '02 · Faça uma consulta', text: 'Inclua o token no cabeçalho Authorization das chamadas à API de Parceiros.', code: 'GET /apiconteudos/v1/variaveis\nAuthorization: Bearer SEU_TOKEN' },
      { title: '03 · Trate os retornos', text: 'Verifique o status HTTP antes de consumir a resposta. Listagens usam success, data, total e timestamp. Fórmulas e normalidades ecocardiográficas retornam mapas por variável; consulte o esquema da operação.' },
      { title: 'Renove quando necessário', text: 'O token deve pertencer ao dia atual em UTC e respeitar a tolerância configurada (24 horas por padrão). Não é uma garantia de 24 horas após a emissão. Ao receber 401, confira a validade e obtenha um novo token.' }
    ]
  },
  autenticacao: {
    eyebrow: 'ACESSO À API', title: 'Autenticação simples. Contextos separados.', intro: 'O token de parceiros e a sessão administrativa têm finalidades diferentes.',
    sections: [
      { title: 'API de Parceiros · JWT', text: 'As operações em /apiconteudos/v1 exigem Bearer JWT, exceto health, token e preflight OPTIONS. O botão Autorizar permite informar um token ou obtê-lo usando a senha de parceiro.', code: 'Authorization: Bearer SEU_TOKEN' },
      { title: 'API Interna', text: 'Os recursos em /api/v1 exigem JWT de parceiro ou JWT web. Usuários web precisam da permissão correspondente ao domínio e à ação. A documentação interna é restrita aos administradores.' },
      { title: 'Web Admin', text: 'Usa o JWT de login do sistema, obtido em /api/web/auth/login, e as permissões de cada usuário. Um JWT de parceiro não substitui a sessão web. Em homologação, use o token administrativo desse ambiente.' },
      { title: 'Validade e compatibilidade', text: 'A API verifica HS256, assinatura, senha e datahora (ou datetime). Horários devem estar em UTC, no dia atual e dentro da tolerância. A expiração exp é respeitada quando presente. Credenciais do console permanecem somente em memória e não aparecem nos exemplos copiados.' }
    ]
  },
  suporte: {
    eyebrow: 'PRECISA DE AJUDA?', title: 'Encontre o contexto do erro.', intro: 'Use o canal fornecido pela equipe Medware ou entre em contato com o administrador responsável pela sua integração.',
    sections: [
      { title: 'O que informar', text: 'Envie método, caminho da operação, ambiente, horário com fuso, status HTTP e identificador de requisição, quando disponível. Não envie senhas, tokens ou dados pessoais.' },
      { title: '401 ou 403', text: '401 indica ausência ou falha de autenticação. 403 indica acesso negado: confira o tipo do token e as permissões. As definições Interna e Web Admin exigem perfil administrativo.' },
      { title: '400, 404 ou 500', text: 'Confira os parâmetros obrigatórios e os tipos para erros 400. Em 404, verifique o identificador do recurso. Em 500, registre o contexto e procure o suporte antes de repetir operações de escrita.' },
      { title: 'Testes seguros', text: 'O console executa consultas no ambiente atual. Alterações de conteúdo só são permitidas na homologação configurada, após confirmação. Operações de publicação, envio de e-mail e outras integrações externas não são executadas pelo portal.' }
    ]
  }
}
