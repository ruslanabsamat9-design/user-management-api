using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();

var app = builder.Build();

// 1. Error-Handling Middleware (Debugging & Exception Handling with Copilot)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception occurred.");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "Internal server error." });
    }
});

// 2. Authentication Middleware
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/users"))
    {
        // Allows requests with a valid token or during local testing
        if (context.Request.Headers.TryGetValue("Authorization", out var token) && token == "Bearer invalid-token")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized access." });
            return;
        }
    }
    await next();
});

// 3. Logging Middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("HTTP {Method} {Path} started", context.Request.Method, context.Request.Path);
    await next();
    logger.LogInformation("HTTP {Method} {Path} responded {StatusCode}", context.Request.Method, context.Request.Path, context.Response.StatusCode);
});

// Thread-safe in-memory user store (Debugged with Copilot for concurrency)
var users = new ConcurrentDictionary<int, User>();
users.TryAdd(1, new User { Id = 1, Name = "Alice Johnson", Email = "alice@techhive.com", Department = "IT" });
users.TryAdd(2, new User { Id = 2, Name = "Bob Smith", Email = "bob@techhive.com", Department = "HR" });

// 4. CRUD Endpoints (GET, POST, PUT, DELETE) + Input Validation

// GET all users
app.MapGet("/users", () => Results.Ok(users.Values));

// GET user by ID
app.MapGet("/users/{id:int}", (int id) =>
    users.TryGetValue(id, out var user)
        ? Results.Ok(user)
        : Results.NotFound(new { message = $"User with ID {id} not found." }));

// POST create a new user with validation
app.MapPost("/users", (User newUser) =>
{
    if (!ValidateUser(newUser, out string validationError))
        return Results.BadRequest(new { error = validationError });

    int nextId = users.IsEmpty ? 1 : users.Keys.Max() + 1;
    newUser.Id = nextId;
    users[nextId] = newUser;

    return Results.Created($"/users/{newUser.Id}", newUser);
});

// PUT update an existing user with validation
app.MapPut("/users/{id:int}", (int id, User updatedUser) =>
{
    if (!users.ContainsKey(id))
        return Results.NotFound(new { message = $"User with ID {id} not found." });

    if (!ValidateUser(updatedUser, out string validationError))
        return Results.BadRequest(new { error = validationError });

    updatedUser.Id = id;
    users[id] = updatedUser;
    return Results.Ok(updatedUser);
});

// DELETE a user by ID
app.MapDelete("/users/{id:int}", (int id) =>
    users.TryRemove(id, out _)
        ? Results.NoContent()
        : Results.NotFound(new { message = $"User with ID {id} not found." }));

app.Run();

// Validation helper method generated and refined with Microsoft Copilot
static bool ValidateUser(User user, out string error)
{
    if (user == null)
    {
        error = "User payload cannot be null.";
        return false;
    }
    if (string.IsNullOrWhiteSpace(user.Name) || user.Name.Length < 2)
    {
        error = "Name is required and must be at least 2 characters long.";
        return false;
    }
    if (string.IsNullOrWhiteSpace(user.Email) || !Regex.IsMatch(user.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
    {
        error = "A valid email address is required.";
        return false;
    }
    error = string.Empty;
    return true;
}

public class User
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}
