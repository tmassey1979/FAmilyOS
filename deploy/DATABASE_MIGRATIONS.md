# FamilyOS database migrations

The API applies pending EF Core migrations before it begins serving requests.

## Production behavior

- `Database__MigrateOnStartup` controls migration execution and defaults to `true` in the production Compose stack.
- The API checks PostgreSQL connectivity and reads EF migration history before applying anything.
- If the database is empty, the initial migration can create the schema.
- If migration history is empty but any application table already exists in the `familyos` schema, startup stops. The API will not guess that an existing database matches the initial migration.
- If migration history exists, pending migrations are applied in order.
- Migration errors and schema-ensure errors fail API startup. The container health check therefore cannot report a healthy API with a known startup schema failure.

## Existing database with no migration history

Do not simply enable migration execution against an existing database with an empty `__EFMigrationsHistory` table.

1. Take and verify a PostgreSQL backup.
2. Inspect the actual schema, including columns, constraints, indexes, and data types.
3. Compare it against the expanded EF migration and model snapshot produced by `scripts/expand-migrations.py`.
4. Only after equivalence is established, perform an intentional baseline operation for the initial migration.
5. Restart the API and verify that the migration history is populated and the readiness endpoint is healthy.

Do not manually insert a migration-history row without verifying the full schema. A false baseline can make later migrations unsafe.

## Configuration

To require migrations to be applied by a separate operator-controlled process, set:

```yaml
Database__MigrateOnStartup: "false"
```

With pending migrations, the API will refuse to start and report that they must be applied first. The default production stack setting is enabled.
