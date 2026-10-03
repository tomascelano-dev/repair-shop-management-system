# RepairShop Web (frontend)

Panel web de **RepairShop** para talleres de reparación: recepción de equipos, órdenes, presupuestos, stock, punto de venta, caja, facturación y reportes. Incluye el **portal público** donde el cliente sigue su reparación.

Stack: React 18 + TypeScript (strict) + Vite 5 + Tailwind CSS 3 + TanStack Query 5 + React Router 6. Tests con Vitest + Testing Library y Playwright.

## Módulos

| Sección | Qué hace |
| --- | --- |
| **Inicio** | Indicadores del día: abiertas, listas para retirar, atrasadas, estancadas, presupuestos sin respuesta, stock bajo, cobrado hoy/mes, carga por técnico e ingresos de 30 días. Vista consolidada por sucursal. |
| **Órdenes** | Listado con filtros en la URL (estado, técnico, “mis órdenes”, atrasadas, prioridad, fechas) y paginación real. **Tablero Kanban** con arrastrar y soltar. |
| **Nueva orden** | Recepción en pasos: cliente (con detección de duplicados por teléfono) → equipo (validación de IMEI) → falla, checklist de ingreso, código de desbloqueo cifrado y firma del cliente. Imprime comprobante y etiqueta con QR. |
| **Detalle de orden** | Cambios de estado con reglas (presupuesto aprobado, control de calidad, saldo), plan de trabajo, presupuestos versionados con aprobación/rechazo, repuestos y reservas, pagos/señas/devoluciones, links de Mercado Pago, factura ARCA, checklist de recepción y QA de salida, notas (internas o visibles para el cliente), fotos, mensajes (plantillas, WhatsApp, email), historial, garantía y sugerencias (reparaciones parecidas + IA opcional). |
| **Clientes** | Ficha 360 (equipos, órdenes, compras, saldo, satisfacción), unificar duplicados, importar/exportar Excel o CSV. |
| **Punto de venta** | Lector de códigos USB (escaneo + Enter) o cámara, catálogo por categorías, ítems libres, descuentos por línea y globales ($ o %), cliente opcional, **pagos divididos** con cálculo de vuelto, ventas en espera, atajos (F2 buscar, F4 cobrar, F8 en espera) y ticket imprimible. |
| **Ventas** | Historial con devoluciones parciales (con o sin reingreso a stock), anulación (admin), ticket y factura. |
| **Caja** | Apertura con cambio inicial, ingresos/gastos/retiros, resumen por medio de pago, **arqueo con contador de billetes**, cierre con diferencias y reporte PDF. |
| **Facturas** | Comprobantes ARCA (PDF, nota de crédito) y cotizaciones USD→ARS usadas en los reportes. |
| **Inventario** | Stock disponible vs. reservado, stock bajo, ajustes y conteo físico, historial de movimientos, modelos compatibles, importación/exportación. |
| **Compras** | Proveedores y órdenes de compra con recepción parcial (actualiza stock y costo). |
| **Transferencias** | Envío de stock entre sucursales con confirmación de recepción. |
| **Reportes** | Ingresos, márgenes, tiempos por estado (cuello de botella), presupuestos, fallas y modelos, técnicos, garantías, satisfacción y stock valorizado. Exportación a Excel. |
| **Mensajes** | Bandeja de avisos automáticos: enviados, pendientes y con error (reintentar / cancelar). |
| **Configuración** | Negocio y logo, reglas de operación, plantillas de mensajes con vista previa, Mercado Pago, ARCA, usuarios (invitaciones por link, roles, acceso por sucursal), sucursales y auditoría. |
| **Portal del cliente** (`/t/:token`) | Estado y avance, novedades, presupuesto para aprobar/rechazar, pago online, certificado de garantía, encuesta de satisfacción y contacto por WhatsApp. No requiere usuario. |

Roles: **Administrador**, **Técnico**, **Recepción** y **Caja**. El menú y las acciones se adaptan a los permisos que devuelve la API; la API vuelve a validar todo.

## Desarrollo

Requisitos: Node 20+ y la API corriendo (ver `backend/README.md`).

```bash
cp .env.example .env.local   # VITE_PROXY_TARGET apunta a la API (por defecto http://localhost:8080)
npm ci
npm run dev                  # http://localhost:5173
```

En desarrollo, Vite hace de proxy de `/api` hacia la API, así la cookie de sesión queda en el mismo origen. Con la API en modo Development y `Seed__Enabled=true` se crean usuarios de prueba (se muestran en la pantalla de ingreso):

| Rol | Email | Contraseña |
| --- | --- | --- |
| Administrador | `admin@local` | `Admin123456` |
| Técnico | `tech@local` | `Tech123456` |
| Recepción | `recepcion@local` | `Recepcion123` |
| Caja | `caja@local` | `Caja123456` |

## Scripts

| Comando | Qué hace |
| --- | --- |
| `npm run dev` | Servidor de desarrollo |
| `npm run build` | Chequeo de tipos + build de producción (`dist/`, con code-splitting por sección) |
| `npm run typecheck` | Solo TypeScript |
| `npm run lint` | ESLint |
| `npm test` | Tests unitarios y de componentes (Vitest + Testing Library) |
| `npm run test:e2e` | Tests end-to-end (Playwright) contra una API real |

### Tests end-to-end

Necesitan la API con una base de datos de prueba y el seed de desarrollo (`Seed__Enabled=true`, `Seed__DemoData=true`):

```bash
# en otra terminal: la API escuchando en http://localhost:8080
npx playwright install chromium        # una sola vez
VITE_PROXY_TARGET=http://localhost:8080 npm run test:e2e
```

Variables útiles: `E2E_BASE_URL` (usar un frontend ya levantado), `PW_CHROMIUM_EXECUTABLE` (usar un Chromium instalado), `E2E_SCREENSHOTS=/carpeta` (capturas de todas las pantallas).

## Sesión y seguridad

- El **access token** (JWT de 15 minutos) vive solo en memoria; nunca en `localStorage`.
- La sesión se mantiene con un **refresh token rotativo en cookie httpOnly** (`SameSite=Strict`, path `/api/v1/auth`), más un header anti-CSRF. Si una petición devuelve 401, se renueva una sola vez y se reintenta.
- “Cerrar sesión en todos lados” (Mi cuenta) revoca todas las sesiones del usuario.
- Los cobros y altas sensibles envían `Idempotency-Key`: un doble clic o un reintento por mala conexión no duplica pagos ni ventas.

## Producción

```bash
npm ci && npm run build   # sirve dist/ como sitio estático (SPA)
```

- Recomendado: servir el frontend y la API bajo el **mismo sitio** (reverse proxy de `/api` → API) y dejar `VITE_API_BASE` sin definir.
- Si la API está en otro origen: `VITE_API_BASE=https://api.tu-dominio.com/api/v1` y en la API `Cors__AllowedOrigins__0=https://app.tu-dominio.com`. Si además son **sitios distintos** (no subdominios), `Auth__RefreshCookie__SameSite=None` (requiere HTTPS).
- En la API, `App__FrontendBaseUrl` debe ser la URL pública de este frontend (se usa en los links de seguimiento, invitaciones y QR).
- `public/_headers` y `public/_redirects` sirven para Cloudflare Pages / Netlify (headers de seguridad, cache de assets y fallback de SPA). La cámara queda habilitada solo para el propio sitio (escaneo de códigos).
- Es una **PWA** instalable (manifest + service worker que solo cachea la app; los datos de la API nunca se cachean).
