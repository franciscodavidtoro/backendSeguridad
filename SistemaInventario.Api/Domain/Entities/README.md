# Reglas de Negocio: Entidades de Dominio (Domain/Entities)

Este directorio contiene las definiciones de datos core del sistema. Toda la persistencia se mapea desde estas clases utilizando Entity Framework Core de forma estricta.

## Reglas Generales de Identificadores
* **Prohibición de Autoincrementales:** Queda estrictamente prohibido el uso de enteros autoincrementales (INT IDENTITY) como llaves primarias.
* **UUID Obligatorio:** Todas las llaves primarias (Id) de la aplicación deben utilizar **UUIDs (GUID v4 de 128 bits)** auto-generados por el backend en cada inserción para mitigar ataques IDOR y de enumeración.

## Reglas Específicas por Entidad

### 1. Usuario (Usuario.cs)
* **Cédula Obligatoria y Única:** Debe incluir un índice de unicidad estricto. Requiere **validación matemática obligatoria de Cédula Ecuatoriana (Módulo 10)** tanto en frontend como en backend.
* **Email Único:** Restricción indexada de unicidad en la base de datos.
* **Roles Restringidos:** Lógicamente acotado a los valores literales: Admin y User. 
* **Password Hash:** Solo se almacena el hash criptográfico resultante, nunca texto plano.

### 2. Elemento (Elemento.cs)
* **Código de Barras Único:** Restricción de unicidad mediante un índice no agrupado (Non-Clustered Unique Index). Cualquier duplicado debe ser rechazado inmediatamente por el motor.
* **Precios:** Campo decimal obligatorio para control de valor monetario del bien.
* **Ruta de Imagen:** Campo opcional de tipo texto que almacena la ruta relativa física del servidor (ej. /images/items/id-elemento.jpg).
* **Propiedad:** Vinculado obligatoriamente a un UsuarioIdPropietario (UUID).

### 3. Revisión (Revision.cs)
* **Estados Controlados:** Restringido lógicamente a los estados fijos: EnCurso, Completada, Incompleta.
* **Control de Concurrencia Distribuida:** Incluye un atributo de control de concurrencia optimista (RowVersion) marcado con [Timestamp], el cual se incrementa automáticamente por SQL Server en cada mutación.
* **Fechas:** FechaInicio obligatoria del servidor. FechaFin es nula mientras el estado sea EnCurso.

### 4. Detalles de Revisión (RevisionDetalle.cs)
* **Relación y Cascada:** Llave foránea hacia Revision con eliminación en cascada (Cascade Delete).
* **Fecha de Escaneo:** Timestamp exacto provisto por el servidor al registrar el escaneo físico.
