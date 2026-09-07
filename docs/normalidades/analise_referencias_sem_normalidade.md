# Referências com normalidades (após revisão PDF)

Política: só permanecem referências que entregam faixas numéricas aproveitáveis para variáveis do laudo de eco.

## Resumo

- **17** referências no banco
- **0** sem normalidade
- **538** faixas de normalidade no total

## Por referência

| COD | Ano | Estudo | Faixas |
|---:|---:|---|---:|
| 1 | 2015 | ASE Chamber Quantification (já existente) | 230 |
| 4 | 2005 | ASE Chamber / `VR_PEC` (JSON) | 8 |
| 722 | 2017 | ASE estenose aórtica | 6 |
| 732 | 2020 | ACC/AHA valvular | 3 |
| 737 | 2017 | SBC valvopatias | 3 |
| 741 | 2016 | ASE/EACVI diástole | 18 |
| 742 | 2018 | ASE Chagas (GLS) | 2 |
| 744 | 2009 | ASE/EAE valve stenosis | 3 |
| 747 | 2009 | JASE diástole | 6 |
| 758 | 2023 | ESC cardiomiopatias (GLS) | 2 |
| 784 | 2023 | Strain DIC/SBC | 6 |
| 785 | 2022 | Strain ESC | 6 |
| 790 | 2022 | VD disfunção ESC | 8 |
| 795 | 2025 | ASE diástole (JSON) | 44 |
| 796 | 2024 | ESC aorta/PAD (JSON) | 36 |
| 797 | 2025 | ASE Right Heart (JSON) | 146 |
| 798 | 2025 | ASE Strain (JSON) | 11 |

## Removidas nesta revisão (33)

Referências sem cutoff eco mapeável às `VR_*` do banco (ECG, MAPA, ergometria, carótidas, tumores, ETE perioperatória, tilt, stress eco genérico, etc.).

SQL: `delete_referencias_sem_corte_eco.sql`

## Arquivos gerados

- `revisao_pdfs_cutoffs.relatorio.md` — detalhe dos cutoffs por PDF
- `insert_normalidades_revisao_pdfs.sql` — inserts aplicados
- `revisar_pdfs_cutoffs.py` — gerador reexecutável
