using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

string connectionString;

if (builder.Environment.IsDevelopment())
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}
else
{
    var keyVaultUrl = builder.Configuration["KeyVaultUrl"];   
    var secretName = "DbConnection";

    var secretClient = new SecretClient(new Uri(keyVaultUrl!), new DefaultAzureCredential());
    KeyVaultSecret secret = secretClient.GetSecret(secretName);
    connectionString = secret.Value;
}


var app = builder.Build();

app.MapGet("/", () => "API is running");

app.MapGet("/debug-conn", () =>
{
    var preview = connectionString?.Length > 80
        ? connectionString.Substring(0, 80) + "..."
        : connectionString ?? "null";

    return Results.Text(preview);
});

app.MapGet("/hello", async () =>
{
    await using var conn = new NpgsqlConnection(connectionString);

    try
    {
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT NOW()", conn);
        var dbTime = await cmd.ExecuteScalarAsync();

        return Results.Ok(new
        {
            message = "/hello endpoint working",
            databaseConnection = "OK",
            dbTime,
            usedConnectionString = connectionString
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database connection FAILED: {ex.Message}");
    }
});

app.Run();
