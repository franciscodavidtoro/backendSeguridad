# Diferencias entre implementación y README

## Resumen
Breve comparativa entre lo documentado en `README.md` y lo efectivamente implementado en el proyecto.

## Endpoints implementados (detectados en código)
- /api/health (GET) — público
- /api/auth/registro (POST) — público
- /api/auth/login (POST) — público (retorna 401 si credenciales inválidas)

Usuarios:
- /api/usuarios (GET)
- /api/usuarios/{id:guid} (GET, PUT, DELETE)

Elementos:
- /api/elementos (GET, POST)
- /api/elementos/{id} (GET, PUT, DELETE)
- /api/elementos/importar (POST) — importa Excel/stream
- /api/elementos/exportar (GET) — descarga Excel

Revisiones (auditorías):
- /api/revisiones (POST) — crear
- /api/revisiones (GET)
- /api/revisiones/{id} (GET)
- /api/revisiones/{id}/escanear (POST)
- /api/revisiones/{id}/finalizar (POST)

(Otros mapeos se encuentran en `Program.cs` y en los archivos bajo `Features/`.)

## Diferencias y hallazgos importantes
- Endpoints para imágenes:
  - Observación: `Program.cs` crea el directorio `wwwroot/images/` y `UseStaticFiles()` sirve archivos estáticos, pero no se encontró ningún endpoint API para subir (`POST`) ni para administrar (subir/borra/obtener vía API) imágenes. El README indica uso de `wwwroot/images/` como almacenamiento, pero la API para carga/retrieval no está implementada.
  - Consecuencia: aunque las imágenes pueden servirse si el archivo existe en disco, no hay mecanismo API seguro para que clientes suban o borren imágenes.

- Documentación de códigos HTTP (401 Unauthorized):
  - En README está listado el código 401 en secciones generales, y en `Features/Usuarios/README.md` varios endpoints documentan `401`.
  - Sin embargo, muchas rutas que usan `.RequireAuthorization()` (por ejemplo los endpoints de `Elementos` y `Revisiones`) no incluyen explícitamente `.Produces(StatusCodes.Status401Unauthorized)` ni la entrada `responses: '401'` en la definición OpenAPI generada. Resultado: Swagger UI puede no mostrar 401 en esos endpoints.
  - Nota técnica: el middleware `JwtValidationMiddleware` simplemente no establece `HttpContext.User` si el token es inválido; los endpoints con `RequireAuthorization()` retornarán 401 automáticamente, pero la documentación OpenAPI no incluye esa respuesta a menos que se agregue `.Produces(401)` o un OperationFilter que la añada.

- Inconsistencias de documentación vs comportamiento real:
  - El README muestra reglas de autorización (JWT obligatorio para la mayoría), y el proyecto implementa `RequireAuthorization()` en muchos endpoints; sin embargo la documentación OpenAPI generada puede no reflejar todas las respuestas de error (401/403) para cada ruta.
  - El endpoint de login devuelve `Results.Unauthorized()` en el código (correcto), y README documenta 401 para login. Para otros endpoints que devuelven 401 por autorización faltante, falta la declaración explícita en OpenAPI.

- Otros puntos lógicos detectados:
  - Se detectó un `QuitarCandadoFiltro` (OperationFilter) que elimina el candado de autorizaciones en Swagger cuando el endpoint tiene `.AllowAnonymous()` — esto está bien; pero no agrega respuestas 401 por defecto a los endpoints protegidos.
  - Uso de esquema de autenticación "Passive" y `JwtValidationMiddleware`: la validación de token se hace en middleware propio y no se registra un handler JWT estándar; comportamiento funcional es correcto, pero conviene asegurar que OpenAPI/Swagger muestre el esquema `Bearer` (ya hay AddSecurityDefinition en Program.cs) y que las respuestas 401 estén visibles por endpoint.

## Archivos con evidencias (ejemplos)
- Endpoints mapeados en: [SistemaInventario.Api/Program.cs](SistemaInventario.Api/Program.cs#L1-L200)
- Login retorna 401: [SistemaInventario.Api/Features/Auth/Login.cs](SistemaInventario.Api/Features/Auth/Login.cs#L1-L120)
- `JwtValidationMiddleware`: [SistemaInventario.Api/Infrastructure/Security/JwtValidationMiddleware.cs](SistemaInventario.Api/Infrastructure/Security/JwtValidationMiddleware.cs#L1-L200)
- `QuitarCandadoFiltro` (Swagger): [SistemaInventario.Api/Infrastructure/Security/QuitarCandadoFiltro.cs](SistemaInventario.Api/Infrastructure/Security/QuitarCandadoFiltro.cs#L1-L200)
- Algunos endpoints declaran `.Produces(401)`: revisa `Features/Usuarios/*` (ej.: `GetUsuarioById.cs`, `UpdateUsuario.cs`, `DeleteUsuario.cs`).

## Recomendaciones (prioritarias)
1. Implementar endpoints para gestión de imágenes (mínimo):
   - `POST /api/images` — subir imagen (multipart/form-data) → validar tipo/tamaño → almacenar en `wwwroot/images/` y devolver ruta pública.
   - `GET /api/images/{filename}` — (opcional) proxy que valide permisos antes de devolver el archivo; o documentar claramente que `UseStaticFiles()` sirve `/images/...`.
   - `DELETE /api/images/{filename}` — borrar imagen (requiere autorización/admin según reglas de negocio).

2. Hacer que OpenAPI documente 401 para todos los endpoints protegidos:
   - Opción A: añadir `.Produces(StatusCodes.Status401Unauthorized)` en cada endpoint protegido.
   - Opción B (recomendado): añadir un `IOperationFilter` que inyecte automáticamente la respuesta 401 en todas las operaciones que no sean `AllowAnonymous()` y que requieran autorización. Esto evita olvidos humanos.

3. Añadir validación y documentación del límite de tamaño de archivos en los endpoints de subida (y devolver 413 Payload Too Large o 400 con mensaje claro).

4. Revisión rápida de seguridad:
   - Asegurarse de que `JwtSettings:SecretKey` esté configurado y no hardcodeada.
   - En endpoints de carga, sanear nombres de archivo y evitar path traversal.

## Resumen corto de prioridades
- Alta: crear APIs de subida/gestión de imágenes y documentarlas.
- Media: asegurar que OpenAPI muestre 401/403 en todos los endpoints protegidos (OperationFilter o `.Produces`).
- Baja: ajustes de UX en mensajes HTTP y validaciones de tamaño/formatos.

---
Fecha del análisis: 2026-06-27
Generado por: revisión automática de código en el workspace
