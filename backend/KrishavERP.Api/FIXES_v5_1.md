# v5.1 Backend Compile Fixes

Fixed:
- `OpdController`: renamed boolean `emergency` to `isEmergency`
- `OpdController`: renamed decimal emergency rate to `emergencyRate`
- Removed all decimal-to-bool conditional expressions
- `LabController`: anonymous return object now uses `OrderId` and `BillId`
- `RolesController`: removed duplicate `Users()` method
- `RolesController`: removed duplicate `AssignRole()` method
- Rewrote `RolesController` in normal formatted C#
- Verified curly-brace balance across all backend `.cs` files
- Checked for duplicate anonymous inferred-property names
- Checked for duplicate public method names in controllers
