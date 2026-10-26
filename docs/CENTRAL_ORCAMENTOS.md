# Central de Orçamentos — primeira etapa

Rota interna: `/biblioteca/orcamentos`. O fluxo manual possui agora análise opcional por IA, descrita em `ORCAMENTOS_IA.md`, desabilitada por padrão até sua configuração. Não há envio de e-mail, cobrança ou exportação de bancos automáticos. O Studio permanece independente.

## Preparação e acesso

A migração foi registrada no migrador padrão `dotnet MdwConteudos.Api.dll --migrate-database`. Execute somente no ambiente e banco previamente conferidos, com backup e procedimento de implantação. A aplicação valida a existência do esquema ao iniciar; iniciar a API não substitui a migração. Os testes automatizados usam bancos temporários, não os bancos operacionais.

São criadas as tabelas `CLIENTECOMERCIAL`, `ORCAMENTOPOLITICA`, `ORCAMENTOCONFIG`, `ORCAMENTO`, `ORCAMENTOREVISAO`, `ORCAMENTOITEM`, `ORCAMENTO_USUARIO`, `ORCAMENTOEVENTO`, `ORCAMENTOANEXO` e `ORCAMENTOPROPOSTA`. Clientes e usuários são vinculados aos cadastros existentes. Não há exclusão de histórico financeiro.

Se o banco recebeu o esquema inicial com nomes como `ORCAMENTO_REVISAO`, aplicar primeiro `sql/scripts/normalize-orcamentos-names.sql`, com backup e API parada. O migrador recusa esse esquema para não misturar nomes antigos e novos. O script só aceita tabelas sem registros comerciais e a política inicial não personalizada; preserva o JSON, motivo e data desse seed. É de execução única e tem fases com commits explícitos por exigências do metadata Firebird. Em caso de erro, parar, conferir a fase concluída e seguir as instruções do cabeçalho; não repetir todo o arquivo nem iniciar a API sobre esquema parcial. Após a correção, usar a API atualizada e o migrador normal. Para banco ainda sem o módulo, o migrador já cria diretamente o padrão novo.

As convenções físicas estão em `PADRAO_BANCO_DADOS.md` e nas instruções para agentes no `../AGENTS.md`.

O domínio de permissões é `orcamentos`: `visualizar`, `elaborar`, `aprovar`, `registrar_marcos`, `configurar_precos`, `configurar_clientes`, `configurar_regras` e `usar_ia`. As regras técnicas são administradas separadamente dos preços. `usar_ia` controla a análise opcional, juntamente com a configuração do servidor. Perfis comuns não recebem acesso automaticamente. Administradores têm acesso, mas também são impedidos de aprovar revisões das quais participaram como editores, inclusive ao responder checklists.

## Uso e cálculo

A tela está organizada em cinco etapas: Solicitação; Análise do escopo (IA opcional ou elaboração manual); Itens e valores; Revisão e aprovação; Proposta e acompanhamento. Avançar pelo indicador ou por Continuar exige alterações salvas e pré-requisitos das etapas anteriores atendidos. É possível voltar e retomar o formulário onde há edição pendente para corrigi-la, sem liberar etapas posteriores. A IA é opcional; um briefing não enviado pode ser descartado explicitamente para elaboração manual. Análises em processamento devem terminar ou ser canceladas; classificações e sugestões concluídas precisam ser confirmadas/aplicadas ou descartadas explicitamente. O descarte conserva resultados e auditoria. Impedimentos nos itens/condições bloqueiam a revisão e a proposta requer aprovação. Registros aprovados/encerrados continuam navegáveis para consulta. O servidor também impede aprovação/envio enquanto houver análise pendente. Navegar não salva, não chama a IA, não aprova e não registra envio.

O checklist da revisão é informativo e não substitui a validação do servidor. Histórico e ações de manutenção ficam recolhíveis e disponíveis independentemente da etapa. Orçamentos em aprovação abrem na revisão; orçamentos aprovados/comerciais abrem no acompanhamento. Os controles por permissão e a leitura de revisões anteriores permanecem iguais.

1. Configure preços e condições comerciais nas configurações do módulo. Valor-hora, custo interno, validade e pagamento não são inventados pelo sistema.
2. Crie um rascunho para um cliente existente e inclua itens de categorias distintas.
3. Salve para recalcular no servidor. Campos pendentes produzem total indefinido, nunca um orçamento aparentemente gratuito.
4. Preencha descrições públicas, justificativas internas de complexidade, validade e pagamento antes de solicitar aprovação.
5. Outro usuário autorizado aprova ou devolve a revisão.
6. Prepare a proposta aprovada para copiar o texto do e-mail, baixar o PDF ou usar **Enviar proposta por e-mail**. Confira o destinatário (e-mail comercial congelado do cliente ou outro endereço informado), a pré-visualização e confirme o envio real. O marco comercial **Registrar envio** continua explícito e separado.
7. Registre aceite, pagamento com referência e encaminhamento com destino. Esses marcos não são deduzidos de downloads ou cliques.

No modo horas, somam-se análise, planejamento, desenvolvimento, testes e validação; essas horas são totais do item e não são multiplicadas novamente pela quantidade. O modo unitário multiplica quantidade pelo preço unitário. Descontos percentuais e absolutos são mutuamente exclusivos, exigem justificativa e não podem gerar valor negativo. O arredondamento é por item, em duas casas, afastando do zero no ponto médio.

A política inicial registra frases pela equipe a R$ 10 por unidade. Regras com interpretação comercial pendente estão marcadas para confirmação manual; selecionar uma regra ambígua não aplica um preço silenciosamente. Use valor manual justificado após esclarecer a condição. Valores ligados à mensalidade exigem que ela esteja informada no cadastro comercial do cliente.

Cada revisão guarda cópias da política, do cliente e dos cálculos. Alterar as configurações não altera revisões existentes. Em rascunhos, a atualização dessas cópias é explícita. Revisões aprovadas são somente leitura; uma nova revisão recomeça o fluxo de aprovação e marcos, conservando todo o histórico e a identificação dos editores. Conflitos de versão retornam HTTP 409, sem sobrescrever alterações concorrentes.

## Propostas, anexos e privacidade

PDF e texto de e-mail são preparados a partir da mesma projeção pública e persistidos por revisão. Solicitação interna, custos, dependências, reaproveitamento e justificativas internas não são exportados. Coloque condições que o cliente deve receber na descrição pública. A proposta antiga permanece disponível, identificada separadamente como superada quando houver revisão posterior.

O PDF usa a fonte DejaVu Sans incorporada à API (licença em `backend/MdwConteudos.Api/Assets/Fonts/LICENSE-DejaVu.txt`), sem depender das fontes do servidor ou de downloads. Preserva acentos e símbolos técnicos como ≥, ≤, →, ±, ² e letras gregas, com quebra por largura e paginação. Caracteres sem glifo disponível, incluindo emojis fora do plano básico, retornam validação explícita com o símbolo e seu código Unicode; nunca são removidos ou substituídos silenciosamente. O texto e os valores aprovados não são alterados. Não há envio SMTP automático.

Anexos ficam em BLOBs privados no Firebird, não em `static` ou URLs públicas. Limites: 20 MB por arquivo, 100 arquivos e 200 MB por orçamento, somando suas revisões. Formatos aceitos: PDF, DOCX, XLSX, CSV UTF-8, PNG e JPEG, com validação de estrutura/assinatura e restrições a conteúdo ativo. Essa validação não substitui antivírus. Anexar não dispara IA: é necessário selecionar materiais de escopo e confirmar o envio ao fornecedor; os limites de análise são menores que os de armazenamento. Anexos de escopo alteram a autoria da revisão; evidências comerciais não.

Eventos de domínio e auditoria são gravados na mesma transação das alterações. A auditoria central recebe identificadores e ações, não o conteúdo integral dos anexos, propostas ou observações confidenciais.

### Envio SMTP da proposta

Para solicitações recebidas pelo bot **Triagem de e-mails**, consulte [o contrato de integração](ORCAMENTOS_TRIAGEM_EMAIL.md). Ele acrescenta origem privada, importação idempotente, confirmação humana de cliente/escopo/destinatário e autorização revogável de envio pelo bot. O envio manual existente continua disponível, mas respeita a origem confirmada e não concorre com uma autorização pendente.

O endpoint `POST /api/web/orcamentos/{id}/proposta/enviar-email` exige `orcamentos.visualizar` e `orcamentos.registrar_marcos`, `version`, `revision`, `recipient` (um endereço) e `requestId` (UUID). Somente a revisão atual, não arquivada, em estado aprovado ou enviado e com proposta preparada pode ser enviada. Não é possível selecionar arquivos privados, alterar os valores aprovados ou informar assunto/corpo/servidor SMTP pelo navegador. A mensagem usa o texto público persistido e anexa exclusivamente o PDF daquela revisão.

Reutiliza `SMTP_SERVER`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD` e `SMTP_SENDER` (fallback no usuário), com STARTTLS obrigatório e timeout de 20 segundos. Homologação bloqueia envio real. Development e Production utilizam a configuração local do respectivo ambiente; não versionar senhas.

O histórico registra `email_iniciado` antes do envio e `email_enviado` (aceito pelo SMTP, não comprova entrega/leitura) ou `email_incerto`. SMTP não participa da transação do Firebird: queda do processo ou timeout pode deixar resultado incerto. Não há retentativa automática. Repetir o mesmo `requestId` nunca envia novamente; abrir um novo envio gera outra tentativa explícita. Confira o recebimento e o histórico antes de reenviar. Destinatário, usuário e horário ficam no histórico privado do orçamento; a auditoria central recebe somente os identificadores e códigos de ação. Nenhum novo esquema ou migrador é necessário para este recurso.

## Validação

- `.NET`: `dotnet test backend/ConversorHtml.Tests/ConversorHtml.Tests.csproj`.
- Frontend, dentro de `frontend/nuxt-app`: `npm run test:orcamentos`, `npm run test:permissions`, `npm run test:auditoria`, `npm run test:docs` e `npm run build`.
- Os testes Firebird precisam da biblioteca Embedded configurada em `PUBLICACAO_TEST_FBCLIENT`, conforme a infraestrutura de testes existente.

## Análise por IA

A análise multiformato pelo Composer possui bridge próprio, sem ferramentas de agente, limites, fila persistente e cancelamento. O fluxo tem identificação da solicitação e análise técnica em chamadas distintas, separadas pela confirmação humana das classificações. Há briefing próprio e catálogo de regras/checklists versionado nas configurações. Não reutiliza o executor do Studio. Consulte `ORCAMENTOS_IA.md` para instalação, configuração, migração aditiva, limitações de extração e tratamento de dados. OCR de PDFs digitalizados e documentos administrativos de referência continuam fora desta entrega; as regras técnicas são orientações textuais administradas pelo módulo.
