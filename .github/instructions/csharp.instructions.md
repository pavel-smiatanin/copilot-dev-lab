---
applyTo: "**/*.cs"
---

# C# Coding Instructions

## File and Project Organization

- One class (or interface, enum, record) per file; filename must match the type name exactly.
- Namespace must reflect the folder path relative to the project root (e.g., a file at `src/UrlShortener.Application/Commands/CreateLinkCommand.cs` uses namespace `UrlShortener.Application.Commands`).
- Place `using` directives at the top of the file, outside the namespace and ordered in alphabetical order. Remove unused usings.
- Use file-scoped namespace declarations (`namespace Foo.Bar;`) rather than block-scoped namespaces.
- Enable nullable reference types in every file: `#nullable enable` (or set `<Nullable>enable</Nullable>` globally in the `.csproj`).
- Every `.csproj` file must include `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in a `PropertyGroup`.

## Directory.Build.props

A shared `Directory.Build.props` at `backend/` applies to all projects in the solution:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|AnyCPU'">
    <TreatWarningsAsErrors>True</TreatWarningsAsErrors>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|AnyCPU'">
    <TreatWarningsAsErrors>True</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Key implications:
- **Do not** repeat `<TargetFramework>`, `<ImplicitUsings>`, `<Nullable>`, or `<TreatWarningsAsErrors>` in individual `.csproj` files — they are inherited globally.
- **`RestorePackagesWithLockFile=true`**: After adding or updating any `<PackageReference>`, run `dotnet restore` to regenerate the `packages.lock.json` file. Commit the updated lock file alongside the `.csproj` change.
- A second `Directory.Build.props` at `backend/tests/` inherits the root props and adds test-specific packages and settings (xUnit, FluentAssertions, Moq, AutoFixture, coverlet).

## Naming Conventions

| Symbol | Convention | Example |
|---|---|---|
| Namespaces | PascalCase | `UrlShortener.Application.Links` |
| Classes, structs, records | PascalCase; do not use postfix like Dto | `CreateLinkCommand`, `Link` |
| Interfaces | `I` + PascalCase | `ILinkResolver`, `IUnitOfWork` |
| Methods | PascalCase | `ResolveAliasAsync` |
| Properties | PascalCase | `DestinationUrl`, `ExpiresAt` |
| Events | PascalCase | `LinkCreated` |
| Constants | PascalCase | `MaxAliasLength` |
| Private / protected fields | `_camelCase` | `_dbContext`, `_cache` |
| Local variables | camelCase | `shortId`, `destinationUrl` |
| Method parameters | camelCase | `cancellationToken`, `alias` |
| Type parameters | `T` prefix or descriptive | `TRequest`, `TResponse` |
| Async methods | `Async` suffix and must accept a `CancellationToken` parameter | `GetLinkByAliasAsync` |
| Boolean variables/properties | Positive affirmative | `expired`, `enabled`, `hasPassword` |
| Test classes | `{ClassUnderTest}Tests` | `CreateLinkCommandHandlerTests` |
| Test methods | `{Method}_{Scenario}_{ExpectedResult}` | `Handle_WhenAliasConflicts_Returns409` |

## Code Style

### Variables and Types
- Use `var` only when the type is unambiguously evident from the right-hand side (e.g., `var link = new Link()`). Prefer explicit types in all other cases.
- Prefer target-typed `new` for brevity when the variable type is already declared: `Link link = new(alias, destinationUrl)`.
- Use primary constructors for simple classes and records where they reduce boilerplate.
- Prefer `record` types for immutable data transfer objects and value objects; prefer `class` for mutable entities and services.

### Control Flow
- Always use curly braces `{}`, even for single-line `if`, `else`, `for`, `foreach`, `while`, `using` blocks — no exceptions.
- Prefer early returns and guard clauses to reduce nesting:
  ```csharp
  // Preferred
  if (link is null) 
  { 
    return Result.NotFound(); 
  }
  // ... rest of method
  
  // Avoid
  if (link is not null)
  {
      // ... deeply nested logic
  }
  ```
- Use `switch` expressions over `switch` statements for exhaustive pattern matching.
- Avoid negated conditions in `if` statements when a positive form is available and equally readable.

### Null Handling
- Nullable reference types are enabled globally — treat every compiler warning as an error.
- Use null-conditional (`?.`) and null-coalescing (`??`, `??=`) operators instead of explicit null checks where appropriate.
- Use `ArgumentNullException.ThrowIfNull(param)` for public API parameter validation instead of manual null checks.
- Never suppress nullable warnings with `!` (null-forgiving operator) without a comment explaining why the value is guaranteed non-null.

### Async / Await
- All I/O-bound operations must use `async`/`await`. Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` — these cause deadlocks in ASP.NET Core.
- Every async method must accept a `CancellationToken` parameter; it must **not** have a default value.
- Pass `CancellationToken` through to all downstream async calls. Never discard it.
- Do not use `async void` except for event handlers.
- Prefer `await using` for `IAsyncDisposable` resources.
- Name async methods with the `Async` suffix: `GetLinkAsync`, not `GetLink`.

```csharp
// Correct
public async Task<Link?> GetLinkByAliasAsync(string alias, CancellationToken cancellationToken)
{
    return await _dbContext.Links
        .FirstOrDefaultAsync(l => l.Alias == alias, cancellationToken);
}
```

### Dependency Injection
- Inject dependencies through the constructor; avoid service locator (`IServiceProvider`) in application logic.
- Declare injected fields as `private readonly`.
- Do not inject concrete implementations — inject abstractions (interfaces).
- Register services with the appropriate lifetime: `Singleton` for stateless services, `Scoped` for per-request/per-unit-of-work, `Transient` for lightweight stateless utilities.

### Immutability and Safety
- Prefer `readonly` fields over properties backed by mutable fields where a value is set once.
- Use `IReadOnlyList<T>` / `IReadOnlyCollection<T>` for return types where the caller must not modify the collection.
- Use `IEnumerable<T>` for parameters that only need to be iterated.
- Avoid exposing `List<T>` or arrays directly as public properties.

## LINQ

- Prefer method syntax over query syntax for consistency.
- Never call `.ToList()` or `.ToArray()` prematurely — materialize only when necessary (e.g., before returning from a repository, or when the query would be executed multiple times).
- Avoid `Count()` when checking existence; use `Any()`.
- Avoid `First()` unless you are certain the sequence is non-empty; use `FirstOrDefault()` and handle null explicitly.
- Do not mix EF Core LINQ queries with in-memory operations in the same expression chain; materialize first if you need client-side logic.

## Exception Handling

- Do not use exceptions for expected control flow (e.g., alias not found). Use result objects or nullable returns.
- Catch specific exception types, never bare `catch (Exception)` unless re-throwing or logging at a top-level handler.
- Always log exceptions with full detail (message + stack trace) before swallowing or re-wrapping.
- Use domain-specific exceptions (`LinkNotFoundException`, `AliasConflictException`) at the application boundary; map them to HTTP status codes in a global exception handler — never in controllers.
- Do not expose internal exception details in API responses.

## Records and DTOs

- Use `record` for all DTOs, request/response shapes, and CQRS commands/queries — they are immutable by default and provide value equality.
- Do not add behaviour (business logic) to DTOs.
- Name response DTOs without a `Dto` suffix: `Link`, `Stats`.
- Name MediatR commands with a `Command` suffix and queries with a `Query` suffix: `CreateLinkCommand`, `GetLinkStatsQuery`.

## MediatR / CQRS Patterns

- Every command or query is a `record` implementing `IRequest<TResponse>` or `IRequest`.
- Handlers are `sealed` classes implementing `IRequestHandler<TRequest, TResponse>`; they are registered via MediatR's assembly scanning.
- Handlers must be stateless — no mutable instance fields beyond injected read-only dependencies.
- Business logic lives in handlers using the Transaction Script pattern; do **not** add it to controllers, entities, or DTOs.
- Validators are `record`-less `class`es inheriting `AbstractValidator<TRequest>`, co-located with their request class.
- The `ValidationBehavior<TRequest, TResponse>` pipeline behavior runs validators before the handler; on failure it throws `ValidationException`.

```csharp
// Command
public sealed record CreateLinkCommand(
    string DestinationUrl,
    string? CustomAlias,
    DateTime? ExpiresAt,
    string? Password) : IRequest<CreateLinkResult>;

// Handler
public sealed class CreateLinkCommandHandler : IRequestHandler<CreateLinkCommand, CreateLinkResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IDistributedCache _cache;

    public CreateLinkCommandHandler(AppDbContext dbContext, IDistributedCache cache)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<CreateLinkResult> Handle(CreateLinkCommand request, CancellationToken cancellationToken)
    {
        // ... Transaction Script logic
    }
}
```

## EF Core

- Inject `DbContext` directly into handlers; do **not** wrap it in a repository — the `DbContext` is the Unit of Work.
- Never call `SaveChangesAsync` in more than one place within a single request handler.
- Use the `[Timestamp]` attribute (or `IsRowVersion()` Fluent API) on every entity for optimistic concurrency.
- All primary keys are GUIDs (`Guid`), generated application-side with `Guid.NewGuid()` or `Guid.CreateVersion7()`.
- Configure the data model in `IEntityTypeConfiguration<T>` classes, not in `OnModelCreating` directly.
- Never use lazy loading — always use explicit `.Include()` / `.ThenInclude()` or projection.
- Prefer `AsNoTracking()` for read-only queries that do not mutate entities.

## Controllers (ASP.NET Core)

- Controllers are thin adapters — they must not contain business logic, validation logic, or direct data access.
- Each action method does exactly one thing: call `_mediator.Send(...)` and return the mapped HTTP result.
- Return `IActionResult` or `ActionResult<T>` (never raw `T`) to enable proper status code semantics.
- Use `[ProducesResponseType]` attributes for Swagger documentation.
- Use `[ApiController]` on every controller — this enables automatic 400 responses for model binding failures and enforces `[FromBody]` inference.

```csharp
[ApiController]
[Route("api/v1/links")]
public sealed class LinksController : ControllerBase
{
    private readonly IMediator _mediator;

    public LinksController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [ProducesResponseType(typeof(CreateLinkResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLink(
        [FromBody] CreateLinkRequest request,
        CancellationToken cancellationToken)
    {
        CreateLinkResult result = await _mediator.Send(request.ToCommand(), cancellationToken);
        return CreatedAtAction(nameof(GetStats), new { id = result.Id }, result.ToResponse());
    }
}
```

## Logging (Serilog)

- Inject `Serilog.ILogger` (not `Microsoft.Extensions.Logging.ILogger`) into handlers and services that require logging.
- Use structured logging with named placeholders — never string interpolation:
  ```csharp
  // Correct
  _logger.Information("Link created {Alias} -> {DestinationUrl}", alias, destinationUrl);
  
  // Wrong — destroys structured data
  _logger.Information($"Link created {alias} -> {destinationUrl}");
  ```
- Log at appropriate levels: `Debug` for verbose diagnostics, `Information` for normal operations, `Warning` for recoverable failures (e.g., metadata fetch timeout), `Error` for unexpected failures.
- Never log sensitive data: passwords, raw IPs, tokens, or personal information.
- Always include a correlation ID in log context for every request (set via middleware).

## Unit Testing

- Test file mirrors the source structure: `UrlShortener.Application.Tests/Links/Commands/CreateLinkCommandHandlerTests.cs`.
- Use the **AAA** pattern (Arrange / Act / Assert) with a blank line between each section.
- Use **AutoFixture** to generate test data; use `[AutoData]` or `[InlineAutoData]` theory attributes.
- Use **Moq** for mocking dependencies; prefer `Mock<T>.Object` injection over manual stub implementations.
- Use **FluentAssertions** (≤ 7.x) for assertions — never `Assert.Equal` directly.
- Mock `DbContext` using an in-memory provider or Moq; do not test EF Core internals.
- Test names follow: `{MethodUnderTest}_{Scenario}_{ExpectedOutcome}`.

```csharp
[Theory, AutoData]
public async Task Handle_WhenAliasIsAvailable_CreatesLinkAndReturns201(
    CreateLinkCommand command,
    Mock<AppDbContext> dbContextMock,
    Mock<IDistributedCache> cacheMock)
{
    // Arrange
    var handler = new CreateLinkCommandHandler(dbContextMock.Object, cacheMock.Object);

    // Act
    CreateLinkResult result = await handler.Handle(command, CancellationToken.None);

    // Assert
    result.Alias.Should().NotBeNullOrWhiteSpace();
    result.ShortUrl.Should().StartWith("http");
}
```

## Security

- Never log plaintext passwords, tokens, raw IP addresses, or any personally identifiable information.
- Always hash passwords with BCrypt at cost factor ≥ 12; never store or compare plaintext passwords.
- Hash IP addresses using HMAC-SHA-256 with a per-deployment secret before persisting to the database.
- Validate all destination URLs: allow only `http` and `https` schemes; resolve the hostname and reject RFC 1918, loopback, and link-local addresses (SSRF prevention).
- Sanitize all user-supplied strings before storage to prevent XSS.
- Use `CancellationToken` to avoid holding resources (DB connections, HTTP clients) longer than necessary.
- Never disable SSL certificate validation in `HttpClient` configuration.
- Do not catch `OperationCanceledException` silently — let it propagate or log at `Debug` level.

## Miscellaneous

- Avoid static mutable state; it breaks testability and thread safety.
- Do not use `Thread.Sleep` — use `Task.Delay` with a `CancellationToken`.
- Prefer `DateTimeOffset.UtcNow` over `DateTime.Now` or `DateTime.UtcNow` for all timestamp storage and comparison.
- Use `TimeProvider` (injectable) rather than `DateTimeOffset.UtcNow` directly in handlers and services, to enable deterministic testing.
- Validate constructor parameters with `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace`.
- Do not use `#region` directives — they hide complexity; refactor instead.
- Keep methods short and focused (Single Responsibility). If a method exceeds ~30 lines, consider extracting private helpers.
