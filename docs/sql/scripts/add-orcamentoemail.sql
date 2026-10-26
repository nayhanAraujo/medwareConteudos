-- Migração aditiva: vínculo privado 1:1 entre mensagem e orçamento.
-- Pré-requisito: esquema ORCAMENTO normalizado. Firebird 4/5, dialect 3, UTF8.
-- Faça backup consistente, confira o alvo e pare apenas a API envolvida.
-- Preferir --migrate-database antes de publicar/iniciar a nova API.
-- FK_ORC_EMAIL_ORC abrevia ORCAMENTOEMAIL -> ORCAMENTO (limite de 31 caracteres).
-- DTHRCRIACAO representa UTC; DOCUMENTO guarda metadados/confirmacão em JSON UTF8.
-- Nenhum dado legado é alterado/removido. PK e unique já criam os índices necessários.
SET SQL DIALECT 3;
SET AUTODDL OFF;
SET BAIL ON;
SET TERM ^ ;
EXECUTE BLOCK AS
BEGIN
  IF (NOT EXISTS(SELECT 1 FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='ORCAMENTOEMAIL')) THEN
    EXECUTE STATEMENT 'CREATE TABLE ORCAMENTOEMAIL(
      CODORCAMENTO BIGINT NOT NULL,
      CHAVEORIGEM CHAR(64) CHARACTER SET ASCII NOT NULL,
      DOCUMENTO BLOB SUB_TYPE TEXT CHARACTER SET UTF8 NOT NULL,
      DTHRCRIACAO TIMESTAMP NOT NULL,
      CONSTRAINT PK_ORCAMENTOEMAIL PRIMARY KEY(CODORCAMENTO),
      CONSTRAINT UNQ_ORCAMENTOEMAIL_CHAVEORIGEM UNIQUE(CHAVEORIGEM),
      CONSTRAINT FK_ORC_EMAIL_ORC FOREIGN KEY(CODORCAMENTO) REFERENCES ORCAMENTO(CODORCAMENTO))';
END^
SET TERM ; ^
COMMIT;
-- Em falha, abortar e ROLLBACK antes do COMMIT. Depois de dados importados,
-- não remover esta tabela para rollback de código; preservar vínculos e histórico.
