# Catalog.API

## Authentication

Catalog API uses ASP.NET Core Identity + JWT bearer authentication, implemented as the reusable
`Identity.Authentication` class library (`Identity.Authentication/`). Any future ASP.NET Core Web
API can reference the library, call `AddIdentityAuthentication(configuration)` and
`AddJwtAuthentication(configuration)`, and get user registration/login, password hashing, and JWT
issuance/validation with a `Jwt` configuration section:

```json
"Jwt": {
  "Key": "...",
  "Issuer": "...",
  "Audience": "...",
  "ExpirationMinutes": 60
}
```

Endpoints exposed by the library and mounted on Catalog API:

- `POST /api/auth/register` — creates a user (`{ email, password }`)
- `POST /api/auth/login` — returns `{ accessToken, expiresAtUtc }` on success

Identity tables (`AspNetUsers`, `AspNetRoles`, etc.) live in the same SQL Server database as the
catalog domain data, tracked under a separate EF Core migrations history table
(`__EFMigrationsHistory_Identity`) to avoid colliding with `CatalogContext`'s own migrations.
