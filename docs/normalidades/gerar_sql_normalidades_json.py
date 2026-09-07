#!/usr/bin/env python3
"""Gera SQL Firebird das normalidades do JSON Medware (exceto ASE Chamber 2015 / COD 1)."""
from __future__ import annotations

import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "docs/normalidades/NormalidadesEcodopplercardiogramaMedware.atualizado.json"
OUT_SQL = ROOT / "docs/normalidades/insert_normalidades_json_atualizado.sql"
OUT_REPORT = ROOT / "docs/normalidades/insert_normalidades_json_atualizado.relatorio.md"
ISQL = Path(r"C:\Program Files\Firebird\Firebird_4_0\isql.exe")
DB = ROOT / "bd/REFERENCIAS.FDB"

COD_USUARIO = 38
SKIP_REF_IDS = {1}  # política 1B
SENTINEL_MIN = -999
SENTINEL_MAX = 999

ALIAS_VAR = {
    "VR_RELACAO_E_ELINHA": "VR_MEDIA_E_ELINHA",
    "VR_DVD_MEDIO": "VR_VD2",
    "VR_GLS": "VR_GLS_2D",
    "VR_DVD_LONGITUDINAL": "VR_VD3",
    "VR_VIA_SAIDA_VE": "VR_DIAMETRO_VSVE",
}

CLASS_MAP = {"normal": 1, "leve": 2, "moderado": 3, "grave": 4}

REF_RULES: list[tuple[int | None, str, int]] = [
    (2015, "cardiac chamber quantification", 1),
    (2025, "right heart", 797),
    (2025, "diastolic function", 795),
    (2024, "peripheral arterial and aortic", 796),
    (2025, "strain echocardiography", 798),
    (2005, "chamber quantification", 4),
]


def sql_str(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def iso_safe(text: str) -> str:
    return (
        text.replace("≥", ">=")
        .replace("≤", "<=")
        .replace("≠", "!=")
        .replace("±", "+/-")
        .replace("µ", "u")
        .replace("×", "x")
        .replace("–", "-")
        .replace("—", "-")
    )


def parse_page(raw) -> int | None:
    if raw is None:
        return None
    if isinstance(raw, int):
        return raw if -32768 <= raw <= 32767 else None
    s = str(raw).strip()
    m = re.match(r"^(-?\d+)", s)
    if not m:
        return None
    n = int(m.group(1))
    return n if -32768 <= n <= 32767 else None


def parse_idade_grupo(grupo: str | None) -> tuple[int | None, int | None]:
    if not grupo:
        return None, None
    m = re.search(r"(\d+)\s*[-–]\s*(\d+)", str(grupo))
    if m:
        return int(m.group(1)), int(m.group(2))
    return None, None


def resolve_ref(fonte: str | None, ano) -> int | None:
    if not fonte:
        return None
    fl = fonte.lower()
    ano_i = int(ano) if ano is not None else None
    for rule_ano, needle, cod in REF_RULES:
        if rule_ano is not None and ano_i is not None and rule_ano != ano_i:
            continue
        if needle in fl:
            return cod
    return None


def sex_code(key: str) -> str:
    return {"F": "F", "M": "M", "U": "A"}.get(key, "A")


def iter_grades(block: dict):
    for grau in ("normal", "leve", "moderado", "grave"):
        if grau in block and isinstance(block[grau], dict):
            yield grau, block[grau]


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


def load_variaveis() -> dict[str, int]:
    mapping: dict[str, int] = {}
    for line in isql("SELECT VARIAVEL, CODVARIAVEL FROM VARIAVEIS;").splitlines():
        m = re.match(r"^\s*(VR_\S+)\s+(\d+)\s*$", line)
        if m:
            mapping[m.group(1)] = int(m.group(2))
    return mapping


def load_siglas() -> set[str]:
    used: set[str] = set()
    for line in isql("SELECT SIGLA FROM VARIAVEIS WHERE SIGLA IS NOT NULL;").splitlines():
        s = line.strip()
        if s and s != "SIGLA" and not set(s) <= {"="} and "SQL>" not in s:
            used.add(s)
    return used


def make_sigla(codigo: str, used: set[str]) -> str:
    base = re.sub(r"^VR_", "", codigo)
    base = re.sub(r"[^A-Za-z0-9]", "", base).upper()[:18] or "VAR"
    candidate = base
    i = 2
    while candidate in used:
        suffix = str(i)
        candidate = (base[: 20 - len(suffix)] + suffix)[:20]
        i += 1
    used.add(candidate)
    return candidate


def add_faixa(
    inserts: list[str],
    stats: dict,
    vexpr: str,
    cod_ref: int,
    grau: str,
    faixa: dict,
    sexo: str,
    idade_min: int | None,
    idade_max: int | None,
    pagina: int | None,
) -> None:
    vmin = faixa.get("min")
    vmax = faixa.get("max")
    if vmin is None and vmax is None:
        return
    if vmin is None:
        vmin = SENTINEL_MIN
    if vmax is None:
        vmax = SENTINEL_MAX
    inserts.append(
        "INSERT INTO NORMALIDADE (CODVARIAVEL,CODREFERENCIA,CODCLASSIFICACAO,VALORMIN,VALORMAX,SEXO,IDADE_MIN,IDADE_MAX,PAGINA_REFERENCIA,CODUSUARIO,DTHRULTMODIFICACAO) "
        f"VALUES ({vexpr},{cod_ref},{CLASS_MAP[grau]},{vmin},{vmax},{sql_str(sexo)},"
        f"{('NULL' if idade_min is None else idade_min)},{('NULL' if idade_max is None else idade_max)},"
        f"{('NULL' if pagina is None else pagina)},{COD_USUARIO},CURRENT_TIMESTAMP);"
    )
    stats["faixas"] += 1
    stats["por_ref"][cod_ref] = stats["por_ref"].get(cod_ref, 0) + 1


def main() -> None:
    data = json.loads(JSON_PATH.read_text(encoding="utf-8"))
    vars_map = load_variaveis()
    used_siglas = load_siglas()

    create_var_sql: list[str] = []
    new_codes: set[str] = set()
    resolved: dict[str, str] = {}  # json_code -> bank VARIAVEL key or new code

    for codigo, medida in data.items():
        if not isinstance(medida, dict):
            continue
        if codigo in vars_map:
            resolved[codigo] = codigo
            continue
        if codigo in ALIAS_VAR and ALIAS_VAR[codigo] in vars_map:
            resolved[codigo] = ALIAS_VAR[codigo]
            continue
        meta = medida.get("_meta") or {}
        banco = (meta.get("VariavelBanco") or "").strip()
        if banco and banco in vars_map and codigo != "VR_AO_SEIOS_VALSALVA":
            resolved[codigo] = banco
            continue
        nome = (meta.get("NomeMedida") or codigo.replace("VR_", "").replace("_", " "))[:100]
        sigla = make_sigla(codigo, used_siglas)
        new_codes.add(codigo)
        resolved[codigo] = codigo
        create_var_sql.append(
            "INSERT INTO VARIAVEIS (NOME,VARIAVEL,SIGLA,ABREVIACAO,CASASDECIMAIS,CODUSUARIO,DTHRULTMODIFICACAO,OBSERVACAO) "
            f"VALUES ({sql_str(nome)},{sql_str(codigo)},{sql_str(sigla)},{sql_str(sigla[:20])},2,{COD_USUARIO},CURRENT_TIMESTAMP,"
            f"{sql_str('Criada p/ import JSON normalidades Medware')});"
        )

    def vexpr_for(codigo: str) -> str:
        bank = resolved[codigo]
        if bank in vars_map:
            return str(vars_map[bank])
        return f"(SELECT CODVARIAVEL FROM VARIAVEIS WHERE VARIAVEL={sql_str(bank)})"

    inserts: list[str] = []
    comments: list[tuple[str, int, str]] = []
    stats = {
        "medidas": 0,
        "faixas": 0,
        "puladas_cod1": 0,
        "sem_fonte": 0,
        "sem_ref": 0,
        "por_ref": {},
    }
    alerts: list[str] = []

    for codigo, medida in data.items():
        if not isinstance(medida, dict):
            continue
        stats["medidas"] += 1
        meta = medida.get("_meta") or {}
        fonte = (meta.get("Fonte") or "").strip() or None
        ano = meta.get("Ano")
        pagina = parse_page(meta.get("Pagina"))
        cod_ref = resolve_ref(fonte, ano)
        vexpr = vexpr_for(codigo)

        if not fonte:
            stats["sem_fonte"] += 1
            alerts.append(f"{codigo}: sem _meta.Fonte (top-level)")
        elif cod_ref is None:
            stats["sem_ref"] += 1
            alerts.append(f"{codigo}: fonte sem match ({ano}) {fonte[:80]}")
        elif cod_ref in SKIP_REF_IDS:
            stats["puladas_cod1"] += 1
        else:
            comentario = ((medida.get("COMENTARIOTEXTO") or {}).get("comentario") or "").strip()
            if comentario:
                comments.append((codigo, cod_ref, iso_safe(comentario)[:500]))
            for sk in ("F", "M", "U"):
                bloco = medida.get(sk)
                if isinstance(bloco, dict):
                    for grau, faixa in iter_grades(bloco):
                        add_faixa(inserts, stats, vexpr, cod_ref, grau, faixa, sex_code(sk), None, None, pagina)

        for ctx in medida.get("REFERENCIASPORCONTEXTO") or []:
            if not isinstance(ctx, dict):
                continue
            ctx_meta = ctx.get("_meta") or {}
            ctx_fonte = (ctx_meta.get("Fonte") or fonte or "").strip() or None
            ctx_ano = ctx_meta.get("Ano", ano)
            ctx_ref = resolve_ref(ctx_fonte, ctx_ano)
            if ctx_ref is None or ctx_ref in SKIP_REF_IDS:
                continue
            ctx_pagina = parse_page(ctx_meta.get("Pagina")) if ctx_meta.get("Pagina") is not None else pagina
            idade_min, idade_max = parse_idade_grupo(ctx_meta.get("IdadeGrupoPublicado") or ctx.get("IdadeGrupoPublicado"))
            sexos: list[tuple[str, dict]] = []
            for sk in ("F", "M", "U"):
                if isinstance(ctx.get(sk), dict):
                    sexos.append((sk, ctx[sk]))
            if not sexos and any(g in ctx for g in CLASS_MAP):
                sk = str(ctx.get("Sexo") or "U").upper()[:1]
                if sk not in ("F", "M", "U"):
                    sk = "U"
                sexos = [(sk, ctx)]
            for sk, bloco in sexos:
                for grau, faixa in iter_grades(bloco):
                    add_faixa(
                        inserts, stats, vexpr, ctx_ref, grau, faixa, sex_code(sk), idade_min, idade_max, ctx_pagina
                    )

    target_refs = sorted(set(stats["por_ref"]) | {4, 795, 796, 797, 798})
    max_var = max(vars_map.values()) if vars_map else 0
    lines = [
        "/* Gerado por gerar_sql_normalidades_json.py */",
        "/* Politica 1B: NAO altera CODREFERENCIA=1 (ASE Chamber 2015). */",
        "",
        f"ALTER TABLE VARIAVEIS ALTER COLUMN CODVARIAVEL RESTART WITH {max_var + 1};",
        "",
        *create_var_sql,
        "",
        f"DELETE FROM NORMALIDADECOMENTARIO WHERE CODREFERENCIA IN ({', '.join(map(str, target_refs))});",
        f"DELETE FROM NORMALIDADE WHERE CODREFERENCIA IN ({', '.join(map(str, target_refs))});",
        "",
        *inserts,
        "",
    ]
    for codigo, cod_ref, texto in comments:
        lines.append(
            "UPDATE OR INSERT INTO NORMALIDADECOMENTARIO (CODVARIAVEL,CODREFERENCIA,TEXTO,CODUSUARIO,DTHRULTMODIFICACAO) "
            f"VALUES ({vexpr_for(codigo)},{cod_ref},{sql_str(texto)},{COD_USUARIO},CURRENT_TIMESTAMP) "
            "MATCHING (CODVARIAVEL,CODREFERENCIA);"
        )
    lines.extend(["", "COMMIT;", ""])
    OUT_SQL.write_text("\n".join(lines), encoding="utf-8")

    report = [
        "# Relatorio — insert normalidades JSON (exceto COD 1)",
        "",
        f"- Medidas no JSON: **{stats['medidas']}**",
        f"- Faixas a inserir: **{stats['faixas']}**",
        f"- Novas variaveis: **{len(new_codes)}** → {', '.join(sorted(new_codes)) or '—'}",
        f"- Alias usadas: {', '.join(f'{k}→{v}' for k,v in ALIAS_VAR.items())}",
        f"- Puladas COD 1: **{stats['puladas_cod1']}**",
        f"- Sem fonte top-level: **{stats['sem_fonte']}**",
        f"- Fonte sem match: **{stats['sem_ref']}**",
        "",
        "## Por referencia",
        "",
    ]
    for cod, qtd in sorted(stats["por_ref"].items()):
        report.append(f"- COD {cod}: **{qtd}** faixas")
    if alerts:
        report.extend(["", "## Alertas", ""])
        report.extend(f"- {a}" for a in alerts)
    OUT_REPORT.write_text("\n".join(report) + "\n", encoding="utf-8")
    print(f"SQL: {OUT_SQL}")
    print(f"faixas={stats['faixas']} new_vars={len(new_codes)} por_ref={stats['por_ref']}")


if __name__ == "__main__":
    main()
