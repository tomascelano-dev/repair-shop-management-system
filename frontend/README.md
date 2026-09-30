# RepairShop Frontend (React)

Frontend en **React + TypeScript + Vite** alineado al backend **RepairShop.Api** (controllers: Auth/Orders/Customers/Devices/Templates/Inventory/Dashboard).

## Requisitos
- Node.js 18+ (ideal 20+)
- Backend corriendo en local

## Cómo correrlo (DEV)

### 1) Levantar el backend
Desde la raíz de tu solución:
```bash
cd RepairShop/src/RepairShop.Api
# o desde donde tengas la solución

dotnet run
```
Por defecto (docker-compose del backend) suele quedar en:
- `http://localhost:8080`
- Swagger: `http://localhost:8080/swagger`

Usuarios seed (Development):
- `admin@local` / `admin1234` (role Admin)
- `tech@local` / `tech1234` (role Tech)

### 2) Levantar el frontend
```bash
cd repairshop-frontend
npm install
npm run dev
```
Vite levanta en `http://localhost:5173`.

> En DEV, Vite hace proxy de `/api` hacia `VITE_PROXY_TARGET` (default `http://localhost:8080`, ver `vite.config.ts`).

## Config (Producción)
Si vas a servir el frontend en otro dominio/origen, podés setear:
- `VITE_API_BASE=https://tu-dominio.com/api`

Ejemplo `.env`:
```env
VITE_API_BASE=https://example.com/api
```

## Qué cubre
- Login (JWT) con guard de rutas
- React Query (cache + invalidación)
- Dashboard (summary)
- CRUD: Clientes, Equipos, Órdenes
- Órdenes PRO:
  - Cambiar status (con suggested message)
  - Historial
  - Notas y adjuntos
  - Cotización (quote)
  - Pagos
  - Checklist recepción
  - Auditoría
  - Preview de mensaje por templateKey
- Inventario:
  - Listado + alta/edición
  - Ajustes
  - Consumo de partes en orden (desde detalle)
- Plantillas:
  - Listado + alta/edición

## Notas importantes
- Algunos endpoints están protegidos por rol `Admin` (creación/edición de inventory/templates, delete, etc.).
- Cuando el backend expone enums como números en requests, el frontend envía valores numéricos.

