using Microsoft.EntityFrameworkCore;
using TasksApi.Data;

// Program.cs ≈ main.ts + AppModule: register services, then build the HTTP pipeline.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();       // Generates the OpenAPI document (/openapi/v1.json).

// The lambda runs when a DbContext is created, not here: tests can therefore
// replace the connection string with an in-memory SQLite database.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Tasks API v1"));
}

// Applies migrations at startup (≈ `prisma migrate deploy`).
// Convenient for a demo project; in production this usually belongs to the deployment step.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.MapControllers();

await app.RunAsync();
