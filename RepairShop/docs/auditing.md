# Auditoría (Audit log)

RepairShop registra eventos de auditoría para tener trazabilidad operativa y facilitar debugging.

## Qué se registra

Un `AuditEvent` contiene (simplificado):

- `ShopId`
- `EntityType` + `EntityId`
- `Action`
- `ActorUserId` + `ActorEmail`
- `DataJson` (payload con before/after o metadata)
- `CreatedAtUtc`

## Acciones auditadas hoy

### Órdenes (`entityType = repair_order`)

- `order_status_changed` — cambio de estado (incluye `from`, `to`).
- `quote_set` / `quote_cleared` — set/clear de cotización.
- `payment_added` — alta de pago.
- `checklist_updated` — upsert de checklist.
- `part_used` — consumo de repuesto en una orden (registro a nivel orden).

### Inventario (`entityType = inventory_item`)

- `inventory_item_created`
- `inventory_item_updated`
- `inventory_adjusted` — correcciones/consumo/etc.

## Por qué importa

- **Operación**: si un cliente discute un cambio, podés ver “quién y cuándo”.
- **Debug**: correlaciona errores (500) con acciones previas.
- **Seguridad**: detecta comportamientos anómalos.

## Recomendaciones

- No loguear bodies sensibles (passwords, JWT).
- Exportar logs a un sink (archivo/ELK/Cloud) en prod.
- (Pro) agregar traces/metrics via OpenTelemetry para ver latencias por endpoint.
