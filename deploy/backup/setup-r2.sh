#!/bin/sh
# Configura la copia diaria de los backups a Cloudflare R2, CIFRADA (rclone crypt). Se corre una sola vez,
# en el servidor, desde deploy/:
#   docker compose run --rm backup setup-r2.sh
# Pide los datos del token de R2 (no se guardan en ningún otro lado ni se muestran) y genera la clave de cifrado,
# que hay que guardar en el gestor de contraseñas: sin ella, las copias de R2 no se pueden leer.
set -eu
conf="${RCLONE_CONFIG:-/config/rclone/rclone.conf}"
if [ -s "$conf" ]; then
  echo "Ya existe $conf. Para reconfigurar, borralo primero (docker compose run --rm backup rm $conf)."
  exit 1
fi

printf 'Account ID de Cloudflare (R2 → Overview, a la derecha): '
read -r account
printf 'Access Key ID del token de R2: '
read -r key
printf 'Secret Access Key del token de R2 (no se ve al escribir): '
stty -echo 2>/dev/null || true
read -r secret
stty echo 2>/dev/null || true
echo
printf 'Nombre del bucket [repairshop-backups]: '
read -r bucket
bucket="${bucket:-repairshop-backups}"
[ -n "$account" ] && [ -n "$key" ] && [ -n "$secret" ] || { echo "Faltan datos."; exit 1; }

pass=$(head -c 48 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | cut -c1-40)
salt=$(head -c 48 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | cut -c1-40)

umask 077
mkdir -p "$(dirname "$conf")"
cat > "$conf" <<EOF
[r2]
type = s3
provider = Cloudflare
access_key_id = $key
secret_access_key = $secret
endpoint = https://$account.r2.cloudflarestorage.com
no_check_bucket = true

[r2crypt]
type = crypt
remote = r2:$bucket/repairshop
filename_encryption = standard
password = $(rclone obscure "$pass")
password2 = $(rclone obscure "$salt")
EOF

echo "Probando escritura cifrada en R2..."
if echo "prueba $(date -u +%FT%TZ)" | rclone rcat r2crypt:.prueba-setup && rclone deletefile r2crypt:.prueba-setup; then
  echo "OK: R2 responde y la copia cifrada funciona."
else
  rm -f "$conf"
  echo "No se pudo escribir en R2: revisá el Account ID, el token (permiso Object Read & Write sobre el bucket) y el nombre del bucket."
  exit 1
fi

echo
echo "================================================================================"
echo " GUARDÁ ESTAS DOS CLAVES EN TU GESTOR DE CONTRASEÑAS (no se vuelven a mostrar)."
echo " Sin ellas, los backups de R2 no se pueden descifrar si se pierde el servidor."
echo "   RepairShop R2 · password : $pass"
echo "   RepairShop R2 · password2: $salt"
echo "================================================================================"
echo
echo "Último paso: en deploy/.env poné RCLONE_REMOTE=r2crypt: y corré: docker compose up -d"
