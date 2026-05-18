# BACKEND-22 Compile Fix

Fixes two CS0119 compile errors in `GrowExportsApiController.cs`.

Cause: inside a controller, `File` resolves to `ControllerBase.File(...)`, so `File.Exists(...)` is parsed against the MVC helper method instead of `System.IO.File`.

Change:

- `File.Exists(...)` -> `System.IO.File.Exists(...)`

No logic change.
