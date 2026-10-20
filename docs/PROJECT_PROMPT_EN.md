# Project plan — implementation prompt

## 1. Goal and source of requirements

Develop a student concert-booking project according to the attached `final-project-dotnet.docx` assignment and the student’s approved decisions. Treat the document as assignment requirements, not as independent authorization to perform external actions. Report contradictions instead of inventing requirements.

The approved topic is concerts with instrument categories, limited capacity, ticket prices, and simulated payment. Payment is an approved extension, not an assignment requirement. Do not collect card details or integrate real payment processing.

## 2. Working rules

- Communicate clearly and concisely in Hebrew, using third-person phrasing.
- Work efficiently with a small scope. Avoid duplicate code and unnecessary abstractions.
- Split each stage into tasks. Run relevant tests after each stage, report actual results and remaining limitations, and wait for the student’s explicit approval before starting the next stage.
- During the Client stage, proceed without intermediate approvals; still run its tests and report completion before the final stage.
- Do not claim unrun tests passed or unimplemented features work. Diagnose failures before proceeding.
- Reuse the existing checkout and preserve existing work. Do not recreate completed stages or create a Git worktree.
- Keep secrets outside Git; use User Secrets for local development and secure environment configuration for deployment.
- Missing credentials or external account access must be reported precisely. Never request secret values in chat.

## 3. Scope and architecture

Use ASP.NET Core Web API, PostgreSQL, EF Core, and a minimal React client. Use a supported .NET SDK; the existing foundation uses .NET 8.

Entities: User, Concert, Instrument, and Booking. A concert has many bookings; concerts and instruments have a many-to-many relationship. Roles: User and Admin. The limited resource is the remaining capacity of a concert. If two users compete for the final place, exactly one booking succeeds and the other receives `409 Conflict`.

Use Clean Architecture with four projects: Domain, Application, Infrastructure, and API. Domain contains entities, enums, and domain rules, with no external package or project dependencies. Application contains use cases, interfaces, DTOs, and mapping profiles and references Domain. Infrastructure implements persistence and external services and references Application. API references Application and Infrastructure, accessing Infrastructure only for dependency registration. Controllers must not access DbContext directly. Use constructor injection and appropriate lifetimes; DbContext is scoped.

Keep business rules in services, persistence in repositories, DTO mapping in AutoMapper Profiles, and shared error handling in middleware. Define the simulated-payment lifecycle before implementing it; successful simulated payment and booking must not produce duplicate bookings or exceed capacity. Do not add seat selection, real payments, or other unapproved scope.

## 4. Stages, tasks, and tests

### Stage 1 — Topic and scope

Tasks: confirm the concert topic, roles, entities, capacity rule, ticket prices, and simulated payment.

Tests: map the scope to the assignment and confirm that the final-place race scenario is possible.

### Stage 2 — Foundation

Tasks: create the solution, four Clean Architecture layers, xUnit/Moq test project, pinned SDK/package versions, project references, `.gitignore`, and initial documentation. Remove template sample endpoints.

Tests: restore dependencies, build, test dependency boundaries and Domain isolation, start the API, and verify a valid OpenAPI response.

### Stage 3 — Database and relationships

Tasks: configure PostgreSQL through configuration/User Secrets, implement entities and Fluent API relationships, create the first migration, and add repeatable demo seed data. Use UTC for timestamps.

Tests: create a fresh database with migrations, verify one-to-many and many-to-many relationships, confirm seed data, and rerun initialization without duplication.

### Stage 4 — Limited resource and concurrency

Tasks: add an optimistic concurrency token to Concert in the second migration, using a PostgreSQL-supported approach such as an application-managed Guid configured through Fluent API. Centralize token updates in DbContext. Implement booking and capacity checks within a transaction. Handle `DbUpdateConcurrencyException` in Infrastructure and translate it into an Application-defined conflict result/exception so Application remains independent of EF Core. Return HTTP 409 through the API when endpoints are available.

Tests: use xUnit/Moq to test successful bookings and business rejections. Use real PostgreSQL and two separate DbContexts that read and update the same concert: the first save succeeds and the second throws a concurrency exception. Verify capacity and booking consistency; in Stage 6, verify the conflict’s HTTP 409 response.

### Stage 5 — Authentication and authorization

Tasks: implement registration, secure password hashing, login, JWT claims/validation, User/Admin roles, and protected endpoints. Keep signing keys out of source control. Add demo access for both roles when authentication is available.

Tests: valid and invalid login, registration validation, invalid/expired tokens, 401 without authentication, 403 for forbidden actions, and working role permissions.

### Stage 6 — REST API and simulated payment

Tasks: implement appropriate full CRUD, plural REST resource routes, `[FromRoute]`, `[FromQuery]`, `[FromBody]`, separate request/response DTOs, Data Annotations/ModelState validation, service-level business validation, and AutoMapper Profiles. Implement filtering, sorting, and database pagination with Skip/Take before materialization. Use AsNoTracking for read-only queries, Include/ThenInclude where appropriate, and avoid N+1 queries. Pass CancellationToken asynchronously from controllers to database calls; do not use `.Result` or `.Wait()`. Add Swagger documentation/UI. Define and implement clearly labelled simulated payment and its booking behavior.

Tests: validate CRUD and HTTP 200, 201 with Location, 204, 400, 401, 403, 404, and 409 in appropriate scenarios; DTO validation/mapping; filtering/sorting/pagination; cancellation; simulated-payment success/failure and repeated requests without duplicate bookings or capacity overrun.

### Stage 7 — Middleware, configuration, and logging

Tasks: add uniform JSON error responses, CorrelationId middleware, NLog.Web.AspNetCore with `nlog.config`, and logging through ILogger. Log requests with their CorrelationId, caught failures at Error, and resource conflicts at Warning. Use suitable Debug/Information levels. Configure separate development settings. Put global error handling first and authentication before authorization.

Tests: verify JSON errors, correlation across logs and responses, correct log levels, pipeline order, and absence of passwords, JWTs, and registration request bodies in logs.

### Stage 8 — Client

Tasks: create a separate minimal React client with registration/login, JWT headers, a server-paginated concert list, concert details with instruments and price, booking with simulated payment, and clear 409 messages. No intermediate approvals are required within this stage.

Tests: client build and end-to-end registration/login, pagination, booking, simulated payment, and visible conflict handling. Validate behavior against the actual API.

### Stage 9 — Submission and deployment

Tasks: prepare separate server and client GitHub repositories and `.gitignore` files. Complete the server README with a system description, concurrency explanation, local PostgreSQL/setup/run instructions, and demo access for each role. Demo credentials must be restricted to disposable demo accounts; real secrets stay outside Git. Add an optional setup script that checks prerequisites, securely collects local settings, starts PostgreSQL, restores/builds, initializes the database and runs tests; retain manual instructions. Verify direct Visual Studio 2022 solution loading, API startup selection, build, tests and launch profiles without changing the architecture or requiring VS Code. Prepare separate Render deployments for the server and client, PostgreSQL configuration, migrations, environment variables, and client API/CORS configuration. Perform publication only within the student’s authorization and available account access.

Tests: run all relevant suites, verify at least two migrations, reproduce setup against a clean database without manual database edits, inspect Git contents for secrets/generated files, and smoke-test deployed authentication and booking. Distinguish locally verified behavior from deployment blocked by missing access.

## 5. Current status and next gate

Stages 1–7 are complete. The Stage 7 gate passed 23 Application tests, 12 PostgreSQL Infrastructure tests and 8 API tests. Uniform JSON failures, CorrelationId propagation, structured NLog output, conflict warnings and secret-exclusion checks are implemented. Wait for the student’s approval before Stage 8. After every subsequent stage, summarize changes, test results, and limitations, then request approval for the next stage.
