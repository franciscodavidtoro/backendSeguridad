# Módulo: Elementos

Documentación específica para las reglas de negocio, validaciones y particularidades de Elementos.

## Campos del bien

Cada elemento del inventario debe registrar la siguiente información básica:

- Código Bien: string para conservar posibles ceros a la izquierda.
- Nombre Bien: string con el nombre descriptivo del bien.
- Serie: string opcional.
- Modelo: string opcional.
- Marca / Raza / Otros: string opcional.
- Ubicación: string opcional.

## Reglas de negocio

- El código de barras, el código del bien, el nombre, el nombre del bien y la categoría son obligatorios al crear o actualizar un elemento.
- El precio no puede ser negativo.
- No se permite duplicar un código de barras dentro del inventario.
- Los usuarios solo pueden modificar o eliminar elementos que les pertenecen, salvo que tengan rol de administrador.
- La importación masiva debe respetar los campos nuevos para que el catálogo quede completo.
