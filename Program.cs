using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// HATA ÇÖZÜMÜ: Key Vault mantığını sildik.
// .NET, Azure 'Connection Strings' altındaki "DefaultConnection"ı otomatik algılar.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var app = builder.Build();

app.MapGet("/", () => "API is running");

// Debug endpoint: Bağlantı dizesinin gelip gelmediğini kontrol etmek için
app.MapGet("/debug-conn", () =>
{
    if (string.IsNullOrEmpty(connectionString))
        return Results.Text("HATA: Connection string NULL veya Boş!");

    // Güvenlik için sadece ilk 20 karakteri gösterelim
    var preview = connectionString.Length > 20
        ? connectionString.Substring(0, 20) + "..."
        : connectionString;

    return Results.Text($"Bağlantı dizesi başarıyla okundu: {preview}");
});

app.MapGet("/hello", async () =>
{
    if (string.IsNullOrEmpty(connectionString))
    {
        return Results.Problem("Bağlantı dizesi bulunamadı. Azure Configuration kontrol edin.");
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
            time = dbTime?.ToString()
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Veritabanı hatası: {ex.Message}");
    }
});

app.Run();