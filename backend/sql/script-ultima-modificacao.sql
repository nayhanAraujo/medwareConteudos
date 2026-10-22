-- Executar uma unica vez no banco de conteudos, com isql (SET TERM).
-- Independente da migracao script-data-criacao.sql.
-- Registros antigos permanecem NULL ate a proxima atualizacao.
ALTER TABLE SCRIPTLAUDO ADD DTHULTMODIFICACAO TIMESTAMP;
COMMIT;

SET TERM ^ ;
CREATE OR ALTER TRIGGER SCRIPTLAUDO_DTHULTMOD FOR SCRIPTLAUDO
ACTIVE BEFORE INSERT OR UPDATE POSITION 100
AS
BEGIN
    NEW.DTHULTMODIFICACAO = CURRENT_TIMESTAMP;
END^
SET TERM ; ^
COMMIT;
