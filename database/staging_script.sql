-- ============================================================
-- BASE DE DATOS ANALÍTICA - ÁREA DE STAGING (Fase E del ETL)
-- Proyecto: Sistema de Análisis de Opiniones de Clientes
-- El Worker Service (ETL.Worker) deposita aquí lo extraído de:
--   * CSV  (encuestas internas)
--   * BD relacional SistemaOpinionesClientes (reseñas web)
--   * API REST (comentarios de redes sociales)
-- El script es idempotente: se puede ejecutar varias veces.
-- ============================================================

IF DB_ID('SistemaOpinionesAnalitica') IS NULL
    CREATE DATABASE SistemaOpinionesAnalitica;
GO
USE SistemaOpinionesAnalitica;
GO

IF SCHEMA_ID('stg') IS NULL
    EXEC('CREATE SCHEMA stg');
GO

-- 1. Tabla staging unificada: un registro por opinión extraída (sin transformar)
IF OBJECT_ID('stg.Opiniones', 'U') IS NULL
BEGIN
    CREATE TABLE stg.Opiniones (
        IdStaging           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_stg_Opiniones PRIMARY KEY,
        LoteId              UNIQUEIDENTIFIER NOT NULL,
        TipoFuente          VARCHAR(20)      NOT NULL,   -- CSV | BaseDatos | API
        NombreFuente        VARCHAR(50)      NOT NULL,   -- EncuestasInternas | ResenasWeb | ComentariosSociales
        IdOrigen            VARCHAR(50)      NOT NULL,   -- Id en el sistema de origen
        IdCliente           VARCHAR(50)      NULL,
        IdProducto          VARCHAR(50)      NULL,
        Fecha               DATETIME2        NULL,
        Comentario          NVARCHAR(MAX)    NULL,
        Clasificacion       VARCHAR(50)      NULL,
        Puntaje             INT              NULL,
        Canal               VARCHAR(50)      NULL,       -- EncuestaInterna, Instagram, Twitter, Web...
        FechaExtraccionUtc  DATETIME2        NOT NULL CONSTRAINT DF_stg_Opiniones_Fecha DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_stg_Opiniones_NombreFuente ON stg.Opiniones (NombreFuente) INCLUDE (LoteId);
END;
GO

-- 2. Bitácora de control: métricas y estado de cada extracción
IF OBJECT_ID('stg.ControlExtraccion', 'U') IS NULL
BEGIN
    CREATE TABLE stg.ControlExtraccion (
        IdEjecucion          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_stg_ControlExtraccion PRIMARY KEY,
        LoteId               UNIQUEIDENTIFIER NOT NULL,
        NombreFuente         VARCHAR(50)      NOT NULL,
        TipoFuente           VARCHAR(20)      NOT NULL,
        InicioUtc            DATETIME2        NOT NULL,
        FinUtc               DATETIME2        NOT NULL,
        DuracionMs           BIGINT           NOT NULL,
        RegistrosExtraidos   INT              NOT NULL,
        RegistrosRechazados  INT              NOT NULL,
        Estado               VARCHAR(20)      NOT NULL,   -- Exitoso | Fallido
        MensajeError         NVARCHAR(MAX)    NULL
    );
END;
GO

-- ============================================================
-- CONSULTAS DE VALIDACIÓN (usar después de ejecutar el Worker)
-- ============================================================

-- V1: Registros extraídos por fuente
SELECT TipoFuente, NombreFuente, COUNT(*) AS Registros, MIN(Fecha) AS FechaMin, MAX(Fecha) AS FechaMax
FROM stg.Opiniones
GROUP BY TipoFuente, NombreFuente
ORDER BY TipoFuente;

-- V2: Muestra de los datos en staging
SELECT TOP 15 * FROM stg.Opiniones ORDER BY IdStaging;

-- V3: Bitácora de las ejecuciones (métricas de rendimiento)
SELECT TOP 20 IdEjecucion, LoteId, NombreFuente, TipoFuente, DuracionMs,
       RegistrosExtraidos, RegistrosRechazados, Estado, MensajeError, InicioUtc
FROM stg.ControlExtraccion
ORDER BY IdEjecucion DESC;
