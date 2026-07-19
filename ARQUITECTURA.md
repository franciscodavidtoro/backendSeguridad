SistemaInventario/ (Raíz del Repositorio Git)
│
├── README.md                     <-- Especificación Técnica Maestra (Portada de Git)
├── ARQUITECTURA.md               <-- Explicación de la arquitectura de Slices (Portada de Git)
├── .gitignore                    <-- Plantilla de exclusión para .NET
├── SistemaInventario.sln         <-- Archivo de Solución de Visual Studio / Rider
│
└── SistemaInventario.Api/        <-- Carpeta exclusiva del código fuente
    ├── Domain/
    │   └── Entities/
    ├── Infrastructure/
    │   ├── Database/
    │   └── Security/
    └── Features/
        ├── Auth/
        ├── Usuarios/
        ├── Elementos/
        └── Revisiones/