# Product Management System — Architecture

```mermaid
flowchart TB
    subgraph Client["Angular SPA (localhost:4200)"]
        UI["Login / Product List / Form / Approve-Reject / Approved view"]
        AUTH["AuthService + JWT Interceptor + Route Guard"]
    end

    subgraph Api["ASP.NET Core Web API (localhost:5001)"]
        AUTHCTRL["AuthController\nPOST /api/auth/login"]
        PRODCTRL["ProductsController\nGET/POST/PUT/DELETE + approve/reject/approved"]
        SVC["JwtTokenService"]
    end

    subgraph Data["SQLite (Entity Framework Core)"]
        IDENTITY["AspNetUsers / AspNetRoles / AspNetUserRoles"]
        PRODUCTS["Products"]
        HISTORY["ProductApprovalHistory"]
        CACHE["ApprovedProductsCache (data lake read table)"]
    end

    UI --> AUTH
    AUTH --> AUTHCTRL
    UI --> PRODCTRL
    PRODCTRL --> SVC
    AUTHCTRL --> IDENTITY
    PRODCTRL --> PRODUCTS
    PRODCTRL --> HISTORY
    PRODCTRL -- "on approval" --> CACHE
    PRODCTRL -- "GET /api/products/approved (unauthenticated)" --> CACHE
```

## Key decisions

- **Workflow state machine** lives on `Product.Status` (`Draft → PendingApproval → Approved`, plus
  `SoftDeleted`). Pending delete requests are tracked with `PendingDelete` while the status stays
  `PendingApproval`; a manager approving a `PendingDelete` moves the record straight to `SoftDeleted`
  and removes its row from the cache.
- **Role enforcement is server-side.** `[Authorize(Roles=...)]` guards mutations, capturers can only
  edit products they created, and a manager cannot approve their own change (403).
- **Data lake read path** is the separate `ApprovedProductsCache` table, populated on approval and
  served by `GET /api/products/approved` with **no auth** — proving an independent fast-read table.
- **Authentication** uses ASP.NET Identity + JWT (OAuth 2.0/OIDC-compatible pattern), not a
  hand-rolled system.
