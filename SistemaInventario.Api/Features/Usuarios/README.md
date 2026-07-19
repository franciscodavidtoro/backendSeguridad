# M�dulo: Usuarios

## 1. DESCRIPCIÓN GENERAL

El módulo **Usuarios** implementa un CRUD completo (Create, Read, Update, Delete) para la gestión de cuentas de usuario del sistema. Proporciona endpoints para listar usuarios, obtener detalles individuales, actualizar perfiles y eliminar cuentas, aplicando estrictamente las reglas de autorización basadas en roles (Admin/User) y validación de propiedad de recursos.

## 2. ARQUITECTURA Y PATRÓN DE DISEÑO

### Vertical Slice Architecture (VSA)
Cada endpoint está implementado siguiendo el patrón **Vertical Slice** dentro de un archivo `.cs` único que contiene:

- **DTOs (Data Transfer Objects):** Estructuras de entrada (Request) y salida (Response) tipadas.
- **Handler:** Clase que encapsula la lógica de negocio (validaciones, acceso a BD, decisiones).
- **Endpoint:** Método estático de extensión que mapea la ruta HTTP y delega al Handler.

### Flujo de Ejecución
```
Solicitud HTTP → Endpoint (mapeo + extracción de claims)
    ↓
Handler (inyección de dependencias)
    ↓
Validaciones (autorización, datos, integridad)
    ↓
Operación de BD (EF Core)
    ↓
Respuesta HTTP (JSON + código de estado)
```

### Dependencias Inyectadas
- **ApplicationDbContext:** Acceso a Entity Framework Core para operaciones de persistencia.
- **HttpContext:** Extracción de claims del JWT para autenticación y autorización.
- **IConfiguration:** (Futuro) Para configuraciones específicas del módulo.

## 3. ENDPOINTS IMPLEMENTADOS

### 3.1 GET /api/usuarios

**Objetivo:** Listar todos los usuarios registrados en el sistema.

**Autorización:** Requiere JWT válido. Acceso universal (cualquier rol autenticado).

**Respuesta Exitosa (200 OK):**
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "cedula": "1712345678",
    "nombre": "Juan Pérez García",
    "email": "juan.perez@sistema.com",
    "rol": "User"
  },
  {
    "id": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
    "cedula": "1798765432",
    "nombre": "María López Rodríguez",
    "email": "maria.lopez@sistema.com",
    "rol": "Admin"
  }
]
```

**Errores Posibles:**
- `401 Unauthorized:` Token JWT ausente o inválido.
- `500 Internal Server Error:` Error no esperado en el servidor.

**Validaciones:** Ninguna (lectura global permitida).

**Flujo Interno:**
1. Middleware de ASP.NET Core valida JWT automáticamente.
2. Handler consulta `DbContext.Usuarios.ToListAsync()`.
3. Mapea entidades a `GetUsuariosResponse` (excluyendo `PasswordHash`).
4. Retorna lista (puede estar vacía si no hay usuarios).

---

### 3.2 GET /api/usuarios/{id}

**Objetivo:** Obtener los detalles completos de un usuario específico.

**Parámetros:**
- `{id}` (path, obligatorio): UUID del usuario. Debe ser un GUID válido (formato: 550e8400-e29b-41d4-a716-446655440000).

**Autorización:** Requiere JWT válido. Acceso universal.

**Respuesta Exitosa (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "cedula": "1712345678",
  "nombre": "Juan Pérez García",
  "email": "juan.perez@sistema.com",
  "rol": "User"
}
```

**Errores Posibles:**
- `401 Unauthorized:` Token JWT ausente o inválido.
- `404 Not Found:` No existe usuario con el ID proporcionado.
- `500 Internal Server Error:` Error no esperado.

**Validaciones:**
- `{id}` debe ser GUID válido (ASP.NET Core routing valida automáticamente, rechaza si es malformado).

**Flujo Interno:**
1. ASP.NET Core parsea `{id}` como Guid.
2. Handler consulta `DbContext.Usuarios.FirstOrDefaultAsync(u => u.Id == id)`.
3. Si no existe, retorna 404 con mensaje descriptivo.
4. Si existe, mapea a DTO y retorna 200.

---

### 3.3 PUT /api/usuarios/{id}

**Objetivo:** Actualizar los datos de un usuario existente.

**Parámetros:**
- `{id}` (path, obligatorio): UUID del usuario a actualizar (GUID válido).

**Autorización:**
- Requiere JWT válido.
- Acceso restringido:
  - Usuario Admin puede modificar **cualquier usuario**.
  - Usuario normal (rol User) puede modificar **solo su propio perfil** (si `UsuarioIdJWT == {id}`).
  - Si no cumple requisitos: `403 Forbidden`.

**Body (JSON):**
```json
{
  "nombre": "Juan Pérez Actualizado",
  "email": "juan.nuevo@sistema.com",
  "cedula": "1712345679",
  "rol": "Admin"
}
```

Todos los campos son **opcionales**. Solo se actualizan los campos proporcionados que pasen validación.

**Respuesta Exitosa (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "cedula": "1712345679",
  "nombre": "Juan Pérez Actualizado",
  "email": "juan.nuevo@sistema.com",
  "rol": "Admin"
}
```

**Errores Posibles:**

| Código | Escenario |
|--------|-----------|
| `400 Bad Request` | Validación fallida (ej: email duplicado, cédula inválida, nombre muy corto). Retorna lista detallada de errores. |
| `401 Unauthorized` | JWT ausente o inválido. |
| `403 Forbidden` | Usuario no tiene permisos (no propietario del perfil ni Admin). |
| `404 Not Found` | Usuario no existe. |
| `409 Conflict` | Email o cédula duplicados en la BD. |
| `500 Internal Server Error` | Error no esperado. |

**Validaciones Implementadas:**

#### Validación de Nombre
- Requisito: Mínimo 2 caracteres.
- Aplicación: Si se proporciona, se valida. Si cumple, se actualiza.
- Error: `"El nombre debe tener al menos 2 caracteres."`

#### Validación de Email
- Requisito: Formato válido de email (sintaxis estándar RFC 5322).
- Requisito: Email único en la BD (no puede estar registrado por otro usuario).
- Aplicación: Si se proporciona, se valida formato, luego se verifica unicidad.
- Error: `"El formato del email es inválido."` o retorna `409 Conflict` si está duplicado.

#### Validación de Cédula
- Requisito: Exactamente 10 dígitos.
- Requisito: Validación matemática de módulo 10 (algoritmo Cédula Ecuatoriana).
- Requisito: Cédula única en la BD.
- Aplicación: Si se proporciona, se valida formato → módulo 10 → unicidad.
- Errores:
  - `"La cédula debe contener exactamente 10 dígitos."`
  - `"La cédula no cumple con la validación de módulo 10."`
  - Retorna `409 Conflict` si está duplicada.

#### Validación de Rol
- Requisito: Debe ser `"Admin"` o `"User"`.
- Restricción: Solo Admin puede cambiar roles.
- Aplicación: Si se proporciona y usuario es Admin, se valida valor y se actualiza.
- Error: `"Solo los administradores pueden cambiar el rol de un usuario."` o `"El rol debe ser 'Admin' o 'User'."`

#### Algoritmo de Validación de Cédula Ecuatoriana (Módulo 10)

```csharp
// Entrada: "1712345678" (10 dígitos)
// Multiplicadores para posiciones 0-6
int[] multiplicadores = { 3, 2, 7, 6, 5, 4, 3 };
int suma = (1*3) + (7*2) + (1*7) + (2*6) + (3*5) + (4*4) + (5*3) = 80
int residuo = 80 % 11 = 3
int digitoEsperado = 11 - 3 = 8
// Comparar con dígito verificador (posición 9): 8 ✓ Válido
```

**Flujo Interno:**
1. Extraer claims JWT (`ClaimTypes.NameIdentifier` → UsuarioId, `ClaimTypes.Role` → Rol).
2. Validar autorización (Admin o propietario).
3. Si denegado: retornar 403.
4. Buscar usuario en BD.
5. Si no existe: retornar 404.
6. Validar cada campo proporcionado en request.
7. Si hay errores de validación: retornar 400 con lista de errores.
8. Si cambios en Email/Cedula violarían unicidad: retornar 409.
9. Aplicar cambios validados a la entidad.
10. Guardar con `SaveChangesAsync()`.
11. Mapear a DTO y retornar 200.

**Ejemplos de Uso:**

Actualizar solo el nombre:
```json
{ "nombre": "Nuevo Nombre" }
```

Cambiar email y verificar (solo Admin puede):
```json
{
  "email": "newemail@ejemplo.com",
  "rol": "Admin"
}
```

---

### 3.4 DELETE /api/usuarios/{id}

**Objetivo:** Eliminar permanentemente un usuario del sistema.

**Parámetros:**
- `{id}` (path, obligatorio): UUID del usuario a eliminar (GUID válido).

**Autorización:**
- Requiere JWT válido.
- Acceso restringido:
  - Admin puede eliminar **cualquier usuario**.
  - Usuario normal puede eliminar **solo su propia cuenta**.

**Respuesta Exitosa (204 No Content):**
```
[Cuerpo vacío - estándar REST para DELETE exitoso]
```

**Errores Posibles:**

| Código | Escenario |
|--------|-----------|
| `401 Unauthorized` | JWT ausente o inválido. |
| `403 Forbidden` | Usuario no tiene permisos (no propietario ni Admin). |
| `404 Not Found` | Usuario no existe. |
| `500 Internal Server Error` | Error no esperado. |

**Consideraciones Críticas de Diseño:**

⚠️ **Eliminación Física (No Lógica)**

El sistema implementa **eliminación física** directa de la entidad Usuario. Esto implica:

**Impacto en Datos Relacionados:**
- **Elementos:** Los bienes creados por el usuario eliminado tendrán un `UsuarioIdPropietario` que apunta a un usuario no existente (**estado huérfano**). Esto es intencional.
- **Revisiones:** Los registros de auditoría se mantienen (referencia histórica mantenida).
- Integridad referencial: No se bloquea la eliminación.

**Justificación:**
1. **Trazabilidad Histórica:** Mantener referencias al usuario eliminado permite auditoría retroactiva.
2. **Simplificación arquitectónica:** Evita lógica compleja de reasignación o borrado en cascada.
3. **Fase 1:** Enfoque pragmático. Future puede implementarse soft-delete con campo `DeletedAt`.

**Alternativas Futuras:**
- Implementar soft-delete (agregar columna `DeletedAt: DateTime?`).
- Reasignar elementos a cuenta administrativa.
- Crear usuario "huérfano" ficticio para capturar referencias.

**Flujo Interno:**
1. Extraer claims JWT.
2. Validar autorización (Admin o propietario).
3. Si denegado: retornar 403.
4. Buscar usuario en BD.
5. Si no existe: retornar 404.
6. Remover usuario: `DbContext.Usuarios.Remove(usuario)`.
7. Guardar: `SaveChangesAsync()`.
8. Retornar 204 No Content (sin cuerpo).

---

## 4. VALIDACIONES TRANSVERSALES

### Autenticación JWT

**Requisito:** Todos los endpoints (excepto públicos) requieren JWT válido.

**Transmisión:**
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Extracción de Claims:**
- `ClaimTypes.NameIdentifier` → `UsuarioId` (Guid del usuario autenticado).
- `ClaimTypes.Role` → `Rol` (string: "Admin" o "User").
- `JwtRegisteredClaimNames.Email` → Email del usuario.

**Validación Automática:** ASP.NET Core valida firma y expiración automáticamente. Si JWT es inválido, middleware rechaza con 401.

### Autorización RBAC (Role-Based Access Control)

**Matriz de Permisos:**

| Operación | Requisito | Acceso |
|-----------|-----------|--------|
| GET /api/usuarios | Autenticado | Universal (cualquier rol) |
| GET /api/usuarios/{id} | Autenticado | Universal |
| PUT /api/usuarios/{id} | Autenticado | Admin O propietario |
| DELETE /api/usuarios/{id} | Autenticado | Admin O propietario |

**Verificación Manual en Handlers:**
```csharp
bool tienePermiso = rolUsuario == "Admin" || usuarioIdJWT == idRecurso;
if (!tienePermiso) return Results.Forbid(); // 403
```

### Validación de Propiedad

**Criterio:** El campo `UsuarioId` extraído del JWT debe coincidir con el `{id}` de la ruta para usuarios normales.

**Excepciones:** Admin bypass (puede modificar/eliminar cualquier usuario).

---

## 5. ESTRUCTURA DE DATOS (DTOs)

### GetUsuariosResponse / GetUsuarioByIdResponse
```csharp
public class GetUsuarioByIdResponse
{
    public Guid Id { get; set; }
    public string Cedula { get; set; }
    public string Nombre { get; set; }
    public string Email { get; set; }
    public string Rol { get; set; }
    // ⚠️ PasswordHash NUNCA se incluye en respuestas (seguridad)
}
```

### UpdateUsuarioRequest
```csharp
public class UpdateUsuarioRequest
{
    public string? Nombre { get; set; }        // Opcional
    public string? Email { get; set; }         // Opcional
    public string? Cedula { get; set; }        // Opcional
    public string? Rol { get; set; }           // Opcional (solo Admin)
    // ⚠️ PasswordHash NO se puede cambiar desde aquí (futura ruta separada)
}
```

### UpdateUsuarioResponse
```csharp
public class UpdateUsuarioResponse
{
    public Guid Id { get; set; }
    public string Cedula { get; set; }
    public string Nombre { get; set; }
    public string Email { get; set; }
    public string Rol { get; set; }
}
```

---

## 6. CÓDIGOS DE ESTADO HTTP

| Código | Método | Significado |
|--------|--------|------------|
| `200 OK` | GET, PUT | Operación exitosa, datos retornados. |
| `204 No Content` | DELETE | Eliminación exitosa, sin cuerpo en respuesta. |
| `400 Bad Request` | PUT | Validación fallida (nombre inválido, email duplicado, etc.). |
| `401 Unauthorized` | GET, PUT, DELETE | JWT ausente, inválido o expirado. |
| `403 Forbidden` | PUT, DELETE | Usuario sin permisos (no propietario ni Admin). |
| `404 Not Found` | GET, PUT, DELETE | Recurso no encontrado. |
| `409 Conflict` | PUT | Email o cédula ya registrados. |
| `500 Internal Server Error` | * | Error no esperado en servidor. |

---

## 7. MANEJO DE ERRORES

### Estructura de Error Estándar
```json
{
  "message": "Descripción clara del error.",
  "errors": [
    "Validación 1 fallida.",
    "Validación 2 fallida."
  ]
}
```

### Ejemplos de Errores

**Validación fallida (400):**
```json
{
  "message": "Validación fallida.",
  "errors": [
    "El email ya se encuentra registrado en el sistema.",
    "La cédula no cumple con la validación de módulo 10."
  ]
}
```

**Duplicidad (409):**
```json
{
  "message": "La cédula ya se encuentra registrada en el sistema."
}
```

**Permiso denegado (403):**
```json
(Sin cuerpo - solo HTTP 403)
```

---

## 8. FLUJOS DE CASO DE USO

### UC1: Admin actualiza perfil de otro usuario

**Precondiciones:**
- Admin autenticado con JWT válido.
- Usuario objetivo existe.

**Flujo:**
1. Admin hace PUT /api/usuarios/550e8400-e29b-41d4-a716-446655440000
2. JWT claim `ClaimTypes.Role` = "Admin" ✓
3. Handler valida autorización (Admin bypass) ✓
4. Procede a validar y aplicar cambios
5. Retorna 200 con datos actualizados

---

### UC2: Usuario normal intenta modificar otro perfil

**Precondiciones:**
- Usuario normal autenticado.
- Intenta modificar usuario diferente al suyo.

**Flujo:**
1. User hace PUT /api/usuarios/other-user-id
2. JWT claim `UsuarioId` ≠ other-user-id
3. JWT claim `Rol` = "User" (no Admin)
4. Validación de autorización falla
5. Handler retorna 403 Forbidden

---

### UC3: Usuario elimina su propia cuenta

**Precondiciones:**
- Usuario autenticado.
- Decide eliminar su propia cuenta.

**Flujo:**
1. User hace DELETE /api/usuarios/{su-propio-id}
2. JWT claim `UsuarioId` == {su-propio-id} ✓
3. Validación autorizada
4. Usuario eliminado físicamente de BD
5. Retorna 204 No Content
6. Solicitudes futuras con ese JWT siguen siendo válidas (token sigue firmado, pero BD no tiene usuario)

---

## 9. CONSIDERACIONES DE SEGURIDAD

### Principios Implementados

1. **Never Expose Passwords:** `PasswordHash` nunca se retorna en respuestas.
2. **No Password Update Here:** Cambios de contraseña deben ir en endpoint separado (no implementado en Phase 1).
3. **GUID Identification:** UUIDs evitan ataques de enumeración secuencial.
4. **Stateless Auth:** JWT permite escalabilidad horizontal sin sesiones server-side.
5. **Role-Based Access:** Autorización verificada explícitamente en cada operación.

### Vectores de Ataque Mitigados

| Ataque | Mitigación |
|--------|-----------|
| IDOR (Insecure Direct Object Reference) | Validación de propiedad + GUID no secuencial |
| Privilege Escalation | Role bypass en Update (solo Admin puede cambiar roles) |
| Cédula Duplicada | Índice único en BD + validación en aplicación |
| Email Duplicado | Índice único + validación en aplicación |
| Token Hijacking | JWT firmado con clave simétrica en appsettings |

---

## 10. INTEGRACIÓN CON ARQUITECTURA DEL SISTEMA

### Dependencias Externas

- **ApplicationDbContext** (Infrastructure/Database)
  - Acceso a tabla `Usuarios`
  - Métodos: `SaveChangesAsync()`, queries LINQ

- **IJwtProvider** (Infrastructure/Security)
  - NO se usa en este módulo (solo lectura de JWT existente)
  - Será usado en Auth (Login/Registro)

- **Microsoft.EntityFrameworkCore**
  - Queries asincrónicas
  - `FirstOrDefaultAsync`, `ToListAsync`, `AnyAsync`

### Entidad Relacionada

- **Usuario** (Domain/Entities)
  - Propiedades: Id, Cedula, Nombre, Email, PasswordHash, Rol
  - PK: Id (Guid)
  - Índices: Unique en Cedula, Email

---

## 11. TESTING Y VALIDACIÓN

### Casos de Prueba Críticos

1. **GET /api/usuarios**: Retorna lista (vacía o llena)
2. **GET /api/usuarios/{id}**: ID válido → 200, ID inválido → 404
3. **PUT**: Email duplicado → 409, Cédula inválida → 400
4. **PUT**: User intenta modificar otro → 403, Admin modifica → 200
5. **DELETE**: User elimina su cuenta → 204, intenta eliminar otra → 403
6. **Cédula Módulo 10**: Válida (1712345678) → acepta, inválida (1712345679) → rechaza

### Herramientas Recomendadas

- **Postman/Insomnia:** Tests manuales
- **dotnet test:** Tests unitarios (future)
- **Swagger UI:** Exploración interactiva

---

## 12. LIMITACIONES Y MEJORAS FUTURAS

### Phase 1 (Actual)
✅ CRUD básico
✅ Validaciones de datos
✅ RBAC simple
✅ EF Core In-Memory

### Phase 2 (Recomendadas)
- [ ] Paginación en GET /api/usuarios
- [ ] Filtros por rol, nombre, email
- [ ] Endpoint separado para cambio de contraseña
- [ ] Soft-delete (agregar `DeletedAt: DateTime?`)
- [ ] Auditoría de cambios (log de quién modificó qué)
- [ ] SQL Server (reemplazar In-Memory DB)
- [ ] MediatR + CQRS (refactorización)
- [ ] Tests unitarios automatizados
- [ ] Rate limiting
- [ ] Encriptación de datos sensibles en tránsito

---

## 13. EJEMPLOS COMPLETOS DE USO

### Ejemplo 1: Listar todos los usuarios

**Request:**
```bash
curl -X GET "https://localhost:5001/api/usuarios" \
  -H "Authorization: Bearer eyJhbGc..."
```

**Response (200 OK):**
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "cedula": "1712345678",
    "nombre": "Admin Sistemas",
    "email": "admin@sistema.com",
    "rol": "Admin"
  },
  {
    "id": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
    "cedula": "1798765432",
    "nombre": "Usuario Normal",
    "email": "user@sistema.com",
    "rol": "User"
  }
]
```

---

### Ejemplo 2: Obtener usuario por ID

**Request:**
```bash
curl -X GET "https://localhost:5001/api/usuarios/550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer eyJhbGc..."
```

**Response (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "cedula": "1712345678",
  "nombre": "Admin Sistemas",
  "email": "admin@sistema.com",
  "rol": "Admin"
}
```

**Response (404 Not Found):**
```json
{
  "message": "Usuario con ID 99999999-9999-9999-9999-999999999999 no encontrado."
}
```

---

### Ejemplo 3: Actualizar usuario (Admin modifica otro)

**Request:**
```bash
curl -X PUT "https://localhost:5001/api/usuarios/6ba7b810-9dad-11d1-80b4-00c04fd430c8" \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Content-Type: application/json" \
  -d '{
    "nombre": "Usuario Actualizado",
    "email": "newemail@sistema.com",
    "rol": "Admin"
  }'
```

**Response (200 OK):**
```json
{
  "id": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "cedula": "1798765432",
  "nombre": "Usuario Actualizado",
  "email": "newemail@sistema.com",
  "rol": "Admin"
}
```

---

### Ejemplo 4: Actualizar usuario (Error de validación)

**Request:**
```bash
curl -X PUT "https://localhost:5001/api/usuarios/550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Content-Type: application/json" \
  -d '{
    "cedula": "1712345679",
    "email": "invalidemail"
  }'
```

**Response (400 Bad Request):**
```json
{
  "message": "Validación fallida.",
  "errors": [
    "El formato del email es inválido.",
    "La cédula no cumple con la validación de módulo 10."
  ]
}
```

---

### Ejemplo 5: Eliminar usuario

**Request:**
```bash
curl -X DELETE "https://localhost:5001/api/usuarios/6ba7b810-9dad-11d1-80b4-00c04fd430c8" \
  -H "Authorization: Bearer eyJhbGc..."
```

**Response (204 No Content):**
```
[Sin cuerpo - solo headers HTTP]
```

**Verificación (GET después de DELETE):**
```bash
curl -X GET "https://localhost:5001/api/usuarios/6ba7b810-9dad-11d1-80b4-00c04fd430c8" \
  -H "Authorization: Bearer eyJhbGc..."
```

**Response (404 Not Found):**
```json
{
  "message": "Usuario con ID 6ba7b810-9dad-11d1-80b4-00c04fd430c8 no encontrado."
}
```

---

## 14. RESUMEN EJECUTIVO

| Aspecto | Descripción |
|--------|------------|
| **Patrón** | Vertical Slice Architecture + Minimal APIs |
| **Endpoints** | 4 (GET list, GET by ID, PUT, DELETE) |
| **Autorización** | JWT + RBAC (Admin/User) + Propiedad |
| **Validaciones** | Nombre, Email (formato + unicidad), Cédula (Módulo 10 + unicidad), Rol |
| **Códigos HTTP** | 200, 204, 400, 401, 403, 404, 409, 500 |
| **Base de Datos** | EF Core In-Memory (Phase 1) |
| **Documentación** | Swagger + OpenAPI |
| **Seguridad** | No expone passwords, GUID identifiers, Stateless auth |
| **Estado** | ✅ Implementado y compilado exitosamente |

-