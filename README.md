# Sistema de Análisis de Opiniones de Clientes (ETL con .NET 8)

Proyecto de proceso de carga de datos (ETL) desarrollado en **.NET 8** utilizando **Entity Framework Core 8** y **SQL Server**, diseñado para procesar archivos CSV heterogéneos, transformar la información y garantizar la integridad referencial en una base de datos relacional unificada.

## 🛠️ Tecnologías Utilizadas
* **Lenguaje:** C# (.NET 8)
* **ORM:** Entity Framework Core 8
* **Base de Datos:** Microsoft SQL Server (LocalDB)
* **Arquitectura:** En capas / Servicios especializados

---

## 🚀 Estructura del Proyecto y Ejecución
1. El script completo de creación de base de datos, tablas, restricciones, vistas y procedimientos almacenados se encuentra en la carpeta [`database/script_completo.sql`](./database/script_completo.sql).
2. Los archivos de datos originales (`surveys_part1.csv`, `social_comments.csv`, `web_reviews.csv`, etc.) se procesan de forma automatizada a través de la aplicación de consola.

---

## 📸 Evidencias del Proceso

### 1. Ejecución exitosa del Proceso ETL
<img width="758" height="672" alt="Captura de pantalla 2026-10-24 162522" src="https://github.com/user-attachments/assets/6457c705-7952-45b4-b2e6-06c0418a9a62" />


### 2. Tablas Pobladas en SQL Server Management Studio (SSMS)

<img width="437" height="650" alt="image" src="https://github.com/user-attachments/assets/1e9778c5-ee08-4540-b00e-0a699728ef40" />
<img width="345" height="650" alt="image" src="https://github.com/user-attachments/assets/e20e2044-028f-407f-8174-236dacd5203d" />
<img width="326" height="642" alt="image" src="https://github.com/user-attachments/assets/9acbe870-d27a-463a-b5f6-83a29f75f7bf" />
<img width="315" height="203" alt="image" src="https://github.com/user-attachments/assets/2c344fbd-381c-4d52-941f-67baf5001542" />
<img width="1042" height="646" alt="image" src="https://github.com/user-attachments/assets/5f68f22a-c5d1-4604-b9d2-ea29bcf89760" />


---

## 🔍 Consultas de Validación e Integridad Referencial

### Consulta 1: Conteo general de opiniones por fuente
<img width="837" height="357" alt="image" src="https://github.com/user-attachments/assets/51696010-791d-4699-aa06-85cf3eae00b4" />


### Consulta 2: Top de productos con mejor puntaje promedio
<img width="990" height="357" alt="image" src="https://github.com/user-attachments/assets/311a4106-c866-4b83-b97b-dd758804a5b9" />

### Consulta 3: Distribución de opiniones por clasificación
<img width="646" height="391" alt="image" src="https://github.com/user-attachments/assets/07550daf-9481-4636-9af8-6cc79bb4d5d2" />

### Consulta 4: Validación de registros huérfanos (Integridad Referencial)
<img width="793" height="362" alt="image" src="https://github.com/user-attachments/assets/581b4508-b62f-44fe-ba52-710940e1e352" />

### Consulta 5: Verificación de vista analítica
<img width="678" height="517" alt="image" src="https://github.com/user-attachments/assets/d694f8be-56b8-4a17-8727-7a4fb14742de" />
