# ESPECIFICACIÓN TÉCNICA MAESTRA: SISTEMA DE GESTIÓN DE INVENTARIO Y AUDITORÍA

## 1. VISIÓN GENERAL Y ARQUITECTURA BASE

El sistema consiste en un backend de servicios RESTful de alto rendimiento diseñado para la administración automatizada de inventarios, registro descentralizado de usuarios y ejecución de auditorías físicas mediante escaneo masivo de códigos de barras. La infraestructura está diseñada para ser horizontalmente escalable, soportando la operación simultánea de múltiples instancias de backend conectadas de forma concurrente a una base de datos centralizada.

### Stack Tecnológico Estándar

* **Framework Principal:** .NET 10 (C#) bajo el modelo ASP.NET Core Web API.
* **Motor de Base de Datos:** Microsoft SQL Server.
* **ORM (Object-Relational Mapping):** Entity Framework Core (EF Core) mediante el enfoque Code-First y manejo estricto de migraciones de datos.
* **Procesamiento y Persistencia de Archivos Multimedia:** Almacenamiento directo en el sistema de archivos local del servidor (directorio `wwwroot/images/`). Queda prohibido el almacenamiento de arreglos binarios (BLOBs) dentro de las tablas de la base de datos.
* **Control y Limitador de Tamaño de Archivos:** Se implementa una restricción de peso máximo para cargas (tanto en imágenes como en lotes de Excel). Esta validación de tamaño debe ejecutarse de forma estricta tanto en el lado del cliente (Frontend) antes del envío, como en la recepción del servidor (Backend).
* **Procesamiento Masivo de Archivos:** Lectura y escritura optimizada en ráfaga (*streams*) mediante el uso de librerías de bajo consumo de memoria como `MiniExcel` o `CsvHelper` para mitigar desbordamientos en la memoria RAM del servidor.
* **Documentación Automatizada:** Interfaz interactiva de Swagger mediante `Swashbuckle.AspNetCore` bajo el estándar OpenAPI 3.0.

---

## 2. CONFIGURACIÓN DEL ORM Y DISEÑO ESTRUCTURAL DE LA BASE DE DATOS

Con el objetivo de cumplir el requerimiento de abstracción de datos y evitar la escritura manual de sentencias SQL, toda la persistencia de datos se gestiona mediante Entity Framework Core.

### 2.1. Estrategias Avanzadas de EF Core

* **Estrategia de Identificadores:** Se prohíbe el uso de enteros autoincrementales y esquemas de encriptación dinámica en tránsito para los identificadores. Todas las llaves primarias de la aplicación utilizarán **UUIDs (GUID v4 de 128 bits)** auto-generados por el backend en cada inserción. Esto elimina los vectores de ataque de enumeración e IDOR, garantizando el anonimato estructural frente al cliente.
* **Optimización de Operaciones Masivas:** Para saltar la limitación de velocidad de EF Core al procesar inserciones registro por registro, se implementa de manera mandatoria la extensión **`EFCore.BulkExtensions`**. Esto permite traducir las solicitudes de carga en operaciones masivas directas `SqlBulkCopy` en SQL Server, procesando miles de registros en milisegundos.
* **Construcción de Consultas Dinámicas:** Las búsquedas y exportaciones filtradas utilizan evaluación diferida mediante la interfaz `IQueryable`. Los filtros se estructuran en caliente y se ejecutan directamente en el motor de SQL Server, evitando traer colecciones de datos masivas sin filtrar a la memoria de la API.
* **Manejo de Concurrencia Distribuida:** Para asegurar el correcto funcionamiento en configuraciones multi-backend, se implementa una estrategia de **Concurrencia Optimista** controlada por el ORM a través de una columna de rastreo de versiones de fila.

### 2.2. Definición Estricta de Entidades y Restricciones de Base de Datos

#### Entidad: `Usuario`

* `Id`: `Guid` (Llave Primaria - Clustered Index).
* `Cedula`: `String` (Obligatorio, **Restricción de Unicidad Estricta - Unique Index**). Requiere validación matemática obligatoria de Cédula Ecuatoriana (Módulo 10), la cual debe verificarse tanto en el Frontend como en el Backend.
* `Nombre`: `String` (Obligatorio).
* `Email`: `String` (Obligatorio, **Restricción de Unicidad Estricta - Unique Index**).
* `PasswordHash`: `String` (Almacena el resultado del hashing criptográfico).
* `Rol`: `String` (Restringido lógicamente a los valores: `Admin`, `User`).

#### Entidad: `Elemento`

* `Id`: `Guid` (Llave Primaria - Clustered Index).
* `CodigoBarras`: `String` (**Restricción de Unicidad Estricta - Non-Clustered Unique Index**). Representa el código físico único del bien. El motor de base de datos y la capa de servicios rechazarán de manera inmediata cualquier intento de duplicidad.
* `Nombre`: `String` (Obligatorio).
* `Descripcion`: `String` (Opcional, almacena detalles adicionales del bien).
* `Categoria`: `String` (Obligatorio, facilita el filtrado).
* `Precio`: `Decimal` (Obligatorio, valor monetario del bien).
* `RutaImagen`: `String` (Opcional, almacena el string con la ruta física relativa en el servidor, ej. `/images/items/id-elemento.jpg`).
* `UsuarioIdPropietario`: `Guid` (Llave Foránea que conecta con la entidad `Usuario`).

#### Entidad: `Revision`

* `Id`: `Guid` (Llave Primaria - Clustered Index).
* `UsuarioId`: `Guid` (Llave Foránea que conecta con el `Usuario` auditor).
* `Estado`: `String` (Restringido lógicamente a los estados: `EnCurso`, `Completada`, `Incompleta`).
* `FechaInicio`: `DateTime` (Fecha y hora de apertura).
* `FechaFin`: `DateTime` (Nulo mientras el estado sea `EnCurso`).
* `RowVersion`: `Byte[]` (**Atributo de Concurrencia Optimista - Con anotación `[Timestamp]` / ConcurrenceToken en EF Core**). Incrementado automáticamente por SQL Server en cada mutación física del registro.

#### Entidad: `RevisionDetalle`

* `Id`: `Guid` (Llave Primaria - Clustered Index).
* `RevisionId`: `Guid` (Llave Foránea con eliminación en cascada hacia `Revision`).
* `ElementoId`: `Guid` (Llave Foránea hacia la entidad `Elemento`).
* `FechaEscaneo`: `DateTime` (Registro de marca de tiempo exacta del escaneo).

---

## 3. SEGURIDADES, ENCRIPTACIÓN Y MANEJO DE SECRETOS

El backend implementa un diseño defensivo alineado con los estándares de seguridad de la industria.

1. **Aislamiento de Credenciales y Secretos:** Queda estrictamente prohibido codificar de forma rígida (*hardcode*) cadenas de conexión a bases de datos, contraseñas maestras o llaves de firma en el código fuente. El string de conexión a SQL Server y la clave simétrica del JWT (`JwtSecretKey`) deben ser inyectados dinámicamente en tiempo de ejecución a través del appsettings.json   .
2. **Protección de Contraseñas en Reposo:** El sistema no posee visibilidad de contraseñas en texto plano. En el proceso de registro, la contraseña provista por el usuario pasa por un algoritmo de hashing criptográfico unidireccional con sal de alta iteración (*BCrypt* o *PBKDF2*), almacenando únicamente el hash resultante.
3. **Mecanismo de Autenticación sin Estado (Stateless Auth):** Toda comunicación subsiguiente al inicio de sesión se valida mediante tokens **JWT (JSON Web Tokens)** transmitidos en la cabecera HTTP de las solicitudes bajo el esquema `Authorization: Bearer <Token>`. El backend descifra y verifica la firma del token usando la clave simétrica del servidor, extrayendo el `UsuarioId` (UUID) y el `Rol` para la toma de decisiones de autorización inmediata.

---

## 4. LÓGICA DE NEGOCIO Y MATRIZ DE AUTORIZACIÓN (RBAC Y PROPIEDAD)

**Regla de Oro de Acceso:** Todos los endpoints expuestos por la API requieren de manera obligatoria un Token JWT válido adjunto en la solicitud, a excepción explícita de los endpoints de autenticación (`/api/auth/login`) y registro público (`/api/auth/registro`).

### 4.1. Registro de Usuarios y Prevención de Escalabilidad de Privilegios

El sistema expone un canal público de registro automatizado (*Self-Service*). Por motivos estrictos de seguridad, el backend sobreescribe y omite cualquier parámetro de rol enviado por el cliente, asignando de forma inmutable el rol **`User`** a todas las cuentas nuevas creadas por esta vía. La elevación al rol **`Admin`** solo puede realizarse de forma interna o mediante modificación explícita en la base de datos por personal autorizado.

Además, el sistema integra un CRUD completo para permitir a los usuarios gestionar sus propios datos (edición de perfil o eliminación de cuenta) y para dotar a la plataforma de un panel administrativo donde los usuarios con rol `Admin` puedan gestionar el ecosistema completo de cuentas.

### 4.2. Matriz de Operaciones CRUD

El sistema aplica un modelo híbrido basado en roles (Role-Based Access Control) y verificación explícita de propiedad cruzada de registros:

* **Operaciones de Lectura Global (`GET`):** Universal. Cualquier usuario autenticado en el sistema (sin importar si su rol es `Admin` o `User`) cuenta con permisos lógicos para buscar, listar, paginar y visualizar la totalidad de los datos de usuarios y elementos de inventario registrados, independientemente de qué usuario haya creado los registros.
* **Operaciones de Escritura, Modificación y Destrucción (`POST`, `PUT`, `DELETE`):** Restricted.
  * **Rol `Admin`:** Posee omnipotencia sobre la plataforma. Puede alterar, actualizar, reasignar o eliminar físicamente cualquier registro de elemento, usuario o revisión de auditoría del ecosistema.
  * **Rol `User`:** Su espectro de acción está acotado por la propiedad física del registro. Al recibir una solicitud de actualización (`PUT`) o eliminación (`DELETE`), el backend intercepta la petición, extrae el UUID del usuario autenticado en el JWT y lo compara con el campo `UsuarioIdPropietario` (o el creador de la auditoría). Si el ID no coincide y el rol no es administrador, la API interrumpe la ejecución de forma inmediata y devuelve un código de estado **HTTP 403 Forbidden**. Excepción: En la modificación de perfil de usuario, el ID comparado es el propio ID del usuario.

### 4.3. Flujo Lógico Exhaustivo del Sistema de Revisiones (Auditoría de Inventario)

El proceso de verificación física de inventarios sigue un ciclo de vida estrictamente controlado para mantener la integridad de los datos de auditoría:

1. **Fase de Apertura:** Un usuario inicia el proceso. El sistema genera un nuevo registro en la tabla `Revisiones` con un UUID único, asocia el `UsuarioId` del token, marca la `FechaInicio` con la hora del servidor y establece el estado inicial en **`EnCurso`**.
2. **Fase de Escaneo y Registro Dinámico (Procesamiento de Códigos de Barras):**
   El cliente realiza peticiones consecutivas enviando el parámetro `codigoBarras` en formato JSON. El backend ejecuta la siguiente lógica secuencial:
   * **Verificación de Estado de Auditoría:** Valida si la revisión asociada al UUID de la ruta sigue activa. Si la revisión tiene un estado diferente de `EnCurso`, se deniega la operación devolviendo **HTTP 400 Bad Request**.
   * **Búsqueda en Inventario General:** Consulta en la tabla `Elementos` por el `CodigoBarras` provisto. Si el código no está registrado en el inventario maestro del sistema, el backend aborta la operación y retorna **HTTP 404 Not Found**.
   * **Control de Duplicados en la Sesión Actual (Regla de Segunda Alerta):** Verifica si el elemento identificado ya cuenta con un registro en la tabla `RevisionDetalles` vinculado a la revisión actual. Si el elemento es detectado por segunda vez, la API **no genera un error crítico de sistema**, sino que rechaza la duplicidad en el almacenamiento pero retorna un código semántico controlado **HTTP 409 Conflict** (o respuesta controlada) informando detalladamente al frontend que el ítem ya se encontraba contabilizado en la auditoría en curso para que este renderice una advertencia visual.
   * **Inserción Exitosa:** Si pasa las comprobaciones, EF Core inserta la tupla en `RevisionDetalles` con la marca de tiempo actual y retorna un estado **HTTP 200 OK** (o 201 Created).
3. **Fase de Cierre y Evaluación de Completitud:**
   Al solicitar la finalización de la auditoría, el backend realiza los siguientes pasos bajo una transacción protegida:
   * **Validación Criptográfica de Concurrencia:** EF Core evalúa la columna `RowVersion`. Si otra instancia u otro hilo modificó la revisión durante el procesamiento, la transacción se revierte lanzando una excepción de concurrencia y denegando el cierre.
   * **Cálculo de Cobertura Física:** El sistema ejecuta de forma asíncrona un conteo del total de registros en la tabla `RevisionDetalles` asociados a esa revisión y lo compara numéricamente contra el conteo total de registros existentes en la tabla maestra de `Elementos`.
   * **Mutación de Estado:** Si el volumen de elementos escaneados iguala con exactitud al inventario esperado del sistema, la propiedad `Estado` se actualiza de forma permanente a **`Completada`**. Si existe un desfase (elementos faltantes), el estado se altera a **`Incompleta`**.
   * **Bloqueo de Escritura:** Se estampa la `FechaFin` con la marca del servidor. A partir de este momento, la API bloquea de forma inmutable la auditoría, impidiendo la adición de nuevos detalles de escaneo.

---

## 5. ESPECIFICACIÓN OPENAPI 3.0 (ESTÁNDAR SWAGGER EN FORMATO YAML)

El siguiente bloque en formato YAML representa la definición exacta del contrato de la API. Puede insertarse directamente en herramientas de renderizado OpenAPI (como Swagger Editor o Postman) para generar la interfaz de pruebas interactiva y automatizada.

```yaml
openapi: 3.0.1
info:
  title: API Sistema de Inventario y Auditoría de Bienes
  description: |
    Especificación maestra del backend desarrollado en .NET 10 con persistencia en SQL Server mediante EF Core.

    ### Reglas Generales de Autorización:
    * **Autenticación:** Requiere de forma obligatoria el uso de un Token Bearer JWT para todas las llamadas, con excepción explícita de las rutas públicas de `/api/auth/login` y `/api/auth/registro`.
    * **Lectura de Datos (`GET`):** Permitida globalmente para cualquier usuario autenticado en el sistema, permitiendo la visibilidad completa de recursos.
    * **Modificación de Datos (`PUT`, `DELETE`):** Restringida estrictamente al usuario propietario del recurso (coincidencia con el UUID del token JWT) o a usuarios con rol `Admin`.
  version: v1.0.0
servers:
  - url: https://localhost:5001
    description: Servidor de Desarrollo Local Seguro (HTTPS)
components:
  securitySchemes:
    bearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
      description: Inserte el token JWT de la siguiente forma en la cabecera: 'Bearer <su_token_jwt>'
  schemas:
    LoginRequest:
      type: object
      required:
        - email
        - password
      properties:
        email:
          type: string
          format: email
          example: administrador@sistema.com
        password:
          type: string
          format: password
          example: Segura123#
    RegistroRequest:
      type: object
      required:
        - cedula
        - nombre
        - email
        - password
      properties:
        cedula:
          type: string
          example: "1712345678"
          description: Cédula ecuatoriana válida (Módulo 10).
        nombre:
          type: string
          example: Francisco Toro
        email:
          type: string
          format: email
          example: user@sistema.com
        password:
          type: string
          format: password
          example: Password987#
    TokenResponse:
      type: object
      properties:
        token:
          type: string
          description: Cadena codificada del Token JWT de autenticación.
    UsuarioResponse:
      type: object
      properties:
        id:
          type: string
          format: uuid
        cedula:
          type: string
        nombre:
          type: string
        email:
          type: string
        rol:
          type: string
          enum: [Admin, User]
    ElementoResponse:
      type: object
      properties:
        id:
          type: string
          format: uuid
          description: Identificador único universal auto-generado (GUID).
        codigoBarras:
          type: string
          description: Código único físico del bien (Restricción UNIQUE indexada en SQL Server).
        nombre:
          type: string
        descripcion:
          type: string
        categoria:
          type: string
        precio:
          type: number
          format: float
        rutaImagen:
          type: string
          description: Dirección URL o path relativo del archivo físico en el servidor.
        usuarioIdPropietario:
          type: string
          format: uuid
          description: UUID del usuario creador del elemento.
    EscaneoRequest:
      type: object
      required:
        - codigoBarras
      properties:
        codigoBarras:
          type: string
          example: "7861001234567"
    RevisionResponse:
      type: object
      properties:
        id:
          type: string
          format: uuid
        usuarioId:
          type: string
          format: uuid
        estado:
          type: string
          enum: [EnCurso, Completada, Incompleta]
        fechaInicio:
          type: string
          format: date-time
        fechaFin:
          type: string
          format: date-time
          nullable: true
    RevisionFinalizadaResponse:
      type: object
      properties:
        id:
          type: string
          format: uuid
        estado:
          type: string
          enum: [Completada, Incompleta]
          example: "Completada"
        elementosFaltantes:
          type: integer
          example: 0

security:
  - bearerAuth: []

paths:
  /api/auth/registro:
    post:
      tags:
        - Autenticación y Cuentas
      summary: Registro autónomo de nuevos usuarios en el sistema
      description: Endpoint público de autoservicio. Descartará cualquier rol enviado e impondrá el rol 'User' por defecto por motivos de seguridad. Valida la Cédula Ecuatoriana (Módulo 10) y hashea la contraseña de forma unidireccional.
      security: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/RegistroRequest'
      responses:
        '201':
          description: Cuenta de usuario creada con éxito en el motor de persistencia.
        '400':
          description: Solicitud malformada, cédula inválida o parámetros faltantes.
        '409':
          description: Conflicto de datos. El correo electrónico o la cédula ya se encuentran registrados.

  /api/auth/login:
    post:
      tags:
        - Autenticación y Cuentas
      summary: Autenticar credenciales de usuario y emitir token JWT
      description: Valida el correo y la contraseña contra el hash de la base de datos. Si tiene éxito, devuelve un token stateless firmado.
      security: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/LoginRequest'
      responses:
        '200':
          description: Autenticación exitosa. Retorna el token de acceso seguro.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/TokenResponse'
        '401':
          description: Acceso denegado. Las credenciales de acceso suministradas son erróneas.

  /api/usuarios:
    get:
      tags:
        - Gestión de Usuarios
      summary: Recuperar la lista completa de usuarios del sistema
      description: Permite la lectura universal de cuentas registradas a cualquier token autenticado dentro del ecosistema.
      responses:
        '200':
          description: Colección de usuarios recuperada correctamente.
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/UsuarioResponse'

  /api/usuarios/{id}:
    get:
      tags:
        - Gestión de Usuarios
      summary: Obtener el detalle de un usuario específico
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '200':
          description: Datos del usuario recuperados.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/UsuarioResponse'
        '404':
          description: Usuario no encontrado.
    put:
      tags:
        - Gestión de Usuarios
      summary: Actualizar datos de usuario
      description: Permite al usuario editar su propio perfil, o a un Administrador editar cualquier perfil del sistema.
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
            format: uuid
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UsuarioResponse'
      responses:
        '200':
          description: Perfil actualizado.
        '403':
          description: Operación denegada. No tiene los privilegios sobre este recurso.
    delete:
      tags:
        - Gestión de Usuarios
      summary: Eliminar cuenta de usuario
      description: Permite a un usuario eliminar su propia cuenta o a un Administrador purgar usuarios del sistema.
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '204':
          description: Cuenta eliminada con éxito.
        '403':
          description: Acceso denegado.

  /api/elementos:
    get:
      tags:
        - Inventario Maestro de Elementos
      summary: Listar el inventario de elementos completo del sistema con filtros
      description: Operación de lectura global con capacidad de filtrado. Cualquier usuario con sesión activa puede visualizar el catálogo completo de activos de la organización.
      parameters:
        - name: buscar
          in: query
          description: Búsqueda parcial por nombre o código de barras.
          schema:
            type: string
        - name: categoria
          in: query
          description: Filtrar por categoría.
          schema:
            type: string
      responses:
        '200':
          description: Lista de elementos de inventario devuelta con éxito.
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/ElementoResponse'

    post:
      tags:
        - Inventario Maestro de Elementos
      summary: Insertar un nuevo elemento al inventario general
      description: Almacena de forma física la imagen adjunta en la infraestructura local (validando límites de peso) y mapea su ruta de acceso en base de datos. Asigna la propiedad del elemento automáticamente al UUID recuperado del JWT.
      requestBody:
        required: true
        content:
          multipart/form-data:
            schema:
              type: object
              required:
                - codigoBarras
                - nombre
                - categoria
                - precio
              properties:
                codigoBarras:
                  type: string
                  description: Identificador de barras físico. Debe ser estrictamente único.
                nombre:
                  type: string
                  description: Nombre descriptivo del activo.
                descripcion:
                  type: string
                categoria:
                  type: string
                precio:
                  type: number
                imagen:
                  type: string
                  format: binary
                  description: Archivo binario de imagen del bien (Sujeto a validación de tamaño máximo).
      responses:
        '201':
          description: Registro del elemento creado e indexado con éxito.
        '400':
          description: Archivo excede el límite de tamaño permitido.
        '409':
          description: Error de duplicidad. El código de barras enviado ya se encuentra asignado a otro bien.

  /api/elementos/{id}:
    get:
      tags:
        - Inventario Maestro de Elementos
      summary: Obtener el detalle individual de un elemento específico
      parameters:
        - name: id
          in: path
          required: true
          description: UUID del elemento a buscar.
          schema:
            type: string
            format: uuid
      responses:
        '200':
          description: Información detallada del elemento.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ElementoResponse'
        '404':
          description: No se localizó el recurso.

    put:
      tags:
        - Inventario Maestro de Elementos
      summary: Actualizar un elemento existente del inventario
      description: Requiere autenticación. El sistema validará que el UUID del token JWT coincida con el propietario original del registro, o que el usuario cuente con rol privilegiado de Administrador.
      parameters:
        - name: id
          in: path
          required: true
          description: Identificador único universal (UUID) del elemento a modificar.
          schema:
            type: string
            format: uuid
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/ElementoResponse'
      responses:
        '200':
          description: Modificación aplicada correctamente en los registros de la base de datos.
        '403':
          description: Operación denegada. Violación de propiedad de datos (No es dueño del recurso ni Administrador).
        '404':
          description: No se localizó ningún elemento con el UUID provisto.
        '409':
          description: Conflicto. El nuevo código de barras colisiona con una restricción UNIQUE existente.

    delete:
      tags:
        - Inventario Maestro de Elementos
      summary: Eliminar físicamente un elemento del inventario general
      description: Elimina el registro en SQL Server y remueve de forma física el archivo de imagen asociado. Valida reglas estrictas de propiedad cruzada basadas en JWT.
      parameters:
        - name: id
          in: path
          required: true
          description: Identificador UUID del elemento que se desea remover.
          schema:
            type: string
            format: uuid
      responses:
        '204':
          description: Elemento removido con éxito del ecosistema de persistencia.
        '403':
          description: Solicitud rechazada. El usuario no cuenta con los privilegios de propiedad o rol necesarios.
        '404':
          description: Recurso no encontrado.

  /api/elementos/importar:
    post:
      tags:
        - Procesamiento Masivo (Carga por Lotes)
      summary: Importar bienes de forma masiva a través de un documento Excel o CSV
      description: Procesa la entrada como un stream de datos fila por fila mediante MiniExcel/CsvHelper. Ejecuta validaciones de formato (y peso) e inyecta en ráfaga a SQL Server usando EFCore.BulkExtensions (SqlBulkCopy), vinculando todos los registros procesados al UUID del solicitante.
      requestBody:
        required: true
        content:
          multipart/form-data:
            schema:
              type: object
              required:
                - archivo
              properties:
                archivo:
                  type: string
                  format: binary
                  description: Archivos compatibles con hojas de cálculo (.xlsx, .csv).
      responses:
        '200':
          description: Procesamiento por lotes finalizado de forma correcta en el servidor.
        '400':
          description: El archivo provisto contiene errores estructurales, tipos de datos incompatibles, sobrepasa el peso límite o tiene códigos duplicados.

  /api/elementos/exportar:
    get:
      tags:
        - Procesamiento Masivo (Carga por Lotes)
      summary: Exportar el catálogo de bienes a un archivo Excel (.xlsx) bajo filtros
      description: Recibe cadenas de texto o rangos, estructura un modelo dinámico `IQueryable` para su procesamiento óptimo en SQL Server y genera explícitamente un archivo Excel (.xlsx) estructurado sobre la marcha para su descarga.
      parameters:
        - name: buscar
          in: query
          description: Criterio de filtrado parcial aplicado sobre el nombre o coincidencia exacta de código de barras.
          schema:
            type: string
      responses:
        '200':
          description: Reporte estructurado generado exitosamente. Retorna un archivo Excel descargable.
          content:
            application/vnd.openxmlformats-officedocument.spreadsheetml.sheet:
              schema:
                type: string
                format: binary

  /api/revisiones:
    get:
      tags:
        - Procesos de Revisión y Auditoría
      summary: Recuperar la lista completa de sesiones de auditoría
      description: Obtiene un listado general del histórico y sesiones activas de revisiones.
      responses:
        '200':
          description: Colección de revisiones obtenida correctamente.
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/RevisionResponse'

    post:
      tags:
        - Procesos de Revisión y Auditoría
      summary: Inicializar una sesión de auditoría física de inventario
      description: Crea una cabecera de auditoría en la tabla maestro con estado inicial 'EnCurso', estampando los datos identificativos del auditor autenticado mediante el JWT.
      responses:
        '201':
          description: Auditoría inicializada correctamente. Devuelve el UUID asignado a la sesión de control.

  /api/revisiones/{id}:
    get:
      tags:
        - Procesos de Revisión y Auditoría
      summary: Obtener el detalle individual de una revisión
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '200':
          description: Datos específicos de la revisión recuperados con éxito.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/RevisionResponse'
        '404':
          description: Revisión no encontrada.

  /api/revisiones/{id}/escanear:
    post:
      tags:
        - Procesos de Revisión y Auditoría
      summary: Procesar el escaneo de un código de barras físico en la auditoría activa
      description: Recibe un código de barras. Si el ítem no existe en el catálogo maestro, devuelve 404. Si ya se procesó previamente en la misma sesión, se intercepta de forma segura devolviendo un código 409 Conflict controlado para notificar duplicidad al frontend sin interrumpir el flujo.
      parameters:
        - name: id
          in: path
          required: true
          description: UUID de la sesión de revisión activa.
          schema:
            type: string
            format: uuid
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/EscaneoRequest'
      responses:
        '200':
          description: Bien verificado de forma exitosa y anexado a los detalles de control físico.
        '400':
          description: Operación inválida. La sesión de revisión seleccionada ya ha sido clausurada de manera permanente.
        '404':
          description: Código de barras inexistente. El bien no pertenece al catálogo maestro de inventario.
        '409':
          description: Alerta de duplicidad detectada. El ítem ya se encontraba previamente registrado dentro de esta auditoría.

  /api/revisiones/{id}/finalizar:
    post:
      tags:
        - Procesos de Revisión y Auditoría
      summary: Clausurar definitivamente una sesión de revisión activa
      description: Aplica bloqueo estructural. Compara el consolidado físico de escaneos versus los bienes globales del inventario. Ejecuta comprobaciones con tokens de exclusión mutua (RowVersion) para evitar escrituras sucias concurrentes desde múltiples servidores backend de forma simultánea.
      parameters:
        - name: id
          in: path
          required: true
          description: UUID de la revisión que se desea cerrar de manera inmutable.
          schema:
            type: string
            format: uuid
      responses:
        '200':
          description: Sesión de auditoría cerrada con éxito en la infraestructura. Retorna los cálculos de cobertura globales.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/RevisionFinalizadaResponse'
        '403':
          description: Acceso denegado. El usuario no cuenta con la propiedad del flujo de auditoría o rol administrativo para efectuar el cierre.

          ```
```