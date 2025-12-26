using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Npgsql;

namespace AzureMidtermProject;

public class Program
{
    // Main metodunu 'async Task' yaparak modern ve performanslı hale getirdik
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // KEYVAULTURL environment variable kontrolü
        var keyVaultUrl = Environment.GetEnvironmentVariable("KEYVAULTURL");

        // Eğer boşsa hata fırlat
        if (string.IsNullOrWhiteSpace(keyVaultUrl))
        {
            throw new InvalidOperationException("KEYVAULTURL environment variable is not set.");
        }

        Console.WriteLine($"Key Vault URL bulundu: {keyVaultUrl}");

        // Key Vault bağlantısı (Managed Identity ile)
        var secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

        // Secret'ları ASENKRON olarak çekiyoruz (Uygulama açılışını hızlandırır)
        // Paralel task başlatıp hepsini aynı anda bekliyoruz
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
            // ÖNEMLİ: Azure PostgreSQL için SSL zorunludur
            SslMode = SslMode.Require,
            TrustServerCertificate = true // Self-signed sertifikalar için gerekebilir
        };

        string connectionString = csb.ConnectionString;

        // Dependency Injection'a ekle
        builder.Services.AddScoped<NpgsqlConnection>(_ => new NpgsqlConnection(connectionString));

        var app = builder.Build();

        // Endpointler
        app.MapGet("/", () => "Azure final web app is running correctly.");

        app.MapGet("/debug-env", () =>
        {
            return Results.Ok(new
            {
                Status = "Secrets Loaded",
                KeyVaultUrl = keyVaultUrl,
                DbHost = dbHost, // Debug için hostu görelim (şifreyi göstermiyoruz)
                SslMode = csb.SslMode.ToString()
            });
        });

        app.MapGet("/hello", async (NpgsqlConnection connection) =>
        {
            try
            {
                await connection.OpenAsync();

                // Tablo oluşturma komutu
                await using var cmd = new NpgsqlCommand(
                    "CREATE TABLE IF NOT EXISTS contributors ( student_id VARCHAR(20) PRIMARY KEY, name VARCHAR(100) NOT NULL );",
                    connection);

                await cmd.ExecuteNonQueryAsync();

                // Basit bir insert denemesi de yapabiliriz (Opsiyonel)
                // await using var insertCmd = new NpgsqlCommand("INSERT INTO contributors VALUES ('123', 'Test User') ON CONFLICT DO NOTHING;", connection);
                // await insertCmd.ExecuteNonQueryAsync();

                return Results.Ok(new
                {
                    message = "/hello endpoint worked!",
                    connectedDatabase = connection.Database,
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