# RepairShop · API

Backend de **RepairShop**, el sistema de gestión para talleres de reparación (celulares, notebooks, consolas): recepción de equipos, órdenes con flujo de estados, presupuestos con aprobación del cliente, stock, punto de venta, caja, facturación electrónica, mensajes automáticos, reportes y portal público de seguimiento.

.NET 8 · ASP.NET Core · EF Core 8 + PostgreSQL 16 · Clean Architecture (Domain / Application / Infrastructure / Api).

## Módulos

- **Recepción y órdenes**: numeración por sucursal, prioridad, técnico asignado, fecha prometida, checklist de ingreso, código de desbloqueo cifrado (se borra al entregar), firmas de recepción y entrega, fotos y adjuntos, notas internas o visibles para el cliente, historial y [flujo de estados con reglas](docs/workflows/order-state-machine.md) (presupuesto aprobado, control de calidad, saldo en cero).
- **Presupuestos** versionados (mano de obra, repuestos, descuentos, garantía, validez), envío al cliente y aprobación/rechazo desde el portal o por el taller; al aprobar se reservan los repuestos.
- **Garantías**: vencimiento al entregar, certificado PDF y reclamos que generan una orden vinculada.
- **Clientes**: ficha 360, detección de duplicados por teléfono normalizado, unificación, importación/exportación Excel/CSV, consentimiento para avisos y promociones.
- **Inventario**: disponible vs. reservado, stock mínimo, ajustes y conteos, compatibilidad por marca/modelo, control de concurrencia (no se vende dos veces la última unidad), importación/exportación.
- **Compras** a proveedores con recepción parcial (actualiza stock y costo) y **transferencias** entre sucursales.
- **Punto de venta**: ventas con varios medios de pago y vuelto, descuentos por línea y globales, devoluciones parciales con o sin reingreso a stock, anulación (admin) y ticket PDF.
- **Caja**: turnos con apertura, movimientos (ventas, cobros de órdenes, ingresos, gastos, retiros), arqueo por medio de pago y reporte PDF de cierre.
- **Cobros**: pagos y señas de órdenes, devoluciones, links de pago de **Mercado Pago** con webhook firmado.
- **Facturación electrónica ARCA** (WSAA/WSFE): facturas A/B/C según condición de IVA, notas de crédito, CAE y QR.
- **Multi-moneda**: ARS/USD con cotizaciones diarias (manual, oficial, blue) para convertir los reportes.
- **Mensajes**: plantillas por sucursal, outbox con reintentos (WhatsApp por Twilio o Meta, email por SMTP), links de WhatsApp manuales, recordatorios de retiro y encuesta de satisfacción.
- **Portal público** (`/public/orders/{token}`): estado, línea de tiempo, presupuesto para aprobar, pago online, garantía y encuesta, con rate limit propio.
- **Reportes**: ingresos, márgenes, tiempos por estado, presupuestos, fallas y modelos, técnicos, garantías, satisfacción, stock valorizado; exportación a Excel. Tablero diario y consolidado por sucursal.
- **Sugerencias de reparación**: órdenes parecidas, ítems y rango de precios habituales, repuestos compatibles y, opcionalmente, hipótesis de diagnóstico con **Claude** (sin datos personales del cliente).
- **Administración**: sucursales (organización multi-sucursal), usuarios con invitación por link, roles por sucursal, configuración del negocio, logo, integraciones y **auditoría**.

## Roles

| Permiso | Admin | Técnico | Recepción | Caja |
| --- | :-: | :-: | :-: | :-: |
| Crear y gestionar órdenes, clientes y presupuestos | ✓ | ✓ | ✓ | |
| Trabajar órdenes (diagnóstico, repuestos, QA, mensajes) | ✓ | ✓ | ✓ | |
| Ventas, caja, cobros y facturas | ✓ | | ✓ | ✓ |
| Inventario, compras y transferencias | ✓ | | | |
| Reportes | ✓ | | | |
| Configuración, usuarios, integraciones, auditoría | ✓ | | | |

Caja puede además registrar la **entrega** de equipos. Entregar con saldo pendiente, anular ventas y emitir notas de crédito requiere Admin.

## Desarrollo

### Con Docker

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

- API: http://localhost:8080 · Swagger: http://localhost:8080/swagger
- Salud: `/healthz` (proceso) y `/readyz` (base de datos)
- Los mensajes se simulan (quedan en el log); las migraciones y el seed corren al iniciar.

### Sin Docker

Con un PostgreSQL 13+ local:

```bash
cd src/RepairShop.Api
export ConnectionStrings__RepairShopDb="Host=localhost;Port=5432;Database=repairshop;Username=postgres;Password=postgres"
export Jwt__Key="dev-signing-key-0123456789abcdef0123456789abcdef"
export Seed__DemoData=true
dotnet run
```

O guardalo en `src/RepairShop.Api/appsettings.Local.json` (gitignored). En Development el seed crea la sucursal, plantillas de mensajes, datos de demo y estos usuarios:

| Rol | Email | Contraseña |
| --- | --- | --- |
| Admin | `admin@local` | `Admin123456` |
| Técnico | `tech@local` | `Tech123456` |
| Recepción | `recepcion@local` | `Recepcion123` |
| Caja | `caja@local` | `Caja123456` |

Política de contraseñas: mínimo 8 caracteres, con letras y números. Archivo de requests de ejemplo: `RepairShop.http`.

## Tests

```bash
dotnet test RepairShop.sln
```

- `RepairShop.Domain.Tests` y `RepairShop.Application.Tests`: reglas de negocio puras.
- `RepairShop.Api.IntegrationTests`: la API completa contra PostgreSQL real (login y refresh, aislamiento entre sucursales, flujo de órdenes, POS y caja, portal, migración desde una base vieja con datos inconsistentes). Necesitan un servidor de PostgreSQL; cada corrida crea y borra su propia base:

```bash
export REPAIRSHOP_TEST_CONNECTION="Host=localhost;Port=5432;Username=postgres;Password=postgres"
dotnet test RepairShop.sln
```

Sin esa variable, los tests de integración se saltean. La CI (`.github/workflows/ci.yml`) corre todo, más los tests del frontend, el build de la imagen Docker y los tests end-to-end con Playwright.

## Configuración

Todo se configura con variables de entorno (`Seccion__Clave`). Referencia completa: `src/RepairShop.Api/appsettings.Example.json`.

| Variable | Para qué |
| --- | --- |
| `ConnectionStrings__RepairShopDb` | PostgreSQL (13 o superior). **Obligatoria.** |
| `Jwt__Key` | Firma de los tokens, mínimo 32 bytes. **Obligatoria.** |
| `Jwt__AccessTokenMinutes`, `Jwt__RefreshTokenDays` | Vida del token de acceso (15) y de la sesión (30). |
| `Auth__RefreshCookie__SameSite` | `Strict` (default). `None` solo si el frontend y la API están en sitios distintos (requiere HTTPS). |
| `App__FrontendBaseUrl`, `App__PublicApiUrl` | URLs públicas: links de seguimiento, QR, invitaciones y webhooks. |
| `Cors__AllowedOrigins__0..n` | Orígenes del frontend (obligatorio fuera de Development, sin `*`). |
| `AllowedHosts` | Dominios de la API (obligatorio en Production). |
| `ReverseProxy__KnownProxies__n`, `ReverseProxy__KnownNetworks__n` | Proxies de confianza para `X-Forwarded-For` (rate limiting por IP real). |
| `RateLimiting__AuthPerMinute`, `RefreshPerMinute`, `PublicPerMinute`, `WebhooksPerMinute` | Límites por IP. |
| `DataProtection__MasterKey` | Cifra el key ring que protege tokens de Mercado Pago y certificados ARCA (`openssl rand -base64 32`). Recomendado en producción. |
| `Storage__Provider` | `Local` (volumen) o `S3` (AWS S3, Cloudflare R2, MinIO) con `Storage__S3__*`. |
| `Notifications__WhatsApp__Provider` | `None`, `Simulated`, `Twilio` o `Meta` (con `Notifications__Twilio__*` / `Notifications__Meta__*`). |
| `Notifications__Email__Provider` | `None`, `Simulated` o `Smtp` (con `Notifications__Smtp__*`). |
| `Jobs__Enabled` | Tareas programadas (ver abajo). |
| `ExchangeRates__Provider` | `DolarApi` (cotizaciones automáticas) o `None`. |
| `Ai__Provider`, `Ai__Anthropic__ApiKey` | Sugerencias con Claude (`Anthropic`) o `None`. |
| `Swagger__Enabled`, `Seed__Enabled`, `Seed__DemoData` | Solo desarrollo: en Production la API no arranca si Swagger o el seed están activos. |
| `OpenTelemetry__Enabled`, `OpenTelemetry__OtlpEndpoint` | Trazas y métricas OTLP. |

Mercado Pago y ARCA se configuran **por sucursal** desde la app (Configuración → Integraciones); los secretos se guardan cifrados.

### Tareas programadas

Con `Jobs__Enabled=true`, la API ejecuta en segundo plano (con locks de PostgreSQL, así que varias réplicas no duplican trabajo): vencimiento de presupuestos, recordatorios de retiro, encuestas de satisfacción, actualización de cotizaciones y limpieza de sesiones vencidas y claves de idempotencia. El despacho del outbox de mensajes corre aparte (`Notifications__DispatcherEnabled`).

## Base de datos

- Las migraciones se aplican al iniciar (`Database__Init=Migrate`; `None` para manejarlas aparte).
- **Primer administrador en producción**: `dotnet RepairShop.Api.dll admin create <email> ["Nombre"] ["Nombre del taller"]`. Crea el taller (si la base está vacía) y el administrador, e imprime un link de un solo uso para elegir la contraseña. Con un email existente genera un link nuevo para cambiarla (recuperación de acceso). Los usuarios de demo con contraseñas conocidas solo se crean en Development.
- La migración `PlatformModules` actualiza bases de versiones anteriores: corrige referencias inconsistentes entre sucursales, completa columnas nuevas, numera órdenes existentes e inicializa contadores. Requiere PostgreSQL 13+.
- Concurrencia optimista (`xmin`) en órdenes, stock, ventas, compras y caja: un conflicto devuelve **409** en vez de pisar datos.

## Producción

Para un servidor con un reverse proxy propio (Caddy/nginx en el host, Cloudflare), usá el stack completo de [`deploy/`](../deploy/README.md): API, PostgreSQL, interfaz servida por Caddy y copias de seguridad diarias.

Para levantar solo la API y PostgreSQL con los archivos de esta carpeta:

```bash
cp .env.example .env    # completar secretos y URLs
docker compose --env-file .env -f docker-compose.yml -f docker-compose.prod.yml up --build -d
```

- La API queda publicada solo en `127.0.0.1:8080`: poné delante un reverse proxy con HTTPS (Caddy, nginx, Traefik) y servilo junto al frontend (`/api` → API) para que la cookie de sesión sea del mismo sitio.
- PostgreSQL no se publica fuera de la red de Docker. `POSTGRES_PASSWORD` solo se aplica al crear el volumen: si ya existía, cambiá la contraseña con `ALTER USER` y actualizá la cadena de conexión.
- Definí `DataProtection__MasterKey` antes de cargar credenciales de Mercado Pago o ARCA.
- Las fotos se guardan en el volumen `repairshop_uploads` (o en S3/R2) y se sirven con URLs firmadas de corta duración.

## Seguridad

- **Sesión**: access token JWT de 15 minutos en memoria del navegador + refresh token rotativo en cookie httpOnly limitada a `/api/v1/auth` y header anti-CSRF. Reutilizar un refresh token ya usado revoca toda la sesión. Cerrar sesión en todos lados, cambio de contraseña y cambios de rol invalidan los tokens al instante (security stamp).
- **Login**: rate limit por IP y bloqueo temporal por cuenta tras intentos fallidos. Las respuestas no revelan si un email existe.
- **Aislamiento por sucursal**: filtro global por `shop_id` en todas las consultas y validación de referencias cruzadas.
- **Idempotencia** (`Idempotency-Key`) en pagos, ventas, ajustes de stock y creación de órdenes: los reintentos no duplican.
- **Secretos**: códigos de desbloqueo y credenciales de integraciones cifrados con Data Protection; ver un código de desbloqueo queda auditado.
- **Webhooks** de Mercado Pago verificados por firma; el pago se consulta a la API de Mercado Pago antes de registrarlo.
- **Errores** en formato ProblemDetails en español, con `correlationId` para rastrear en los logs (Serilog).

## Más documentación

- [Flujo de estados de las órdenes](docs/workflows/order-state-machine.md)
- [Checklist, notas y adjuntos](docs/workflows/checklist-notes-attachments.md)
- [Auditoría](docs/auditing.md)
