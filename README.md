# RepairShop v2 · instalación local

**Abrir:** http://127.0.0.1:5182

Esta entrega implementa el circuito principal de v2 sobre una copia independiente de RepairShop. La base es `RepairShop_productized_C1_E.zip`, que contiene más funciones que la copia antigua descomprimida. Los proyectos originales no se modificaron.

## Acceso

| Perfil | Usuario | Contraseña de demostración |
| --- | --- | --- |
| Administrador | admin@local | Admin12345 |
| Técnico | tech@local | Tech123456 |
| Taller B, pruebas de aislamiento | beta@local | DemoBeta12345 |

Las 8 órdenes precargadas están identificadas como DEMO. Son ficticias. Los registros y cambios que realices se guardan en PostgreSQL; no se reinician al volver a abrir.

## Arrancar y detener

Requisitos: Docker Desktop iniciado, .NET SDK 8 o superior compatible, Node.js 24 y npm.

Desde esta carpeta, en PowerShell:

```powershell
.\Start-Local.ps1
# Con compilación ya realizada:
.\Start-Local.ps1 -SkipBuild
# Detener sin borrar datos:
.\Stop-Local.ps1
```

El inicio deja dos procesos ocultos en segundo plano; cerrar la terminal no los detiene. No configura el arranque automático de Windows. Después de reiniciar la PC, ejecutá el script de inicio.

- Interfaz: `127.0.0.1:5182`, React/TypeScript y Vite.
- API: `127.0.0.1:5282`, ASP.NET Core 8. Salud: `/healthz` y `/readyz`.
- PostgreSQL 16: `127.0.0.1:5482`, contenedor `repairshop-v2-local-db`, volumen persistente `repairshop_v2_local_repairshop_v2_local_data`.
- Registros del servicio: `logs/api.log`, `logs/api.error.log`, `logs/frontend.log` y `logs/frontend.error.log`.
- Fotos: `RepairShop/src/RepairShop.Api/data/photos`.
- Secretos locales: `.env.local` y `RepairShop/src/RepairShop.Api/appsettings.Development.Local.json`; están excluidos de Git. El script selecciona explícitamente la conexión local para evitar variables previas de otros proyectos. En un clon nuevo, crealos copiando `.env.local.example` y `RepairShop/src/RepairShop.Api/appsettings.Development.Local.example.json` (quitando `.example`); las contraseñas de ambos deben coincidir.

Todos los puertos están limitados a esta PC. El enlace del portal funciona aquí; todavía no es un enlace público para enviar al teléfono de un cliente.

## Qué se puede probar

1. **Nueva recepción:** cliente nuevo o existente, equipo, identificador, falla, condición, accesorios, prioridad y checklist.
2. **Orden:** ficha con datos de ingreso, fotos internas, historial y comprobante imprimible (también se puede guardar como PDF desde el navegador).
3. **Diagnóstico:** observaciones, costo de mano de obra y seis pruebas de control de calidad.
4. **Presupuesto:** conceptos, cantidades, moneda ARS/USD, costo interno, margen estimado, vigencia, términos y garantía expresada en días. Las modificaciones se publican como versiones nuevas.
5. **Portal:** desde Resumen → Crear enlace → Abrir como cliente. El cliente ve estado y propuesta, y aprueba o rechaza con nombre y fecha. La nueva versión vuelve a requerir aprobación. Los costos internos no se publican.
6. **Operación:** avanzar a reparación, espera de repuesto, control de calidad y listo para retirar. La API verifica la aprobación vigente y las pruebas finales.
7. **Cobros:** seña o pago completo; moneda alineada con el presupuesto y validación del saldo. Devoluciones con motivo, sólo administrador.
8. **Entrega:** registrar quién retira. Si queda deuda, sólo un administrador puede entregarlo con justificación.
9. **Clientes e inventario:** alta/edición de clientes; repuestos, movimientos, lotes, reservas e historial desde Stock avanzado.
10. **Tablero:** métricas, búsqueda, filtros y vistas lista/kanban.

Para un recorrido rápido abrí el iPhone 13 de Lucía (DEMO): ya tiene una propuesta pendiente. Generá su portal, aprobala, volvé a la orden y seguí el circuito. La ficha se actualiza cada 15 segundos mientras estás en Resumen, Cobros o Historial; los formularios de diagnóstico y presupuesto no se refrescan automáticamente para preservar lo escrito.

## Alcance de esta versión

Incluye el núcleo operativo y los seis módulos premium detallados en [la guía de prueba](docs/modulos-premium.md). WhatsApp y los bots de mensajería quedan excluidos por decisión del usuario. Quedan fuera de esta entrega: IA/Ollama, firma manuscrita, diagnóstico por modelo, facturación fiscal, cobro automático de suscripciones y despliegue público del SaaS.

Los costos y checklists se registran manualmente; los consumos de repuestos y los tiempos calculan su costo automáticamente. El identificador se ingresa a mano o con un lector que funcione como teclado. La aprobación registra nombre/fecha/versión; no implementa firma digital certificada. El comprobante de orden no es una factura fiscal. Las escrituras del inventario anterior están bloqueadas para evitar eludir lotes y reservas. Las operaciones premium usan transacciones e idempotencia y se serializan por taller; las modificaciones de órdenes se coordinan mediante bloqueo de fila. El tablero original carga hasta 1000 órdenes y el catálogo de clientes hasta 200; los módulos nuevos todavía requieren paginación para grandes volúmenes.

Los archivos anteriores del frontend permanecen como referencia; el punto de entrada y la compilación de esta entrega utilizan `src/v2`.

## Verificación

```powershell
dotnet test RepairShop/RepairShop.sln
node tests/workflow-smoke.mjs
node tests/premium-smoke.mjs
cd frontend
npm run build
npm audit
```

El circuito original tiene 16 pruebas .NET y 56 verificaciones HTTP. El circuito premium prueba importación de archivos CSV/XLSX/PDF reales, aislamiento entre talleres, concurrencia de reservas, idempotencia, consumos, transferencias, moneda, cálculo de costos, garantías, reventa con control de calidad y contratos con liquidación mensual. Los scripts crean sus registros únicamente en el taller B. Las fuentes están incluidas localmente y la aplicación no llama a servicios de IA.

## Módulos premium

Abrir directamente: http://127.0.0.1:5182/premium

- Compras y precios: importación con revisión, comparación por equivalencia declarada, recepción y actualización del costo de referencia.
- Stock avanzado: lotes/series, sucursales, mínimos, reservas, consumo y transferencias.
- Rentabilidad: costos históricos o estimados, tiempos, comisiones, devoluciones y garantías, separados por moneda.
- Garantías y calidad: casos vinculados a reparaciones entregadas, repuestos instalados, reclamos y recuperos de proveedor.
- Reacondicionados y canjes: adquisición, costos, grado, calidad, venta e historial por IMEI/serie.
- Empresas y sucursales: contratos, parque de equipos, recepción por lote, plazos, liquidaciones y resultados por local.

La migración `PremiumModules` conserva las órdenes y convierte el stock previo en lotes de apertura sin volver a sumarlo. El respaldo previo está en `backups/before-premium.dump` (PostgreSQL custom format). No lo restaures sobre los cambios nuevos salvo que quieras volver explícitamente al estado anterior.
