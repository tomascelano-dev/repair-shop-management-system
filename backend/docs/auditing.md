# Auditoría

RepairShop registra un evento de auditoría en cada acción sensible, para saber **quién hizo qué y cuándo**. Se consulta en la app (Configuración → Auditoría, solo administradores) o por API (`GET /api/v1/audit` con filtros por entidad, acción, usuario y fechas).

## Qué guarda cada evento

- `entityType` + `entityId`: sobre qué se hizo (orden, venta, caja, usuario…).
- `action`: qué se hizo.
- `actorUserId` + `actorEmail`: quién (o el sistema, en procesos automáticos y webhooks).
- `dataJson`: detalle del cambio (estados, montos, motivos). Nunca contraseñas, tokens ni códigos de desbloqueo.
- `createdAtUtc`.

Los eventos se escriben en la misma transacción que el cambio: si la operación falla, no queda un evento huérfano.

## Acciones registradas

| Entidad | Acciones |
| --- | --- |
| Órdenes (`repair_order`) | `order_created`, `order_updated`, `order_planned`, `order_deleted`, `status_changed`, `quote_created`, `quote_updated`, `quote_sent`, `quote_approved`, `quote_rejected`, `quote_set`, `quote_cleared`, `payment_added`, `payment_refunded`, `payment_link_created`, `payment_link_unapplied`, `part_used`, `checklist_updated`, `qa_saved`, `public_note_added`, `attachment_uploaded`, `attachment_deleted`, `unlock_secret_set`, `unlock_secret_viewed`, `tracking_token_regenerated`, `warranty_claimed`, `warranty_claim_created`, `invoice_issued`, `credit_note_issued` |
| Clientes y equipos (`customer`, `device`) | `customer_created`, `customer_updated`, `customer_deleted`, `customer_merged`, `customers_imported`, `device_created`, `device_updated`, `device_deleted` |
| Ventas (`sale`) | `sale_created`, `sale_refunded`, `sale_voided`, `invoice_issued`, `credit_note_issued` |
| Caja (`cash_session`) | `cash_opened`, `cash_movement_added`, `cash_closed` |
| Inventario y compras (`shop`, `purchase_order`, `stock_transfer`) | `inventory_item_created`, `inventory_item_updated`, `inventory_adjusted`, `inventory_imported`, `supplier_created`, `supplier_updated`, `purchase_order_created`, `purchase_order_updated`, `purchase_order_ordered`, `purchase_order_received`, `purchase_order_cancelled`, `transfer_created`, `transfer_received`, `transfer_cancelled` |
| Usuarios (`user`) | `user_invited`, `user_updated`, `user_password_reset_link`, `user_shop_access_granted`, `user_shop_access_revoked` |
| Sucursal (`shop`) | `shop_settings_updated`, `shop_logo_updated`, `integration_mercadopago_updated`, `integration_fiscal_updated`, `branch_created`, `branch_deactivated` |

## Por qué importa

- **Operación**: ante un reclamo (“yo pagué”, “no autoricé ese arreglo”) se ve quién registró qué.
- **Control**: anulaciones, devoluciones, entregas con saldo y diferencias de caja quedan a nombre de alguien.
- **Seguridad**: ver un código de desbloqueo o cambiar permisos deja rastro.
- **Soporte**: cada respuesta de error trae un `correlationId` que también aparece en los logs (Serilog) para cruzar con la auditoría.
