# Deploy en un VPS con proxy propio

Stack de producción de esta versión de RepairShop para un servidor que **ya tiene un reverse proxy con HTTPS** en los puertos 80/443 (por ejemplo Caddy en el host con certificado de origen de Cloudflare).

| Servicio | Qué hace | Puerto |
| --- | --- | --- |
| `web` | Caddy: sirve la interfaz y pasa `/api`, `/healthz` y `/readyz` a la API | `127.0.0.1:${WEB_PORT}` (8086 por defecto) |
| `api` | .NET 8; aplica las migraciones al arrancar | solo red interna |
| `db` | PostgreSQL 16 propio de este stack | solo red interna |
| `backup` | copia diaria de la base y de las fotos, verificada, con retención | — |

El proyecto de Compose se llama `repairshop-taller`: sus contenedores y volúmenes no se mezclan con otros stacks del servidor (por ejemplo, el proyecto `repairshop` de v2).

Requisitos del servidor: Docker con Compose v2, ~1,5 GB de RAM libres para el build y ~3 GB de disco.

## 1. Primera instalación

Conviene levantarlo primero en un subdominio de prueba (en estos pasos, `nuevo.techxto.ar`) sin tocar lo que ya funciona. El cambio al dominio definitivo está en el paso 4.

### DNS y proxy (con sudo)

1. En Cloudflare: registro `A` `nuevo` → IP del servidor, con proxy (nube naranja).
2. En `/etc/caddy/Caddyfile` del host, un bloque como el que ya usan los otros sitios:

   ```caddyfile
   nuevo.techxto.ar {
   	import cf_tls
   	reverse_proxy 127.0.0.1:8086
   }
   ```

   Si el certificado de origen de Cloudflare es el comodín `*.techxto.ar` (el que Cloudflare genera por defecto), sirve tal cual.
3. `sudo cp /etc/caddy/Caddyfile /etc/caddy/Caddyfile.bak_$(date +%F_%H%M) && sudo caddy validate --config /etc/caddy/Caddyfile && sudo systemctl reload caddy`

### Código y configuración (usuario deploy)

```bash
# Clonar esta rama en una carpeta propia, con la misma URL (y credenciales) que el clon existente
git clone -b claude/hopeful-rubin-n1cl2m "$(git -C ~/repair-shop-management-system remote get-url origin)" ~/repairshop-taller
cd ~/repairshop-taller/deploy

cp .env.example .env
sed -i "s|^DB_PASSWORD=.*|DB_PASSWORD=$(openssl rand -hex 24)|" .env
sed -i "s|^JWT_KEY=.*|JWT_KEY=$(openssl rand -base64 48 | tr -d '\n')|" .env
sed -i "s|^MASTER_KEY=.*|MASTER_KEY=$(openssl rand -base64 32)|" .env
nano .env   # revisar DOMAIN y WEB_PORT
```

**Guardá `MASTER_KEY` también en tu gestor de contraseñas.** Cifra las credenciales de Mercado Pago y los certificados ARCA que se cargan en la app: si se pierde, una copia de la base restaurada no puede descifrarlos.

### Levantar

```bash
docker compose up -d --build        # el primer build tarda unos minutos
docker compose ps                   # db, api y web deben quedar "healthy"
curl -s -H "Host: nuevo.techxto.ar" http://127.0.0.1:8086/readyz
```

### Crear el primer administrador

```bash
docker compose exec api dotnet RepairShop.Api.dll admin create tu@email.com "Tu nombre" "Nombre del taller"
```

Imprime un link de un solo uso (vence en 7 días): abrilo, elegí la contraseña y ya entrás. La contraseña nunca pasa por la terminal ni queda en el servidor. Las demás personas se invitan desde la app (Configuración → Usuarios), que genera el link para mandarlo por WhatsApp o email.

El mismo comando sirve para **recuperar el acceso**: con un email que ya existe genera un link nuevo para cambiar la contraseña (y reactiva el usuario si estaba desactivado).

> En producción no existen los usuarios de demo (`admin@local` y compañía): solo se crean en modo Development.

## 2. Actualizar

```bash
cd ~/repairshop-taller && git pull
cd deploy && docker compose up -d --build
```

Las migraciones de la base se aplican solas al arrancar la API. Para actualizar solo la interfaz: `docker compose up -d --build web`.

## 3. Chequeos y operación

```bash
docker compose ps
curl -s -H "Host: nuevo.techxto.ar" http://127.0.0.1:8086/readyz
docker compose logs --tail 80 api
docker compose exec backup backup.sh        # copia manual ahora
docker compose exec backup ls -lh /backups
```

Restaurar una copia (pisa la base actual):

```bash
docker compose stop api
docker compose exec backup restore.sh /backups/db-AAAAMMDD-HHMMSS.dump
docker compose start api
```

Copia fuera del servidor: configurá un remoto en `backup/rclone.conf` (hay un ejemplo para Cloudflare R2) y poné `RCLONE_REMOTE` en `.env`. Hasta entonces, las copias viven en el mismo servidor.

## 4. Pasar al dominio definitivo

Cuando lo hayas probado en el subdominio:

1. En `.env`: `DOMAIN=app.techxto.ar` y `docker compose up -d` (la API usa el dominio para los links de seguimiento, las invitaciones, CORS y los hosts permitidos).
2. En el Caddy del host, que `app.techxto.ar` apunte a `127.0.0.1:8086` en vez del stack anterior; validar y recargar como en el paso 1.
3. Cuando confirmes que anda, frenar el stack anterior (sus volúmenes quedan intactos por si hay que volver):

   ```bash
   cd ~/repair-shop-management-system/deploy && docker compose -f compose.yml -f compose.behind-proxy.yml down
   ```

Los datos cargados en otra versión no pasan a esta: son bases con estructuras distintas.

## Integraciones

- **Mercado Pago y ARCA**: se configuran desde la app, por sucursal (Configuración → Integraciones). La URL del webhook de Mercado Pago aparece ahí mismo.
- **Email**: sin SMTP la app funciona igual (los links de invitación y de contraseña se copian desde la app). Para que salgan solos los avisos y links: `EMAIL_PROVIDER=Smtp` y los `SMTP_*` en `.env`.
- **WhatsApp automático**: `WHATSAPP_PROVIDER=Twilio` o `Meta` con sus credenciales. Sin esto, la app abre WhatsApp con el mensaje ya escrito.
- **Sugerencias con IA**: `AI_PROVIDER=Anthropic` y `ANTHROPIC_API_KEY`.

Después de cambiar `.env`: `docker compose up -d`.

## Suscripciones (cobro a los talleres)

Cualquier taller puede crear su cuenta en `/registro` y usar todo gratis durante `TRIAL_DAYS` días, sin tarjeta. Al terminar la prueba, la cuenta queda en modo consulta hasta que elija un plan en **Suscripción**. Los precios públicos están en `/precios`.

- **Argentina** paga en pesos con **Mercado Pago** (suscripción mensual). Usa tu cuenta de Mercado Pago, no la de un taller.
  1. En https://www.mercadopago.com.ar/developers → Tus integraciones → tu aplicación → Credenciales de producción: copiá el *Access token* a `MP_BILLING_ACCESS_TOKEN`.
  2. En Webhooks → Configurar notificaciones: URL `https://app.techxto.ar/api/v1/billing/webhooks/mercadopago`, eventos **Planes y suscripciones**. Copiá la clave secreta a `MP_BILLING_WEBHOOK_SECRET`.
- **Resto del mundo** paga en dólares con **Paddle**, que cobra como revendedor (merchant of record): agrega los impuestos de cada país y te liquida el neto. Vos le emitís una factura E por cada liquidación.
  1. Creá la cuenta en https://www.paddle.com y completá la verificación del negocio (empezá en sandbox: https://sandbox-vendors.paddle.com).
  2. Catálogo → Productos: un producto "RepairShop" con tres precios mensuales en USD (Básico, Estándar, Profesional). Copiá los ids `pri_...` a `PADDLE_PRICE_BASIC`, `PADDLE_PRICE_STANDARD` y `PADDLE_PRICE_PRO`, y que los montos coincidan con `PRICE_USD_*`.
  3. Checkout → Configuración: *Default payment link* = `https://app.techxto.ar/billing` (y aprobá el dominio `app.techxto.ar`).
  4. Developer tools → Authentication: una *API key* (`PADDLE_API_KEY`) y un *client-side token* (`PADDLE_CLIENT_TOKEN`).
  5. Developer tools → Notifications: destino `https://app.techxto.ar/api/v1/billing/webhooks/paddle` con los eventos `subscription.*`. Copiá la *secret key* a `PADDLE_WEBHOOK_SECRET`.
  6. Cuando pruebes un pago en sandbox y veas el plan activo, cambiá a las credenciales de producción y `PADDLE_ENVIRONMENT=production`.
- Antes de pedir la aprobación de Paddle, completá `CONTACT_EMAIL` (email de soporte) y `LEGAL_NAME` (tu nombre o el de tu empresa): aparecen en Términos, Privacidad y en la política de reembolsos (`/reembolsos`), que Paddle revisa. Después de cambiarlos, `docker compose up -d` (los usan la API y el contenedor `web`).
- Con un medio de cobro listo, abrí el alta: `SIGNUP_ENABLED=true` y `docker compose up -d`.

Los talleres que ya existían antes de esta versión tienen el plan Profesional bonificado. Para bonificar otro:

```bash
docker compose exec api dotnet RepairShop.Api.dll admin plan <email> Pro
```

## Notas de seguridad

- Solo el contenedor `web` publica un puerto, y solo en `127.0.0.1`: desde internet se llega únicamente a través del proxy con HTTPS.
- La IP real del cliente llega a la API a través de los dos proxies (los límites de intentos de login son por IP); una IP falsa agregada por el cliente se descarta. Para que sea la IP del visitante y no la de Cloudflare, el Caddy del host tiene que confiar en los rangos de Cloudflare (`trusted_proxies` con la lista de https://www.cloudflare.com/ips/); si no, la API ve la IP del nodo de Cloudflare, que sigue siendo distinta entre visitantes.
- La cookie de sesión es `Secure`, `HttpOnly` y `SameSite=Strict`, limitada a `/api/v1/auth`.
