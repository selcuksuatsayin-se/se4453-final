using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Npgsql;

namespace AzureMidtermProject;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var keyVaultUrl = Environment.GetEnvironmentVariable("KeyVaultUrl");

        if (string.IsNullOrWhiteSpace(keyVaultUrl))
        {
            throw new InvalidOperationException("KEYVAULTURL environment variable is not set.");
        }

        Console.WriteLine($"Key Vault URL bulundu: {keyVaultUrl}");

        var secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

        var hostTask = secretClient.GetSecretAsync("PGHOST");
        var userTask = secretClient.GetSecretAsync("PGUSER");
        var passTask = secretClient.GetSecretAsync("PGPASSWORD");
        var dbTask = secretClient.GetSecretAsync("PGDATABASE");
        var portTask = secretClient.GetSecretAsync("PGPORT");

        await Task.WhenAll(hostTask, userTask, passTask, dbTask, portTask);

        string dbHost = hostTask.Result.Value.Value;
        string dbUser = userTask.Result.Value.Value;
        string dbPassword = passTask.Result.Value.Value;
        string dbName = dbTask.Result.Value.Value;
        string dbPort = portTask.Result.Value.Value;

        Console.WriteLine("Loaded DB secrets from Azure Key Vault.");

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = dbHost,
            Database = dbName,
            Username = dbUser,
            Password = dbPassword,
            Port = int.Parse(dbPort),
            SslMode = SslMode.Require,
            TrustServerCertificate = true
        };

        string connectionString = csb.ConnectionString;

        builder.Services.AddScoped<NpgsqlConnection>(_ => new NpgsqlConnection(connectionString));

        var app = builder.Build();

        app.MapGet("/", () => "Azure final web app is running correctly.");

        app.MapGet("/debug-env", () =>
        {
            return Results.Ok(new
            {
                Status = "Secrets Loaded",
                KeyVaultUrl = keyVaultUrl,
                DbHost = dbHost,
                SslMode = csb.SslMode.ToString()
            });
        });

        app.MapGet("/hello", async (NpgsqlConnection connection) =>
        {
            try
            {
                await connection.OpenAsync();

                await using var cmd = new NpgsqlCommand("SELECT NOW()", connection);
                var dbTime = await cmd.ExecuteScalarAsync();


                return Results.Ok(new
                {
                    message = "/hello endpoint is working.",
                    connectedDatabase = connection.Database,
                    serverTime = dbTime?.ToString(),
                    state = connection.State.ToString()
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Database connection failed: {ex.Message}");
            }
        });

        app.Run();
    }
}