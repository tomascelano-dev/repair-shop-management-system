# Checklists, notas, fotos y firmas

La información operativa de una orden se guarda en piezas chicas, editables y auditadas. Todas las rutas cuelgan de `/api/v1/orders/{orderId}`.

## Checklist de recepción

Cómo ingresó el equipo: pantalla, cámaras, parlantes, micrófono, botones, Face ID, huella, bloqueo de cuenta (iCloud/Google), batería y estado estético. Se completa en la recepción y queda impreso en el comprobante que firma el cliente.

- `GET checklist` → el checklist o `null` si todavía no se cargó.
- `PUT checklist` → crea o actualiza (permiso de gestión de órdenes). Auditoría: `checklist_updated`.

## Control de calidad de salida (QA)

Pruebas antes de entregar: enciende, pantalla, táctil, cámaras, audio, micrófono, botones, carga, conectividad, biometría, salud de batería y notas. Cada ítem es OK / falla / no aplica.

- `GET qa` → el control o `null`.
- `PUT qa` con `approve: true` lo aprueba (no se puede aprobar con fallas). **Es obligatorio para pasar a “Listo para retirar”**, y un reproceso posterior lo invalida. Auditoría: `qa_saved`.

## Notas

- `GET notes` / `POST notes` (`body`, `isPublic`).
- Las notas **públicas** se muestran en el portal del cliente como “novedades del taller”; las internas solo las ve el equipo. Auditoría de las públicas: `public_note_added`.

## Fotos y documentos

- `POST attachments/upload` (multipart, campo `file`, `label` opcional): fotos JPG/PNG/WebP/GIF/HEIC hasta 10 MB y PDF hasta 15 MB. El tipo se detecta por el contenido del archivo, no por la extensión.
- `POST attachments` registra un link externo (`url`, `label`).
- `GET attachments` devuelve URLs **firmadas que vencen a los 30 minutos**: una foto no queda accesible para siempre aunque se comparta el link.
- `DELETE attachments/{id}`.
- Los archivos se guardan en disco (volumen Docker) o en S3/R2 según `Storage__Provider`. Auditoría: `attachment_uploaded` / `attachment_deleted`.

## Firmas

- `POST signatures` (`kind`: `Reception` | `Delivery`, `signerName`, `imageDataUrl` PNG). La firma de recepción se pide al ingresar el equipo y aparece en el comprobante; la de entrega, al retirarlo.

## Código de desbloqueo

- `PUT unlock` (`method`: `None` | `Pin` | `Password` | `Pattern`, `value`): se guarda cifrado.
- `POST unlock/reveal` lo muestra y deja registro en la auditoría (`unlock_secret_viewed`).
- Se borra automáticamente al entregar o cancelar la orden.

## Recomendación operativa

- Checklist y fotos al ingresar: evitan discusiones por daños previos.
- Notas públicas en cada avance: bajan las consultas de “¿ya está?”.
- QA siempre antes de avisar que está listo, y fotos del “después”.
