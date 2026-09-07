# Limpeza ESC 2024 (COD 796) — graus operacionais

## Problema

A Figura 5 da ESC 2024 publica so limite superior de normalidade por sexo.
Leve/moderado/grave tinham sido recolocados do JSON historico (ASE 2015) e se sobrepunham ao Normal.

## Acao

- Apagar `NORMALIDADE` com `CODREFERENCIA=796` e `CODCLASSIFICACAO IN (2,3,4)`.
- Ajustar comentarios de limiar por sexo.

## Resultado esperado (apenas Normal)

| Variavel | F | M | Comentario |
|---|---|---|---|
| VR_AO_ASCENDENTE | <= 36 | <= 40 | F <= 36; M <= 40 |
| VR_AO_ASC_PROX | <= 36 | <= 40 | F <= 36; M <= 40 |
| VR_AO_JUNCAO | <= 33 | <= 38 | F <= 33; M <= 38 |
| VR_AO_SEIOS_VALSALVA | <= 34 | <= 40 | F <= 34; M <= 40 |
| VR_AO_SEIO_VALSALVA_INDEX | < 22 | < 22 | < 22 |
| VR_ARCOAO | <= 34 | <= 37 | F <= 34; M <= 37 |

Fonte: 2024 ESC PAAD, Figura 5, p.3563.
