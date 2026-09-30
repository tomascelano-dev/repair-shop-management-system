# Checklist, notas y adjuntos en el flujo

En RepairShop, la información “operativa” de una orden se guarda separada en piezas chicas para que el flujo sea editable y auditable.

## Checklist de recepción

Entidad: `RepairOrderReceptionChecklist`

Casos típicos:

- Estado del equipo al ingresar (pantalla, cámara, sensores, face-id, etc.)
- Señales de humedad
- Accesorios entregados
- Observaciones de recepción

### Endpoints

- `GET /api/v1/orders/{orderId}/checklist`
  - Devuelve **404** si todavía no existe checklist (orden “sin checklist cargado”).
- `PUT /api/v1/orders/{orderId}/checklist`
  - **Upsert**: crea o actualiza.
  - Genera evento de auditoría `checklist_updated`.

## Notas

Notas simples para registrar diagnóstico, acuerdos con el cliente, etc.

### Endpoints

- `GET /api/v1/orders/{orderId}/notes`
  - Lista notas.
- `POST /api/v1/orders/{orderId}/notes`
  - Crea una nota.

## Adjuntos

Los adjuntos se modelan como **links** (URL) + etiqueta (por ejemplo, foto en Cloudinary/Drive).

### Endpoints

- `GET /api/v1/orders/{orderId}/attachments`
  - Lista adjuntos.
- `POST /api/v1/orders/{orderId}/attachments`
  - Crea adjunto.

## Recomendación operativa

- Checklist al ingresar (te ahorra disputas).
- Notas durante diagnóstico y antes de “Ready”.
- Adjuntos para evidencias (antes/después, daño previo, etc.).
