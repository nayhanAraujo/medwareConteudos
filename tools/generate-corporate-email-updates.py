"""Generate a reviewable Firebird script using only corporate identity columns."""
import argparse
from collections import Counter
from html.parser import HTMLParser
from pathlib import Path
import re


class TableParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.rows, self.row, self.cell = [], [], None

    def handle_starttag(self, tag, attrs):
        if tag == 'tr':
            if self.row:
                self.rows.append(self.row)
            self.row = []
        if tag in ('td', 'th'):
            self.cell = []

    def handle_data(self, data):
        if self.cell is not None:
            self.cell.append(data)

    def handle_endtag(self, tag):
        if tag in ('td', 'th') and self.cell is not None:
            self.row.append(''.join(self.cell).strip())
            self.cell = None
        if tag == 'tr' and self.row:
            self.rows.append(self.row)
            self.row = []


def literal(value):
    return "'" + value.replace("'", "''") + "'"


def generate(source, destination):
    table = TableParser()
    table.feed(source.read_text(encoding='utf-8-sig'))
    header = [h.upper() for h in table.rows[0]]
    rows, skipped = [], []
    for cells in table.rows[1:]:
        if len(cells) != len(header):
            raise ValueError('Malformed source table row')
        record = dict(zip(header, cells))
        login, name = record['USUARIO'].strip(), record['NOME'].strip()
        email = record['EMAILINTERNO'].strip().lower()
        if email in ('', 'null'):
            skipped.append(f'{login}: EMAILINTERNO vazio')
            continue
        if not re.fullmatch(r'[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+', email) or not email.isascii() or len(email) > 254:
            skipped.append(f'{login}: EMAILINTERNO invalido')
            continue
        if not login or not name or len(login) > 100 or len(name) > 150:
            raise ValueError('Invalid source identity')
        rows.append((login, name, email))
    if not rows or any(count > 1 for count in Counter(row[2] for row in rows).values()):
        raise ValueError('No valid emails or duplicate corporate emails')
    selections = [
        f'    SELECT CAST({literal(login)} AS VARCHAR(100) CHARACTER SET UTF8), '
        f'CAST({literal(name)} AS VARCHAR(150) CHARACTER SET UTF8), '
        f'CAST({literal(email)} AS VARCHAR(254) CHARACTER SET ASCII) FROM RDB$DATABASE'
        for login, name, email in rows
    ]
    sql = f'''-- Atualizar USUARIO.EMAIL a partir de {source.name} / EMAILINTERNO.
-- Fonte: {len(table.rows) - 1} registros; {len(rows)} e-mails validos; {len(skipped)} registros ignorados.
-- Ignorados: {'; '.join(skipped)}.
-- Banco de destino: CONTEUDO / USUARIO. CODUSUARIO da fonte NAO e utilizado.
-- Pre-requisito: migracao password-recovery aplicada (EMAIL e PASSWORD_RESET).
-- Conecte com charset UTF8, desative autocommit e execute com a API parada.
-- Requer Firebird 3 ou superior (validado em Firebird 5).
-- Correspondencia exata por nome completo OU login, ignorando caixa/espacos externos.
-- Nao utiliza primeiro nome, nem cria usuarios, nem modifica senhas/perfis/status.
-- Ambiguidades, varios registros para um destino e e-mails em uso sao ignorados.
-- Links de recuperacao anteriores das contas alteradas sao expirados.
-- Leia TODOS os resultados antes de confirmar a transacao.
-- COMMIT/ROLLBACK estao comentados no final, para revisao antes de confirmar.

SET TERM ^ ;
EXECUTE BLOCK RETURNS (
  LOGIN_ORIGEM VARCHAR(100) CHARACTER SET UTF8,
  NOME_ORIGEM VARCHAR(150) CHARACTER SET UTF8,
  EMAIL_CORPORATIVO VARCHAR(254) CHARACTER SET ASCII,
  CODUSUARIO_DESTINO INTEGER,
  RESULTADO VARCHAR(40)
)
AS
DECLARE VARIABLE CORRESPONDENCIAS INTEGER;
DECLARE VARIABLE FONTES_POR_DESTINO INTEGER;
DECLARE VARIABLE EMAIL_ANTERIOR VARCHAR(254) CHARACTER SET ASCII;
BEGIN
  FOR
    WITH ORIGEM (LOGIN_FONTE, NOME_FONTE, EMAIL_FONTE) AS (
''' + '\n    UNION ALL\n'.join(selections) + '''
    ), CANDIDATOS AS (
      SELECT O.LOGIN_FONTE, O.NOME_FONTE, O.EMAIL_FONTE, U.CODUSUARIO
      FROM ORIGEM O
      LEFT JOIN USUARIO U ON LOWER(TRIM(U.NOME)) = LOWER(TRIM(O.NOME_FONTE))
         OR LOWER(TRIM(U.IDENTIFICACAO)) = LOWER(TRIM(O.LOGIN_FONTE))
    ), RESUMO AS (
      SELECT C.*,
        COUNT(CODUSUARIO) OVER (PARTITION BY EMAIL_FONTE) AS TOTAL_DESTINOS,
        COUNT(CODUSUARIO) OVER (PARTITION BY CODUSUARIO) AS TOTAL_FONTES,
        ROW_NUMBER() OVER (PARTITION BY EMAIL_FONTE ORDER BY CODUSUARIO) AS ORDEM
      FROM CANDIDATOS C
    )
    SELECT LOGIN_FONTE, NOME_FONTE, EMAIL_FONTE, TOTAL_DESTINOS, CODUSUARIO, TOTAL_FONTES
    FROM RESUMO WHERE ORDEM = 1
    INTO :LOGIN_ORIGEM, :NOME_ORIGEM, :EMAIL_CORPORATIVO,
         :CORRESPONDENCIAS, :CODUSUARIO_DESTINO, :FONTES_POR_DESTINO
  DO
  BEGIN
    RESULTADO = 'SEM_CORRESPONDENCIA';
    IF (CORRESPONDENCIAS > 1) THEN RESULTADO = 'CORRESPONDENCIA_AMBIGUA';
    ELSE IF (CORRESPONDENCIAS = 1) THEN
    BEGIN
      IF (FONTES_POR_DESTINO > 1) THEN RESULTADO = 'DESTINO_COM_VARIAS_FONTES';
      ELSE IF (EXISTS(SELECT 1 FROM USUARIO U WHERE LOWER(TRIM(U.EMAIL)) = :EMAIL_CORPORATIVO
                     AND U.CODUSUARIO <> :CODUSUARIO_DESTINO)) THEN RESULTADO = 'EMAIL_EM_USO';
      ELSE
      BEGIN
        SELECT EMAIL FROM USUARIO WHERE CODUSUARIO = :CODUSUARIO_DESTINO INTO :EMAIL_ANTERIOR;
        RESULTADO = 'JA_ATUALIZADO';
        IF (EMAIL_ANTERIOR IS DISTINCT FROM EMAIL_CORPORATIVO) THEN
        BEGIN
          UPDATE USUARIO SET EMAIL = :EMAIL_CORPORATIVO, DTHRULTMODIFICACAO = CURRENT_TIMESTAMP
            WHERE CODUSUARIO = :CODUSUARIO_DESTINO;
          UPDATE PASSWORD_RESET SET EXPIRES_UTC = CREATED_UTC
            WHERE CODUSUARIO = :CODUSUARIO_DESTINO AND USED_UTC IS NULL;
          RESULTADO = 'ATUALIZADO';
        END
      END
    END
    SUSPEND;
  END
END^
SET TERM ; ^

-- Depois de conferir os resultados, execute UMA das opcoes:
-- COMMIT;
-- ROLLBACK;
'''
    destination.write_text(sql, encoding='utf-8')
    print(f'Generated {destination}: {len(rows)} valid emails; {len(skipped)} skipped.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    args = parser.parse_args()
    generate(args.source, args.destination)
