# Product Management System — PRD

Source: MOYO Software Development Case Study v1.3

## 1. Scope

Build the Product Management System only. Client Portal and Order Management System are out of scope. They do not exist in this build. Do not build queues, message brokers, or callers for them.

## 2. Users and Roles

Two roles, both authenticated via the same login.

**Capturer**
- Create Product
- Read Product
- Update Product (edits go to PendingApproval, do not overwrite the live approved record)

**Manager**
- Read Product
- Approve Product (accepts a pending change, updates the live record)
- Reject Product (sends a pending change back to Draft)
- SoftDelete Product (also requires approval)

## 3. Entity: Product

| Field | Type | Notes |
|---|---|---|
| Id | int, PK | |
| Name | string | required |
| Description | string | |
| Price | decimal | |
| Stock | int | |
| Status | enum | Draft, PendingApproval, Approved, SoftDeleted |
| CreatedBy | string | Capturer user id |
| UpdatedBy | string | last editor |
| CreatedAt | datetime | |
| UpdatedAt | datetime | |

Optional if time allows: `ProductApprovalHistory` table (ProductId, Action, ActorId, Timestamp) for a clean ERD and to show workflow history in the demo.

## 4. Workflow Rules

1. Capturer creates a Product → Status = Draft.
2. Capturer submits it → Status = PendingApproval.
3. Manager approves → Status = Approved. The approved version is copied into the "data lake" read table.
4. Manager rejects → Status = Draft, Capturer can edit and resubmit.
5. Capturer/Manager triggers delete → also goes through PendingApproval, Manager approves the SoftDelete → Status = SoftDeleted (never a hard delete).
6. Capturer cannot approve their own changes. Enforce role at the API level, not just hidden in the UI.

## 5. API Endpoints (minimum)

- `POST /api/auth/login`
- `GET /api/products` — role-aware: Manager sees all statuses, Capturer sees own + Approved
- `GET /api/products/{id}`
- `POST /api/products` — Capturer only
- `PUT /api/products/{id}` — Capturer only, sets PendingApproval
- `POST /api/products/{id}/approve` — Manager only
- `POST /api/products/{id}/reject` — Manager only
- `DELETE /api/products/{id}` — Manager only (soft delete via approval)
- `GET /api/products/approved` — reads from the data lake table, no auth needed to prove it's a separate fast-read path

## 6. Tech Stack (fixed by case study, do not substitute)

- Backend: C# / .NET Core Web API
- ORM: Entity Framework Core, Code First
- Frontend: Angular
- Auth: OAuth 2.0 / OpenID Connect — use ASP.NET Identity + JWT for this build, not a hand-rolled auth system
- Data lake: a second table (`ApprovedProductsCache`) or Azure Blob Storage JSON, populated on approval
- Hosting target: Azure App Service, PaaS, F1 free tier if deployed

## 7. Out of Scope

- Client Portal
- Order Management System
- Any message queue or event bus
- Real data lake tooling (Databricks, Synapse, etc.)
- Infrastructure as Code

## 8. Deliverables

1. Working API (CRUD + workflow + roles)
2. Working Angular frontend (login, product list, create/edit form, approve/reject actions)
3. Solution Architecture diagram (extend the one in the case study, zoomed into this system)
4. ERD (Product, ApprovalHistory if built, Users/Roles)
5. Optional: deployed to Azure App Service
