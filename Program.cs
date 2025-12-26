using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);


// 1. Development ortamında (Yerel) appsettings.json'dan oku

// 2. Production ortamında (Azure) Key Vault'tan oku


var host = builder.Configuration["PGHOST"];
var db = builder.Configuration["PGDATABASE"];
var user = builder.Configuration["PGUSER"];
var pass = builder.Configuration["PGPASSWORD"];
var port = builder.Configuration["PGPORT"] ?? "5432";

var connectionString =
    $"Host={host};Database={db};Username={user};Password={pass};Port={port};Ssl Mode=Require;";

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