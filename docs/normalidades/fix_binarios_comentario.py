#!/usr/bin/env python3
"""Corrige medidas binarias (sem leve/moderado/grave): mantem so Normal + comentario."""
from __future__ import annotations

import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ISQL = Path(r"C:\Program Files\Firebird\Firebird_4_0\isql.exe")
DB = ROOT / "bd/REFERENCIAS.FDB"
OUT_SQL = ROOT / "docs/normalidades/fix_binarios_comentario.sql"
COD_USUARIO = 38
SENT_MIN, SENT_MAX = -999.0, 999.0

# Refs da revisao PDF com pares binarios Normal+Grave inventados
BINARY_REFS = (741, 742, 747, 758, 784, 785, 790)
# Tambem completar comentarios no COD1 (default) para TAPSE/FAC/S'
COD1_VARS = ("VR_TAPSE_VD", "VR_FACVD", "VR_ONDA_S_ANEL_TRICUSPIDE")


def isql(sql: str) -> str:
    proc = subprocess.run(
        [str(ISQL), "-user", "SYSDBA", "-password", "masterkey", f"127.0.0.1/3052:{DB}", "-page", "500"],
        input=sql,
        text=True,
        capture_output=True,
        encoding="latin-1",
        errors="replace",
        check=False,
    )
    return proc.stdout or ""


def sql_str(s: str) -> str:
    return "'" + s.replace("'", "''") + "'"


def fmt_num(n: float) -> str:
    if abs(n - round(n)) < 1e-9:
        return str(int(round(n)))
    return f"{n:.4g}".rstrip("0").rstrip(".") if "." in f"{n:.4g}" else f"{n:.4g}"


def comment_from_normal(vmin: float, vmax: float) -> str:
    """Gera comentario curto no padrao do JSON Medware."""
    open_low = vmin <= SENT_MIN + 1
    open_high = vmax >= SENT_MAX - 1
    if open_high and not open_low:
        # faixa normal a partir de vmin (ex.: TAPSE >= 17 -> "> 17")
        return f"> {fmt_num(vmin)}"
    if open_low and not open_high:
        # faixa normal ate vmax (ex.: E/e' <= 14)
        return f"<= {fmt_num(vmax)}"
    return f"{fmt_num(vmin)}-{fmt_num(vmax)}"


def main() -> None:
    # Carregar linhas Normal (class 1 ou 51) das refs binarias
    sql = f"""
SELECT v.VARIAVEL, n.CODVARIAVEL, n.CODREFERENCIA, n.VALORMIN, n.VALORMAX, n.CODCLASSIFICACAO
FROM NORMALIDADE n
JOIN VARIAVEIS v ON v.CODVARIAVEL=n.CODVARIAVEL
WHERE n.CODREFERENCIA IN ({','.join(map(str, BINARY_REFS))})
  AND n.CODCLASSIFICACAO IN (1, 51);
"""
    normals = []
    for line in isql(sql).splitlines():
        m = re.match(
            r"^\s*(VR_\S+)\s+(\d+)\s+(\d+)\s+([-\d.]+)\s+([-\d.]+)\s+(\d+)\s*$",
            line,
        )
        if m:
            normals.append(
                {
                    "var": m.group(1),
                    "cod_var": int(m.group(2)),
                    "cod_ref": int(m.group(3)),
                    "vmin": float(m.group(4)),
                    "vmax": float(m.group(5)),
                    "cls": int(m.group(6)),
                }
            )

    # COD1 default
    sql1 = f"""
SELECT v.VARIAVEL, n.CODVARIAVEL, n.CODREFERENCIA, n.VALORMIN, n.VALORMAX, n.CODCLASSIFICACAO
FROM NORMALIDADE n
JOIN VARIAVEIS v ON v.CODVARIAVEL=n.CODVARIAVEL
WHERE n.CODREFERENCIA=1 AND v.VARIAVEL IN ({','.join(sql_str(v) for v in COD1_VARS)})
  AND n.CODCLASSIFICACAO IN (1, 51);
"""
    for line in isql(sql1).splitlines():
        m = re.match(
            r"^\s*(VR_\S+)\s+(\d+)\s+(\d+)\s+([-\d.]+)\s+([-\d.]+)\s+(\d+)\s*$",
            line,
        )
        if m:
            normals.append(
                {
                    "var": m.group(1),
                    "cod_var": int(m.group(2)),
                    "cod_ref": int(m.group(3)),
                    "vmin": float(m.group(4)),
                    "vmax": float(m.group(5)),
                    "cls": int(m.group(6)),
                }
            )

    # Dedup por (cod_var, cod_ref) — prefer class 1, um sexo so
    best: dict[tuple[int, int], dict] = {}
    for row in normals:
        key = (row["cod_var"], row["cod_ref"])
        if key not in best or row["cls"] == 1:
            best[key] = row

    lines = [
        "/* Medidas sem grau (binarias): remove Grave inventado; grava comentario do limiar */",
        f"DELETE FROM NORMALIDADE WHERE CODREFERENCIA IN ({','.join(map(str, BINARY_REFS))}) AND CODCLASSIFICACAO = 4;",
        "",
    ]
    for row in sorted(best.values(), key=lambda r: (r["cod_ref"], r["var"])):
        # So processa se era binario (refs revisao) ou COD1 alvos
        if row["cod_ref"] not in BINARY_REFS and not (row["cod_ref"] == 1 and row["var"] in COD1_VARS):
            continue
        texto = comment_from_normal(row["vmin"], row["vmax"])
        lines.append(
            "UPDATE OR INSERT INTO NORMALIDADECOMENTARIO (CODVARIAVEL,CODREFERENCIA,TEXTO,CODUSUARIO,DTHRULTMODIFICACAO) "
            f"VALUES ({row['cod_var']},{row['cod_ref']},{sql_str(texto)},{COD_USUARIO},CURRENT_TIMESTAMP) "
            "MATCHING (CODVARIAVEL,CODREFERENCIA);"
        )
        lines.append(f"/* {row['var']} @ {row['cod_ref']}: {texto} */")

    # Strain livre / GLS binarios em 784/785: apos remover Grave, se ainda existirem leve/mod, nao mexer nos graus;
    # mas STRAIN_PAREDE era so 1+4 — fica so Normal + comentario.
    # GLS em 784/785 tem 1,2,3,4 — Grave ja removido acima! Preciso NAO remover Grave de GLS com graus.
    # Corrigir: so deletar Grave quando a medida NAO tem leve/moderado.
    lines = [
        "/* Medidas sem grau (binarias): remove Grave inventado; grava comentario do limiar */",
        "",
        "/* Remove class Grave apenas quando nao existem leve/moderado na mesma var+ref */",
        f"""DELETE FROM NORMALIDADE n
WHERE n.CODREFERENCIA IN ({','.join(map(str, BINARY_REFS))})
  AND n.CODCLASSIFICACAO = 4
  AND NOT EXISTS (
    SELECT 1 FROM NORMALIDADE x
    WHERE x.CODVARIAVEL = n.CODVARIAVEL
      AND x.CODREFERENCIA = n.CODREFERENCIA
      AND x.CODCLASSIFICACAO IN (2, 3)
  );""",
        "",
    ]
    for row in sorted(best.values(), key=lambda r: (r["cod_ref"], r["var"])):
        if row["cod_ref"] not in BINARY_REFS and not (row["cod_ref"] == 1 and row["var"] in COD1_VARS):
            continue
        texto = comment_from_normal(row["vmin"], row["vmax"])
        lines.append(
            "UPDATE OR INSERT INTO NORMALIDADECOMENTARIO (CODVARIAVEL,CODREFERENCIA,TEXTO,CODUSUARIO,DTHRULTMODIFICACAO) "
            f"VALUES ({row['cod_var']},{row['cod_ref']},{sql_str(texto)},{COD_USUARIO},CURRENT_TIMESTAMP) "
            "MATCHING (CODVARIAVEL,CODREFERENCIA);"
        )

    lines.extend(["", "COMMIT;", ""])
    OUT_SQL.write_text("\n".join(lines), encoding="utf-8")
    print(f"wrote {OUT_SQL} comments={len(best)}")


if __name__ == "__main__":
    main()
