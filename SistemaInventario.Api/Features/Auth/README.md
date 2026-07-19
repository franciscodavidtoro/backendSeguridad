# Módulo de Autenticación y Cuentas (Features/Auth)

Este directorio contiene la lógica completa para el registro de nuevos usuarios, validación de credenciales y emisión de tokens de acceso para el Sistema de Gestión de Inventario y Auditoría. 

El módulo está construido utilizando **Vertical Slice Architecture** (Arquitectura de Cortes Verticales), aislando de manera limpia las peticiones (Requests), respuestas (Responses), lógica de negocio (Handlers) y la exposición de rutas (Endpoints) utilizando Minimal APIs.

## Reglas de Negocio y Seguridad Implementadas

### 1. Validación Estricta de Identidad y Formatos
* **Cédula Ecuatoriana (Módulo 10):** El sistema rechaza cualquier intento de registro que no cumpla con el algoritmo matemático oficial de validación de cédulas ecuatorianas.
* **Formato de Correo Electrónico:** Se utiliza validación mediante Expresiones Regulares (`Regex`) para garantizar que la cadena de texto posea una estructura de correo electrónico válida.
* **Control de Unicidad:** Se valida a nivel de aplicación (antes de llegar al ORM) que no existan correos electrónicos ni números de cédula duplicados para evitar conflictos en la base de datos (HTTP 409 Conflict).

### 2. Protección de Credenciales y Políticas de Acceso
* **Contraseñas Fuertes:** Se obliga al usuario a registrar credenciales de alta complejidad (mínimo 8 caracteres, al menos una mayúscula, un número y un carácter especial).
* **Hashing Unidireccional:** El sistema no tiene visibilidad de las contraseñas en texto plano. Se utiliza la librería `BCrypt.Net-Next` para aplicar un algoritmo de hashing criptográfico unidireccional con sal (*salt*) de alta iteración antes de la persistencia.

### 3. Asignación Autónoma de Roles (RBAC)
* **Seeding Dinámico (Primer Usuario = Admin):** Si la base de datos se encuentra completamente vacía, el sistema asignará automáticamente el rol **`Admin`** al primer usuario registrado, facilitando la configuración inicial del sistema.
* **Prevención de Escalabilidad de Privilegios:** Todos los usuarios subsiguientes recibirán de manera inmutable el rol **`User`**. El endpoint de registro descarta cualquier intento del cliente por manipular o enviar un rol diferente.

### 4. Protección contra Fuerza Bruta (Rate Limiting)
* **Bloqueo Temporal (Account Lockout):** El sistema rastrea los intentos fallidos de inicio de sesión utilizando un diccionario concurrente en memoria (`ConcurrentDictionary`). Al alcanzar 5 intentos fallidos consecutivos, la cuenta se bloquea automáticamente por 5 minutos, devolviendo un código **HTTP 429 (Too Many Requests)**. Esto protege la API sin saturar la base de datos con escrituras de auditoría de errores.

### 5. Autenticación Sin Estado (Stateless)
* **Emisión de JWT:** El inicio de sesión exitoso emite un JSON Web Token (JWT) firmado con el algoritmo `HMAC-SHA256`. 
* **Claims Integrados:** El token encapsula el UUID del usuario (Subject), su Rol y su Correo Electrónico para facilitar la toma de decisiones de autorización inmediata en el resto de los módulos sin necesidad de consultar repetitivamente a la base de datos.
* **Tiempo de Vida (TTL):** Los tokens emitidos tienen una validez estricta de 8 horas desde su generación y respetan el `Issuer` y `Audience` de la configuración global del equipo.

## Endpoints Expuestos

| Método | Ruta | Descripción | Acceso |
| :--- | :--- | :--- | :--- |
| **POST** | `/api/auth/registro` | Crea una nueva cuenta aplicando validaciones de cédula, formato de email y contraseñas fuertes. Asigna el rol dependiendo del estado de la BD. | Público (`AllowAnonymous`) |
| **POST** | `/api/auth/login` | Valida credenciales, rastrea intentos fallidos en memoria y retorna un Token JWT si la cuenta no está bloqueada. | Público (`AllowAnonymous`) |

## Dependencias Clave del Módulo
* `System.IdentityModel.Tokens.Jwt`: Para la estructuración y firma del Token Bearer.
* `BCrypt.Net-Next`: Para la encriptación segura de contraseñas.