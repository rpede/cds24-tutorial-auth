# Authorization

Authentication on its own does not make a system secure.
When we authenticate a user, it just means that we know their identity.

Identity is not part of the CIA-triad.

![CIA triad](./cia-triad.drawio.png)

But confidentiality and integrity is.
Both goals have to do with authorization.
To determine whether someone (or something) is authorized to do something, we
need to have established identity.

We need authentication to determine authorization.

- **Confidentiality** means information is not made available or disclosed to
  unauthorized individuals, entities, or processes.
- **Integrity** means that data cannot be modified in an unauthorized or
  undetected manner.

<small>[Source](https://en.wikipedia.org/wiki/Information_security#Basic_principles)</small>

Authorization is simply policies on who can do what.

There are many ways such policies can be implemented.
Here, I will cover a couple techniques that are relatively simple to understand.
But still able to cover a broad spectrum of use-case required when developing
distributed systems.

They are _simple authorization_, _role-based authorization_ and
_resource-based authorization_.

## Simple authorization

In lack of a better name.
It simply means to authorize access to an endpoint only authenticated users.
We call users that haven't been authenticated for anonymous users.

It works on an endpoint level.
Meaning actions in controllers.

There are two approaches to this.

- **Whitelist**
  - Deny anonymous users access to all other endpoints.
  - Then explicitly mark endpoints with `[AllowAnonymous]` that we want to
    allow anonymous users to access.
- **Blacklist**
  - Allow anonymous users access to all endpoints.
  - Then explicitly mark endpoints with `[Authorize]` that only authenticated
    can access.

We are less likely to accidentally violate the security goals if we deny by
default.
So we will go with the whitelist approach.

_Beware that most online resources with use the blacklist approach._

We need to tell ASP.NET framework to globally require that users are
authenticated to access any endpoints.

In `server/Api/Program.cs` you need to find the line that says something with
`builder.Services.AddAuthentication` (it should be within `#region Security`).
Then add these lines right below the statement.

```cs
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        // Globally require users to be authenticated
        .RequireAuthenticatedUser()
        .Build();
});
```

It is important that both `AddAuthentication` and `AddAuthorization` is invoked
before all the swagger stuff.

Right above `app.MapControllers()` (towards the end of the file), add:

```cs
app.UseAuthentication();
app.UseAuthorization();
```

- Restart the server.
- Clear your browser data for localhost:5173 or open an Incognity window.
- Go to <http://localhost:5173/> and you should see `Request failed with status code 401`.

Now, anonymous users can't access anything at all on the back-end.
Not even the login endpoint.
Hey, wait that's a problem.
How can they login if they can't access the login endpoint?

We can override the new `RequireAuthenticatedUser` rule for select endpoints.
To do this, we add `[AllowAnonymous]` to the methods in our controllers.

In `server/Api/Controllers/AuthController.cs`, add `[AllowAnonymous]` to the
`Login` method.
It should look like this:

```cs
[HttpPost]
[Route("login")]
[AllowAnonymous]
public async Task<LoginResponse> Login(
  /* Truncated for brevity */
)
{
  /* Truncated for brevity */
}
```

Restart the server and see if you can login again.

We also want users to be able to register themselves.
And we want anonymous users to be able to read blog posts (but not comment).
So, we need to add `[AllowAnonymous]` attribute to a couple of more endpoints.
Here is the full list of endpoints that it should be added too:

- AuthController
  - Login
  - Register
- BlogController
  - List
  - Get

Go ahead and add the attribute to those endpoints.

Try it out.
See if you can read a blog post without authentication by going to
<http://localhost:5173/post/1>.

[Official documentation for Simple Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/simple)

## Role-based authorization

We will often have requirements where access is restricted based on role.

Btw. A user can be added to a role using [UserManager<TUser>.AddToRoleAsync(TUser,
String)
](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.addtoroleasync).

The blog application has multiple roles.
You can find them in [Role.cs](server/Service/Role.cs).

The application supports creating and updating drafts.
Where a draft is simply an unpublished blog post.

We don't want users with the "Reader" role to be able to access drafts.
That should be restricted to only "Admin" and "Editor" roles.
We can specify the allowed roles with the `[Authorize]` attribute.

Open `server/Api/Controllers/DraftController.cs` and add `[Authorize(Roles =
$"{Role.Admin},{Role.Editor}")]` to the class.
Should look like this:

```cs
[ApiController]
[Route("api/draft")]
[Authorize(Roles = $"{Role.Admin},{Role.Editor}")]
public class DraftController(IDraftService service) : ControllerBase
{
  /* Truncated for brevity */
}
```

Try it out.
Remember to restart the server first.

Login with "reader@example.com" see if you can access <http://localhost:5173/draft>.
Then login with "admin@example.com" or "editor@example.com" and see if you can
access the page.

Also.
Only editors should be allowed to create and update drafts.
So change the endpoints as shown:

```cs
[HttpPost]
[Route("")]
[Authorize(Roles = Role.Editor)]
public async Task<long> Create(DraftFormData data) => await service.Create(data);

[HttpPut]
[Route("{id}")]
[Authorize(Roles = Role.Editor)]
public async Task Update(long id, DraftFormData data) => await service.Update(id, data);
```

_Note: `[AllowAnonymous]` and `[Authorize]` can both be used on controllers
(classes) and actions (methods)._

Restart server.
Login with "admin@example.com" and create a draft from
<http://localhost:5173/draft/create>.
You should see a message that says "Draft creation failed".
It means that the authorization policy works.

[Official documentation for Role-based Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles)

## Resource-based Authorization

Sometimes we need an authorization policy depends on the resource.
The term "resource" sounds a bit abstract.
For demonstration; a resource can be a row in a database table.

In this application, we need a policy that says "only the author of a post is
allowed to update it".

The database table for a post has a `AuthorId` column.
You can find the entity/model for it in
[Post.cs](server/DataAccess/Entities/Post.cs).
To implement the policy, we need to make sure that `HttpContext.User` matches
`AuthorId` on the post before updating is allowed.

Navigate to `server/Api/Controllers/DraftController.cs` and change the `Update`
method to:

```cs
[HttpPut]
[Route("{id}")]
[Authorize(Roles = Role.Editor)]
public async Task Update(long id, DraftFormData data) =>
    await service.Update(HttpContext.User, id, data);
```

_Read ahead to fix the red line_

**Notice:** we added `HttpContext.User` as parameter to the service.

Then in `server/Service/Draft/DraftService.cs` change the `Update` method to:

```cs
public async Task Update(ClaimsPrincipal principal, long id, Dto.DraftFormData data)
{
    _draftValidator.ValidateAndThrow(data);
    var post =
        _postRepository.Query().SingleOrDefault(x => x.Id == id)
        ?? throw new NotFoundError(nameof(Entities.Post), new { id });
    if (principal.GetUserId() != post.AuthorId) throw new ForbiddenError();
    post.Title = data.Title;
    post.Content = data.Content;
    post.UpdatedAt = DateTime.UtcNow;
    if (data.Publish ?? false)
    {
        post.PublishedAt = DateTime.UtcNow;
    }
    await _postRepository.Update(post);
}
```

We throw a `ForbiddenError` if the user ID doesn't match the one in
`ClaimsPrincipal`.

Add the `ClaimsPrincipal` parameter to `IDraftService` interface to satisfy the
compiler.

Btw.
Now that we are at it.
We also need to change the `Create` method, so it adds `AuthorId` before
saving the post.

Open `server/Api/Controllers/DraftController.cs` and change the `Create`
method to:

```cs
[HttpPost]
[Route("")]
[Authorize(Roles = Role.Editor)]
public async Task<long> Create(DraftFormData data) =>
    await service.Create(HttpContext.User, data);
```

Then in `server/Service/Draft/DraftService.cs`, do:

```cs
public async Task<long> Create(ClaimsPrincipal principal, Dto.DraftFormData data)
{
    _draftValidator.ValidateAndThrow(data);
    var post = new Entities.Post
    {
        Title = data.Title,
        Content = data.Content,
        AuthorId = principal.GetUserId(),
        PublishedAt = data.Publish ?? false ? DateTime.UtcNow : null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
    await _postRepository.Add(post);
    return post.Id;
}
```

**Notice:** `AuthorId = principal.GetUserId()`

We need to set an author when a draft is created for the new update policy to
make sense.

Fix the interface again to make the mighty compiler happy.

Try it out!
Login with "editor@example.com", then go to <http://localhost:5173/draft> and
create a draft.
Then login with "othereditor@example.com" and see if you can edit it.

For _Simple Authorization_ and _Role-based Authorization_ we decorated the
endpoints with the attributes `[AllowAnonymous]` and `[Authorize]`.
For _Resource-based Authorization_ we didn't use an attribute.
We implemented it in the service layer instead.
That is because, we need to talk to the database to find the AuthorId.
I think it makes sense to implement it where we are already retrieving the
resource from the database.

It is actually also possible to implement _Resource-based Authorization_ with
attributes.
That is how the [official documentation for resource-based
authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased)
does it.
However, it involves more code and cost an extra round-trip to the database.

## Outro

There are many other ways that authorization policies can be implemented.
I believe that with these 3 ways shown here, will cover many use-cases.

That said, you will learn other ways as mature in the profession.

[Reference solution](https://github.com/rpede/cds24-tutorial-auth/tree/02-authorization)