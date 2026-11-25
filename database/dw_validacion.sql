USE SistemaOpinionesAnalitica;
GO
SET NOCOUNT ON;

-- Insumos de la carga.
SELECT TipoFuente, NombreFuente, COUNT(*) AS Filas
FROM stg.Opiniones
GROUP BY TipoFuente, NombreFuente
ORDER BY TipoFuente, NombreFuente;

-- Filas materializadas por dimensión. No incluye datos personales.
SELECT 'Dim_Cliente' AS Dimension, COUNT(*) AS Filas FROM dw.Dim_Cliente
UNION ALL SELECT 'Dim_Producto', COUNT(*) FROM dw.Dim_Producto
UNION ALL SELECT 'Dim_Fuente', COUNT(*) FROM dw.Dim_Fuente
UNION ALL SELECT 'Dim_Tiempo', COUNT(*) FROM dw.Dim_Tiempo
UNION ALL SELECT 'Dim_TipoFeedback', COUNT(*) FROM dw.Dim_TipoFeedback;

SELECT TOP (3) ClienteKey, IdCliente FROM dw.Dim_Cliente ORDER BY ClienteKey;
SELECT TOP (3) ProductoKey, IdProducto, Nombre, Categoria FROM dw.Dim_Producto ORDER BY ProductoKey;
SELECT FuenteKey, TipoFuente, NombreFuente, Canal FROM dw.Dim_Fuente ORDER BY FuenteKey;
SELECT TOP (3) TiempoKey, Fecha, Anio, Trimestre, Mes, Dia, DiaSemana FROM dw.Dim_Tiempo ORDER BY Fecha;
SELECT TipoFeedbackKey, NombreTipo FROM dw.Dim_TipoFeedback ORDER BY NombreTipo;

-- Las dos últimas ejecuciones permiten comprobar la repetición sin duplicados.
;WITH Ultimas AS (
    SELECT TOP (2) IdEjecucion, InicioUtc, FinUtc, FilasStaging
    FROM dw.ControlCargaDimensiones
    ORDER BY IdEjecucion DESC
)
SELECT u.IdEjecucion, u.FilasStaging, d.Dimension, d.Insertadas, d.Actualizadas, d.Total,
       DATEDIFF_BIG(millisecond, u.InicioUtc, u.FinUtc) AS DuracionMs
FROM Ultimas AS u
INNER JOIN dw.DetalleCargaDimensiones AS d ON d.IdEjecucion = u.IdEjecucion
ORDER BY u.IdEjecucion, d.Dimension;
