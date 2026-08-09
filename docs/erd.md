# Product Management System — ERD

```mermaid
erDiagram
    AspNetUsers {
        string Id PK
        string UserName
        string Email
        string PasswordHash
    }

    AspNetRoles {
        string Id PK
        string Name
    }

    AspNetUserRoles {
        string UserId FK
        string RoleId FK
    }

    Product {
        int Id PK
        string Name
        string Description
        decimal Price
        int Stock
        int Status
        string CreatedBy
        string UpdatedBy
        datetime CreatedAt
        datetime UpdatedAt
        boolean PendingDelete
    }

    ProductApprovalHistory {
        int Id PK
        int ProductId FK
        string Action
        string ActorId
        string ActorName
        datetime Timestamp
        string Note
    }

    ApprovedProductsCache {
        int Id PK
        int ProductId
        string Name
        string Description
        decimal Price
        int Stock
        datetime ApprovedAt
        string ApprovedBy
    }

    AspNetUsers ||--o{ AspNetUserRoles : has
    AspNetRoles ||--o{ AspNetUserRoles : assigned_to
    Product ||--o{ ProductApprovalHistory : records
    ProductApprovalHistory }o--|| AspNetUsers : "acted_by"
```

## Notes

- `Product.CreatedBy` / `UpdatedBy` store the `AspNetUsers.Id` (Capturer user id).
- `ProductApprovalHistory` is the workflow audit trail (Created, Submitted, UpdateRequested,
  Approved, Rejected, SoftDeleteRequested, SoftDeleted).
- `ApprovedProductsCache` is the denormalized "data lake" read table: one row per approved product,
  upserted on approval and removed on soft delete.
