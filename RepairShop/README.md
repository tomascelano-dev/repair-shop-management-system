# RepairShop

Backend de **RepairShop**, un panel operativo para talleres de reparación (celulares/PC) orientado a flujo real de trabajo: órdenes con estado, clientes/equipos, inventario, plantillas de mensajes, auditoría y un outbox de notificaciones.

> Este repo está pensado para verse como **producto** (no demo): credenciales fuera del repo, dev/prod separados, observabilidad mínima y seguridad operativa básica.

## Problema → Solución

**Problema:** en un taller real se pierde tiempo entre WhatsApp, notas sueltas y planillas; no queda trazabilidad de cambios ni un flujo claro por estado.

**Solución:** RepairShop centraliza el ciclo de vida de cada reparación: recepción → diagnóstico → trabajo → listo → finalización/entrega; con auditoría, plantillas y reportes básicos.

## Features

- **Auth con roles**: `Admin` y `Tech`.
- **Órdenes** con *state machine* (transiciones validadas en dominio) + historial.
- **Clientes** + **Equipos** (devices) por cliente.
- **Quote** (cotización) y **pagos** asociados a la orden.
- **Checklist de recepción** + **notas** + **adjuntos**.
- **Inventario** + **repuestos por orden**.
- **Plantillas** de mensajes (para previews y outbox).
- **Auditoría** (quién/cuándo/qué cambió, con before/after cuando aplica).
- **Observabilidad**: logs estructurados (Serilog), correlation-id, `/healthz` + `/readyz`.
- **Seguridad operativa**: rate limiting + lockout en login, CORS por entorno, separación dev/prod estricta.

## Docs de workflow

- Estado de órdenes (state machine): `docs/workflows/order-state-machine.md`
- Checklist, notas y adjuntos: `docs/workflows/checklist-notes-attachments.md`
- Auditoría: `docs/auditing.md`

## Screenshots

Colocá capturas en `docs/screenshots/` y referencialas acá (ideal para portfolio):

- `docs/screenshots/dashboard.png`
- `docs/screenshots/orders.png`
- `docs/screenshots/order-status.png`
- `docs/screenshots/customers.png`

## Demo (credenciales seed)

En **DEV** (seed habilitado) se crean:

- **Admin**: `admin@local` / `Admin12345`
- **Tech**: `tech@local` / `Tech123456`

> La política de password mínima en backend requiere **>= 10 chars** y mezcla **letras + números**.

## API

- Base: `/api/v1/...`
- Swagger: solo en **Development** (o compose dev)
- Archivo de pruebas: `RepairShop.http`

## Quickstart (DEV)

### 1) Variables de entorno (dev)

Podés usar `.env.dev.local` (gitignored) o variables del sistema. Ejemplo mínimo:

```bash
JWT__KEY=dev_dev_dev_dev_dev_dev_dev_dev_dev_dev_32chars_min
CONNECTIONSTRINGS__REPAIRSHOPDB=Host=postgres;Port=5432;Database=repairshop;Username=postgres;Password=postgres
CORS_ALLOWED_ORIGIN_0=http://localhost:5173
ALLOWED_HOSTS=localhost;127.0.0.1
```

### 2) Levantar con Docker (dev)

```bash
docker compose --env-file .env.dev.local -f docker-compose.yml -f docker-compose.dev.yml up --build
```

- API: `http://localhost:8080`
- Health: `http://localhost:8080/healthz`
- Ready: `http://localhost:8080/readyz`

## Deploy (PROD)

En prod se fuerza:

- `Swagger__Enabled=false`
- `Seed__Enabled=false`

Y el app **se niega a arrancar** si intentás habilitarlos.

```bash
docker compose --env-file .env.prod.local -f docker-compose.yml -f docker-compose.prod.yml up --build -d
```

### Recomendado detrás de reverse proxy

- HTTPS termination en proxy (Caddy/Nginx/Traefik)
- `ForwardedHeaders` habilitado (ya soportado)
- HSTS si aplica (cuando estés 100% en HTTPS)

## Decisiones técnicas

- **Clean Architecture** (Domain/Application/Infrastructure/API).
- **EF Core + PostgreSQL** con migraciones aplicadas al arranque.
- **ProblemDetails** para errores consistentes.
- **Side-effects best-effort**: el cambio de estado persiste primero; preview/outbox no bloquean.
- **Idempotencia mínima** en endpoints críticos (cuando aplica) para evitar duplicados.

## Threat model mínimo

- **Bruteforce / credential stuffing** → rate limit + lockout en `/auth/login`.
- **JWT secret leakage** → secreto solo por env/secret manager (nunca en repo).
- **Token theft** → expiración razonable + logout/rotación en frontend; HTTPS obligatorio en prod.
- **CORS** → explícito por entorno (dev localhost; prod solo dominios reales).
- **Misconfig** (seed/swagger en prod) → hard-off y bloqueo de arranque.
- **Logs con PII** → logs estructurados; evitar loguear body sensible.

## Roadmap (vNext)

- Quote como entidad con estados: `Draft → Sent → Approved/Rejected`.
- OpenTelemetry (traces/metrics) exportable (OTLP) para prod.
