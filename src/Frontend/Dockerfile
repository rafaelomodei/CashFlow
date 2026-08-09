# Build a partir da raiz do repositório: `docker build -f src/Frontend/Dockerfile .`
# Multi-stage e usuário não-root, conforme ADR-009.

FROM node:24-alpine AS build
WORKDIR /app

# O manifesto vem antes do código: a camada de instalação só é invalidada
# quando uma dependência muda, e não a cada alteração de componente.
COPY src/Frontend/package.json src/Frontend/package-lock.json ./
RUN npm ci

COPY src/Frontend/ ./
RUN npm run build

FROM nginx:1.29-alpine AS final

COPY src/Frontend/nginx.conf        /etc/nginx/conf.d/default.conf
COPY src/Frontend/proxy-common.inc  /etc/nginx/conf.d/proxy-common.inc
COPY --from=build /app/dist /usr/share/nginx/html

# A imagem oficial já traz um usuário `nginx`; o que falta é permitir que ele
# escreva o pid e os arquivos temporários fora de /run, que pertence ao root.
# O caminho do pid muda entre versões da imagem, então a linha é reescrita
# inteira em vez de casada por valor — um `sed` que não casa falha em silêncio
# e só aparece como container reiniciando.
RUN sed -i -e 's|^pid .*|pid /tmp/nginx.pid;|' -e '/^user /d' /etc/nginx/nginx.conf \
    && grep -q '^pid /tmp/nginx.pid;$' /etc/nginx/nginx.conf \
    && chown -R nginx:nginx /usr/share/nginx/html /var/cache/nginx

USER nginx
EXPOSE 8080

# O busybox responde pelo healthcheck; a imagem não traz curl.
#
# 127.0.0.1 e não `localhost`: no Alpine o nome resolve para ::1 primeiro, e o
# nginx escuta apenas em IPv4 — o container ficava `unhealthy` servindo
# normalmente pela porta publicada.
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q --spider http://127.0.0.1:8080/healthz || exit 1
