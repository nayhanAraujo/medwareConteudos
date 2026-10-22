-- Executar uma unica vez no banco de conteudos (Firebird).
-- A coluna e criada sem default para preservar NULL nos registros antigos.
ALTER TABLE SCRIPTLAUDO ADD DATA_CRIACAO TIMESTAMP;
COMMIT;

-- Novos cadastros recebem a data/hora do servidor do banco automaticamente.
ALTER TABLE SCRIPTLAUDO ALTER DATA_CRIACAO SET DEFAULT CURRENT_TIMESTAMP;
COMMIT;
