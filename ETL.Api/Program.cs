using ETL.Api;
using Microsoft.OpenApi.Models;

// API REST de comentarios de redes sociales.
// Simula el sistema externo del que el Worker extrae los comentarios mediante HttpClient.
var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["Seguridad:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
    throw new InvalidOperationException(
        "Falta 'Seguridad:ApiKey'. Configúrela con: dotnet user-secrets set \"Seguridad:ApiKey\" \"<clave>\" --project ETL.Api");

builder.Services.AddSingleton<ComentariosRepository>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API Comentarios Sociales", Version = "v1" });
    c.AddSecurityDefinition(ApiKeyFilter.Encabezado, new OpenApiSecurityScheme
    {
        Name = ApiKeyFilter.Encabezado,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Clave de acceso a la API"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = ApiKeyFilter.Encabezado } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { estado = "OK", fechaUtc = DateTime.UtcNow }))
   .WithTags("Salud");

var api = app.MapGroup("/api")
    .AddEndpointFilter(new ApiKeyFilter(apiKey))
    .WithTags("Comentarios");

api.MapGet("/comentarios", (ComentariosRepository repo, int pagina = 1, int tamanoPagina = 50) =>
    {
        if (pagina < 1 || tamanoPagina is < 1 or > 1000)
            return Results.BadRequest(new { error = "pagina >= 1 y tamanoPagina entre 1 y 1000." });

        return Results.Ok(repo.ObtenerPagina(pagina, tamanoPagina));
    })
   .WithName("ObtenerComentarios");

app.Run();
