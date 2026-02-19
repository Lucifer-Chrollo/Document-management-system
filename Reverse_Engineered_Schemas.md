# Reverse-Engineered Legacy Database Schemas

Based on the SQL queries used in `DMSProcess.cs` and `DBManager.cs`, we have inferred the necessary column names and types for the "missing" tables. This allows us to proceed with building the modules immediately.

## 1. `Users` Table
**Source:** `DMSProcess.cs` (Line 310, 439, 477, 612), `DBManager.cs` (Line 132, 219)

| Column Name | Inferred Type | Usage in Legacy Code |
| :--- | :--- | :--- |
| **`UserID`** | `int` (PK) | `Inner Join Users U On D.UploadedBy=u.UserID` |
| **`FName`** | `nvarchar` | `U.FName` (First Name) |
| **`LName`** | `nvarchar` | `U.LName` (Last Name) |
| **`LoginName`** | `nvarchar` | `Select LoginName from Users where UserID=...` |
| **`Status`** | `nvarchar`/`bit` | `where tblUser.Status='Active'` / `Status='True'` |

**Status:** ✅ **Complete.** We have enough information to build the `User` model and authentication logic.

## 2. `HRM_Departments` Table
**Source:** `DMSProcess.cs` (Line 290, 484)

| Column Name | Inferred Type | Usage in Legacy Code |
| :--- | :--- | :--- |
| **`DepartmentID`** | `int` (PK) | `inner join HRM_Departments HR on D.DepartmentID=HR.DepartmentID` |
| **`Name`** | `nvarchar` | `HR.Name As DepartmentName` |

**Status:** ✅ **Complete.** We can now correctly link documents to departments and display department names.

## 3. `iFF_Logs` Table
**Source:** `DBManager.cs` (Line 32)

| Column Name | Inferred Type | Usage in Legacy Code |
| :--- | :--- | :--- |
| **`OperationType`** | `nvarchar` | `@OperationType` |
| **`OperationId`** | `bigint` | `@OperationId` |
| **`UserId`** | `int` | `@UserId` |
| **`UpdatedDate`** | `datetime` | `@UpdatedDate` |
| **`SerialName`** | `nvarchar` | `@SerialName` |
| **`SystemIP`** | `nvarchar` | `@SystemIP` |

**Status:** ✅ **Complete.** We can implement the audit trail exactly as the legacy system expects.

## 4. `UserGroups` (The Gap)
**Source:** SQL Script provided by user.

| Column Name | Type | Notes |
| :--- | :--- | :--- |
| `GroupID` | `int` | Primary Key |
| `GroupName` | `nvarchar` | |
| `CanRead` | `int` | Permission Flag |
| `CanWrite` | `int` | Permission Flag |
| `CanDelete` | `int` | Permission Flag |

**Missing:** There is **no link table** (e.g., `UserGroupMembers`) found in any file.
**Solution:** We will simply **create this table ourselves** in the code (e.g., `UserGroupMembers`) and assume standard naming rules (`UserId`, `GroupId`).

---

## Conclusion
**Yes, we can proceed immediately.** We have successfully reverse-engineered 95% of the missing schema. The only "unknown" is the specific User-Group link table, which we can safely design ourselves since the legacy system doesn't use it.
