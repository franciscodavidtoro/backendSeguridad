# Reglas de Persistencia: Infraestructura de Base de Datos (Infrastructure/Database)

Este directorio gestiona el ApplicationDbContext, las migraciones y toda la interacción directa con el motor de base de datos relacional.

## Reglas Técnicas Obligatorias

### 1. Configuración de ORM y Estrategia Code-First
* **Uso de EF Core:** Toda la persistencia se manipula a través de Entity Framework Core mediante el enfoque **Code-First**.
* **Manejo Estricto de Migraciones:** No se permiten modificaciones manuales en las tablas del servidor SQL Server; cualquier cambio estructural debe estar respaldado por una migración de EF Core.

### 2. Almacenamiento y Manejo de Archivos Multimedia
* **Prohibición de BLOBs:** Queda estrictamente prohibido almacenar arreglos de bytes binarios (BLOBs/VARBINARY(MAX)) dentro de las tablas de la base de datos para evitar la degradación del rendimiento.
* **Persistencia Local:** Las imágenes de los bienes se guardarán directamente en el sistema de archivos del servidor bajo el directorio físico wwwroot/images/.
* **Control de Tamaño:** Validación estricta del peso máximo de los archivos (Tanto imágenes como lotes de Excel) en la API antes de ser procesados.

### 3. Procesamiento Masivo y Rendimiento de Consultas
* **Optimización de Operaciones Masivas:** Para la inserción de ráfagas masivas (importaciones), es mandatorio el uso de la extensión **EFCore.BulkExtensions** para traducir operaciones complejas en un SqlBulkCopy nativo de SQL Server.
* **Procesamiento por Streams:** La lectura/escritura de archivos masivos (Excel/CSV) debe hacerse mediante streaming de bajo consumo de memoria con librerías como **MiniExcel o CsvHelper** para evitar desbordamientos de RAM.
* **Consultas Diferidas (IQueryable):** Las búsquedas y filtros se construirán dinámicamente y se evaluarán en caliente diferida directamente en el motor de base de datos, previniendo la carga de colecciones masivas sin filtrar en la memoria de la API.







# 🗄️ Manual de Migraciones - Entity Framework Core

Este documento explica el flujo de trabajo para inicializar y actualizar la base de datos SQL Server utilizando el enfoque Code-First de Entity Framework Core.

> **Nota de Arquitectura:** El proyecto utiliza `UseInMemoryDatabase` para el entorno de desarrollo. Para permitir que las migraciones funcionen correctamente y apunten a SQL Server, este directorio incluye la clase `ApplicationDbContextFactory`. EF Core detecta esta clase automáticamente en tiempo de diseño e ignora la configuración de desarrollo al ejecutar los comandos de migración.

---

## 🚀 1. Inicialización de la Base de Datos (Primera vez)

Si es la primera vez que vas a levantar la base de datos real en tu máquina o acabas de clonar el repositorio, sigue estos pasos para construir las tablas en SQL Server:

1. Abre la **Consola del Administrador de Paquetes** en Visual Studio (`Herramientas` > `Administrador de paquetes NuGet` > `Consola del Administrador de paquetes`).
2. Asegúrate de que el **Proyecto predeterminado** (en el menú desplegable superior de la consola) apunte a `SistemaInventario.Api`.
3. Ejecuta el comando para aplicar la migración inicial:

```text
Update-Database
```

---

## 🔄 2. Flujo de Actualización (Migraciones Posteriores)

Cada vez que modifiques las propiedades de una entidad (ej. agregar un campo a `Usuario`) o crees una nueva tabla (agregando un nuevo `DbSet` al `ApplicationDbContext`), debes reflejar esos cambios en SQL Server siguiendo este ciclo de 2 pasos:

### Paso A: Generar la migración
Este paso lee tu código y crea un archivo con las instrucciones SQL necesarias. Usa siempre nombres descriptivos que expliquen qué cambió (ej. `AgregarColumnaTelefono`, `CrearTablaCategorias`).

**En la consola ejecuta:**
```text
Add-Migration NombreDescriptivoDelCambio
```

### Paso B: Aplicar la migración
Una vez que el archivo de migración se genera con éxito, debes impactar esos cambios en el motor de la base de datos.

**En la consola ejecuta:**
```text
Update-Database
```

---

## 🚑 3. Comandos Útiles de Rescate

Si cometiste un error, estos comandos te ayudarán a revertir los cambios:

* **Deshacer la última migración creada (solo si AÚN NO has hecho `Update-Database`):**
  ```text
  Remove-Migration
  ```

* **Revertir la base de datos a una versión anterior:**
  Si ya aplicaste los cambios a SQL Server y rompiste algo, puedes volver a una migración estable especificando su nombre.
  ```text
  Update-Database NombreDeLaMigracionEstable
  ```