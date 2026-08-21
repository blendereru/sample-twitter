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