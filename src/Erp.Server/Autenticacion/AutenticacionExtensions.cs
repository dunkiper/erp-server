using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Erp.Server.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Server.Autenticacion;

public static class AutenticacionExtensions
{
    public static IServiceCollection AddAutenticacionJwt(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var seccion = configuracion.GetSection(Configuration.OpcionesJwt.Seccion).Get<Configuration.OpcionesJwt>() ?? new();
        var clave = seccion.SigningKey;

        if (string.IsNullOrWhiteSpace(clave) || clave.Length < 32)
        {
            throw new InvalidOperationException(
                $"'{Configuration.OpcionesJwt.Seccion}:SigningKey' no esta configurada o es demasiado corta (minimo 32 caracteres). " +
                "Definala via appsettings, variable de entorno Jwt__SigningKey o user-secrets.");
        }

        servicios.AddSingleton(seccion);

        servicios
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opciones =>
            {
                opciones.RequireHttpsMetadata = false;
                opciones.SaveToken = true;
                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = seccion.Issuer,
                    ValidateAudience = true,
                    ValidAudience = seccion.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(seccion.ClockSkewSegundos),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };

                opciones.Events = new JwtBearerEvents
                {
                    OnMessageReceived = contexto =>
                    {
                        // Los clientes SignalR (WebSocket / SSE) no pueden fijar cabeceras,
                        // por lo que el token llega en la query string.
                        var query = contexto.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(query) && contexto.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            contexto.Token = query;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async contexto =>
                    {
                        contexto.HandleResponse();

                        if (contexto.Response.HasStarted)
                        {
                            return;
                        }

                        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        contexto.Response.ContentType = "application/json";
                        await contexto.Response.WriteAsJsonAsync(new
                        {
                            error = "no_autorizado",
                            mensaje = "Token JWT ausente o invalido."
                        });
                    },
                    OnForbidden = async contexto =>
                    {
                        if (contexto.Response.HasStarted)
                        {
                            return;
                        }

                        contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await contexto.Response.WriteAsJsonAsync(new
                        {
                            error = "prohibido",
                            mensaje = "El token no tiene permisos suficientes."
                        });
                    }
                };
            });

        servicios.AddAuthorization();
        return servicios;
    }

    public static string CrearToken(
        Configuration.OpcionesJwt opciones,
        string subject,
        string[] ambitos,
        DateTimeOffset ahora,
        out DateTimeOffset expira)
    {
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        expira = ahora.AddMinutes(opciones.ExpirationMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(ClaimTypes.NameIdentifier, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, ahora.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        claims.AddRange(ambitos.Select(a => new Claim("scope", a)));

        var token = new JwtSecurityToken(
            issuer: opciones.Issuer,
            audience: opciones.Audience,
            claims: claims,
            notBefore: ahora.UtcDateTime,
            expires: expira.UtcDateTime,
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class SolicitudToken
{
    [Required]
    public string Usuario { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class RespuestaToken
{
    public required string AccessToken { get; init; }

    public string TokenType { get; init; } = "Bearer";

    public int ExpiresInSegundos { get; init; }

    public required DateTime ExpiraUtc { get; init; }
}