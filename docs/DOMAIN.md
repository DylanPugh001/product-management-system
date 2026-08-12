# Domain — Product Management System

## 1. Glossary

### Product

The core entity. Represents a product record in the system with fields for commercial data (Name, Description, Price, Stock) and workflow state (Status, PendingDelete). A product progresses through statuses via the approval workflow. Products are never hard-deleted.

### Draft

Initial status of a product after creation, or after a Manager rejects a pending change. A Draft product is visible only to the Capturer who created it and to Managers. It has not been submitted for approval.

### PendingApproval

A product (or a delete request) that has been submitted and is awaiting a Manager's decision. While in this status, the product cannot be edited by the Capturer. The `PendingDelete` flag distinguishes whether this is a content change approval or a delete-request approval.

### Approved

A product whose content has been approved by a Manager. Approved products appear in `ApprovedProductsCache` and are visible to all authenticated users and to anonymous consumers of `GET /api/products/approved`.

### SoftDeleted

Terminal status. A product that has been soft-deleted following an approved delete request. Not visible in normal list views. Never hard-deleted. The audit trail (`ProductApprovalHistory`) is preserved.

### PendingDelete flag

A boolean field on `Product` (`PendingDelete = true`) that, when combined with `Status = PendingApproval`, signals that the pending approval is for a **deletion** rather than a content change. This disambiguates the two uses of `PendingApproval` in the state machine. It is always reset to `false` after any approval or rejection.

### Capturer

A user assigned the `Capturer` role. Responsible for creating and editing product records. Can see their own products and all Approved products. Cannot approve, reject, or request deletion.

### Manager

A user assigned the `Manager` role. Responsible for approving or rejecting product changes and requesting/approving soft deletes. Can see all products regardless of status. Cannot create or edit product content. Cannot approve their own changes.

### Approval History / Audit Trail

The `ProductApprovalHistory` table records every status transition. Each entry captures: the action name, the actor's identity (id + display name), a timestamp, and an optional note. History is immutable — entries are only ever inserted, never updated or deleted (EF cascade delete removes history only if the Product is hard-deleted, which never happens in normal operation).

### ApprovedProductsCache / Data Lake Read Path

A denormalised table (`ApprovedProductsCache`) that holds a copy of the commercial fields for every currently-Approved product. It is the backing store for `GET /api/products/approved`. It is upserted on approval and the row is removed on soft-delete approval. It exists so that the public endpoint requires no authentication, no joins to the Products table, and no status filtering at query time.

### Self-Approval

The forbidden act of a Manager approving a product change that they themselves created (i.e., `Product.CreatedBy == Manager.UserId`). This is enforced as a domain rule in `ProductService.ApproveAsync` and returns HTTP 403.

### Soft Delete Request

When a Manager calls `DELETE /api/products/{id}`, it does not delete the product. It sets `PendingDelete = true` and `Status = PendingApproval`, creating a pending approval record in `ProductApprovalHistory` with Action = `"SoftDeleteRequested"`. A second (different) Manager must then approve this request.

---

## 2. Business Rules

| ID     | Rule                                                                                                                                                                                                                                              |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BR-001 | Only a user with the `Capturer` role can create a product (`POST /api/products`).                                                                                                                                                                 |
| BR-002 | A newly created product has `Status = Draft`.                                                                                                                                                                                                     |
| BR-003 | Only the Capturer who originally created a product (`Product.CreatedBy == CurrentUserId`) can edit it.                                                                                                                                            |
| BR-004 | Submitting an edit (`PUT /api/products/{id}`) moves `Status` to `PendingApproval`, regardless of the product's current status (Draft or previously Approved).                                                                                     |
| BR-005 | A product with `Status = SoftDeleted` cannot be edited. The API returns 400.                                                                                                                                                                      |
| BR-006 | Only a user with the `Manager` role can approve or reject a pending change.                                                                                                                                                                       |
| BR-007 | A Manager cannot approve a change they created. `Product.CreatedBy == CurrentUserId` returns HTTP 403. This applies to both content approvals and delete-request approvals.                                                                       |
| BR-008 | Approving a normal pending change (`PendingDelete = false`) sets `Status = Approved` and upserts a row in `ApprovedProductsCache` with the current product fields.                                                                                |
| BR-009 | Rejecting a pending change sets `Status = Draft` and resets `PendingDelete = false`. An optional reason is recorded in `ProductApprovalHistory`.                                                                                                  |
| BR-010 | Only a Manager can request a soft delete (`DELETE /api/products/{id}`).                                                                                                                                                                           |
| BR-011 | A soft delete request sets `PendingDelete = true` and `Status = PendingApproval`. The same self-approval rule (BR-007) applies when a second Manager approves it.                                                                                 |
| BR-012 | Approving a soft delete request (`PendingDelete = true`) sets `Status = SoftDeleted`, resets `PendingDelete = false`, and removes the corresponding row from `ApprovedProductsCache` (if one exists).                                             |
| BR-013 | Products are never hard-deleted. `SoftDeleted` is a terminal status. `ProductApprovalHistory` is preserved indefinitely.                                                                                                                          |
| BR-014 | `GET /api/products/approved` is unauthenticated (`[AllowAnonymous]`). It reads exclusively from `ApprovedProductsCache`, not from the `Products` table.                                                                                           |
| BR-015 | A Capturer's product list is filtered to: products they created (`CreatedBy == userId`) OR products with `Status = Approved`. A Manager sees all products with no filter.                                                                         |
| BR-016 | A product in any status is not editable by the Capturer while it is in `PendingApproval` (the edit endpoint will return the record with a 200, but the transition logic always moves it back to `PendingApproval` — effectively a re-submission). |

---

## 3. Invariants

These must be true at all times after any operation completes:

| #    | Invariant                                                                                                    |
| ---- | ------------------------------------------------------------------------------------------------------------ |
| I-01 | A product with `Status = SoftDeleted` has no corresponding row in `ApprovedProductsCache`.                   |
| I-02 | A product with `Status = Approved` has exactly one row in `ApprovedProductsCache` whose `ProductId` matches. |
| I-03 | Every status transition produces exactly one entry in `ProductApprovalHistory`.                              |
| I-04 | `Product.CreatedBy` never changes after the product is created.                                              |
| I-05 | `PendingDelete = true` only occurs when `Status = PendingApproval`.                                          |
| I-06 | After any approval or rejection, `PendingDelete` is reset to `false`.                                        |
| I-07 | No product record is ever removed from the `Products` table.                                                 |
| I-08 | `ProductApprovalHistory` entries are append-only. No updates or deletes in normal operation.                 |

---

## 4. Action → History Entry Mapping

| HTTP Operation                    | Trigger Condition               | `Action` value          | Note                                         |
| --------------------------------- | ------------------------------- | ----------------------- | -------------------------------------------- |
| `POST /api/products`              | Product created                 | `"Created"`             | null                                         |
| `PUT /api/products/{id}`          | Product was in Draft            | `"Submitted"`           | `"Capturer submitted changes for approval."` |
| `PUT /api/products/{id}`          | Product was in any other status | `"UpdateRequested"`     | `"Capturer submitted changes for approval."` |
| `POST /api/products/{id}/approve` | `PendingDelete = false`         | `"Approved"`            | `"Manager approved the change."`             |
| `POST /api/products/{id}/approve` | `PendingDelete = true`          | `"SoftDeleted"`         | `"Manager approved the delete request."`     |
| `POST /api/products/{id}/reject`  | Any pending                     | `"Rejected"`            | Optional reason from request body            |
| `DELETE /api/products/{id}`       | Request soft delete             | `"SoftDeleteRequested"` | `"Manager requested a soft delete."`         |

---

## 5. Role Permission Matrix

| Operation                                         | Capturer       | Manager     | Anonymous |
| ------------------------------------------------- | -------------- | ----------- | --------- |
| `GET /api/products`                               | Own + Approved | All         | ✗         |
| `GET /api/products/{id}`                          | Own + Approved | All         | ✗         |
| `POST /api/products`                              | ✓              | ✗           | ✗         |
| `PUT /api/products/{id}` (own product)            | ✓              | ✗           | ✗         |
| `PUT /api/products/{id}` (others' product)        | ✗ (403)        | ✗           | ✗         |
| `POST /api/products/{id}/approve`                 | ✗              | ✓ (not own) | ✗         |
| `POST /api/products/{id}/approve` (own)           | ✗              | ✗ (403)     | ✗         |
| `POST /api/products/{id}/reject`                  | ✗              | ✓           | ✗         |
| `DELETE /api/products/{id}` (soft delete request) | ✗              | ✓           | ✗         |
| `GET /api/products/approved`                      | ✓              | ✓           | ✓         |
| `POST /api/auth/login`                            | ✓              | ✓           | ✓         |
| `GET /api/auth/me`                                | ✓              | ✓           | ✗         |
| `GET /api/health`                                 | ✓              | ✓           | ✓         |

---

## 6. Error Responses

| Condition                                   | HTTP Status | Message                                             |
| ------------------------------------------- | ----------- | --------------------------------------------------- |
| Product not found                           | 404         | (no body)                                           |
| Product not visible to caller               | 404         | (no body — don't reveal existence)                  |
| Caller is not the product owner             | 403         | (no body from Forbid())                             |
| Self-approval attempt                       | 403         | `"A manager cannot approve their own change."`      |
| Product not in PendingApproval              | 400         | `"Only pending products can be approved/rejected."` |
| Product is SoftDeleted (on edit)            | 400         | `"Soft-deleted products cannot be edited."`         |
| Product already deleted or pending deletion | 400         | `"Product is already deleted or pending deletion."` |
| Invalid credentials                         | 401         | `"Invalid email or password."`                      |
| Name is empty                               | 400         | `"Name is required."`                               |
| Price is negative                           | 400         | `"Price must be non-negative."`                     |
| Stock is negative                           | 400         | `"Stock must be non-negative."`                     |
| Description too long                        | 400         | `"Description must be 2000 characters or fewer."`   |
