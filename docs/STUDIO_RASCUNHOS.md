# Studio — editor integrado e rascunhos

Nas telas `/studio/modelos` e `/studio/voz`, a opção **Usar editor integrado
com rascunhos e versões** aparece quando o armazenamento persistente está
disponível. Use o editor para trabalhar em uma composição antes de exportar
ou confirmar uma publicação. Rascunho, revisão e modelo publicado são estados
distintos: editar ou pré-visualizar não publica.

## Habilitação

Configure `"StudioDrafts": { "Enabled": true }` no JSON privado da API.
O padrão é desabilitado. Com a opção habilitada, o migrador oficial
`--migrate-database` prepara o esquema dos rascunhos antes de iniciar a nova
release. O SQL de referência fica em
[`docs/sql/scripts/add-studio-rascunhos.sql`](sql/scripts/add-studio-rascunhos.sql).
Use apenas um banco autorizado e o fluxo de [deploy IIS](PRODUCAO_IIS.md);
não aplique DDL em um banco operacional como teste.

`GET /api/voice/rascunhos/status` informa a disponibilidade real. Em
`/studio/voz`, `GET /api/voice/capabilities` também informa se voz guiada,
persistência e conversação contínua estão disponíveis. São recursos
independentes: habilitar rascunhos não habilita automaticamente a xAI.

## Fluxo e diagnóstico

- Rascunhos e histórico são vinculados ao usuário autenticado. O editor
  consulta o catálogo, aplica comandos ao rascunho e consulta revisões.
- Valide antes de exportar ou publicar. A API separa a preparação da publicação
  (`POST /api/voice/rascunhos/{id}/publicacoes`) da confirmação
  (`POST /api/voice/rascunhos/{id}/publicacoes/confirmar`).
- Se a opção não aparecer, confira o flag, a migração do banco alvo e a resposta
  de disponibilidade. Não confunda uma sessão de voz em memória com um
  rascunho persistente: sessões em memória expiram e são perdidas no reinício.
- Para entrada de arquivos, consulte [importação JSON](STUDIO_IMPORTACAO_JSON.md).
  Para interação por microfone, veja [voz guiada](STUDIO_VOZ_GUIADO.md) e
  [voz em tempo real](STUDIO_VOZ_TEMPO_REAL.md).
