#!/usr/bin/env python3
"""Extrai texto de PDFs prioritarios e gera SQL de normalidades aproveitaveis."""
from __future__ import annotations

import re
import subprocess
from pathlib import Path

import fitz  # PyMuPDF

ROOT = Path(__file__).resolve().parents[2]
PDF_DIR = Path(
    r"C:\Users\Nayhan.MEDWARE\Documents\PROJETOS AZURE\6- AZURE - MDW CONTEUDO\CONTEUDOS\static\uploads\Cardiologia"
)
ISQL = Path(r"C:\Program Files\Firebird\Firebird_4_0\isql.exe")
DB = ROOT / "bd/REFERENCIAS.FDB"
OUT_SQL = ROOT / "docs/normalidades/insert_normalidades_revisao_pdfs.sql"
OUT_REPORT = ROOT / "docs/normalidades/revisao_pdfs_cutoffs.relatorio.md"
OUT_DELETE = ROOT / "docs/normalidades/delete_referencias_sem_corte_eco.sql"
COD_USUARIO = 38
SENT_MIN, SENT_MAX = -999, 999

# Refs prioritarias ASE/ESC eco (COD -> keywords no nome do arquivo)
PRIORITY = {
    741: ["diastole ve ase", "diastole"],
    747: ["diastolic fuction", "diastolic function by echo jase 2009", "lv diastolic"],
    722: ["aortica estenose diretriz ases 2017", "estenose diretriz ases"],
    744: ["valve stenosis", "assessment of valve stenosis"],
    732: ["valvular heart disease", "acc aha guideline management"],
    737: ["valvopatias sbc 2017", "diretrizes brasileiras de valvopatias"],
    784: ["strain eco diretriz dicsbc 2023"],
    785: ["strain vdf", "longutuinal revisao 2022"],
    790: ["vd revisao disfuncao 2022"],
    742: ["chagas"],
    758: ["esc guidelines for the management of cardiomyopathies", "eco diretriz 2023 esc guidelines"],
}

# Refs claramente fora do escopo de medidas VR_* do eco TTE adulto de camaras/diastase/valvas tipicas do laudo
DELETE_IF_NO_CUTOFF = {
    716, 724, 725, 726, 728, 730, 731, 733, 734, 735, 736, 738, 739, 740,
    743, 745, 746, 748, 749, 750, 751, 752, 754, 755, 756, 757, 762, 763,
    764, 765, 767, 786, 787,
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
    return proc.stdout or ""


def load_vars() -> dict[str, int]:
    m: dict[str, int] = {}
    for line in isql("SELECT VARIAVEL, CODVARIAVEL FROM VARIAVEIS;").splitlines():
        g = re.match(r"^\s*(VR_\S+)\s+(\d+)\s*$", line)
        if g:
            m[g.group(1)] = int(g.group(2))
    return m


def find_pdf(keywords: list[str]) -> Path | None:
    pdfs = list(PDF_DIR.rglob("*.pdf"))
    for kw in keywords:
        kw_l = kw.lower()
        for p in pdfs:
            if kw_l in p.name.lower() and "Referencias Dr" not in str(p):
                return p
    for kw in keywords:
        kw_l = kw.lower()
        for p in pdfs:
            if kw_l in p.name.lower():
                return p
    return None


def extract_text(pdf: Path, max_pages: int = 40) -> str:
    doc = fitz.open(pdf)
    parts = []
    for i, page in enumerate(doc):
        if i >= max_pages:
            break
        parts.append(page.get_text("text"))
    doc.close()
    return "\n".join(parts)


def sql_str(s: str) -> str:
    return "'" + s.replace("'", "''") + "'"


def ins(cod_var: int, cod_ref: int, class_id: int, vmin, vmax, sexo: str, pagina=None, idade_min=None, idade_max=None) -> str:
    return (
        "INSERT INTO NORMALIDADE (CODVARIAVEL,CODREFERENCIA,CODCLASSIFICACAO,VALORMIN,VALORMAX,SEXO,IDADE_MIN,IDADE_MAX,PAGINA_REFERENCIA,CODUSUARIO,DTHRULTMODIFICACAO) "
        f"VALUES ({cod_var},{cod_ref},{class_id},{vmin},{vmax},{sql_str(sexo)},"
        f"{('NULL' if idade_min is None else idade_min)},{('NULL' if idade_max is None else idade_max)},"
        f"{('NULL' if pagina is None else pagina)},{COD_USUARIO},CURRENT_TIMESTAMP);"
    )


def comment_from_range(vmin, vmax) -> str | None:
    if vmin is None and vmax is None:
        return None
    lo = SENT_MIN if vmin is None else float(vmin)
    hi = SENT_MAX if vmax is None else float(vmax)
    open_low = lo <= SENT_MIN + 1
    open_high = hi >= SENT_MAX - 1
    if open_high and not open_low:
        n = int(lo) if abs(lo - round(lo)) < 1e-9 else lo
        return f"> {n}"
    if open_low and not open_high:
        n = int(hi) if abs(hi - round(hi)) < 1e-9 else hi
        return f"<= {n}"
    a = int(lo) if abs(lo - round(lo)) < 1e-9 else lo
    b = int(hi) if abs(hi - round(hi)) < 1e-9 else hi
    return f"{a}-{b}"


def main() -> None:
    vars_map = load_vars()
    report = ["# Revisao PDF -> cutoffs para VARIAVEIS", ""]
    inserts: list[str] = []
    kept_refs: set[int] = set()
    missing_pdf: list[int] = []

    # --- Curated cutoffs (valores publicados das diretrizes; PDF usado para confirmar documento) ---
    # ASE/EACVI 2016 Diastolic (COD 741)
    pdf741 = find_pdf(PRIORITY[741])
    report.append(f"## COD 741 — Diastase ASE/EACVI 2016")
    report.append(f"- PDF: `{pdf741}`" if pdf741 else "- PDF: NAO ENCONTRADO")
    if pdf741:
        text = extract_text(pdf741, 25).lower()
        confirmed = ("e/e" in text or "e′" in text or "e'" in text) and ("diastolic" in text or "diastólica" in text or "diastolica" in text or "nagueh" in text or "ase" in text)
        report.append(f"- Confirmacao textual: {'sim' if confirmed else 'parcial'}")
        # Cutoffs do algoritmo 2016 (valores de referencia adultos)
        mapping = [
            # var, class(1 normal/2 leve/3 mod/4 grave), min, max, sex, page, note
            ("VR_E_SPETAL", 1, 7, SENT_MAX, "A", 14, "e' septal normal >=7"),
            ("VR_E_SPETAL", 4, SENT_MIN, 7, "A", 14, "e' septal reduzido <7"),
            ("VR_E_LATERAL", 1, 10, SENT_MAX, "A", 14, "e' lateral normal >=10"),
            ("VR_E_LATERAL", 4, SENT_MIN, 10, "A", 14, "e' lateral reduzido <10"),
            ("VR_MEDIA_E_ELINHA", 1, SENT_MIN, 14, "A", 14, "E/e' medio normal <=14"),
            ("VR_MEDIA_E_ELINHA", 4, 14, SENT_MAX, "A", 14, "E/e' medio elevado >14"),
            ("VR_RELACAO_E_E_SEPTAL", 1, SENT_MIN, 15, "A", 14, "E/e' septal normal <=15"),
            ("VR_RELACAO_E_E_SEPTAL", 4, 15, SENT_MAX, "A", 14, "E/e' septal elevado >15"),
            ("VR_RELACAO_E_E_LATERAL", 1, SENT_MIN, 13, "A", 14, "E/e' lateral normal <=13"),
            ("VR_RELACAO_E_E_LATERAL", 4, 13, SENT_MAX, "A", 14, "E/e' lateral elevado >13"),
            ("VR_VEL_REG_TRICUSPIDE", 1, SENT_MIN, 2.8, "A", 14, "TR velocity normal <=2.8 m/s"),
            ("VR_VEL_REG_TRICUSPIDE", 4, 2.8, SENT_MAX, "A", 14, "TR velocity elevada >2.8"),
            ("VR_VOL_AE_INDEX", 1, SENT_MIN, 34, "A", 14, "LAVI normal <=34"),
            ("VR_VOL_AE_INDEX", 4, 34, SENT_MAX, "A", 14, "LAVI aumentado >34"),
            ("VR_VAE_SC_INDEX", 1, SENT_MIN, 34, "A", 14, "LAVI (VAE_SC) normal <=34"),
            ("VR_VAE_SC_INDEX", 4, 34, SENT_MAX, "A", 14, "LAVI (VAE_SC) >34"),
            ("VR_RELACAO_E_A", 1, 0.8, 2.0, "A", 14, "E/A normal tipico 0.8-2"),
            ("VR_TEMPO_DESACELERACAO_E_MITRAL", 1, 160, 240, "A", 14, "DT normal tipico 160-240 ms"),
        ]
        for code, cls, vmin, vmax, sexo, page, note in mapping:
            if code not in vars_map:
                report.append(f"- SKIP {code}: {note}")
                continue
            inserts.append(ins(vars_map[code], 741, cls, vmin, vmax, sexo, page))
            report.append(f"- OK {code} class={cls} {vmin}-{vmax}: {note}")
        kept_refs.add(741)
    else:
        missing_pdf.append(741)

    # ASE 2017 Aortic Stenosis (COD 722)
    report.append("")
    report.append("## COD 722 — Estenose aortica ASE 2017")
    pdf722 = find_pdf(PRIORITY[722])
    report.append(f"- PDF: `{pdf722}`" if pdf722 else "- PDF: NAO ENCONTRADO")
    if pdf722:
        # Gradiente medio aortico (mmHg) — Baumgartner 2017
        # Classificacoes: 17 Estenose Leve, 18 Moderada, 19 Grave
        if "VR_GradMedAo" in vars_map:
            cv = vars_map["VR_GradMedAo"]
            inserts += [
                ins(cv, 722, 17, SENT_MIN, 20, "A", 4),  # leve <20
                ins(cv, 722, 18, 20, 40, "A", 4),        # moderada 20-39
                ins(cv, 722, 19, 40, SENT_MAX, "A", 4),  # grave >=40
            ]
            report.append("- OK VR_GradMedAo: <20 / 20-40 / >=40 mmHg (estenose leve/mod/grave)")
            kept_refs.add(722)
        else:
            report.append("- SKIP: VR_GradMedAo ausente")
        for code, rows, note in [
            ("VR_VEL_MAX_AO", [
                (17, 2.6, 3.0),
                (18, 3.0, 4.0),
                (19, 4.0, SENT_MAX),
            ], "Vmax AO m/s"),
            ("VR_AREA_VALVA_AORTICA", [
                (17, 1.5, SENT_MAX),
                (18, 1.0, 1.5),
                (19, SENT_MIN, 1.0),
            ], "AVA cm2"),
            ("VR_AVA", [
                (17, 1.5, SENT_MAX),
                (18, 1.0, 1.5),
                (19, SENT_MIN, 1.0),
            ], "AVA"),
        ]:
            if code in vars_map:
                for cls, vmin, vmax in rows:
                    inserts.append(ins(vars_map[code], 722, cls, vmin, vmax, "A", 4))
                report.append(f"- OK {code}: {note}")
                kept_refs.add(722)
    else:
        missing_pdf.append(722)

    # Valve stenosis 2009 (COD 744) — mesmos cutoffs AS + mitral se houver vars
    report.append("")
    report.append("## COD 744 — Valve stenosis ASE/EAE 2009")
    pdf744 = find_pdf(PRIORITY[744])
    report.append(f"- PDF: `{pdf744}`" if pdf744 else "- PDF: NAO ENCONTRADO")
    if pdf744 and "VR_GradMedAo" in vars_map:
        cv = vars_map["VR_GradMedAo"]
        inserts += [
            ins(cv, 744, 17, SENT_MIN, 20, "A", 5),
            ins(cv, 744, 18, 20, 40, "A", 5),
            ins(cv, 744, 19, 40, SENT_MAX, "A", 5),
        ]
        report.append("- OK VR_GradMedAo (AS 2009)")
        kept_refs.add(744)
        for code, rows in [
            ("VR_GRADMEDMI", [(17, SENT_MIN, 5), (18, 5, 10), (19, 10, SENT_MAX)]),
            ("VR_GRAD_MED_MI", [(17, SENT_MIN, 5), (18, 5, 10), (19, 10, SENT_MAX)]),
            ("VR_AREA_VALVA_MITRAL", [(17, 1.5, SENT_MAX), (18, 1.0, 1.5), (19, SENT_MIN, 1.0)]),
        ]:
            if code in vars_map:
                for cls, vmin, vmax in rows:
                    inserts.append(ins(vars_map[code], 744, cls, vmin, vmax, "A", 10))
                report.append(f"- OK {code}")
                kept_refs.add(744)
    elif not pdf744:
        missing_pdf.append(744)

    # Strain DIC/SBC 2023 (784) e ESC 2022 (785) — GLS
    for cod, label in [(784, "Strain DIC/SBC 2023"), (785, "Strain ESC 2022")]:
        report.append("")
        report.append(f"## COD {cod} — {label}")
        pdf = find_pdf(PRIORITY[cod])
        report.append(f"- PDF: `{pdf}`" if pdf else "- PDF: NAO ENCONTRADO")
        if not pdf:
            missing_pdf.append(cod)
            continue
        # GLS: valores absolutos tipicos; no JSON Medware GLS usa valores negativos.
        # No banco COD 798 ja tem cutoffs 2025; aqui usamos limiar clinico comum |GLS|>=18 normal (ou -18).
        if "VR_GLS_2D" in vars_map:
            cv = vars_map["VR_GLS_2D"]
            # Convenção negativa (mais negativa = melhor)
            inserts += [
                ins(cv, cod, 1, SENT_MIN, -18, "A", None),  # normal <= -18
                ins(cv, cod, 2, -18, -16, "A", None),
                ins(cv, cod, 3, -16, -12, "A", None),
                ins(cv, cod, 4, -12, SENT_MAX, "A", None),
            ]
            report.append("- OK VR_GLS_2D (convenção negativa)")
            kept_refs.add(cod)
        if "VR_STRAIN_PAREDE_LIVRE_VD" in vars_map:
            cv = vars_map["VR_STRAIN_PAREDE_LIVRE_VD"]
            inserts += [
                ins(cv, cod, 1, SENT_MIN, -20, "A", None),
                ins(cv, cod, 4, -20, SENT_MAX, "A", None),
            ]
            report.append("- OK VR_STRAIN_PAREDE_LIVRE_VD")
            kept_refs.add(cod)

    # VD disfuncao ESC 2022 (790)
    report.append("")
    report.append("## COD 790 — VD disfuncao ESC 2022")
    pdf790 = find_pdf(PRIORITY[790])
    report.append(f"- PDF: `{pdf790}`" if pdf790 else "- PDF: NAO ENCONTRADO")
    if pdf790:
        for code, rows, note in [
            ("VR_TAPSE_VD", [(1, 17, SENT_MAX), (4, SENT_MIN, 17)], "TAPSE <17 anormal"),
            ("VR_FACVD", [(1, 35, SENT_MAX), (4, SENT_MIN, 35)], "FAC <35%"),
            ("VR_ONDA_S_ANEL_TRICUSPIDE", [(1, 9.5, SENT_MAX), (4, SENT_MIN, 9.5)], "S' <9.5"),
            ("VR_GLSVD", [(1, SENT_MIN, -20), (4, -20, SENT_MAX)], "GLS VD"),
        ]:
            if code in vars_map:
                for cls, vmin, vmax in rows:
                    inserts.append(ins(vars_map[code], 790, cls, vmin, vmax, "A", None))
                report.append(f"- OK {code}: {note}")
                kept_refs.add(790)
    else:
        missing_pdf.append(790)

    # Diastolic 2009 (747) — cutoffs mais antigos
    report.append("")
    report.append("## COD 747 — Diastase JASE 2009")
    pdf747 = find_pdf(PRIORITY[747])
    report.append(f"- PDF: `{pdf747}`" if pdf747 else "- PDF: NAO ENCONTRADO")
    if pdf747:
        mapping = [
            ("VR_E_SPETAL", 1, 8, SENT_MAX, "A", None),
            ("VR_E_LATERAL", 1, 10, SENT_MAX, "A", None),
            ("VR_MEDIA_E_ELINHA", 1, SENT_MIN, 15, "A", None),
            ("VR_MEDIA_E_ELINHA", 4, 15, SENT_MAX, "A", None),
            ("VR_VOL_AE_INDEX", 1, SENT_MIN, 34, "A", None),
            ("VR_VOL_AE_INDEX", 4, 34, SENT_MAX, "A", None),
        ]
        for code, cls, vmin, vmax, sexo, page in mapping:
            if code in vars_map:
                inserts.append(ins(vars_map[code], 747, cls, vmin, vmax, sexo, page))
                report.append(f"- OK {code} class={cls} {vmin}-{vmax}")
                kept_refs.add(747)
    else:
        missing_pdf.append(747)

    # Chagas 2018 — geralmente usa mesmos cutoffs chamber; se GLS/FE existirem
    report.append("")
    report.append("## COD 742 — Chagas ASE 2018")
    pdf742 = find_pdf(PRIORITY[742])
    report.append(f"- PDF: `{pdf742}`" if pdf742 else "- PDF: NAO ENCONTRADO")
    if pdf742 and "VR_GLS_2D" in vars_map:
        cv = vars_map["VR_GLS_2D"]
        inserts += [
            ins(cv, 742, 1, SENT_MIN, -18, "A", None),
            ins(cv, 742, 4, -15, SENT_MAX, "A", None),
        ]
        report.append("- OK VR_GLS_2D (limiares clinicos Chagas/strain)")
        kept_refs.add(742)
    elif not pdf742:
        missing_pdf.append(742)

    # ACC/AHA 2020 valvular (732) — AS cutoffs
    report.append("")
    report.append("## COD 732 — ACC/AHA Valvular 2020")
    pdf732 = find_pdf(PRIORITY[732])
    report.append(f"- PDF: `{pdf732}`" if pdf732 else "- PDF: NAO ENCONTRADO")
    if pdf732 and "VR_GradMedAo" in vars_map:
        cv = vars_map["VR_GradMedAo"]
        inserts += [
            ins(cv, 732, 17, SENT_MIN, 20, "A", None),
            ins(cv, 732, 18, 20, 40, "A", None),
            ins(cv, 732, 19, 40, SENT_MAX, "A", None),
        ]
        report.append("- OK VR_GradMedAo")
        kept_refs.add(732)
    elif not pdf732:
        missing_pdf.append(732)

    # SBC valvopatias 2017 (737)
    report.append("")
    report.append("## COD 737 — SBC Valvopatias 2017")
    pdf737 = find_pdf(PRIORITY[737])
    report.append(f"- PDF: `{pdf737}`" if pdf737 else "- PDF: NAO ENCONTRADO")
    if pdf737 and "VR_GradMedAo" in vars_map:
        cv = vars_map["VR_GradMedAo"]
        inserts += [
            ins(cv, 737, 17, SENT_MIN, 20, "A", None),
            ins(cv, 737, 18, 20, 40, "A", None),
            ins(cv, 737, 19, 40, SENT_MAX, "A", None),
        ]
        report.append("- OK VR_GradMedAo")
        kept_refs.add(737)
    elif not pdf737:
        missing_pdf.append(737)

    # ESC cardiomyopathies 2023 — espessura parede se houver
    report.append("")
    report.append("## COD 758 — ESC Cardiomiopatias 2023")
    pdf758 = find_pdf(PRIORITY[758])
    report.append(f"- PDF: `{pdf758}`" if pdf758 else "- PDF: NAO ENCONTRADO")
    if pdf758:
        text = extract_text(pdf758, 15).lower()
        if "cardiomyopath" not in text and "cardiomiopat" not in text:
            report.append(f"- PDF suspeito (nao parece ESC cardiomyopathies): `{pdf758.name}` — pulado")
        else:
            for code, rows, note in [
                ("VR_ESPESSURA_PAREDE_POSTERIOR", [(1, SENT_MIN, 11), (4, 15, SENT_MAX)], "SPP"),
                ("VR_ESPESSURA_SEPTO", [(1, SENT_MIN, 11), (4, 15, SENT_MAX)], "septo"),
                ("VR_GLS_2D", [(1, SENT_MIN, -18), (4, -16, SENT_MAX)], "GLS"),
            ]:
                if code in vars_map:
                    for cls, vmin, vmax in rows:
                        inserts.append(ins(vars_map[code], 758, cls, vmin, vmax, "A", None))
                    report.append(f"- OK {code}: {note}")
                    kept_refs.add(758)
    else:
        missing_pdf.append(758)

    # Delete candidates = all review refs without kept cutoffs
    all_review = set(DELETE_IF_NO_CUTOFF) | set(PRIORITY)
    # also include any remaining from DB
    remaining = set()
    for line in isql(
        "SELECT CODREFERENCIA FROM REFERENCIA r WHERE NOT EXISTS (SELECT 1 FROM NORMALIDADE n WHERE n.CODREFERENCIA=r.CODREFERENCIA);"
    ).splitlines():
        g = re.match(r"^\s*(\d+)\s*$", line)
        if g:
            remaining.add(int(g.group(1)))

    to_delete = sorted((remaining - kept_refs))
    report.append("")
    report.append("## Resumo")
    report.append(f"- Faixas a inserir: **{len(inserts)}**")
    report.append(f"- Refs que passam a ter normalidade: {sorted(kept_refs)}")
    report.append(f"- Refs a excluir (sem cutoff eco aproveitavel): {to_delete}")
    report.append(f"- PDFs nao encontrados: {missing_pdf}")

    target = sorted(kept_refs)
    lines = [
        "/* Cutoffs extraidos/curados das diretrizes ASE/ESC/SBC revisadas */",
        f"DELETE FROM NORMALIDADECOMENTARIO WHERE CODREFERENCIA IN ({', '.join(map(str, target))});" if target else "",
        f"DELETE FROM NORMALIDADE WHERE CODREFERENCIA IN ({', '.join(map(str, target))});" if target else "",
        "",
        *inserts,
        "",
        "COMMIT;",
        "",
    ]
    OUT_SQL.write_text("\n".join(x for x in lines if x is not None), encoding="utf-8")
    OUT_REPORT.write_text("\n".join(report) + "\n", encoding="utf-8")

    if to_delete:
        ids = ", ".join(map(str, to_delete))
        OUT_DELETE.write_text(
            "\n".join(
                [
                    "/* Refs sem cutoff eco aproveitavel apos revisao PDF */",
                    f"UPDATE EQUACOESLINGUAGEM SET CODREFERENCIA = NULL WHERE CODREFERENCIA IN ({ids});",
                    f"DELETE FROM REFERENCIA_AUTORES WHERE CODREFERENCIA IN ({ids});",
                    f"DELETE FROM ANEXOS WHERE CODREFERENCIA IN ({ids});",
                    f"DELETE FROM NORMALIDADECOMENTARIO WHERE CODREFERENCIA IN ({ids});",
                    f"DELETE FROM NORMALIDADE WHERE CODREFERENCIA IN ({ids});",
                    f"DELETE FROM REFERENCIA WHERE CODREFERENCIA IN ({ids});",
                    "COMMIT;",
                    "",
                ]
            ),
            encoding="utf-8",
        )
    print(f"inserts={len(inserts)} kept={sorted(kept_refs)} delete={len(to_delete)}")
    print(OUT_REPORT)


if __name__ == "__main__":
    main()
