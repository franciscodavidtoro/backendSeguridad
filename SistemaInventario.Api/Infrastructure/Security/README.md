# Reglas de Seguridad: Infraestructura de Seguridad y Criptografía (Infrastructure/Security)

Este directorio aloja las políticas de cifrado, la gestión de la autenticación de la plataforma y el control de accesos por propiedad.

## Reglas Técnicas Obligatorias

### 1. Aislamiento Estricto de Secretos e Inyección
* **Prohibición de Hardcode:** Queda estrictamente prohibido escribir cadenas de conexión, contraseñas maestras o llaves simétricas directamente en el código fuente (Hardcoding).
* **Inyección Dinámica:** El string de conexión de SQL Server y la clave simétrica del JWT (JwtSecretKey) deben inyectarse en tiempo de ejecución a través del archivo de configuración del servidor (web.config / ppsettings.json).

### 2. Protección de Credenciales en Reposo
* **Hashing Unidireccional:** El sistema nunca almacena contraseñas en texto plano. En el registro de usuarios, la clave debe pasar por un algoritmo de hashing con sal de alta iteración (**BCrypt** o **PBKDF2**).

### 3. Mecanismo de Autenticación Stateless (JWT)
* **Tokens de Acceso:** La validación de identidad se realiza mediante **JSON Web Tokens (JWT)** pasados en la cabecera HTTP bajo el estándar Authorization: Bearer <Token>.
* **Descifrado Centralizado:** El backend descifra y verifica la firma del token utilizando la clave simétrica del servidor, extrayendo de forma segura el UsuarioId (UUID) y el Rol.

### 4. Matriz de Autorización Híbrida (RBAC + Propiedad)
* **Registro Autofirmado Seguro:** El endpoint público de registro ignora cualquier rol enviado por el cliente e impone de manera inmutable el rol **User** para evitar la escalabilidad de privilegios no autorizada.
* **Lectura Global Abierta:** Cualquier usuario autenticado (con JWT válido) tiene autorización lógica para listar y visualizar (GET) la totalidad de los datos de usuarios y elementos del inventario.
* **Escritura, Modificación y Eliminación Restringida (POST, PUT, DELETE):**
  * **Rol Admin:** Posee control total sobre el ecosistema completo de datos.
  * **Rol User:** Su acción está acotada estrictamente a la **propiedad física del registro**. El backend intercepta la petición, compara el UUID extraído del JWT contra el campo UsuarioIdPropietario (o ID de usuario). Si estos no coinciden y el rol no es administrador, la API debe abortar inmediatamente la transacción y retornar un código **HTTP 403 Forbidden**.
