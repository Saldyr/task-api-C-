using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace TasksApi.Tests;

// Boots the real API in memory (≈ Test.createTestingModule + supertest in NestJS),
// wired to an in-memory SQLite database instead of the tasks.db file.
public class TasksApiFactory : WebApplicationFactory<Program>
{
    // A shared in-memory database disappears when its last connection closes,
    // so one connection is kept open for the whole test run.
    private readonly string _connectionString =
        $"Data Source=tests-{Guid.NewGuid()};Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public TasksApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}
