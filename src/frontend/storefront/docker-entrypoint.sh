#!/bin/sh
set -e

# The nginx resolver address is environment-specific: Docker Compose's
# embedded DNS is 127.0.0.11, Kubernetes' CoreDNS Service is a different
# ClusterIP per cluster. Reading it from /etc/resolv.conf at container start
# (rather than hardcoding one value) lets the same image proxy /api/ to the
# gateway correctly in both environments.
DNS_RESOLVER=$(awk '/^nameserver/{print $2; exit}' /etc/resolv.conf)
export DNS_RESOLVER

# nginx's resolver directive does its own minimal DNS client — unlike a
# normal client (curl, .NET's HttpClient, etc.) it does NOT apply
# /etc/resolv.conf's ndots/search-domain expansion, so querying the bare
# name "gateway" against Kubernetes' CoreDNS returns SERVFAIL. Build the
# fully-qualified in-cluster name from the search domain instead; Docker
# Compose has no "svc.cluster.local" search suffix, so the bare service
# name resolves there as before.
SEARCH_DOMAIN=$(awk '/^search/{print $2; exit}' /etc/resolv.conf)
case "$SEARCH_DOMAIN" in
  *svc.cluster.local*) GATEWAY_HOST="gateway.${SEARCH_DOMAIN}" ;;
  *) GATEWAY_HOST="gateway" ;;
esac
export GATEWAY_HOST

envsubst '${DNS_RESOLVER} ${GATEWAY_HOST}' < /etc/nginx/conf.d/default.conf.template > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'
