# Verification

Verified with .NET SDK 10.0.401 and EF Core 10.0.12.

- Solution build: succeeded with 0 warnings and 0 errors.
- Initial Code First migration: generated with the pinned EF CLI tool.
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- Integration executable: **52 checks passed** against the running API and a temporary SQLite database.

Checks cover CRUD status codes and persisted values, Location headers, pagination and overflow limits, name/SKU normalization, exact money round-trips, malformed JSON, missing fields, validation, duplicate SKUs, conflicting updates, concurrent duplicate creation, optimistic concurrency at the repository, BLL validation without MVC, ProblemDetails contracts, safe infrastructure errors, and development OpenAPI.

The integration executable creates its own database, starts its own hidden API process, and cleans them up. Console logging is configured explicitly so Windows Event Log privileges are unnecessary.

Build artifacts and databases are excluded from the source archive. Restore and build before running the supplied verification command; see README.md.
