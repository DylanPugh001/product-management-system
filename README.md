# Product Management System

A full-stack product management workflow demo per the case-study PRD
(`product-management-system-prd.md`): Capturers create and submit products,
Managers approve/reject/soft-delete them, and approved records land in a
separate "data lake" read table.

- **Backend:** .NET 8 Web API + Entity Framework Core (Code First, SQLite) + ASP.NET Identity + JWT
- **Frontend:** Angular 22 (standalone components)
- **Diagrams:** [`docs/architecture.md`](docs/architecture.md), [`docs/erd.md`](docs/erd.md)

## Prerequisites

- .NET SDK 8
- Node.js 22+ (npm 11+)

## Run it (two terminals)

```bash
# Terminal 1 — API on http://localhost:5001
cd ProductManagementSystem.Api
dotnet run --launch-profile http

# Terminal 2 — SPA on http://localhost:4200 (proxies /api to :5001)
cd product-management-client
npm start
```

Open http://localhost:4200 and sign in.

## Demo accounts

Seeded automatically on startup (idempotent):

| Email                 | Password   | Role     |
| --------------------- | ---------- | -------- |
| capturer@demo.local   | `Demo123!` | Capturer |
| manager@demo.local    | `Demo123!` | Manager  |

> Local-only fixed credentials. There is no self-registration endpoint.

## Workflow walkthrough

1. **Capturer** signs in → **New Product** → product is created as **Draft**.
2. **Capturer** edits and submits → status becomes **Pending Approval**.
3. **Manager** signs in → sees **Approve** / **Reject** (and **Soft delete**) actions.
   - **Approve** → status **Approved** and a snapshot is upserted into the
     `ApprovedProductsCache` table (the data lake read path).
   - **Reject** → status returns to **Draft**, capturer can edit and resubmit.
   - A manager cannot approve their own change (403).
4. **Soft delete** also goes through approval: DELETE sets `PendingApproval` +
   `PendingDelete`; a manager's **Approve** moves it to **SoftDeleted** and removes
   it from the cache. Products are never hard-deleted.
5. Open **Approved (data lake)** — reads `GET /api/products/approved`
   (unauthenticated, from the cache table only).

## API surface

| Method | Route                         | Auth                     |
| ------ | ----------------------------- | ------------------------ |
| POST   | `/api/auth/login`             | anonymous                |
| GET    | `/api/products`               | JWT, role-aware list     |
| GET    | `/api/products/{id}`          | JWT, role/visibility     |
| POST   | `/api/products`               | Capturer                 |
| PUT    | `/api/products/{id}`          | Capturer (own product)   |
| POST   | `/api/products/{id}/approve`  | Manager                  |
| POST   | `/api/products/{id}/reject`   | Manager                  |
| DELETE | `/api/products/{id}`          | Manager (soft via approval) |
| GET    | `/api/products/approved`      | anonymous (data lake)    |
| GET    | `/api/health`                 | anonymous                |

## Database

SQLite file `ProductManagementSystem.Api/productmanagement.db`, created and
migrated automatically on startup. To start clean:

```bash
cd ProductManagementSystem.Api
rm -f productmanagement.db bin/Debug/net8.0/productmanagement.db
```

To add a new EF migration:

```bash
cd ProductManagementSystem.Api
dotnet tool restore
dotnet ef migrations add <Name>
```
