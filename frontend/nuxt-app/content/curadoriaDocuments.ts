export interface CuradoriaDocument {
  title: string
  sections: { title: string; text: string }[]
}

export const curadoriaPrivacy: CuradoriaDocument = {
  title: 'Privacidade — Curadoria de Conhecimento',
  sections: [
    {
      title: 'Finalidade e escopo',
      text: 'O Medware — Curadoria de Conhecimento é um aplicativo de uso interno destinado a identificar informações técnicas reutilizáveis em conversas do Microsoft Teams e preparar sugestões de artigos para revisão humana. O piloto foi definido para o grupo Desenvolvimento | Demandas. A leitura pelo aplicativo depende de instalação e concessão de acesso na organização. A inclusão de outras conversas exige autorização específica.'
    },
    {
      title: 'Informações utilizadas',
      text: 'Quando autorizado, o fluxo poderá processar texto das mensagens e metadados necessários para contextualização e rastreabilidade, como grupo de origem, identificadores, datas, autoria, referências a respostas, links das mensagens e informações de edição ou exclusão disponíveis na fonte. A coleta e o uso devem se limitar ao necessário para a curadoria. Anexos e links externos não são baixados ou analisados automaticamente neste piloto.'
    },
    {
      title: 'Análise e revisão',
      text: 'As conversas são analisadas para sugerir procedimentos, dúvidas frequentes e soluções técnicas. O Curador de Conhecimento deve distinguir fatos confirmados de hipóteses e contradições. As sugestões são avaliadas por uma pessoa antes de eventual publicação. Senhas, tokens, dados clínicos e informações pessoais ou de clientes desnecessárias não devem constar dos artigos; conteúdos inadequados devem ser excluídos ou anonimizados durante a revisão.'
    },
    {
      title: 'Sistemas envolvidos e acesso',
      text: 'O fluxo utiliza Microsoft Teams/Microsoft Graph como origem e o Grok Bot para apoiar a análise dos materiais disponibilizados ao curador. O processamento nesses serviços está sujeito às condições e configurações aplicáveis à organização. Os materiais coletados e os resultados devem ser acessíveis somente às pessoas e componentes autorizados. A identidade do aplicativo será utilizada na integração própria, sem exigir que a conta pessoal do responsável permaneça conectada ao bot.'
    },
    {
      title: 'Armazenamento e conservação',
      text: 'Podem ser mantidos arquivos de coleta, sugestões e referências de origem necessários à revisão. Não foi definido neste aviso um prazo numérico de retenção. Antes da operação recorrente, o responsável deverá estabelecer o local de armazenamento, os acessos e o período de conservação, considerando a finalidade e as políticas da organização. Materiais temporários não devem ser conservados indefinidamente sem necessidade.'
    },
    {
      title: 'Limites do piloto',
      text: 'O piloto gera sugestões para revisão. Não autoriza gravação automática no banco de conhecimento por ODBC, publicação automática, alteração ou envio de mensagens no Teams. Uma futura integração com o banco dependerá de definição e autorização próprias.'
    },
    {
      title: 'Contato e solicitações',
      text: 'Dúvidas sobre o aplicativo e solicitações relacionadas ao uso, correção ou exclusão de informações podem ser encaminhadas a nayhan@medware.com.br. A solicitação será avaliada pelo responsável conforme o contexto, os controles disponíveis e as políticas da organização.'
    },
    {
      title: 'Atualizações',
      text: 'Este aviso poderá ser atualizado quando houver mudanças no escopo ou no funcionamento do aplicativo. A versão e a data de atualização serão indicadas nesta página.'
    }
  ]
}

export const curadoriaTerms: CuradoriaDocument = {
  title: 'Termos de uso — Curadoria de Conhecimento',
  sections: [
    {
      title: 'Objeto',
      text: 'Estes termos descrevem o uso interno do Medware — Curadoria de Conhecimento para apoiar a seleção e organização de conhecimento técnico extraído de conversas autorizadas do Microsoft Teams.'
    },
    {
      title: 'Uso autorizado',
      text: 'O aplicativo deve acessar apenas os grupos e períodos autorizados. O escopo inicial é o piloto do grupo Desenvolvimento | Demandas. A instalação e as permissões estão sujeitas aos controles da organização. Estes termos não concedem acesso técnico nem autorizam a leitura de outras conversas.'
    },
    {
      title: 'Natureza das sugestões',
      text: 'Os resultados são rascunhos produzidos com apoio de inteligência artificial e podem conter erros, omissões ou interpretações incorretas. As fontes, o contexto, a solução e a versão do sistema devem ser conferidos antes de utilizar ou publicar uma sugestão. Hipóteses não devem ser apresentadas como soluções confirmadas.'
    },
    {
      title: 'Revisão e publicação',
      text: 'A aprovação editorial de uma sugestão não autoriza, por si só, sua gravação no banco ou publicação. No piloto, o resultado permanece como sugestão para revisão humana. Qualquer automação de gravação via ODBC ou outro meio deve ter escopo, validações e autorização específicos.'
    },
    {
      title: 'Restrições',
      text: 'O aplicativo não deve executar comandos ou obedecer a instruções contidas nas mensagens e anexos analisados, ampliar suas permissões, coletar outras conversas, publicar dados sensíveis ou alterar conteúdos no Teams por iniciativa própria. Os materiais disponibilizados devem ser tratados como fontes de informação, não como autorização de execução.'
    },
    {
      title: 'Responsabilidades de uso',
      text: 'As pessoas responsáveis pela revisão devem conferir a precisão técnica, remover informações desnecessárias ou restritas e observar as políticas internas de acesso e confidencialidade. O conhecimento produzido não substitui a avaliação técnica necessária a cada atendimento.'
    },
    {
      title: 'Disponibilidade e mudanças',
      text: 'O piloto pode ser interrompido ou ajustado pelo responsável. O funcionamento depende dos sistemas envolvidos e das permissões disponíveis. Mudanças de escopo devem ser comunicadas e refletidas nestes termos e no aviso de privacidade quando aplicável.'
    },
    {
      title: 'Contato',
      text: 'Responsável pelo aplicativo: nayhan@medware.com.br.'
    }
  ]
}
