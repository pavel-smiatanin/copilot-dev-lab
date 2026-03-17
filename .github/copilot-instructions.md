# Project Overview

A lightweight URL shortener for anonymous users. The system consists of:
- **Frontend**: Single-page application (React + TypeScript + Vite)
- **Backend**: REST API (.NET 8 / ASP.NET Core)
- **Database**: MS SQL Server (application state)
- **Cache**: Redis (alias resolution, rate-limiting counters)

Key features: short link creation with optional custom alias, expiry, and password protection; fast cached redirects; basic visit analytics.

**Out of scope**: user accounts, authentication, administration, link editing/deletion after creation, bulk import/export, custom domain mapping.

This project is for experiments, study and local development only — not intended for production.

---

# Architecture, Design, and Technology Stack

## General Design Principles
- Follow **SOLID**, **DRY**, **KISS** principles
- Follow **Fail Fast** principle: validate inputs at the earliest opportunity; reject invalid state before it propagates

## Backend

### Technology Stack
- **.NET 8**, **C# 12**, **ASP.NET Core Web API**
- **MediatR** (CQRS — commands and queries)
- **FluentValidation** (request validation via MediatR pipeline behavior)
- **EF Core** (data access, Code First, Migrations) with **MS SQL Server**
- **FluentMigrations** library is used for database migrations
- **Redis** via `IDistributedCache` (alias resolution cache, rate-limiting counters)
- **Serilog** (structured logging, console/stdout sink)
- **xUnit**, **Moq**, **AutoFixture**, **FluentAssertions** ≤ 7.x (unit tests)
- **Docker**. Dockerfiles for local development; no production deployment targets. Docker Compose for local multi-container setup (API + Worker + SQL Server + Redis)

### Architecture
- **Hexagonal (Ports & Adapters)**: application logic is independent of infrastructure and frameworks (DB, cache, HTTP). There is no necessity to use Rich Domain Model, DDD, or complex architectural patterns for this simple CRUD app.
- **Controllers are thin** — they translate HTTP to/from MediatR requests only; no business logic
- **MediatR handlers** implement the **Transaction Script** pattern; they are stateless
- **Repository pattern is not used** — EF Core `DbContext` is injected directly as Unit of Work inside handlers
- `IDistributedCache` is injected into handlers that require caching
- `Serilog.ILogger` is injected into handlers that require logging

### Project / Solution Structure
```
UrlShortener.sln
  src/
    UrlShortener.Adapter.Api/             # ASP.NET Core project as entry point; controllers, middleware, DI wiring, rate limiting, global exception handling. Depends on UrlShortener.Application, UrlShortener.Shared and UrlShortener.Adapter.BackingServices.
    UrlShortener.Adapter.Worker/          # Project based on .NET Worker template; background service for cleaning up expired links. Depends on UrlShortener.Application, UrlShortener.Shared and UrlShortener.Adapter.BackingServices.
    UrlShortener.Application.Abstract/    # Project with application contracts (commands and queries/CQRS). Defines primary and secondary ports in terms of hexagonal architecture. Defines DbContext as secondary port. No dependencies on other projects.
    UrlShortener.Application/             # Application MediatR handlers, commands, queries, validators, DTOs. Depends on UrlShortener.Application.Abstract and UrlShortener.Shared.
    UrlShortener.Adapter.BackingServices/ # Application infrastructure, secondary port implementation, EF Core DbContext configuration, cache adapters, external services. Depends on UrlShortener.Application.Abstract and UrlShortener.Shared.
    UrlShortener.Database.Migrations/     # FluentMigrations-based schema migration project. No dependencies on other solution projects.
    UrlShortener.Shared/                  # Cross-cutting concerns, shared utilities, constants. Project does not have any application or domain logic. No dependencies on other projects.
  tests/
    UrlShortener.Application.Tests/  # Unit tests for MediatR handlers and validators only
    UrlShortener.Adapter.Api.Tests/  # Unit tests for helpers, middleware, and controllers (if necessary)
    UrlShortener.Adapter.BackingServices.Tests/  # Unit tests for backing services (if necessary)
    UrlShortener.Shared.Tests/  # Unit tests for shared utilities and extensions (if necessary)
```

### API Conventions
- All routes versioned under `/api/v1/`
- Allowed HTTP status codes: **200, 201, 204, 400, 404, 409, 429, 500**
  - `400` — validation failure or malformed request
  - `404` — alias not found or link expired
  - `409` — alias already taken
  - `429` — rate limit exceeded (must include `Retry-After` header)
  - `500` — unhandled server error

### Data Model Conventions
- **GUID** primary key on all EF entities (`Id`)
- **Optimistic concurrency**: all entities carry a `RowVersion` (`byte[]`, `[Timestamp]`) concurrency token
- Link passwords stored as **bcrypt hashes** (cost factor ≥ 12); plain-text passwords are never logged or returned

### Validation
- **FluentValidation** validators are registered as a MediatR pipeline behavior (`ValidationBehavior<,>`)
- Validators are co-located with their request class in `UrlShortener.Application`
- The pipeline behavior throws `ValidationException` on failure; a global exception handler maps it to HTTP 400

### Testing
- **Primary**: unit tests in `UrlShortener.Application.Tests` cover all MediatR handlers and FluentValidation validators
- **Secondary**: `UrlShortener.Adapter.Api.Tests`, `UrlShortener.Adapter.BackingServices.Tests`, and `UrlShortener.Shared.Tests` exist for helpers, middleware, and utilities where meaningful tests are warranted
- Pattern: **AAA** (Arrange / Act / Assert)
- Libraries: **xUnit**, **Moq**, **AutoFixture**, **FluentAssertions** ≤ 7.x
- Dependencies (`DbContext`, `IDistributedCache`, `Serilog.ILogger`) are mocked; do not write tests for framework-provided code

## Frontend

### Technology Stack
- **React 18+**, **TypeScript** (strict mode), **Vite**
- **React Testing Library** + **Vitest** (unit/component tests — Vitest, not Jest, for Vite ecosystem compatibility)
- **Axios** for HTTP requests (consistent error handling across all API calls)
- **React Query (TanStack Query)** for server-state management, loading/error states, and request deduplication
- **CSS Modules** for component-scoped styles

Full React and TypeScript coding standards (naming conventions, component guidelines, hooks, state management, API integration, testing, accessibility, security) are defined in `.github/instructions/react.instructions.md` and apply automatically to all `*.ts` and `*.tsx` files.

---

# .NET / C# Coding Standards

Full C# coding standards (naming conventions, code style, async/await rules, DI, EF Core, MediatR, logging, testing, security) are defined in `.github/instructions/csharp.instructions.md` and apply automatically to all `*.cs` files.

---

# Documentation
- Document complex logic and non-obvious business rules with inline comments
- Keep `README.md` up to date with local setup instructions (DB, Redis, migrations)
- OpenAPI/Swagger annotations on controllers for API documentation
- Add comments only where the intent is not self-evident from the code

---

# Conversation Logging
**MANDATORY**: All interactions with GitHub Copilot MUST be logged in files of the /docs/copilot-chat-log directory. Create the directory if it does not exist. Chat log file name template is yyyyMMdd.md, where yyyyMMdd is timestamp with current date (yyyy means 4 digits for year, MM - 2 digits for month number, dd - 2 digits for day of month). Chat log file name examples are 20260226.md, 20260318.md

When writing logs: always append entries to the file for the current date (format `yyyyMMdd.md`). Do not create additional numbered files for the same date (for example, avoid creating `20260317-2.md` or `20260317-3.md`). If numbered/duplicate files already exist, merge their entries into the corresponding `yyyyMMdd.md` file and delete the duplicates.

## When to Log
- **ALWAYS** after chat responses
- **ALWAYS** after completing any task that modifies files
- **ALWAYS** after fixing bugs or issues
- **ALWAYS** after creating new features or components
- **ALWAYS** after running tests or making configuration changes
- **RULE**: If you made changes, you MUST update log file before completing your response

## Logging Format
```markdown
## yyyy-MM-dd HH:mm:ss

### Prompt
[User's raw prompt/request]

### Result
[Summary of what was created/changed - focus on outcomes, not full code]

-------------
```
In the logging format HH:mm:ss is timestamp, yyyy-MM-dd is date as described above. HH - means 2 digits of hours, mm - 2 digits for minutes and ss - 2 digits for seconds. For example 02:53:15, 14:01:05, 23:59:59

## What to Log
- **Prompt**: Keep raw user prompts exactly as provided
- **Result**: Summarize changes, files created/modified, key decisions made
- Avoid including full code blocks - focus on what was accomplished
- Include file names and high-level descriptions of changes
- Note test results if tests were run
- Document any issues encountered and how they were resolved

## How to Update
Use `replace_string_in_file` or `multi_replace_string_in_file` to append new entries to current log file before finishing your response. Never skip this step. New entries added at the end of the file, separated by a horizontal rule (`-------------`).

---

# Notes
- Regularly update this file with project-specific patterns and decisions
- Add examples of common scenarios as the project evolves
- Document architectural decisions and their rationale
- **Review and update `.gitignore`** when adding new tools, frameworks, or build outputs

## Key Project Decisions
- **Alias auto-generation**: 6-character Base62 shortId when no custom alias is provided
- **Visit recording**: fire-and-forget (async, non-blocking); redirect response does not wait for the analytics write
- **Rate limiting**: IP-based counters in Redis; `Retry-After` header always included in 429 responses
- **Password protection**: bcrypt (cost factor ≥ 12); IP locked after 5 failed unlock attempts per alias per 15 minutes
- **SSRF prevention**: destination URL and metadata fetch both validate resolved hostname against RFC 1918/ loopback/link-local ranges
- **IP privacy**: raw IPs are HMAC-SHA-256 hashed before storage; never persisted in plain text
- **Redis fallback**: if Redis is unavailable, alias resolution falls back to direct DB lookup; a warning is logged and redirects continue unaffected
- **Alias collision retry**: auto-generated 6-char Base62 shortId is retried up to 5 times on uniqueness collision before returning 500
- **Background cleanup**: a `BackgroundService` in `UrlShortener.Adapter.Worker` runs on a configurable schedule to hard-delete expired links (with Redis cache eviction), orphaned visits, and visits beyond the 90-day retention window; deletions are batched (default 500 rows) to avoid lock contention
