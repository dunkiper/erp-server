using Erp.Server.Autenticacion;
using Erp.Server.Configuration;
using Erp.Server.Conexiones;
using Erp.Server.Endpoints;
using Erp.Server.Hubs;
using Erp.Server.Licencias;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAutenticacionJwt(builder.Configuration);

builder.Services
    .AddOptions<OpcionesLicencias>()
    .Bind(builder.Configuration.GetSection(OpcionesLicencias.Seccion))
    .ValidateOnStart();

builder.Services.AddSingleton<IServicioLicencias, ServicioLicencias>();
builder.Services.AddSingleton<IRegistroConexiones, RegistroConexiones>();
builder.Services.AddSingleton<ISolicitudDispatcher, SolicitudDispatcher>();

builder.Services.AddSignalR(opciones =>
{
    opciones.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opciones.KeepAliveInterval = TimeSpan.FromSeconds(10);
    opciones.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
})
.AddJsonProtocol(opciones =>
{
    opciones.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opciones.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Los enums viajan como texto en toda la API (REST y SignalR) para que el payload
// sea legible y estable para los clientes externos.
builder.Services.ConfigureHttpJsonOptions(opciones =>
{
    opciones.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opciones.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddCors(opciones => opciones.AddPolicy("signalr", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opciones =>
    {
        opciones.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "ERP Server",
            Version = "v1",
            Description = "API REST y hub SignalR con autenticacion JWT y licencia."
        });

        opciones.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Token JWT. En el hub SignalR debe ir en la query: ?access_token=..."
        });

        opciones.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("signalr");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHub<HubConexion>(HubConexion.Ruta).RequireAuthorization();
app.MapEndpointPrueba();

app.Logger.LogInformation(
    "Servidor ERP arrancando. entorno={Entorno} hub={Hub}",
    app.Environment.EnvironmentName,
    HubConexion.Ruta);

app.Run();