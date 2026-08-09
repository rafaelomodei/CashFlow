# Cabeçalhos comuns aos dois encaminhamentos.
#
# `X-Correlation-Id` não aparece aqui porque não precisa: o nginx repassa os
# cabeçalhos da requisição original, e o que o browser envia chega à API.

proxy_http_version 1.1;
proxy_set_header Host $host;
proxy_set_header X-Real-IP $remote_addr;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;

# Uma API fora do ar deve virar erro na tela em segundos, não deixar a região
# carregando até o timeout padrão de 60 s.
proxy_connect_timeout 5s;
proxy_send_timeout 30s;
proxy_read_timeout 30s;
