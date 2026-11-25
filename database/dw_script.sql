USE SistemaOpinionesAnalitica;
GO

IF SCHEMA_ID('dw') IS NULL
    EXEC('CREATE SCHEMA dw');
GO

IF OBJECT_ID('dw.Dim_Cliente', 'U') IS NULL
    CREATE TABLE dw.Dim_Cliente (
        ClienteKey INT IDENTITY PRIMARY KEY,
        IdCliente VARCHAR(20) NOT NULL UNIQUE,
        Nombre NVARCHAR(100) NOT NULL
    );

IF OBJECT_ID('dw.Dim_Producto', 'U') IS NULL
    CREATE TABLE dw.Dim_Producto (
        ProductoKey INT IDENTITY PRIMARY KEY,
        IdProducto VARCHAR(20) NOT NULL UNIQUE,
        Nombre NVARCHAR(150) NOT NULL,
        Categoria NVARCHAR(100) NOT NULL
    );

IF OBJECT_ID('dw.Dim_Fuente', 'U') IS NULL
    CREATE TABLE dw.Dim_Fuente (
        FuenteKey INT IDENTITY PRIMARY KEY,
        TipoFuente VARCHAR(20) NOT NULL,
        NombreFuente VARCHAR(50) NOT NULL,
        Canal VARCHAR(50) NOT NULL,
        UNIQUE (TipoFuente, NombreFuente, Canal)
    );

IF OBJECT_ID('dw.Dim_Tiempo', 'U') IS NULL
    CREATE TABLE dw.Dim_Tiempo (
        TiempoKey INT IDENTITY PRIMARY KEY,
        Fecha DATE NOT NULL UNIQUE,
        Anio SMALLINT NOT NULL,
        Trimestre TINYINT NOT NULL,
        Mes TINYINT NOT NULL,
        Dia TINYINT NOT NULL,
        DiaSemana TINYINT NOT NULL -- 1 = lunes, 7 = domingo
    );

IF OBJECT_ID('dw.Dim_TipoFeedback', 'U') IS NULL
    CREATE TABLE dw.Dim_TipoFeedback (
        TipoFeedbackKey INT IDENTITY PRIMARY KEY,
        NombreTipo NVARCHAR(50) NOT NULL UNIQUE
    );

IF OBJECT_ID('dw.ControlCargaDimensiones', 'U') IS NULL
    CREATE TABLE dw.ControlCargaDimensiones (
        IdEjecucion BIGINT IDENTITY PRIMARY KEY,
        InicioUtc DATETIME2 NOT NULL,
        FinUtc DATETIME2 NOT NULL,
        FilasStaging INT NOT NULL
    );

IF OBJECT_ID('dw.DetalleCargaDimensiones', 'U') IS NULL
    CREATE TABLE dw.DetalleCargaDimensiones (
        IdEjecucion BIGINT NOT NULL REFERENCES dw.ControlCargaDimensiones (IdEjecucion),
        Dimension VARCHAR(30) NOT NULL,
        Insertadas INT NOT NULL,
        Actualizadas INT NOT NULL,
        Total INT NOT NULL,
        PRIMARY KEY (IdEjecucion, Dimension)
    );
GO

CREATE OR ALTER PROCEDURE dw.CargarDimensiones
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @InicioUtc DATETIME2 = SYSUTCDATETIME();
    DECLARE @IdEjecucion BIGINT, @ResultadoBloqueo INT;
    DECLARE @Cambios TABLE (Dimension VARCHAR(30), Operacion VARCHAR(10));

    BEGIN TRY
        BEGIN TRANSACTION;

        EXEC @ResultadoBloqueo = sys.sp_getapplock
            @Resource = 'dw.CargarDimensiones', @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 30000;
        IF @ResultadoBloqueo < 0
            THROW 50001, 'No se pudo obtener el bloqueo de la carga dimensional.', 1;

        -- Una sola copia de las columnas de staging usadas por las dimensiones.
        SELECT TRIM(TipoFuente) AS TipoFuente, TRIM(NombreFuente) AS NombreFuente,
               COALESCE(NULLIF(TRIM(Canal), ''), 'Sin canal') AS Canal,
               CONVERT(date, Fecha) AS Fecha, TRIM(Clasificacion) AS Clasificacion
        INTO #Opiniones
        FROM stg.Opiniones WITH (HOLDLOCK);

        DECLARE @FilasStaging INT = @@ROWCOUNT;
        IF @FilasStaging = 0
            THROW 50002, 'stg.Opiniones está vacía. Ejecute la extracción antes de cargar las dimensiones.', 1;

        -- Los maestros OLTP ya contienen los identificadores normalizados.
        MERGE dw.Dim_Cliente AS d
        USING SistemaOpinionesClientes.dbo.Clientes AS o
            ON d.IdCliente = o.IdCliente COLLATE DATABASE_DEFAULT
        WHEN MATCHED AND d.Nombre <> o.Nombre COLLATE DATABASE_DEFAULT THEN
            UPDATE SET Nombre = o.Nombre
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (IdCliente, Nombre) VALUES (o.IdCliente, o.Nombre)
        OUTPUT 'Dim_Cliente', $action INTO @Cambios;

        MERGE dw.Dim_Producto AS d
        USING SistemaOpinionesClientes.dbo.Productos AS o
            ON d.IdProducto = o.IdProducto COLLATE DATABASE_DEFAULT
        WHEN MATCHED AND (d.Nombre <> o.Nombre COLLATE DATABASE_DEFAULT
                          OR d.Categoria <> o.Categoria COLLATE DATABASE_DEFAULT) THEN
            UPDATE SET Nombre = o.Nombre, Categoria = o.Categoria
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (IdProducto, Nombre, Categoria) VALUES (o.IdProducto, o.Nombre, o.Categoria)
        OUTPUT 'Dim_Producto', $action INTO @Cambios;

        MERGE dw.Dim_Fuente AS d
        USING (SELECT DISTINCT TipoFuente, NombreFuente, Canal FROM #Opiniones) AS o
            ON d.TipoFuente = o.TipoFuente AND d.NombreFuente = o.NombreFuente AND d.Canal = o.Canal
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (TipoFuente, NombreFuente, Canal) VALUES (o.TipoFuente, o.NombreFuente, o.Canal)
        OUTPUT 'Dim_Fuente', $action INTO @Cambios;

        MERGE dw.Dim_Tiempo AS d
        USING (SELECT DISTINCT Fecha FROM #Opiniones WHERE Fecha IS NOT NULL) AS o
            ON d.Fecha = o.Fecha
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (Fecha, Anio, Trimestre, Mes, Dia, DiaSemana)
            VALUES (o.Fecha, YEAR(o.Fecha), DATEPART(quarter, o.Fecha), MONTH(o.Fecha), DAY(o.Fecha),
                    1 + ((DATEDIFF(day, CONVERT(date, '19000101'), o.Fecha) % 7 + 7) % 7))
        OUTPUT 'Dim_Tiempo', $action INTO @Cambios;

        ;WITH Clasificaciones AS (
            SELECT Clasificacion AS Nombre FROM #Opiniones
            UNION ALL
            SELECT TRIM(NombreTipo) COLLATE DATABASE_DEFAULT
            FROM SistemaOpinionesClientes.dbo.TiposFeedback
        ), TiposNormalizados AS (
            SELECT CASE
                WHEN UPPER(Nombre) IN ('POSITIVO', 'POSITIVA') THEN 'Positivo'
                WHEN UPPER(Nombre) IN ('NEGATIVO', 'NEGATIVA') THEN 'Negativo'
                WHEN UPPER(Nombre) IN ('NEUTRAL', 'NEUTRO', 'NEUTRA') THEN 'Neutral'
                ELSE COALESCE(NULLIF(Nombre, ''), 'Sin clasificar')
            END AS NombreTipo
            FROM Clasificaciones
        )
        MERGE dw.Dim_TipoFeedback AS d
        USING (SELECT DISTINCT NombreTipo FROM TiposNormalizados) AS o
            ON d.NombreTipo = o.NombreTipo
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (NombreTipo) VALUES (o.NombreTipo)
        OUTPUT 'Dim_TipoFeedback', $action INTO @Cambios;

        INSERT dw.ControlCargaDimensiones (InicioUtc, FinUtc, FilasStaging)
        VALUES (@InicioUtc, SYSUTCDATETIME(), @FilasStaging);
        SET @IdEjecucion = CONVERT(bigint, SCOPE_IDENTITY());

        INSERT dw.DetalleCargaDimensiones (IdEjecucion, Dimension, Insertadas, Actualizadas, Total)
        SELECT @IdEjecucion, t.Dimension,
               COUNT(CASE WHEN c.Operacion = 'INSERT' THEN 1 END),
               COUNT(CASE WHEN c.Operacion = 'UPDATE' THEN 1 END), t.Total
        FROM (
            SELECT 'Dim_Cliente' AS Dimension, COUNT(*) AS Total FROM dw.Dim_Cliente
            UNION ALL SELECT 'Dim_Producto', COUNT(*) FROM dw.Dim_Producto
            UNION ALL SELECT 'Dim_Fuente', COUNT(*) FROM dw.Dim_Fuente
            UNION ALL SELECT 'Dim_Tiempo', COUNT(*) FROM dw.Dim_Tiempo
            UNION ALL SELECT 'Dim_TipoFeedback', COUNT(*) FROM dw.Dim_TipoFeedback
        ) AS t
        LEFT JOIN @Cambios AS c ON c.Dimension = t.Dimension
        GROUP BY t.Dimension, t.Total;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT IdEjecucion, Dimension, Insertadas, Actualizadas, Total
    FROM dw.DetalleCargaDimensiones
    WHERE IdEjecucion = @IdEjecucion
    ORDER BY Dimension;
END;
GO
