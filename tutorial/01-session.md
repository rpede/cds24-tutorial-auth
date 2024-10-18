# Tutorial Session

## What are JWT?

JWT is short for [JSON Web Token](https://en.wikipedia.org/wiki/JSON_Web_Token).
It is a token with a specific format.
A JWT consists of 3 parts.

1. **Header** JSON object indicating the signature algorithm.
2. **Payload** JSON object with claims (commonly user ID and role).
3. **Signature** a digital signature of the header and payload.

All 3 parts are [Base64](https://en.wikipedia.org/wiki/Base64) encoded and
separated by a `.` (dot).
Base64 is **_not_** encryption!

Hers is an example of a JWT:

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
```

[jwt.io](https://jwt.io/) is a useful tool when debugging code using JWTs.
You can paste a JWT into the site to see the decoded parts.

The signature is a keyed-hash over the header and payload.
It protects the integrity of the JWT.
If claims in the payload gets manipulated then the signature will no longer
match and the manipulation can therefore be detected.
This protection mechanism relies on only the server knowing the secret used to
generate the signature.

When using JWTs.
The server will issue a JWT after successful authentication.
The client will then include the JWT in `Authorization` HTTP header.

```
GET /admin-panel HTTP/1.1
Host: example.com
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
```

When used this way it might also be referred to as a bearer token, since the
word "Bearer" appears before the token.
A bearer token can have other formats, it doesn't have to be a JWT.

<dl>
  <dt>Bearer token</dt>
  <dd>How the token is supplied in HTTP requests.</dd>
  <dt>JWT</dt>
  <dd>How the token is structured.</dd>
</dl>

## Cookie vs Bearer token

The big difference between cookies and bearer tokens, is that cookies are
included automatically in all request by the browser.
Whereas bearer tokens are set by JavaScript in the client app.
Each approach has its pros and cons.

In this application, the built-in cookie mechanism would be just fine.
However, since using JWT as bearer tokens are so widely used these days, you
will need to learn how they work and how to use them.

Here are a comparison between the two approaches:

- **Cookies** are automatically managed by the browser.
- **Bearer tokens** are managed by client-side code.
  Meaning that the front-end developer is responsible managing the token.
- **Cookie** based authorization can be susceptible to
  [CSRF](https://en.wikipedia.org/wiki/Cross-site_request_forgery).
- **Bearer tokens** allow for cross-origin authorization.
  Cookies don't.
- **Bearer tokens** can work better for native applications such as
  desktop and mobile apps.

[Advanced reading](https://auth0.com/blog/cookies-tokens-jwt-the-aspnet-core-identity-dilemma/)

Imagine you have your front-end being served from my-awesome-frontend.com and your back-end from my-awesome-backend.com.
This scenario is trivial with bearer tokens.
But doesn't really work with cookies.
There is a workaround though.
You can just have the client app send all requests to front-end web-server,
then have it rewrite requests intended for back-end.

## Server implementation

JWTs in our application will have their integrity protected by a digital
signature.
However, they are not encrypted.
It means that if somebody gets a hold of a JWT they can see what data it
contains, including when it was issued, user ID, roles etc.
What they can not do, is change the token without breaking the signature.

Our setup we will use a symmetric key for the signature.
Meaning, same key that was used to issue the JWT is also used verify it.

While developing, the JwtSecret in
[appsettings.Development.json](server/Api/appsettings.Development.json) will be
used to issue and verify JWTs.

**Same secret shall NEVER be used when deployed to production!**

To use JWT in the back-end, we need to add a couple of packages.

```sh
dotnet add server/Api package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.7

dotnet add server/Service package Microsoft.IdentityModel.JsonWebTokens --version 8.0.1
```

The **JwtBearer** package is used to extract session information from the
bearer token header.
**JsonWebTokens** provides classes for working with JWTs.

ASP.NET represents information about an authenticated user as claims through
the ClaimsPrincipal and ClaimsIdentity classes.
The domain model in this application use User and Role classes instead.
So there is a mismatch.
Therefore, we need a way to convert between the two representations.
We can add some [extensions
methods](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods)
to do the conversion.

Create a new folder named `Security` in `server/Service`.
Then create `server/Service/Security/ClaimExtensions.cs` with:

```cs
using System.Security.Claims;
using DataAccess.Entities;

namespace Service.Security;

public static class ClaimExtensions
{
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)!.Value;

    public static IEnumerable<Claim> ToClaims(this User user, IEnumerable<string> roles) =>
        [
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            .. roles.Select(role => new Claim(ClaimTypes.Role, role))
        ];

    public static IEnumerable<Claim> ToClaims(this User user, params string[] roles) =>
        ToClaims(user, roles.AsEnumerable());

    public static ClaimsPrincipal ToPrincipal(this User user, IEnumerable<string> roles) =>
        new ClaimsPrincipal(new ClaimsIdentity(user.ToClaims(roles)));

    public static ClaimsPrincipal ToPrincipal(this User user, params string[] roles) =>
        new ClaimsPrincipal(new ClaimsIdentity(user.ToClaims(roles.AsEnumerable())));
}
```

Next, we need a service to issue and validate JWTs.

Add the following to the files:

`server/Service/Security/ITokenClaimsService.cs`

```cs
namespace Service.Security;

public interface ITokenClaimsService
{
    Task<string> GetTokenAsync(string userName);
}
```

And:

`server/Service/Security/JwtTokenClaimService.cs`

```cs
using System.Security.Claims;
using DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Service.Security;

namespace Service.Security;

public class JwtTokenClaimService : ITokenClaimsService
{
    public const string SignatureAlgorithm = SecurityAlgorithms.HmacSha512;

    private readonly AppOptions _options;
    private readonly UserManager<User> _userManager;

    public JwtTokenClaimService(IOptions<AppOptions> options, UserManager<User> userManager)
    {
        _options = options.Value;
        _userManager = userManager;
    }

    public async Task<string> GetTokenAsync(string userName)
    {
        var user = await _userManager.FindByNameAsync(userName)
            ?? throw new NotFoundError(nameof(User), new { Username = userName });
        var roles = await _userManager.GetRolesAsync(user);

        var key = Convert.FromBase64String(_options.JwtSecret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SignatureAlgorithm
            ),
            Subject = new ClaimsIdentity(user.ToClaims(roles)),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = _options.Address,
            Audience = _options.Address,
        };
        var tokenHandler = new JsonWebTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return token;
    }

    public static TokenValidationParameters ValidationParameters(AppOptions options)
    {
        var key = Convert.FromBase64String(options.JwtSecret);
        return new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidAlgorithms = [SignatureAlgorithm],

            // These are important when we are validating tokens from a
            // different system
            ValidIssuer = options.Address,
            ValidAudience = options.Address,

            // Set to 0 when validating on the same system that created the token
            ClockSkew = TimeSpan.FromSeconds(0),

            // Default value is true already.
            // They are just set here to emphasis the importance.
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
        };
    }
}
```

The `GetTokenAsync` method is used to issue new tokens.
`TokenValidationParameters` is used to tell Identity how to validate them.

Tokens can be issued by one system and consumed by another (audience).
Similar to what happens when you login to a service with your Google or
Facebook account.

Here `Issuer` and `Audience` is the same, because we are going to both issue
the token and consume it (audience) from the same system.

`Expires` is when the token is valid until.
This is for security reasons.
If someone malicious manage to steal the token it would be nice, if it had
already expired when they try to use it.

`Subject` is the payload of the token.
We can store whatever information we want in the claims.
But we should stay away from storing secret information such as password in it,
since it isn't encrypted.
Most applications will store user ID and roles as claims.

We get the signing-key from `AppOptions.JwtSecret`.
This is loaded from `appsettings.json` or `appsettings.Development.json` by the
configuration system.
You can see it in the `Configuration` region in the top of
[Program.cs](server/Api/Program.cs).

To make use of JWT Bearer for authentication, we need to configure it in
[Program.cs](server/Api/Program.cs).
Add the following lines inside `#region Security`.

```cs
var options = builder.Configuration.GetSection(nameof(AppOptions)).Get<AppOptions>()!;
builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = JwtTokenClaimService.ValidationParameters(options);
    });
builder.Services.AddScoped<ITokenClaimsService, JwtTokenClaimService>();
```

The application will now accept JWT bearer.
There is one slight issue though.
Users login via SignInManager, which still use cookies.

Therefore, the `Login` method in
[AuthController.cs](server/Api/Controllers/AuthController.cs) needs to be
changed to issue a JWT:

```cs
[HttpPost]
[Route("login")]
public async Task<LoginResponse> Login(
    [FromServices] UserManager<User> userManager,
    [FromServices] IValidator<LoginRequest> validator,
    [FromServices] ITokenClaimsService tokenClaimsService,
    [FromBody] LoginRequest data
)
{
    await validator.ValidateAndThrowAsync(data);
    var user = await userManager.FindByEmailAsync(data.Email);
    if (user == null || !await userManager.CheckPasswordAsync(user, data.Password))
    {
        throw new AuthenticationError();
    }

    var token = await tokenClaimsService.GetTokenAsync(data.Email);

    return new LoginResponse(Jwt: token);
}
```

Notice that if the credentials are invalid (email or password is wrong) we
respond the same way.
We don't want to help attackers guess the correct combination for credentials.

We also need to add a field for the JWT in `LoginResponse`.
Open `server/Service/Auth/Dto/Response.cs` and change it to:

```cs
public record LoginResponse(string Jwt);
```

We are using a
[record](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
instead of plain-old-C#-class (POCO) to save some typing.
See the linked article if you haven't used records in C# before.

What the code does.

1. Verify supplied credentials from `LoginRequest`
   - If they are not valid, throw `AuthenticationError`
2. Issue a JWT containing user ID and roles as claims.
3. Return the token to client in `LoginResponse`

## Client implementation

When the client has called the login endpoint with correct credentials, it will
receive a token in response body.
The client needs to hold on to the token, so it can be attached to upcoming
requests.
Otherwise, the user would have to re-authenticate themselves all the time,
which would be silly.

Cookies are automatically included in requests by the browser.
But, with bearer tokens, we need to write code to include them.

### Token storage

We need to be able to access the token across components, because we have
different pages that make requests to the server.
They all need to include the token in their requests.

You could just use an atom from Jotai.
But, then the user would have to re-authenticate if they reload the page or
open it in a new tab.
Instead of using a plain atom, we can use an
[atomWithStorage](https://jotai.org/docs/utilities/storage).

A `atomWithStorage` stores a value in the browser meaning it can persist across
multiple tabs.
Each tab you open (even if the URL is the same) is a new instance of the
client.

Browsers have two storage mechanisms that can be used to store key/value pairs.
They are
[localStorage](https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage)
and
[sessionStorage](https://developer.mozilla.org/en-US/docs/Web/API/Window/sessionStorage).
The difference is that:

- **sessionStorage** is cleared when the browser is closed.
- **localStorage** is only cleared when user clears the browser data.

A `atomWithStorage` can use either storage mechanism.
Which one to use depends on your security requirements.
For high-secure applications such as online-banking, you defiantly want to go
with `sessionStorage`.
Most social-networks will use `localStorage`, since it is more convenient for
the user, so they don't have to sign-in each time they open a browser.

`localStorage` would be fine for a blog application like this example project.
But, since I'm supposed to teach you about security we will go with
`sessionStorage`.

Replace `client/src/atoms/auth.ts` with:

```ts
import { useNavigate } from "react-router-dom";
import { atom, useAtom } from "jotai";
import { AuthUserInfo } from "../api";
import { http } from "../http";
import { atomWithStorage, createJSONStorage } from "jotai/utils";

// Storage key for JWT
export const TOKEN_KEY = "token";
export const tokenStorage = createJSONStorage<string | null>(
  () => sessionStorage,
);

const jwtAtom = atomWithStorage<string | null>(TOKEN_KEY, null, tokenStorage);

const userInfoAtom = atom(async (get) => {
  // Create a dependency on 'token' atom
  const token = get(jwtAtom);
  if (!token) return null;
  // Fetch user-info
  const response = await http.authUserinfoList();
  return response.data;
});

export type Credentials = { email: string; password: string };

type AuthHook = {
  user: AuthUserInfo | null;
  login: (credentials: Credentials) => Promise<void>;
  logout: () => void;
};

export const useAuth = () => {
  const [_, setJwt] = useAtom(jwtAtom);
  const [user] = useAtom(userInfoAtom);
  const navigate = useNavigate();

  const login = async (credentials: Credentials) => {
    const response = await http.authLoginCreate(credentials);
    const data = response.data;
    setJwt(data.jwt!);
    navigate("/");
  };

  const logout = async () => {
    setJwt(null);
    navigate("/login");
  };

  return {
    user,
    login,
    logout,
  } as AuthHook;
};
```

Take a moment to understand the code.
Notice that with bearer tokens, we simply log-out by "forgetting" the token.

### Include token in requests

We don't want to write the same code to include token each time we make an HTTP
request.
It would be nice to have it all in one place.

The API client we scaffold from swagger definitions uses
[Axios](https://axios-http.com/) to make the requests.
With Axios, we can attach something called an interceptor.
Interceptors are kinda like middleware in ASP.NET.
Meaning, some code that executes for each HTTP requests.

Replace the file `client/src/http.ts` with:

```ts
import { Api } from "./api";
import { tokenStorage, TOKEN_KEY } from "./atoms/auth.ts";

// URL prefix for own server
// This is to protect us from accidently sending the JWT to 3rd party services.
const AUTHORIZE_ORIGIN = "/";

const _api = new Api();

_api.instance.interceptors.request.use((config) => {
  // Get the JWT from storage.
  const jwt = tokenStorage.getItem(TOKEN_KEY, null);
  // Add Authorization header if we have a JWT and the request goes to our own
  // server.
  if (jwt && config.url?.startsWith(AUTHORIZE_ORIGIN)) {
    // Set Authorization header, so server can tell hos is logged in.
    config.headers.Authorization = `Bearer ${jwt}`;
  }
  return config;
});

// Expose API-client which will handle authorization.
export const http = _api.api;
```

Notice that we import `tokenStorage` from the other file.
This allows us to have the storage mechanism defined in a single place.

**Important:** the bearer tokens should only be added to requests for our own
back-end.
If we happened to use 3rd party APIs in the app, we don't want those 3rd
parties to be able to steal the tokens.
Because that would allow them to make requests on behalf of our users.

That is what the `AUTHORIZE_ORIGIN` check is to protect against.

Here is what could happen without it.
Say you have an app that use a 3rd party API.
That 3rd party API gets hacked.
An administrator logs in to your app.
Without the origin check, the client will send the JWT to the hacked 3rd party.
Meaning the administrator account in your app will get hacked too.

## Outro

That's it.
Try it out!

Make sure you have both client and server running.
Then login at <http://localhost:5173/login>.

Inspect the page and see if you can spot the JWT.

![JWT in sessionStorage](./jwt-in-storage.png)

![JWT in header](./jwt-in-header.png)

Using JWT bearer is a bit more involved than cookies, as you can see.
But, they are getting popular.
So it is important to know how they work.

The reason they are popular is that they can be used to seamless transition
user claims between increasing complex and interconnected infrastructure.

There is one issue we haven't tackled here.
That is that (at some point) the JWT will expire.
When it happens then the user will no longer be authorized.
To fix it, you would have to write code that periodically renews the token.
It can be a bit involved, so I excluded it here to keep it short.

[Reference solution](https://github.com/rpede/cds24-tutorial-auth/tree/01-session)