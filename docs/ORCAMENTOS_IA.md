# Análise de orçamentos por IA

## Evolução: classificação, briefing e regras técnicas

O fluxo novo tem **duas chamadas explícitas** ao fornecedor:

1. Na etapa de análise, preencher o briefing “O que você deseja que a IA analise?”, inicialmente copiado da solicitação interna; selecionar anexos e confirmar o envio.
2. “Identificar solicitação” produz apenas classificação, sem horas. O usuário confirma/corrige categoria, tecnologia, perfil e serviços de cada frente; pode haver contextos mistos no mesmo orçamento.
3. “Confirmar classificação e analisar” solicita outra chamada com as regras correspondentes. Frentes de laudo exigem tecnologia, perfil e pelo menos um serviço. Códigos inexistentes/inativos são recusados; a IA deve devolver `null`/lista vazia quando não souber classificar.
4. Se alguma frente não tiver regras específicas publicadas, a análise genérica exige confirmação própria. As sugestões técnicas só são adicionadas após seleção e aplicação manual.

Cada fase ocupa um registro na fila, conta para o limite de 50 registros por orçamento e pode gerar custo no fornecedor. Atualizar status apenas consulta a fila. Indicadores distinguem envio, fila, identificação, análise e espera pela confirmação; não há porcentagem presumida. Animações respeitam movimento reduzido.

Em **Configurações de Orçamentos → Regras técnicas**, usuários com `orcamentos.configurar_regras` podem consultar o cadastro completo e publicar versões do catálogo, com motivo obrigatório. A consulta `/api/web/orcamentos/regras-tecnicas/aplicaveis`, protegida por `usar_ia`, retorna somente entradas ativas; ambas exigem visualizar Orçamentos. Perfis iniciais: Geral e Ecocardiograma. Serviços iniciais: ajuste de layout, inclusão/remoção de campos e atualização de normalidades. Exemplos de regras são inicialmente **inativos**; a gestão deve revisá-los e ativá-los explicitamente. Tecnologias são lidas de `TIPOLINGUAGEM`, sem códigos HTML/C# fixados no código da aplicação.

Regras têm aplicabilidade, orientação e checklist obrigatório/orientativo, sem preços ou horas-base. Regras gerais e específicas são combinadas por frente, sem substituição silenciosa. O prompt orienta o modelo a destacar contradições como dúvidas/impedimentos: a gestão e o revisor devem conferir conflitos de conteúdo, que não são demonstráveis automaticamente por simples comparação de textos.

O catálogo é congelado **na primeira fase**, junto às opções enviadas para classificação, e reutilizado na confirmação/análise técnica. Uma publicação posterior não muda os códigos, orientações ou checklists dessa solicitação. Para adotar regras recém-publicadas, solicitar nova identificação. Contextos e regras são copiados para os itens aplicados; respostas do checklist e resoluções registram autoria/data no servidor e tornam o usuário editor da revisão. A IA não responde os checklists pelo usuário.

No painel “O que falta para concluir este orçamento”, todas as pendências aparecem separadamente, com efeito e atalhos para campos/etapas. Pendências técnicas ficam visíveis no item, fora das seções recolhidas. Desmarcar um impedimento previamente salvo ou registrar esclarecimento exige justificativa. Problemas objetivos de preço, validade, regras ou pagamento só desaparecem corrigindo os dados; não podem ser encerrados por justificativa genérica. Após editar, salvar para revalidar; o painel identifica pendências da última revisão salva enquanto o formulário estiver sujo. Alterar descrição, categoria, dependências ou premissas reabre respostas de checklist que foram apenas copiadas sem nova justificativa; alterações exclusivamente de preço não reabrem verificações técnicas.

Migração aditiva: `ORCAMENTOREGRAVERSAO`, `ORCAMENTOREGRACONFIG` e colunas `ETAPA`, `CODANALISEORIGEM`, `CONTEXTO` em `ORCAMENTOANALISE`. O migrador padrão instala a estrutura, o catálogo inicial e a permissão. O arquivo `sql/scripts/evolve-orcamentos-technical-rules.sql` prepara a estrutura e é repetível; se utilizado manualmente, concluir com `--migrate-database` para seeds/permissões. Não executar em banco operacional sem backup, conferência do alvo e autorização. A API recusa iniciar com estrutura/seed incompletos.

Registros anteriores recebem fase `analise`, mantendo entradas/resultados legados legíveis. Itens antigos não recebem regras/checklists retroativamente, e propostas aprovadas permanecem preservadas. Rotas novas: `GET/PUT /api/web/orcamentos/regras-tecnicas` e `POST /api/web/orcamentos/{id}/analises/{job}/confirmar`. O POST de solicitação pelo portal agora exige briefing e inicia a classificação. As pendências estruturadas são aditivas; o texto legado `pending` permanece disponível.

## Recursos gerais

Análise pelo Composer separada do Studio, com fila persistente no Firebird, seleção explícita de documentos, confirmação de processamento externo, consulta de estado, cancelamento e aplicação manual de itens. O modelo sugere escopo, categoria, complexidade justificada, horas por etapa, dependências e pendências. Não define preços, descontos, condições de pagamento, validade ou prazos; não aprova propostas nem envia e-mails.

As sugestões são exibidas antes de qualquer alteração. O usuário seleciona quais itens adicionar ao rascunho; depois pode editá-los normalmente. As horas não são fatos nem garantias: precisam de revisão técnica. A política comercial congelada na revisão continua sendo aplicada exclusivamente pelo cálculo decimal do servidor. O aplicador torna-se editor e não pode aprovar sua própria revisão.

## Segurança e tratamento de dados

O bridge `backend/quote-ai-bridge` é independente do `agent-bridge` usado pelo Studio. Usa `@cursor/sdk` fixado em `1.0.40`, versão verificada em runtime, com `tools: []`, bloqueio de shell/MCP/subagentes e `settingSources: []`. Não retoma agentes nem reutiliza conversas. O agente não recebe ferramentas para ler/gravar arquivos, consultar bancos, executar comandos ou navegar na rede. Isso é uma restrição do conjunto de ferramentas do SDK, **não uma sandbox de sistema operacional**.

O processo Node roda em diretório temporário próprio, com ambiente reduzido; não recebe senhas Firebird nem caminhos/configuração do Studio. A chave existente `Cursor:ApiKey` é fornecida apenas ao SDK, nunca ao prompt ou ao navegador. A conta do pool deve ter os privilégios mínimos necessários; não executar o pool como administrador. O serviço precisa de saída HTTPS para o Cursor. O processo é encerrado com seus descendentes no timeout/cancelamento e o diretório temporário é removido quando possível.

O usuário confirma o envio ao fornecedor. O contexto contém título, solicitação interna, descrições dos itens já existentes e somente os anexos de escopo selecionados da revisão atual. Não são adicionados automaticamente cadastro comercial, contatos, mensalidade, preços, propostas ou evidências. Ainda assim, esses dados podem estar no texto/documento fornecido: o usuário deve removê-los antes de enviar. Não anexar dados de pacientes ou dados pessoais desnecessários.

Documentos e saída do modelo são dados não confiáveis. Textos do resultado são escapados pela renderização Vue; JSON e campos são validados no servidor. Campos comerciais extras e estimativas inválidas são rejeitados. A configuração do modelo não pode ser informada pelo navegador. Não registrar conteúdo dos documentos, prompts, resultados ou credenciais nos logs de infraestrutura.

## Formatos e limites

- Escopo textual salvo no orçamento e CSV UTF-8.
- PDF com texto selecionável, até 60 páginas; PDFs digitalizados sem texto são recusados com orientação para enviar PNG/JPEG. Não há OCR automático de PDF.
- DOCX: texto principal; imagens, cabeçalhos, notas, objetos e layout complexo não são analisados como uma reprodução fiel do Word.
- XLSX: valores, fórmulas como texto e strings compartilhadas; sem executar fórmulas, macros ou links. Estilos, datas formatadas, gráficos e layout não têm fidelidade garantida. O modelo deve tratar referências de strings compartilhadas e índices como parte da representação técnica recebida.
- PNG/JPEG: enviados à análise visual do modelo.
- Até cinco anexos, 8 MB somados e 50 mil caracteres de contexto. XML Office tem limites adicionais de tamanho; as validações de upload continuam valendo.
- Resposta: até 100 mil caracteres, 100 itens e 30 avisos; até 50 análises por orçamento. As dez mais recentes aparecem na consulta.

As limitações de extração e a incerteza do modelo exigem conferência dos arquivos originais. Não interpretar uma extração parcial de formatos complexos como garantia de completude.

## Configuração e publicação

Nenhuma migração operacional ou chamada paga ao Cursor foi executada durante a implementação. A funcionalidade fica desabilitada por padrão.

1. Preparar backup e publicar o código pelo procedimento habitual. Executar o migrador padrão, no banco conferido, para adicionar `ORCAMENTOANALISE`. O script `sql/scripts/add-orcamentoanalise.sql` é alternativa manual de execução única; não executar ambos indiscriminadamente. A migração não remove dados existentes. Se houver o esquema antigo com underscores, corrigir sua nomenclatura antes, conforme `CENTRAL_ORCAMENTOS.md`.
2. O pacote IIS inclui `api/quote-ai-bridge` e suas dependências. No desenvolvimento, executar `npm ci` nessa pasta. Node 22 ou superior é necessário. O SDK antigo do Studio não é atualizado por esta mudança.
3. Acrescentar à configuração local não versionada do ambiente:

```json
"OrcamentosAI": {
  "Enabled": true,
  "NodePath": "C:\\Program Files\\nodejs\\node.exe",
  "BridgePath": "quote-ai-bridge/analyze.mjs",
  "Model": "composer-2.5",
  "TimeoutSeconds": 180
}
```

4. Manter `Cursor:ApiKey` configurado no servidor. Não inserir chaves em arquivos versionados. Reiniciar apenas o componente desta API após a configuração, em janela autorizada.
5. Conceder `orcamentos.visualizar`, `orcamentos.elaborar` e `orcamentos.usar_ia` aos operadores. Administradores passam pelo mesmo fluxo de confirmação e revisão.

Não habilitar o serviço antes de conferir a versão instalada do bridge, permissões do pool, saída HTTPS e política organizacional de envio de documentos ao fornecedor.

## Processamento, concorrência e recuperação

`ORCAMENTOANALISE` segue o padrão físico do projeto: chave `CODORCAMENTOANALISE`, vínculo composto à revisão, versão-base, entradas e resultado privados, usuário, modelo e horários UTC. Estados: `pendente`, `executando`, `concluida`, `erro`, `cancelada` e `aplicada`.

A criação serializa pela versão do orçamento, impede duas análises simultâneas do mesmo orçamento e incrementa essa versão. A execução usa reivindicação condicional para evitar duplicidade entre instâncias. Não mantém transação aberta durante a chamada ao fornecedor. O worker verifica as permissões e a atividade do solicitante antes e depois da chamada.

Revisões aprovadas não podem ser analisadas/aplicadas. Qualquer mudança posterior à solicitação invalida sua aplicação, mesmo que o resultado tenha sido concluído. Aplicação e auditoria são transacionais e não podem ser repetidas.

Cancelar uma execução em andamento interrompe o processo quando o worker observar a mudança, normalmente em até um segundo. O fornecedor pode já ter processado parte da chamada e cobrado por ela: cancelamento não garante estorno. Execuções abandonadas após queda/recycle são marcadas como erro depois do timeout acrescido de tolerância; **não são reenviadas automaticamente**, evitando custos duplicados. Análises pendentes continuam na fila.

Entradas e resultados permanecem privados no banco para rastreabilidade. Não há limpeza automática nem política legal de retenção presumida; o limite por orçamento reduz crescimento, mas backups e retenção devem ser dimensionados pela organização.

## Testes

As entradas, resultados e documentos do módulo são enviados ao driver Firebird como parâmetros explícitos de BLOB de texto. Não usar strings longas genéricas nesses parâmetros: o Dapper pode definir `Size=-1` acima de 4000 caracteres, rejeitado pelo driver antes da execução SQL. A regressão automatizada cobre anexo com texto extenso, resultado com acentos, aplicação, salvamento e cópia para nova revisão sem truncamento. Esta correção é de código e não requer alteração do esquema.

`npm test` em `backend/quote-ai-bridge`; testes .NET em `backend/ConversorHtml.Tests`; testes/build Nuxt habituais. A integração automatizada usa Firebird temporário e provedor simulado: prova persistência, consentimento, permissões, aplicação explícita, rejeição de sugestão superada, cancelamento e ausência de preços arbitrados pelo modelo. O teste de segurança do bridge verifica a allowlist vazia de ferramentas. A validação real do fornecedor deve ser feita depois da configuração, com documento ilustrativo sem dados pessoais e ciência do custo.

Referência do controle de ferramentas: [documentação oficial do SDK Cursor](https://cursor.com/docs/sdk/typescript#restricting-the-toolset).
