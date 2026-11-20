// Program.cs ≈ main.ts + AppModule: register services, then build the HTTP pipeline.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();       // Generates the OpenAPI document (/openapi/v1.json).

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Tasks API v1"));
}

app.MapControllers();

app.Run();
