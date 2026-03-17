# URL Shortener Implementation Backlog

## Scope Decisions (Locked)
- Backend is split into separate projects from day one.
- Visit recording is implemented with an in-process background queue.
- Frontend delivery starts with MVP flows only (create link, unlock, stats), with UI polish deferred.

## Ordered Tasks
1. [x] Create backend skeleton (separate projects + solution wiring)
2. [x] Create front-end skeleton (React + TypeScript + Vite MVP shell)
3. [ ] Define database schema and indexes with FluentMigrations (`links`, `visits`, alias unique index, expiry index, analytics index, `row_version`)
4. [ ] Configure core backend infrastructure (SQL Server EF Core, Redis `IDistributedCache`, MediatR, FluentValidation pipeline behavior, Serilog, health checks)
5. [ ] Implement link creation command and endpoint (`POST /api/v1/links`) with validation, Base62 alias generation, collision retries, and 409 conflict alternatives
6. [ ] Implement destination URL validation and SSRF protections (scheme + resolved host restrictions for private/loopback/link-local ranges)
7. [ ] Implement metadata prefetch during link creation (title/OpenGraph/favicon, 5s timeout, warning-only failure path)
8. [ ] Implement redirect flow (`GET /{alias}`) with cache-first lookup, DB fallback, cache repopulation, expiry handling, and 302/404 behavior
9. [ ] Implement asynchronous visit recording via in-process background queue (fire-and-forget from redirect path)
10. [ ] Implement password-protected link flow (`GET /{alias}` unlock response + `POST /api/v1/links/{alias}/unlock` with bcrypt verification, HMAC token issuance, token-based redirect completion)
11. [ ] Implement rate limiting policies (create: per-IP hourly, unlock: per-IP-per-alias 15-minute window) including `Retry-After` on 429
12. [ ] Implement stats endpoint (`GET /api/v1/links/{id}/stats`) with total visits, unique visitors (hashed IP), last-30-days series, and top referrers
13. [ ] Implement worker cleanup jobs (expired links + Redis eviction, orphaned visits, retention purge) with batching, schedule config, and exception resilience
14. [ ] Implement global exception handling and API response mapping to allowed status codes (200, 201, 204, 400, 404, 409, 429, 500)
15. [ ] Build frontend MVP create-link flow (form, validation, conflict suggestions, short URL output, copy action)
16. [ ] Build frontend MVP unlock and stats flows with React Query + typed Axios service layer and explicit loading/error/success states
17. [ ] Add automated tests (backend handlers/validators and frontend component/flow tests with Vitest + RTL)
18. [ ] Final hardening and documentation sync (README setup steps, config defaults, SRS traceability check, minimal polish)
