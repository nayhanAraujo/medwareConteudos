#!/usr/bin/env python3
"""Inventaria referencias sem normalidade e classifica anexos para limpeza 2B."""
from __future__ import annotations

import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ISQL = Path(r"C:\Program Files\Firebird\Firebird_4_0\isql.exe")
DB = ROOT / "bd/REFERENCIAS.FDB"
KEEP = {1, 4, 795, 796, 797, 798}
OUT = ROOT / "docs/normalidades/analise_referencias_sem_normalidade.md"
OUT_DELETE_SQL = ROOT / "docs/normalidades/delete_referencias_sem_normalidade.sql"

# Titulos/anexos que tipicamente NAO entregam faixas numericas reaproveitaveis para VARIAVEIS
POSTER_HINTS = (
    "poster",
    "imagem central",
    "central illustration",
    "dic - ase",
    "pster",
    "algoritmo",
    "como medir",
    "aonde medir",
    "definir aneurisma",
)
# Guidelines antigas ja cobertas por versoes novas no JSON / COD1
SUPERSEDED = {
    2: "Right Heart 2010 — supersedida por COD 797 (2025)",
    3: "Diastolic 2016 (titulo curto) — supersedida por COD 795 (2025)",
    6: "Duplicata Right Heart 2010",
    7: "Diastolic 2016 — supersedida por COD 795 (2025)",
}


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
    return (proc.stdout or "") + (proc.stderr or "")


def classify(title: str, anexo_nome: str, anexo_link: str) -> tuple[str, str]:
    blob = f"{title} {anexo_nome} {anexo_link}".lower()
    ext = ""
    for part in (anexo_nome, anexo_link):
        m = re.search(r"\.(pdf|jpg|jpeg|png|webp)\b", part.lower())
        if m:
            ext = m.group(1)
            break
    if any(h in blob for h in POSTER_HINTS) or ext in {"jpg", "jpeg", "png", "webp"}:
        return "excluir", "Poster/imagem educativa sem faixas estruturadas para VARIAVEIS"
    if "strain" in blob and "2023" in blob:
        return "revisar", "Strain DIC/SBC 2023 — pode ter cutoffs; revisar PDF manualmente"
    if "ase 2010" in blob or "coracao direito" in blob and "2010" in blob:
        return "excluir", "Right Heart 2010 supersedida pela ASE 2025 (COD 797)"
    if "diast" in blob and ("2016" in blob or "2022" in blob):
        return "revisar", "Material de diástole — checar se ha cutoffs nao cobertos pelo COD 795"
    if ext == "pdf" and any(k in blob for k in ("guideline", "diretriz", "recommendations", "jase", "esc", "ase")):
        return "revisar", "PDF de diretriz/revisao — avaliar cutoffs numericos reaproveitaveis"
    if not anexo_nome and not anexo_link:
        return "excluir", "Sem anexo e sem normalidade"
    return "revisar", "Revisar anexo manualmente"


def main() -> None:
    sql = """
SELECT r.CODREFERENCIA, COALESCE(r.ANO,0), r.TITULO,
  COALESCE((SELECT FIRST 1 a.NOME FROM ANEXOS a WHERE a.CODREFERENCIA=r.CODREFERENCIA),'') ,
  COALESCE((SELECT FIRST 1 a.LINK FROM ANEXOS a WHERE a.CODREFERENCIA=r.CODREFERENCIA),'')
FROM REFERENCIA r
WHERE NOT EXISTS (SELECT 1 FROM NORMALIDADE n WHERE n.CODREFERENCIA=r.CODREFERENCIA)
ORDER BY r.CODREFERENCIA;
"""
    out = isql(sql)
    rows = []
    for line in out.splitlines():
        m = re.match(r"^\s*(\d+)\s+(\d+)\s+(.+)$", line)
        if not m:
            continue
        cod = int(m.group(1))
        ano = int(m.group(2)) or None
        rest = m.group(3).rstrip()
        # rest ends with nome then link; split by last /static or trailing spaces blocks is hard.
        # Fallback: keep full rest as title+anexo blob for classify
        rows.append((cod, ano, rest))

    # Better structured query via python + isql one row format
    sql2 = """
SELECT r.CODREFERENCIA || '|' || COALESCE(CAST(r.ANO AS VARCHAR(10)),'') || '|' ||
       REPLACE(r.TITULO, '|', '/') || '|' ||
       REPLACE(COALESCE((SELECT FIRST 1 a.NOME FROM ANEXOS a WHERE a.CODREFERENCIA=r.CODREFERENCIA),''), '|', '/') || '|' ||
       REPLACE(COALESCE((SELECT FIRST 1 a.LINK FROM ANEXOS a WHERE a.CODREFERENCIA=r.CODREFERENCIA),''), '|', '/')
FROM REFERENCIA r
WHERE NOT EXISTS (SELECT 1 FROM NORMALIDADE n WHERE n.CODREFERENCIA=r.CODREFERENCIA)
ORDER BY r.CODREFERENCIA;
"""
    parsed = []
    for line in isql(sql2).splitlines():
        line = line.strip()
        if "|" not in line:
            continue
        parts = line.split("|")
        if len(parts) < 5:
            continue
        try:
            cod = int(parts[0])
        except ValueError:
            continue
        ano = int(parts[1]) if parts[1].isdigit() else None
        titulo, nome, link = parts[2], parts[3], parts[4]
        if cod in SUPERSEDED:
            decisao, motivo = "excluir", SUPERSEDED[cod]
        else:
            decisao, motivo = classify(titulo, nome, link)
        parsed.append(
            {
                "cod": cod,
                "ano": ano,
                "titulo": titulo.strip(),
                "nome": nome.strip(),
                "link": link.strip(),
                "decisao": decisao,
                "motivo": motivo,
            }
        )

    excluir = [r for r in parsed if r["decisao"] == "excluir"]
    revisar = [r for r in parsed if r["decisao"] == "revisar"]

    md = [
        "# Analise de referencias sem normalidade (politica 2B)",
        "",
        f"Total sem normalidade: **{len(parsed)}**",
        f"- Propostas de exclusao automatica: **{len(excluir)}**",
        f"- Revisao manual recomendada: **{len(revisar)}**",
        f"- Mantidas com normalidade (fora desta lista): {sorted(KEEP)}",
        "",
        "## Excluir (sem faixas aproveitaveis / supersedidas / so poster)",
        "",
    ]
    for r in excluir:
        md.append(
            f"- **COD {r['cod']}** ({r['ano'] or 's/ano'}): {r['titulo'][:100]}  \n  Anexo: `{r['nome'] or r['link'] or '—'}`  \n  Motivo: {r['motivo']}"
        )
    md.extend(["", "## Revisar manualmente (PDF de diretriz/revisao — possivel cutoff)", ""])
    for r in revisar:
        md.append(
            f"- **COD {r['cod']}** ({r['ano'] or 's/ano'}): {r['titulo'][:100]}  \n  Anexo: `{r['nome'] or r['link'] or '—'}`  \n  Motivo: {r['motivo']}"
        )

    OUT.write_text("\n".join(md) + "\n", encoding="utf-8")

    # SQL delete only for clear 'excluir' set — cascading comments exist; anexos need explicit delete
    ids = ", ".join(str(r["cod"]) for r in excluir)
    delete_sql = [
        "/* Limpeza de referencias sem normalidade aproveitavel (gerado automaticamente) */",
        "/* NAO inclui itens marcados como 'revisar' */",
        "",
    ]
    if excluir:
        delete_sql.extend(
            [
                f"UPDATE EQUACOESLINGUAGEM SET CODREFERENCIA = NULL WHERE CODREFERENCIA IN ({ids});",
                f"DELETE FROM REFERENCIA_AUTORES WHERE CODREFERENCIA IN ({ids});",
                f"DELETE FROM ANEXOS WHERE CODREFERENCIA IN ({ids});",
                f"DELETE FROM NORMALIDADECOMENTARIO WHERE CODREFERENCIA IN ({ids});",
                f"DELETE FROM NORMALIDADE WHERE CODREFERENCIA IN ({ids});",
                f"DELETE FROM REFERENCIA WHERE CODREFERENCIA IN ({ids});",
                "",
                "COMMIT;",
                "",
            ]
        )
    OUT_DELETE_SQL.write_text("\n".join(delete_sql), encoding="utf-8")
    print(f"parsed={len(parsed)} excluir={len(excluir)} revisar={len(revisar)}")
    print(f"report={OUT}")
    print(f"delete_sql={OUT_DELETE_SQL}")


if __name__ == "__main__":
    main()
