# Design Decisions

This document records specific design decisions made during the development of SampleTwitter API,
along with the reasoning behind them. It also clarifies non-obvious framework concepts that are
tightly coupled to those decisions. The intent is that a future reader — including the author —
can open this file and immediately understand the "why" and "how" of each choice without needing
to trace through the source code or framework internals.

---

## 1. `AppException` — Abstract Base Class for Domain Errors

### The Problem It Solves

In a typical ASP.NET Core controller, when a domain rule is violated (e.g., a user tries to
confirm an already-used token), the naive approach is to return an `IActionResult` directly from
the service layer — but services should not know about HTTP. The other extreme is throwing a raw
`Exception` and losing all context about what HTTP response to produce.

`AppException` sits in between: it is a strongly-typed, domain-aware exception that carries just
enough HTTP metadata so that a single, central exception handler can convert it into a proper HTTP
response — without any controller or service needing to do so manually.

### The Three Properties

```csharp
public abstract class AppException : Exception
{
    public int    StatusCode     { get; }  // (1)
    public string Title          { get; }  // (2)
    public string PublicMessage  { get; }  // (3)
}
```

**`StatusCode` (int)**
The HTTP status code this error maps to (e.g., `400`, `404`, `409`). It is set once in the
concrete subclass constructor and never changes. The central handler (`AppExceptionHandler`) reads
this value and sets `httpContext.Response.StatusCode` before writing the response. This is why no
controller needs a `try/catch` — the handler does the translation automatically.

**`Title` (string)**
A short, human-readable label for the error category, such as `"Invalid token"` or
`"Conflict"`. This maps directly to the `title` field of the RFC 9457 Problem Details response
body. It is intended to be stable and machine-readable enough that a client could branch on it,
but descriptive enough for a developer reading the response in a browser or tool.

**`PublicMessage` (string)**
A sentence-length explanation that is safe to expose to the end user, such as
`"This confirmation link is invalid or has expired."`. Crucially, this is **not** `Exception.Message`.
`Exception.Message` (the base class field, set via `base(internalMessage)`) is reserved for
internal diagnostic detail — it appears in logs — while `PublicMessage` is what the client
actually receives in the `detail` field of the Problem Details body. This separation prevents
accidentally leaking internal implementation details (stack traces, query strings, table names)
to the outside world.

### How a Concrete Subclass Uses It

```csharp
// Exceptions/InvalidTokenException.cs
public class InvalidTokenException : AppException
{
    public InvalidTokenException(string internalMessage)
        : base(
            internalMessage,                                      // -> Exception.Message (logs only)
            "This confirmation link is invalid or has expired.",  // -> PublicMessage (client sees this)
            StatusCodes.Status400BadRequest,                      // -> StatusCode
            "Invalid token")                                      // -> Title
    { }
}
```

The caller throws it with a detailed internal message:

```csharp
throw new InvalidTokenException("Token hash mismatch for userId=42, tokenId=7");
```

The internal message goes to the log. The client receives only the sanitised `PublicMessage`.

### Why Abstract?

`AppException` itself cannot be instantiated — it forces every error category to be its own
named class (`InvalidTokenException`, `ConflictException`, etc.). This means:
- A `catch (AppException)` block (or the handler's `is not AppException` check) covers all
  domain errors in one place.
- Each concrete type can be caught individually when a caller genuinely needs to react
  differently to one specific error.
- The error taxonomy is visible at a glance by looking at the `Exceptions/` folder.

---

## 2. `IProblemDetailsService` — What It Is and Where Response Writing Actually Happens

### High-Level Recap

RFC 9457 ("Problem Details for HTTP APIs") defines a standard JSON shape for error responses:

```json
{
  "type":      "https://tools.ietf.org/html/rfc9457",
  "title":     "Invalid token",
  "status":    400,
  "detail":    "This confirmation link is invalid or has expired.",
  "instance":  "POST /api/account/confirm-email",
  "requestId": "0HN7...",
  "traceId":   "00-4bf9..."
}
```

`IProblemDetailsService` is the ASP.NET Core abstraction responsible for producing and writing
this shape. It is registered automatically when you call `builder.Services.AddProblemDetails(...)`.

### The Exact Moment Response Writing Happens

This is the part that is easy to misunderstand. Trace the call chain precisely:

```
AppExceptionHandler.TryHandleAsync()
    |
    +- httpContext.Response.StatusCode = appException.StatusCode;   <- (A) status line written
    |
    +- _problemDetailsService.TryWriteAsync(new ProblemDetailsContext { ... })
            |
            +- Selects the correct IOutputFormatter based on the request Accept header
            |   (defaults to application/problem+json when no Accept header is present)
            |
            +- Merges the ProblemDetails object you supplied with the customizations
            |   registered in AddProblemDetails() -- this is where instance, requestId,
            |   traceId and timestamp are injected (see Program.cs)
            |
            +- Writes the serialised JSON to httpContext.Response.Body  <- (B) body written
```

Step **(A)** — setting `httpContext.Response.StatusCode` — must happen **before** `TryWriteAsync`
because once the response body starts being written the status line is already flushed to the
network and cannot be changed. This is why `AppExceptionHandler` sets the status code on the line
immediately before calling `TryWriteAsync`, and not inside the `ProblemDetails` object alone
(though it is also set there, for the framework to include it in the JSON body).

Step **(B)** — `TryWriteAsync` — is where bytes actually hit the network socket. The method
returns `true` if it successfully wrote the response, and `false` if it could not (e.g., no
registered writer could handle the requested content type). The `bool` return from `TryWriteAsync`
is forwarded as the return value of `TryHandleAsync`, which tells the exception handling
middleware whether the exception was fully handled.

### What `AddProblemDetails(config => ...)` Does

The lambda passed to `AddProblemDetails` in `Program.cs` registers a **customisation delegate**
that runs inside every `TryWriteAsync` call, just before serialisation. This is where
cross-cutting fields — fields unrelated to any specific error but required on every error
response — are injected:

```csharp
builder.Services.AddProblemDetails(config =>
{
    config.CustomizeProblemDetails = context =>
    {
        // These fields are appended to every Problem Details response,
        // regardless of which exception or handler produced it.
        context.ProblemDetails.Instance =
            $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
        var activity = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity;
        context.ProblemDetails.Extensions.TryAdd("traceId", activity?.Id);
        context.ProblemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;
    };
});
```

`IProblemDetailsService` calls this delegate internally during every `TryWriteAsync`. Neither
`AppExceptionHandler` nor any individual controller needs to know these fields exist — they are
always present on every error response automatically.

### Mental Model

```
[ Your Code ]     throws AppException
                         |
                         v
[ Middleware ]    UseExceptionHandler() catches it, invokes TryHandleAsync()
                         |
                         v
[ Your Handler ]  AppExceptionHandler sets StatusCode, calls TryWriteAsync()
                         |
                         v
[ Framework ]     IProblemDetailsService merges your ProblemDetails with the
                  global customisations, picks a formatter, and writes the
                  JSON body to the response stream.
                         |
                         v
[ Network ]       Client receives a well-formed RFC 9457 JSON response
```

`IProblemDetailsService` is entirely a framework concern. You interact with it only through:
1. `builder.Services.AddProblemDetails(...)` — to register it and inject global fields.
2. `_problemDetailsService.TryWriteAsync(...)` in exception handlers — to pass the error-specific
   fields (status, title, detail) for one particular response.

Everything else — content negotiation, serialisation, merging global fields — is handled
internally by the framework.

---

## 3. Cookie Security Options in `AddCookie`

### Context

The application uses cookie-based authentication. After a user confirms their email, the server
calls `HttpContext.SignInAsync()`, which triggers ASP.NET Core's cookie middleware to create an
encrypted authentication ticket, serialise it, and attach it to the response as a `Set-Cookie`
header. On every subsequent request, the browser sends this cookie back automatically, the
middleware decrypts it, and the user is considered authenticated.

The options inside the `AddCookie(options => { options.Cookie.X = ... })` block control
**who can read, send, or access this cookie**. They are security properties, not functional ones
— they do not change what the cookie contains, only how browsers are permitted to handle it.

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name         = "SampleTwitter.Auth";
        options.Cookie.HttpOnly     = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite     = SameSiteMode.Lax;
        options.ExpireTimeSpan      = TimeSpan.FromDays(14);
        options.SlidingExpiration   = true;
    });
```

### `options.Cookie.HttpOnly = true`

**What it does:** Instructs the browser to block JavaScript from reading this cookie via
`document.cookie`. The cookie is still sent automatically with every HTTP request — JavaScript
just cannot see or touch its value.

**Why it is set:** This is the primary defence against Cross-Site Scripting (XSS). If an
attacker injects malicious JavaScript into the page (e.g., through an unsanitised user input or
a compromised third-party script), that script cannot steal the auth cookie and exfiltrate it to
a remote server. The session cannot be hijacked purely through XSS.

**The trade-off:** The Vue.js client cannot inspect the cookie at all. It cannot determine
whether the user is logged in by checking `document.cookie`. Instead, the Vue app must infer
authentication state by calling a dedicated API endpoint (e.g., `GET /api/account/me`) and
checking whether the response is `200 OK` or `401 Unauthorized`. This is the correct pattern and
is not a practical limitation.

### `options.Cookie.SecurePolicy = CookieSecurePolicy.Always`

**What it does:** Adds the `Secure` attribute to the `Set-Cookie` header. A browser that
receives a cookie with `Secure` set will **only transmit it over HTTPS**. On plain HTTP
requests, the browser silently withholds the cookie entirely.

**Why it is set:** Prevents the auth cookie from traveling in cleartext. If a user is on an
untrusted network (e.g., public Wi-Fi) and somehow reaches an HTTP endpoint, the cookie is never
transmitted — a passive network observer cannot capture it (man-in-the-middle attack prevention).

**Development note:** During local development the API may run over plain HTTP (`http://localhost`).
`CookieSecurePolicy.Always` will cause the browser to refuse to store or send the cookie on
`http://` origins, breaking local testing. The recommended fix is to use `https://localhost`
locally with the ASP.NET Core dev certificate (`dotnet dev-certs https --trust`), so that
`Always` works uniformly across all environments.

### `options.Cookie.SameSite = SameSiteMode.Lax`

**What it does:** Controls whether the browser attaches the cookie to requests that originate
from a **different site** (a different registrable domain). `Lax` is the middle ground between
`Strict` and `None`.

The three values compared:

| Value    | Same-site requests | Cross-site navigations (link clicks, redirects) | Cross-site subresource requests (fetch, XHR, form POST) |
|----------|--------------------|------------------------------------------------|----------------------------------------------------------|
| `Strict` | Sent               | Not sent                                       | Not sent                                                 |
| `Lax`    | Sent               | Sent                                           | Not sent                                                 |
| `None`   | Sent               | Sent                                           | Sent — but requires `Secure` flag                        |

**Why `Lax` is chosen:** It provides strong CSRF (Cross-Site Request Forgery) protection.
A malicious third-party website cannot embed a hidden `<form>` or use `fetch()` to cause the
browser to silently attach the auth cookie to a state-changing API request. At the same time,
`Lax` is less restrictive than `Strict` because the cookie is still sent when a user follows
a direct link to the application from an external page (a common login flow).

**The cross-origin fetch situation with Vue:** When the Vue app (e.g., `http://localhost:5173`)
makes a `fetch()` or `axios` call to the API (e.g., `http://localhost:5042`), the browser
categorises this as a cross-site subresource request — the category where `Lax` does not send
the cookie. In practice this works because:
- Locally, both run on `localhost`, which browsers treat as same-site.
- The Vue app must use `credentials: 'include'` (fetch) or `axios.defaults.withCredentials = true`
  on every request.
- The API must be configured with CORS `AllowCredentials()` and an explicit allowed origin
  (not a wildcard).

In production, if the client and API live on subdomains of the same registrable domain
(`app.mytwitter.com` and `api.mytwitter.com`), they are considered same-site and `Lax` continues
to work without any extra configuration. If they are on entirely different domains, `SameSite=None`
with `Secure` would be required — but this eliminates the CSRF protection that `Lax` provides,
meaning a separate anti-CSRF mechanism would then become necessary.

**CSRF safety under `Lax`:** Because cross-site non-navigational `POST`/`PUT`/`DELETE` requests
do not carry the cookie, no separate CSRF token is required for any state-changing endpoint in
this API, provided that no state-changing actions are accessible via `GET` requests.

---

## 4. `parallelizeTestCollections: false` in `xunit.runner.json`

### What xUnit Parallelism Actually Means

xUnit has two distinct levels of parallelism that are easy to confuse:

| Level | Setting | What it controls |
|---|---|---|
| **Between collections** | `parallelizeTestCollections` | Whether different `[Collection("...")]` groups run concurrently |
| **Within a collection** | `parallelizeAssembly` (not set here) | Whether tests *inside* the same collection run concurrently — **off by default** |

Setting `"parallelizeTestCollections": false` disables the first level. Tests in different
collections will not run concurrently.

### Why This Matters for Integration Tests

All integration test classes inherit `IntegrationTestBase`, which carries `[Collection("Integration")]`.
This means every test in the project belongs to the exact same collection. Because tests within
one collection are always sequential (xUnit's default), the `parallelizeTestCollections: false`
setting has no direct effect on our tests today — they run sequentially regardless.

However, it is essential as a **defensive safeguard** for two concrete reasons:

**Reason 1 — All tests share one database.**
The `ApiWebApplicationFactory` is a `ICollectionFixture<ApiWebApplicationFactory>`, meaning one
factory instance — and therefore one Postgres container, one schema — is shared across every test.
If a second collection were added later without `[Collection("Integration")]`, xUnit would spin
up a second parallel runner. That runner would hit the same database without the Respawn reset
synchronisation, causing data from one test to bleed into assertions of another:

```
[Collection A]               [Collection B — hypothetical]
TestA1: seeds user X         TestB1: seeds user Y
TestA1: asserts user count   TestB1: asserts email count
           ^                            ^
           Both query the same shared Postgres — data bleeds across
```

**Reason 2 — `FakeEmailSender` is a Singleton.**
`FakeEmailSender` is registered as `AddSingleton` inside `ApiWebApplicationFactory.ConfigureWebHost`.
If two collections ran concurrently and both triggered `IEmailSender.Send()`, both would write
to the same `FakeEmailSender` instance. Even with `ConcurrentBag` making the writes safe, the
assertion `Assert.Single(Factory.FakeEmailSender.SentEmails)` in one test could see emails
injected by a concurrent test in the other collection, causing a false failure.

### What Happens If This Setting Is Removed

If `"parallelizeTestCollections": false` is deleted and a second `[Collection]` is added to
the project later, tests become non-deterministic. Some runs pass, some fail depending on
scheduler timing — the hardest class of test failure to diagnose.

Keeping `parallelizeTestCollections: false` makes the intent explicit and prevents this footgun
from being introduced silently.

---

## 5. `FakeEmailSender` — Why `ConcurrentBag` and `IReadOnlyList` at the Same Time

### The Setup

`FakeEmailSender` is the test double for `IEmailSender`. It is registered in the DI container
with `AddSingleton`, which means the **same instance is shared for the entire lifetime of the
`ApiWebApplicationFactory`** — across all tests in the suite. This has one important consequence:
concurrent HTTP requests handled during a single test all call `Send()` on the same object.

### The Write Problem: Why `ConcurrentBag<T>`

`Send()` is called from inside ASP.NET Core's request pipeline. When a test fires two HTTP
requests simultaneously (via `Task.WhenAll`), two thread-pool threads handle those requests
concurrently and both call `Send()` at essentially the same time.

`List<T>` is **not thread-safe for concurrent writes**. Under the hood, `List<T>.Add()` can:
1. Read the current length
2. Resize the internal array if needed
3. Write the new element at `index = length`

If two threads both reach step 1 before either completes step 3, one thread overwrites the
other's element. The list silently loses an item — or throws an `IndexOutOfRangeException` — with
no indication that anything went wrong. This is a data race.

`ConcurrentBag<T>` is part of `System.Collections.Concurrent` and is designed precisely for this
pattern: multiple threads adding items concurrently, with no external locking required. Each
`Add()` is an atomic operation.

### The Read Problem: Why `IReadOnlyList<T>`

`ConcurrentBag<T>` does not implement `IReadOnlyList<T>`. It implements `IEnumerable<T>` and
`IProducerConsumerCollection<T>`. This creates a problem for assertions:

```csharp
// Without the snapshot:
var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
// Assert.Single receives an IEnumerable backed by the live ConcurrentBag.
// If Send() is called on another thread while Assert.Single iterates,
// the enumeration can see a different count than was present when iteration started.
```

The fix is to expose a **snapshot** — a new, stable `List<T>` copied from the bag at the moment
of access:

```csharp
public IReadOnlyList<SentEmail> SentEmails => _sentEmails.ToList();
```

Every time the property is read, `ToList()` enumerates the bag and returns a frozen copy. The
assertion then operates on that frozen list, which cannot change underneath it mid-assertion.
`IReadOnlyList<T>` is the return type because:
- It communicates intent: callers should not modify this collection.
- It provides indexed access (`[0]`, `.Count`) which `IEnumerable<T>` alone does not guarantee.

### Why Not `ConcurrentBag<T>` Directly?

Returning `ConcurrentBag<T>` directly from `SentEmails` would expose the mutable internal state
to test code. Nothing would prevent a test from accidentally calling `.Add()` on it. Returning
`IReadOnlyList<T>` via `.ToList()` gives a snapshot that is both stable and write-protected.

### The `Clear()` Method: Why Replace the Bag

`ConcurrentBag<T>` has no `Clear()` method. The options are:
1. Drain the bag by looping `TryTake()` until empty.
2. Replace the field with a new empty bag.

Option 2 is cleaner and is safe here because `Clear()` is **only ever called from
`IntegrationTestBase.InitializeAsync()`**, which runs in the single-threaded test setup phase
— never concurrently with `Send()`. Replacing the reference is a single atomic pointer
assignment in .NET.

---

## 6. xUnit Lifetime Model — When `InitializeAsync` and `DisposeAsync` Fire

This was the source of the original `Respawner` misconfiguration. Understanding the lifetime
model precisely is essential.

### The Three Layers

There are three distinct layers of xUnit lifetime, each with a different scope:

```
Layer 1: ICollectionFixture<T>    — one instance for the entire test run
Layer 2: IAsyncLifetime on class  — one instance per test class
Layer 3: Constructor/Dispose      — one instance per individual test
```

In this project:

| Layer | Class | Scope |
|---|---|---|
| `ICollectionFixture<ApiWebApplicationFactory>` | `ApiWebApplicationFactory` | Entire test run (all tests in the "Integration" collection) |
| `IAsyncLifetime` on `IntegrationTestBase` | `IntegrationTestBase` | Per individual test |
| `IAsyncLifetime` on `ApiWebApplicationFactory` | `ApiWebApplicationFactory` | Entire test run (same as above — it is itself the fixture) |

### Layer 1 — `ICollectionFixture<ApiWebApplicationFactory>`

`IntegrationTestCollection` declares:

```csharp
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<ApiWebApplicationFactory> { }
```

This tells xUnit: **create exactly one `ApiWebApplicationFactory` and share it across every class
that carries `[Collection("Integration")]`**.

Because `ApiWebApplicationFactory` implements `IAsyncLifetime`, xUnit calls:
- `InitializeAsync()` **once**, before the first test in the collection runs.
- `DisposeAsync()` **once**, after the last test in the collection finishes.

`ApiWebApplicationFactory.InitializeAsync()` does the expensive work:
1. Starts the Postgres Docker container.
2. Runs EF Core migrations.
3. Creates the `Respawner` by inspecting the schema (the slow part).

This runs **exactly once** for the entire test suite. If it ran before every test class, each run
would start a new Docker container and run migrations — unacceptably slow.

### Layer 2 — `IAsyncLifetime` on `IntegrationTestBase`

```csharp
public abstract class IntegrationTestBase : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();  // calls _respawner.ResetAsync()
        Factory.FakeEmailSender.Clear();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}
```

`IntegrationTestBase` is the **base class for every test class** (e.g., `SignUpTests`). xUnit
creates a new instance of each test class for every individual `[Fact]` or `[Theory]` case.
`IAsyncLifetime` on the test class means:
- `InitializeAsync()` is called **after** the constructor but **before** the test method body.
- `DisposeAsync()` is called **after** the test method body but **before** the instance is discarded.

The complete sequence for a single `[Fact]`:

```
1.  new SignUpTests(factory)       ← constructor — DI injects the shared factory
2.  InitializeAsync()              ← ResetDatabaseAsync() + Clear() emails
3.  [Test method body runs]
4.  DisposeAsync()                 ← Client.Dispose()
5.  GC collects the SignUpTests instance
```

This means `ResetDatabaseAsync()` and `FakeEmailSender.Clear()` run **before every single test**,
giving each test a clean slate.

### The Original Misconfiguration — Why It Was Wrong

The original `ResetDatabaseAsync()` called `Respawner.CreateAsync()` every time:

```csharp
// WRONG — original code
public async Task ResetDatabaseAsync()
{
    using var scope = Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

    await using var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    // This was called before EVERY test:
    var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions { ... });
    await respawner.ResetAsync(connection);
}
```

`Respawner.CreateAsync()` queries `information_schema` to build a dependency-ordered list of
every table in the schema. This schema introspection is the expensive part — it is designed to be
called **once** and the resulting `Respawner` instance reused for all subsequent resets.

By calling `CreateAsync()` before every test, the schema was re-introspected for every single
`[Fact]`. With 16 tests, this was 16 schema introspections instead of 1. The fix was to create
the `Respawner` during `ApiWebApplicationFactory.InitializeAsync()` (Layer 1 — once) and store
it as a field, then only call `_respawner.ResetAsync()` in `ResetDatabaseAsync()` (Layer 2 —
once per test).

```csharp
// CORRECT — current code
// In ApiWebApplicationFactory.InitializeAsync() — runs ONCE:
_respawner = await Respawner.CreateAsync(connection, new RespawnerOptions { ... });

// In ResetDatabaseAsync() — runs before EVERY test, but cheaply:
await _respawner.ResetAsync(connection);
```

### Full Timeline for the Entire Test Run

```
╔══════════════════════════════════════════════════════════════════╗
║  ApiWebApplicationFactory.InitializeAsync()   [runs ONCE]       ║
║  - Postgres container started                                    ║
║  - EF Core migrations applied                                    ║
║  - Respawner created (schema introspected)                       ║
╠══════════════════════════════════════════════════════════════════╣
║  For each [Fact] / [Theory case]:                                ║
║  ┌─────────────────────────────────────────────────────────────┐ ║
║  │  new SignUpTests(factory)   [constructor]                   │ ║
║  │  IntegrationTestBase.InitializeAsync()                      │ ║
║  │    - _respawner.ResetAsync()   ← wipes all rows             │ ║
║  │    - FakeEmailSender.Clear()   ← empties sent emails        │ ║
║  │  [Test method body]                                         │ ║
║  │  IntegrationTestBase.DisposeAsync()                         │ ║
║  │    - Client.Dispose()                                       │ ║
║  └─────────────────────────────────────────────────────────────┘ ║
║  (repeated for every test in sequence)                           ║
╠══════════════════════════════════════════════════════════════════╣
║  ApiWebApplicationFactory.DisposeAsync()      [runs ONCE]       ║
║  - Postgres container stopped and removed                        ║
╚══════════════════════════════════════════════════════════════════╝
```

### Key Rules to Remember

1. `ICollectionFixture<T>` lifetime = **the entire test run**. Use it for anything expensive to
   create: Docker containers, database migrations, `Respawner` creation.

2. `IAsyncLifetime` on a test class = **per individual test**. Use it for cleanup that must
   happen before every test: resetting state, clearing fakes.

3. The constructor of a test class also runs **per individual test**. Use it only for
   dependency injection — never for async work (constructors cannot be `async`).

4. The shared fixture is injected through the constructor parameter. It is the same object
   instance for every test in the collection. Any mutable state on it (like `FakeEmailSender`)
   must either be reset per-test (via `InitializeAsync`) or be thread-safe.