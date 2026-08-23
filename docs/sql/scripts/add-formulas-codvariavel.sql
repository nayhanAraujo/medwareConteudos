SET AUTODDL ON;

-- =============================================================================
-- REGRA: uma fórmula por variável/medida.
-- NÃO apague a variável 209 de VARIAVEIS.
-- Se a mesma variável aparecer em mais de uma fórmula, apague o VÍNCULO extra
-- (FORMULA_VARIAVEL) e/ou a FÓRMULA extra — nunca a linha em VARIAVEIS.
--
-- Índice UNIQUE UNQ_FORMULAS_CODVARIAVEL deve permanecer. Firebird permite
-- vários NULL nesse índice; o -803 ocorre ao gravar o mesmo CODVARIAVEL em
-- duas fórmulas.
-- =============================================================================
--
-- RECUPERAÇÃO AGORA (índice único já existe, backfill falhou com -803 / 209)
-- -----------------------------------------------------------------------------
-- 1) Diagnóstico — cole e execute ANTES de qualquer DELETE/UPDATE:
--
--    -- Fórmulas ligadas à variável 209
--    SELECT FV.CODFORMULA,
--           F.NOME,
--           F.FORMULA,
--           F.CODVARIAVEL AS CODVARIAVEL_NA_FORMULA,
--           FV.CODVARIAVEL
--      FROM FORMULA_VARIAVEL FV
--      JOIN FORMULAS F ON F.CODFORMULA = FV.CODFORMULA
--     WHERE FV.CODVARIAVEL = 209
--     ORDER BY FV.CODFORMULA;
--
--    -- Todas as variáveis em mais de uma fórmula
--    SELECT FV.CODVARIAVEL,
--           COUNT(*) AS QTD_FORMULAS,
--           LIST(FV.CODFORMULA) AS CODFORMULAS
--      FROM FORMULA_VARIAVEL FV
--     GROUP BY FV.CODVARIAVEL
--    HAVING COUNT(*) > 1
--     ORDER BY FV.CODVARIAVEL;
--
-- 2) Escolha QUAL fórmula fica com 209. Depois apague o extra.
--    Troque 999 pelo CODFORMULA que NÃO deve ficar com a variável.
--
--    -- Opção A: só solta o vínculo extra (a fórmula 999 continua existindo)
--    DELETE FROM FORMULA_VARIAVEL
--     WHERE CODVARIAVEL = 209
--       AND CODFORMULA = 999;
--
--    -- Opção B: apaga a fórmula extra inteira (filhos primeiro)
--    DELETE FROM EQUACOES_LINGUAGEM WHERE CODFORMULA = 999;
--    DELETE FROM FORMULA_VARIAVEL   WHERE CODFORMULA = 999;
--    DELETE FROM FORMULAS           WHERE CODFORMULA = 999;
--
--    -- PROIBIDO:
--    -- DELETE FROM VARIAVEIS WHERE CODVARIAVEL = 209;
--
-- 3) Repita o diagnóstico até QTD_FORMULAS = 1 (sem linhas no SELECT de
--    count > 1). Só então rode o passo 3 (backfill) deste arquivo — ou o
--    arquivo inteiro: os passos 1, 4 e 5 são idempotentes.
-- =============================================================================

-- 1) Coluna INTEGER anulável (mesmo tipo dos demais CODVARIAVEL)
SET TERM ^ ;
EXECUTE BLOCK
AS
BEGIN
  IF (NOT EXISTS (
    SELECT 1 FROM RDB$RELATION_FIELDS
     WHERE TRIM(RDB$RELATION_NAME) = 'FORMULAS'
       AND TRIM(RDB$FIELD_NAME) = 'CODVARIAVEL'
  )) THEN
    EXECUTE STATEMENT 'ALTER TABLE FORMULAS ADD CODVARIAVEL INTEGER';
END^
SET TERM ; ^

-- 2) Diagnóstico (rode e confira o resultado ANTES do passo 3).
--    Se qualquer CODVARIAVEL tiver QTD_FORMULAS > 1, PARE e limpe como acima.
SELECT FV.CODVARIAVEL,
       COUNT(*) AS QTD_FORMULAS,
       LIST(FV.CODFORMULA) AS CODFORMULAS
  FROM FORMULA_VARIAVEL FV
 GROUP BY FV.CODVARIAVEL
HAVING COUNT(*) > 1
 ORDER BY FV.CODVARIAVEL;

SELECT FV.CODFORMULA,
       F.NOME,
       F.FORMULA,
       F.CODVARIAVEL AS CODVARIAVEL_NA_FORMULA,
       FV.CODVARIAVEL
  FROM FORMULA_VARIAVEL FV
  JOIN FORMULAS F ON F.CODFORMULA = FV.CODFORMULA
 WHERE FV.CODVARIAVEL = 209
 ORDER BY FV.CODFORMULA;

-- 3) Backfill a partir de FORMULA_VARIAVEL (primeira variável da fórmula).
--    Só é seguro depois que cada variável estiver em UMA fórmula.
--    Não escolhe “vencedor” automaticamente: se ainda houver duplicata, o
--    UPDATE falha com -803 no UNQ_FORMULAS_CODVARIAVEL — limpe e rode de novo.
UPDATE FORMULAS
   SET CODVARIAVEL = (
         SELECT FIRST 1 FV.CODVARIAVEL
           FROM FORMULA_VARIAVEL FV
          WHERE FV.CODFORMULA = FORMULAS.CODFORMULA
          ORDER BY FV.CODVARIAVEL
       )
 WHERE CODVARIAVEL IS NULL;

-- 4) Índice único: uma fórmula dona por variável (vários NULL são permitidos).
--    Se uma execução intermediária criou IDX_FORMULAS_CODVARIAVEL, remove.
SET TERM ^ ;
EXECUTE BLOCK
AS
BEGIN
  IF (EXISTS (
    SELECT 1 FROM RDB$INDICES
     WHERE TRIM(RDB$INDEX_NAME) = 'IDX_FORMULAS_CODVARIAVEL'
  )) THEN
    EXECUTE STATEMENT 'DROP INDEX IDX_FORMULAS_CODVARIAVEL';

  IF (NOT EXISTS (
    SELECT 1 FROM RDB$INDICES
     WHERE TRIM(RDB$INDEX_NAME) = 'UNQ_FORMULAS_CODVARIAVEL'
  )) THEN
    EXECUTE STATEMENT 'CREATE UNIQUE INDEX UNQ_FORMULAS_CODVARIAVEL ON FORMULAS (CODVARIAVEL)';
END^
SET TERM ; ^

-- 5) FK para VARIAVEIS. Se a variável for excluída, zera a chave (não apaga a fórmula).
SET TERM ^ ;
EXECUTE BLOCK
AS
BEGIN
  IF (NOT EXISTS (
    SELECT 1 FROM RDB$RELATION_CONSTRAINTS
     WHERE TRIM(RDB$CONSTRAINT_NAME) = 'FK_FORMULAS_CODVARIAVEL'
  )) THEN
    EXECUTE STATEMENT
      'ALTER TABLE FORMULAS ADD CONSTRAINT FK_FORMULAS_CODVARIAVEL '
      || 'FOREIGN KEY (CODVARIAVEL) REFERENCES VARIAVEIS (CODVARIAVEL) '
      || 'ON DELETE SET NULL ON UPDATE CASCADE';
END^
SET TERM ; ^
