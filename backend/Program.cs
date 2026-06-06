using Backend.Data;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Origen del frontend Blazor (puerto fijo). CORS explícito, sin AllowAnyOrigin.
const string FrontendOrigin = "http://localhost:5081";
const string CorsPolicy = "FrontendPolicy";

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Acceso a datos y reglas de negocio.
builder.Services.AddSingleton<Db>();
builder.Services.AddScoped<RecargaService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(FrontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Inicialización automática de la base: crea y puebla banco.db si no existe.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<Db>().InitSchema();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Sin UseHttpsRedirection: el perfil es solo HTTP (evita el prompt de dev-certs).
app.UseCors(CorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();
