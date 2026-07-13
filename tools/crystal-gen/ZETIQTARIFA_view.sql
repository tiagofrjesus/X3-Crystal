-- View da tarifa de venda para a etiqueta TEB_ITM_ETIQx60
-- Devolve o preço da tarifa ZPPE por artigo, apenas se a tarifa estiver
-- ativa (SPRICCONF.PLIENAFLG_0=2) e dentro do período (SPRICFICH).
-- Criar em AMBAS as BDs (o print engine do X3 remapeia a ligação por folder):
--   * Produção : 192.168.1.211 / BD tebx3
--   * Dev      : X3-SQL\SQLDEV / BD teb
CREATE OR ALTER VIEW TEB.ZETIQTARIFA AS
SELECT SPL.PLICRI1_0 AS ITMREF_0,
       SPL.PRI_0     AS PRI_0
FROM TEB.SPRICLIST SPL
INNER JOIN TEB.SPRICFICH SPF
        ON SPF.PLI_0 = SPL.PLI_0 AND SPF.PLICRD_0 = SPL.PLICRD_0
INNER JOIN TEB.SPRICCONF SPC
        ON SPC.PLI_0 = SPL.PLI_0
WHERE SPL.PLI_0 = 'ZPPE'
  AND SPC.PLIENAFLG_0 = 2
  AND SPF.PLISTRDAT_0 <= CAST(GETDATE() AS DATE)
  AND SPF.PLIENDDAT_0 >= CAST(GETDATE() AS DATE);
