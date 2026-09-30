# Despliegue en producción

Esta carpeta publica RepairShop como SaaS en un servidor propio. Necesitás un VPS con Linux y Docker, y un dominio.

El stack tiene cuatro servicios:

| Servicio | Qué hace |
| --- | --- |
| `web` | Caddy: HTTPS automático (Let's Encrypt), la app React y el proxy de `/api`. |
| `api` | ASP.NET Core 8. Aplica las migraciones al arrancar. |
| `db` | PostgreSQL 16 con volumen persistente. |
| `backup` | Copia diaria de la base y de `/app/data` (fotos y claves), verificada y con retención. |

## 1. Servidor y dominio

1. Contratá un VPS (2 vCPU y 4 GB de RAM alcanzan para empezar) con Ubuntu 24.04 e instalá Docker ([docs.docker.com/engine/install](https://docs.docker.com/engine/install/ubuntu/)).
2. Creá un registro DNS `A` (por ejemplo `app.tutaller.com.ar`) que apunte a la IP del servidor.
3. Abrí los puertos 80 y 443.

## 2. Configuración

```bash
git clone https://github.com/tomascelano-dev/repair-shop-management-system.git
cd repair-shop-management-system/deploy
cp .env.example .env
openssl rand -base64 48   # usalo para DB_PASSWORD
openssl rand -base64 48   # usalo para JWT_KEY
nano .env
docker compose up -d --build
```

Cuando termina, `https://TU_DOMINIO` muestra el login. El alta de talleres está en `https://TU_DOMINIO/signup`.

## 3. Mercado Pago (cobro de la suscripción)

1. En [Tus integraciones](https://www.mercadopago.com.ar/developers/panel/app) creá una aplicación y copiá el **Access Token de producción** en `MP_ACCESS_TOKEN`.
2. En **Webhooks**, configurá la URL `https://TU_DOMINIO/api/saas/billing/webhook`, activá el evento **Planes y suscripciones** y copiá la clave secreta en `MP_WEBHOOK_SECRET`.
3. `docker compose up -d` para aplicar los cambios.

Cada taller elige su plan en **Configuración → Plan**. Mercado Pago cobra el abono mensual y el webhook actualiza el estado. Si un cobro falla, el taller conserva el acceso completo durante 7 días. Después queda en modo solo lectura hasta que regularice el pago.

## 4. ARCA (factura electrónica)

Con `FISCAL_PROVIDER=Arca`, cada taller carga su propio certificado en **Configuración → Facturación ARCA**:

1. Generar la clave y el pedido de certificado (CSR):
   `openssl genrsa -out taller.key 2048` y `openssl req -new -key taller.key -subj "/C=AR/O=RAZON SOCIAL/CN=repairshop/serialNumber=CUIT 20XXXXXXXXX" -out taller.csr`
2. En ARCA, entrar con clave fiscal a **Administración de certificados digitales** (homologación: **WSASS**), subir el CSR y descargar el `.crt`.
3. En **Administrador de relaciones**, delegar el servicio **Facturación electrónica (wsfe)** al certificado.
4. Crear un punto de venta **Web Services** en **Puntos de venta y domicilios**.
5. En la app: subir el `.crt` y el `.key`, indicar el punto de venta, probar la conexión y activar.

Recomendación: probá primero en **Homologación**. Los certificados se guardan cifrados con las claves de `/app/data/keys`; si perdés ese volumen, hay que volver a subirlos.

## 5. Email y SMS

- Email: cualquier SMTP (Amazon SES, Brevo, Mailgun…). Configurá SPF y DKIM del dominio de `SMTP_FROM` para no caer en spam.
- SMS: API compatible con Twilio. Si queda vacío, solo se envían emails.

## 6. Copias de seguridad

- Todos los días a `BACKUP_HOUR_UTC` se generan `db-*.dump` y `data-*.tar.gz` en el volumen `backups`. Se verifican y se conservan `BACKUP_KEEP_DAYS` días.
- Copia fuera del servidor: completá `backup/rclone.conf` (por ejemplo con Backblaze B2 o S3) y `RCLONE_REMOTE`.
- Backup manual: `docker compose exec backup backup.sh`
- Restaurar:
  ```bash
  docker compose stop api
  docker compose exec backup ls /backups
  docker compose exec backup restore.sh /backups/db-AAAAMMDD-HHMMSS.dump
  docker compose start api
  ```

## 7. Monitoreo y actualizaciones

- `https://TU_DOMINIO/healthz` indica si la API responde; `https://TU_DOMINIO/readyz` incluye la base de datos. Conectalos a un monitor externo (UptimeRobot, Better Stack, Uptime Kuma).
- Logs: `docker compose logs -f api`.
- Actualizar: `git pull && docker compose up -d --build`. Las migraciones se aplican solas al iniciar.
