using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

string connectionString = string.Empty;

// 1. Development ortamında (Yerel) appsettings.json'dan oku

// 2. Production ortamında (Azure) Key Vault'tan oku

try
{
    // Key Vault URL'sini ortam değişkenlerinden al
    // (Azure App Service Configuration'da "KeyVaultUrl" tanımlı olmalı)
    var keyVaultUrl = builder.Configuration["KeyVaultUrl"];

    if (!string.IsNullOrEmpty(keyVaultUrl))
    {
        var secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

        // Görseldeki secret isimlerine göre verileri çek
        var hostTask = secretClient.GetSecretAsync("PGHOST");
        var userTask = secretClient.GetSecretAsync("PGUSER");
        var passTask = secretClient.GetSecretAsync("PGPASSWORD");
        var dbTask = secretClient.GetSecretAsync("PGDATABASE");
        var portTask = secretClient.GetSecretAsync("PGPORT");

        // Hepsini paralel bekle
        await Task.WhenAll(hostTask, userTask, passTask, dbTask, portTask);

        var host = hostTask.Result.Value.Value;
        var user = userTask.Result.Value.Value;
        var pass = passTask.Result.Value.Value;
        var db = dbTask.Result.Value.Value;
        var port = portTask.Result.Value.Value;

        // Connection String'i oluştur
        connectionString = $"Host={host};Database={db};Username={user};Password={pass};Port={port};Ssl Mode=Require;";
    }
    else
    {
        Console.WriteLine("HATA: KeyVaultUrl bulunamadı!");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Key Vault Hatası: {ex.Message}");
}

var app = builder.Build();

app.MapGet("/", () => "API Calisiyor (Key Vault Versiyonu)");

// Debug Endpoint: Bağlantı dizesinin oluşup oluşmadığını kontrol etmek için
app.MapGet("/debug-conn", () =>
{
    if (string.IsNullOrEmpty(connectionString))
        return Results.Text("HATA: Connection string oluşturulamadı! Key Vault izinlerini kontrol edin.");

    // Güvenlik için şifreyi gizleyerek gösterelim
    return Results.Text($"Bağlantı dizesi başarıyla oluşturuldu. Host: {connectionString.Split(';')[0]}");
});

app.MapGet("/hello", async () =>
{
    if (string.IsNullOrEmpty(connectionString))
    {
        return Results.Problem("Bağlantı dizesi boş. Key Vault okunamadı.");
    }

    try
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT NOW()", conn);
        var dbTime = await cmd.ExecuteScalarAsync();

        return Results.Ok(new
        {
            message = "Veritabanı bağlantısı BAŞARILI",
            source = builder.Environment.IsDevelopment() ? "Local Config" : "Azure Key Vault",
            time = dbTime?.ToString()
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Veritabanı hatası: {ex.Message}");
    }
});

app.Run();