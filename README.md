# Family OS

**Make the next meaningful action obvious.**

Household management app: ASP.NET Core API + React Native (Expo) mobile, Postgres, RabbitMQ, Keycloak.

## Quick start (local Docker)

```bash
git clone https://github.com/tmassey1979/FamilyOS.git
cd FamilyOS
chmod +x scripts/dev-up.sh
./scripts/dev-up.sh
```

Or: `docker compose up --build -d`

| Service | URL |
|---------|-----|
| API / Swagger | http://localhost:5080/swagger |
| Health | http://localhost:5080/health |
| Keycloak | http://localhost:8080 (admin / admin) |
| RabbitMQ | http://localhost:15672 |
| Postgres | localhost:5432 |

Dev auth: send header `X-Dev-User: terry.owner` (or `michelle.adult`, `mia.teen`, `eli.child`).
