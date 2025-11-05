## 📘 README — Database Tests

### 📂 Location
`WorkSchedulerApp.TestProject1/Database/`

This directory contains **NUnit integration tests** for the `DatabaseHandler` in `WorkSchedulerApp.Database`.  
All tests validate the SQLite schema, CRUD operations, and relational integrity (foreign keys + cascade deletes).

---

### 🧱 Structure Overview

| File                       | Purpose                                                                                                |
|----------------------------|--------------------------------------------------------------------------------------------------------|
| **DatabaseTestBase.cs**    | Shared setup/teardown for all tests. Creates an isolated SQLite test database before running tests.    |
| **WorkLoadTests.cs**       | Tests creation, reading, and updating of all workload types (`PerEmployee`, `PerItem`, `Fixed`).       |
| **WorkGroupTests.cs**      | Tests `WorkGroup` creation, workload mappings, rename (update), and deletion.                          |
| **EmployeeTests.cs**       | Tests `Employee` creation, retrieval, and updating of stored details.                                  |
| **EmployeeSkillsTests.cs** | Tests the link table `EmployeeSkills`: adding, removing, and listing employee–workload skill mappings. |
| **ForeignKeyTests.cs**     | Verifies SQLite foreign key enforcement and cascade deletions for all relationships.                   |

---

### 🧩 Test Initialization

All test classes inherit from **`DatabaseTestBase`**, which:

- Creates a fresh test SQLite database in `/TestDB/` under the test binary folder.
- Calls `DatabaseHandler.Instance.Initialize()` with that file path.
- Ensures the schema is created before any tests run.
- Closes all database connections after tests complete.

This guarantees that tests never affect the real application database.