-- ============================================================
-- SCRIPT COMPLETO Y FINAL: BD, Tablas, SPs, Vistas y Consultas
-- Proyecto: Sistema de Análisis de Opiniones de Clientes
-- ============================================================

CREATE DATABASE SistemaOpinionesClientes;
GO
USE SistemaOpinionesClientes;
GO

-- 1. Tabla Maestra: Clientes
CREATE TABLE Clientes (
    IdCliente VARCHAR(20) NOT NULL PRIMARY KEY,
    Nombre VARCHAR(100) NOT NULL,
    Email VARCHAR(150) NOT NULL
);

-- 2. Tabla Maestra: Productos
CREATE TABLE Productos (
    IdProducto VARCHAR(20) NOT NULL PRIMARY KEY,
    Nombre VARCHAR(150) NOT NULL,
    Categoria VARCHAR(100) NOT NULL
);

-- 3. Tabla Maestra: FuentesDatos
CREATE TABLE FuentesDatos (
    IdFuente VARCHAR(20) NOT NULL PRIMARY KEY,
    TipoFuente VARCHAR(50) NOT NULL,
    FechaCarga DATETIME NULL
);

-- 4. Tabla Catálogo: TiposFeedback
CREATE TABLE TiposFeedback (
    IdTipoFeedback INT IDENTITY(1,1) PRIMARY KEY,
    NombreTipo VARCHAR(50) NOT NULL UNIQUE
);

-- 5. Tabla Transaccional Unificada: OpinionesClientes
CREATE TABLE OpinionesClientes (
    IdOpinion INT IDENTITY(1,1) PRIMARY KEY,
    CodigoOrigen VARCHAR(20) NULL,            
    IdCliente VARCHAR(20) NULL,                
    IdProducto VARCHAR(20) NOT NULL,
    IdFuente VARCHAR(20) NOT NULL,
    IdTipoFeedback INT NOT NULL,
    Fecha DATETIME NOT NULL,
    Comentario TEXT NOT NULL,
    Puntaje INT NULL,                     
    Clasificacion VARCHAR(50) NULL,            

    CONSTRAINT FK_Opiniones_Clientes FOREIGN KEY (IdCliente) REFERENCES Clientes(IdCliente),
    CONSTRAINT FK_Opiniones_Productos FOREIGN KEY (IdProducto) REFERENCES Productos(IdProducto),
    CONSTRAINT FK_Opiniones_Fuentes FOREIGN KEY (IdFuente) REFERENCES FuentesDatos(IdFuente),
    CONSTRAINT FK_Opiniones_TiposFeedback FOREIGN KEY (IdTipoFeedback) REFERENCES TiposFeedback(IdTipoFeedback)
);
GO

-- ============================================================
-- CREACIÓN DE VISTAS (Entregable 2)
-- ============================================================
CREATE VIEW vw_ResumenOpinionesPorProducto AS
SELECT 
    p.IdProducto,
    p.Nombre AS NombreProducto,
    p.Categoria,
    COUNT(o.IdOpinion) AS TotalOpiniones,
    AVG(CAST(o.Puntaje AS FLOAT)) AS PromedioPuntaje
FROM Productos p
LEFT JOIN OpinionesClientes o ON p.IdProducto = o.IdProducto
GROUP BY p.IdProducto, p.Nombre, p.Categoria;
GO

CREATE VIEW vw_OpinionesPorClasificacion AS
SELECT 
    Clasificacion,
    COUNT(*) AS Cantidad
FROM OpinionesClientes
GROUP BY Clasificacion;
GO

-- ============================================================
-- CREACIÓN DE PROCEDIMIENTOS ALMACENADOS (Entregable 2)
-- ============================================================
CREATE PROCEDURE sp_ObtenerOpinionesPorCliente
    @IdCliente VARCHAR(20)
AS
BEGIN
    SELECT 
        o.IdOpinion,
        p.Nombre AS Producto,
        o.Fecha,
        o.Comentario,
        o.Puntaje,
        o.Clasificacion
    FROM OpinionesClientes o
    INNER JOIN Productos p ON o.IdProducto = p.IdProducto
    WHERE o.IdCliente = @IdCliente;
END;
GO

CREATE PROCEDURE sp_EstadisticasGeneralesETL
AS
BEGIN
    SELECT 
        (SELECT COUNT(*) FROM Clientes) AS TotalClientes,
        (SELECT COUNT(*) FROM Productos) AS TotalProductos,
        (SELECT COUNT(*) FROM FuentesDatos) AS TotalFuentes,
        (SELECT COUNT(*) FROM OpinionesClientes) AS TotalOpiniones;
END;
GO


-- ============================================================
-- 5 CONSULTAS SQL DE VALIDACIÓN (Entregable 4)
-- ============================================================

-- Consulta 1: Validar integridad referencial y conteo general de opiniones por fuente
SELECT f.TipoFuente, COUNT(o.IdOpinion) AS TotalOpiniones
FROM FuentesDatos f
LEFT JOIN OpinionesClientes o ON f.IdFuente = o.IdFuente
GROUP BY f.TipoFuente;

-- Consulta 2: Top 5 de productos con mejor puntaje promedio
SELECT TOP 5 p.Nombre, AVG(CAST(o.Puntaje AS FLOAT)) AS PuntajePromedio
FROM Productos p
JOIN OpinionesClientes o ON p.IdProducto = o.IdProducto
GROUP BY p.Nombre
ORDER BY PuntajePromedio DESC;

-- Consulta 3: Distribución de opiniones según su clasificación (Positivo, Neutro, Negativo)
SELECT Clasificacion, COUNT(*) AS Total
FROM OpinionesClientes
GROUP BY Clasificacion;

-- Consulta 4: Validar opiniones huérfanas o errores de integridad (Debe retornar 0 filas)
SELECT o.IdOpinion
FROM OpinionesClientes o
LEFT JOIN Clientes c ON o.IdCliente = c.IdCliente
WHERE o.IdCliente IS NOT NULL AND c.IdCliente IS NULL;

-- Consulta 5: Verificación de ejecución de las vistas creadas
SELECT * FROM vw_ResumenOpinionesPorProducto;
