# Software Requirements Specification

## URL Shortener Application

| Field       | Value                     |
|-------------|---------------------------|
| Version     | 0.5                       |
| Date        | 2026-03-16                |
| Status      | Draft                     |

---

## 1. Introduction

### 1.1 Purpose

This document specifies the requirements for a lightweight URL shortener web application. It serves as the primary reference for design, development, and testing.

### 1.2 Project Scope

All users interact with the system anonymously — there are no user accounts, authentication, or administration features.

### 1.3 Constraints

- Backend: .NET 8, ASP.NET Core Web API, C# 12, Hexagonal architecture, MediatR (CQRS)
- Background worker: .NET Worker Service (hosted service) for scheduled cleanup jobs
- Frontend: React 18+, TypeScript, Vite
- Database: MS SQL Server (EF Core Code First; schema migrations managed by FluentMigrations)
- Cache: Redis (`IDistributedCache`) for alias resolution and rate counters
- All REST API routes are versioned under `/api/v1/`
- HTTP status codes restricted to: 200, 201, 204, 400, 404, 409, 429, 500
- No authentication, authorization, or user accounts
- No browser extensions or bookmarklets in scope
- No custom domain mapping in scope
- No event streaming pipelines (e.g., Kafka) in scope

### 1.4 Assumptions

- A single MS SQL Server instance runs locally for development, preferably in a Docker container (SQL Server Developer Edition or SQL Express image).
- A single Redis instance runs locally in a Docker container for caching.
- All users interact with the system through the SPA anonymously; no mobile-native client exists.
- Links are publicly readable; there is no concept of private or owner-only links.

---

## 2. Glossary

| Term               | Definition |
|--------------------|------------|
| **Short link**     | A URL consisting of the service base URL and a short alias, e.g. `http://localhost/abc123` |
| **Alias / shortId** | The path segment that resolves to a destination URL |
| **Destination URL** | The full URL a short link redirects to |
| **Visit**          | A single redirect event; recorded asynchronously for analytics |

---

## 3. User Roles

| Role          | Description |
|---------------|-------------|
| **Anonymous** | Any visitor; creates short links and follows redirects without signing in |

---

## 4. User Flows

### 4.1 Create Short Link
1. User opens SPA and enters a destination URL.
2. Optionally sets a custom alias, expiry date, and/or link password.
3. Submits form; system validates the URL format and alias uniqueness.
4. On success: system returns the shortUrl; UI shows the short link and a copy action.
5. On alias conflict: UI shows a 409 error and suggests 2–3 alternatives (random suffix, auto-generated alias).

### 4.2 Custom Alias Collision Handling
1. User requests a custom alias.
2. System checks uniqueness at DB level.
3. If taken: returns 409 with the conflicting alias and 2–3 alternative suggestions.
4. User retries with a different alias or accepts a suggestion.

### 4.3 Redirect Flow (fast path)
1. Visitor requests `GET /:alias`.
2. System performs cache lookup (`alias:{shortId}`).
3. **Cache hit and link not-expired:** Return `302` to destination. Enqueue visit record asynchronously (non-blocking).
4. **Cache miss:** Fallback to DB lookup; if found, populate cache then redirect. If not found, return 404.
5. **Link expired:** Return 404.
6. **Password-protected link:** Return unlock page; see Flow 4.4.

### 4.4 Password-Protected Link Access
1. Visitor hits `/:alias`; system detects password requirement and serves the unlock form (200).
2. Visitor submits password via `POST /api/v1/links/{alias}/unlock`.
3. System verifies the password against the stored bcrypt hash.
4. **Success:** Returns a short-lived unlock token; client redirects to destination; visit is recorded.
5. **Failure:** Increment attempt counter. After N failures (configurable, default 5): return 429 with a `Retry-After` header and lock the alias for the requesting IP for a cooldown period.

### 4.5 View Link Stats
1. Visitor opens the stats page for a link using its link ID (GUID).
2. System returns aggregated visit statistics for that link.

---

## 5. Functional Requirements

### 5.1 Link Creation
- **FR-LINK-1:** Create link with: destination URL (required), optional custom alias, optional expiry (datetime), optional password.
- **FR-LINK-2:** Auto-generate a 6-character Base62 shortId (`[A-Za-z0-9]`, no separators) when no custom alias is provided; retry generation up to 5 times on collision before returning 500.
- **FR-LINK-3:** Custom alias must match pattern `[a-zA-Z0-9_-]{3,50}`; return 400 on violation.
- **FR-LINK-4:** Enforce uniqueness of alias at DB level (unique index); return 409 on conflict with 2–3 alternative suggestions in the response body.
- **FR-LINK-5:** Destination URL must be a valid `http` or `https` URL; block private IP ranges (RFC 1918, loopback) and non-http/https schemes; return 400 on violation.
- **FR-LINK-6:** Implement optimistic concurrency using a row version token on the `links` table.

### 5.2 Alias & Redirect
- **FR-REDIR-1:** `GET /:alias` resolves via Redis cache first; on cache miss, fall back to DB and repopulate cache.
- **FR-REDIR-2:** Return `302 Found` for valid, non-expired links.
- **FR-REDIR-3:** Return `404 Not Found` for unknown or expired aliases.
- **FR-REDIR-4:** Record a visit event asynchronously (fire-and-forget); redirect response must not wait for the analytics write.
- **FR-REDIR-5:** Cache entry TTL is set to `Min(link expiry, 24 h)`; for links with no expiry the default TTL is 24 hours.

### 5.3 Password-Protected Links
- **FR-PASS-1:** If a link has a password, `GET /:alias` responds with 200 and the unlock form instead of redirecting.
- **FR-PASS-2:** `POST /api/v1/links/{alias}/unlock` accepts a plain-text password, verifies it against the bcrypt hash, and on success returns a short-lived (5-minute) unlock token signed with an HMAC-SHA-256 server secret.
- **FR-PASS-3:** Client presents the unlock token as a query parameter to complete the redirect: `GET /:alias?token=<value>`.
- **FR-PASS-4:** Failed unlock attempts are rate-limited: max 5 per IP per alias per 15 minutes; return 429 on violation.

### 5.4 Analytics & Visits
- **FR-ANALYT-1:** Each visit record stores: `link_id`, `occurred_at` (UTC), HMAC-SHA-256 of IP address (keyed with a per-deployment secret), `referrer_host` (origin only), `user_agent`, `country_code` (best-effort geo-lookup).
- **FR-ANALYT-2:** `GET /api/v1/links/{id}/stats` returns: total visit count, unique visitor count (by hashed IP), visits by day for the last 30 days, top 10 referrer hosts.
- **FR-ANALYT-3:** Visit records are subject to a configurable retention period (default 90 days); purge is handled by the scheduled background job described in FR-BG-3.

### 5.5 Metadata Prefetch
- **FR-META-1:** On link creation, the system synchronously fetches the destination URL and extracts `<title>`, `og:title`, `og:image`, and `favicon` URL; extracted values are stored alongside the link record. The API response is not returned until the fetch completes or times out.
- **FR-META-2:** The metadata fetch must resolve the destination hostname and reject requests targeting RFC 1918 private ranges, loopback (`127.0.0.0/8`), and link-local (`169.254.0.0/16`) addresses to prevent SSRF.
- **FR-META-3:** Metadata fetch timeout: 5 seconds. On timeout or error the link is still saved without metadata, the link creation response is returned normally, no error is surfaced to the caller, and the failure is logged at warning level.

### 5.6 Rate Limiting
- **FR-RATE-1:** Link creation: max 10 requests per hour per IP; return 429 with `Retry-After` header on violation.
- **FR-RATE-2:** Redirect endpoint: no explicit rate limit (protected by cache).
- **FR-RATE-3:** Password unlock: max 5 attempts per IP per alias per 15 minutes; return 429 on violation.

### 5.7 Background Jobs

- **FR-BG-1 — Expired link cleanup:** A scheduled job hard-deletes links whose `expiry_at` timestamp is in the past. Before removing each link from the database, the job evicts the corresponding `alias:{shortId}` entry from Redis to ensure immediate cache consistency (Redis TTL would also eventually evict the entry, but proactive eviction prevents stale hits during the remaining TTL window).
- **FR-BG-2 — Orphaned visit cleanup:** Following expired link deletion, visit records whose `link_id` no longer exists in the `links` table are deleted to maintain referential integrity and prevent unbounded table growth.
- **FR-BG-3 — Visit retention purge:** Visit records older than a configurable retention period (default 90 days) are hard-deleted regardless of link status.
- **FR-BG-4 — Schedule:** Jobs run on configurable intervals defined in application settings. Expired link cleanup (FR-BG-1) and subsequent orphaned visit cleanup (FR-BG-2) run together as a single cycle, defaulting to every hour. Visit retention purge (FR-BG-3) runs as a separate cycle, defaulting to once per day.
- **FR-BG-5 — Batch processing:** Deletions are performed in configurable batch sizes (default 500 rows per run) to avoid long-running transactions and excessive table locking.
- **FR-BG-6 — Implementation:** Each background job is implemented as a .NET `BackgroundService` (hosted service) registered in the DI container. Jobs run independently and do not block application startup or request processing.
- **FR-BG-7 — Resilience:** Any unhandled exception during a job run must be caught and logged with full exception details; the host process must not terminate. The job resumes on its next scheduled interval.

---

## 6. Non-Functional Requirements

### 6.1 Performance
- **NFR-PERF-1:** Redirect response time target: < 100 ms p95 under local development load.
- **NFR-PERF-2:** Alias cache hit rate target: > 95% after warm-up.
- **NFR-PERF-3:** Analytics writes must not add latency to redirect responses (async/fire-and-forget).

### 6.2 Reliability & Availability
- **NFR-REL-1:** The redirect path must degrade gracefully if the analytics write path is unavailable; redirects continue unaffected.
- **NFR-REL-2:** If Redis is unavailable, the system falls back to direct DB lookups for alias resolution; a warning is logged.
- **NFR-REL-3:** The application exposes a health-check endpoint (`GET /health`) reporting status of DB and cache dependencies.

### 6.3 Observability
- **NFR-OBS-1:** Structured logging via Serilog to stdout; log level configurable via environment variable.
- **NFR-OBS-2:** Log entries include correlation ID, endpoint, HTTP status, and duration for every request.
- **NFR-OBS-3:** Errors (5xx) are logged with full exception details and stack trace.

### 6.4 Testability
- **NFR-TEST-1:** Backend unit tests cover all MediatR handlers and FluentValidation validators (xUnit, Moq, AutoFixture, FluentAssertions).
- **NFR-TEST-2:** Frontend component tests use React Testing Library with Vitest; critical user flows (create link, redirect unlock) have integration-level tests.

### 6.5 Maintainability
- **NFR-MAINT-1:** All API routes follow the `/api/v1/` versioning prefix.
- **NFR-MAINT-2:** FluentMigrations manages all schema changes; no manual SQL scripts.
- **NFR-MAINT-3:** No blocking async calls (`.Result`, `.Wait()`); all I/O uses `async`/`await`.

---

## 7. Security Requirements

- **SEC-1 — Input sanitization:** All user-supplied string fields (alias, destination URL) are sanitized server-side to prevent XSS before storage and before rendering in API responses.
- **SEC-2 — SSRF prevention:** Metadata fetch and destination URL validation must resolve the hostname and reject requests targeting RFC 1918 private ranges, loopback (`127.0.0.0/8`), and link-local (`169.254.0.0/16`) addresses.
- **SEC-3 — Password hashing:** Link passwords are stored as bcrypt hashes (cost factor ≥ 12). Plain-text passwords are never logged or returned by any API endpoint.
- **SEC-4 — IP privacy:** IPs are HMAC-SHA-256 hashed with a per-deployment secret before storage. Raw IPs are never persisted.
- **SEC-5 — Content Security Policy:** SPA is served with a strict CSP header; inline scripts are prohibited.
- **SEC-6 — Dependency hygiene:** Third-party packages are pinned to specific versions; known-vulnerable packages must be updated before merging.

---

## 8. Data Model

### 8.1 Core Entities

| Table    | Key Fields |
|----------|-----------|
| `links`  | `id` (GUID PK), `alias` (unique), `destination_url`, `title`, `og_title`, `og_image_url`, `favicon_url`, `password_hash`, `expiry_at`, `created_at`, `row_version` |
| `visits` | `id` (GUID PK), `link_id` (FK → links), `occurred_at`, `hashed_ip`, `referrer_host`, `user_agent`, `country_code` |

### 8.2 Indexes

- Unique index on `links.alias` (case-insensitive collation).
- Index on `links.expiry_at` for efficient expired-link queries by the background cleanup job.
- Index on `visits.link_id, visits.occurred_at DESC` for analytics queries.

### 8.3 Cache Keys

| Key pattern                | Value                    | TTL |
|----------------------------|--------------------------|-----|
| `alias:{shortId}`          | Serialized link metadata | Min(link expiry, 24 h) |
| `rate:create:{ip}`         | Request counter          | 1 h |
| `rate:unlock:{ip}:{alias}` | Failed attempt counter   | 15 min |

---

## 9. API Overview

API management endpoints are prefixed with `/api/v1/`. The redirect endpoint (`/{alias}`) and health check operate at the root path and are not versioned. No authentication is required for any endpoint.

| Method | Path | Description |
|--------|------|-------------|
| `GET`  | `/{alias}` | Redirect to destination (or serve unlock form if password-protected) |
| `POST` | `/api/v1/links` | Create a short link; returns `201` with link details and shortUrl |
| `GET`  | `/api/v1/links/{id}/stats` | Get aggregated visit statistics for a link |
| `POST` | `/api/v1/links/{alias}/unlock` | Submit password for a protected link; returns unlock token |
| `GET`  | `/health` | Health check; reports DB and cache status |

---

## 10. Out of Scope

The following are explicitly excluded from this project:

- User accounts, authentication, and authorization
- Admin panel and moderation tools
- Link editing, disabling, or deletion after creation
- Tags and metadata editing after creation
- Bulk import / export
- API keys
- CI/CD pipelines and production deployments
- Horizontal scaling, DB read replicas, or event streaming (Kafka/RabbitMQ)
- Browser extensions or bookmarklets
- Custom domain mapping
- Push notifications or webhooks
- QR code generation
- Mobile-native clients
- CAPTCHA integration
- Blue-green or canary deployment strategies
