# Job conversão Onda E/A (210/211) — ASE 2025 (#795)

Dry-run em `bd/REFERENCIAS.FDB` (2026-09-07):

| Item | Situação |
|---|---|
| Unidade variável | `m/s` (COD 24) → alvo `cm/s` (COD 8) |
| Faixas sem idade com `VALORMAX <= 5` | **7** (legado m/s; excluído 1.7–999 por sentinela) |
| Faixas etárias 20–39 / 40–59 / 60–80 | **6** já em cm/s (`ja_parece_destino`) — **não tocar** |
| Comentários gerais (`IDADE -1/-1`) | `0.8 +/- 0.2` (E) e `0.5 +/- 0.2` (A) |
| Comentários etários | `54-111`… — **não tocar** |

Heurística da API (preview) marca as 6 etárias como destino e as 8 faixas legadas (incl. 1.7–999) como origem; o job SQL é um pouco mais conservador e não multiplica linhas com `VALORMAX` sentinela 999.

Script: `job_converter_onda_ea_ms_para_cms.sql` (idempotente se valores já ≥ 10).
