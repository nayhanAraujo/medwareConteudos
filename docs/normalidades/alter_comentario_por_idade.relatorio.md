# Comentário por idade — ASE 2025 (COD 795) Tabela 5

Fonte: Left Ventricular Diastolic Function PDF (p.15 / artigo p.551), Tabela 5.

## Medidas com faixa etária publicadas (20–39 / 40–60 / 60–80)

Operacional no banco: **20–39, 40–59, 60–80** (idade 60 sem sobreposição).

| Medida | Variável | Unidade no comentário/faixa |
|---|---|---|
| E wave | VR_ONDA_E_MITRAL (210) | cm/s (já no banco; tabela em m/s ×100) |
| A wave | VR_ONDA_A_MITRAL (211) | cm/s |
| E/A ratio | VR_RELACAO_E_A (236) | adimensional |
| e' lateral | VR_E_LATERAL (214) | cm/s |
| e' septal | VR_E_SPETAL (213) | cm/s |
| e' average | VR_E_MEDIA (243) | cm/s |
| E/e' lateral | VR_RELACAO_E_E_LATERAL (253) | adimensional |
| E/e' septal | VR_RELACAO_E_E_SEPTAL (244) | adimensional |
| E/e' average | VR_MEDIA_E_ELINHA (104) | adimensional |
| LAVi (geral) | VR_VOL_AE_INDEX (251) | mL/m² |
| TR velocity | VR_VEL_REG_TRICUSPIDE (129) | m/s |
| LA strain | VR_SAER (164) | % |

Não mapeados como variáveis distintas (método/vendor): LAVi Simpson, LAVi A-L, LAS TomTec, LAS EchoPAC.

## Política de dados

- **Não** apagar faixas `NORMALIDADE` legadas (sem idade / por sexo).
- Apenas acrescentar faixas e comentários por idade.
- Comentários legados permanecem com `IDADE_MIN=IDADE_MAX=-1`.
- Script: `docs/normalidades/alter_comentario_por_idade.sql`
