using System.Security.Claims;
using Erp.Server.Autenticacion;
using Erp.Server.Configuration;
using Erp.Server.Contratos;
using Erp.Server.Conexiones;
using Erp.Server.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Erp.Server.Endpoints;

public static class EndpointPrueba
{
    public static void MapEndpointPrueba(this WebApplication app)
    {
        var grupo = app.MapGroup("/api").WithTags("Prueba");

        grupo.MapGet("/saludo", () => Results.Ok(new
        {
            mensaje = "Hola desde el servidor ERP",
            servidorUtc = DateTime.UtcNow
        }))
        .WithName("Saludo")
        .AllowAnonymous();

        grupo.MapGet("/conexiones", (Erp.Server.Conexiones.IRegistroConexiones registro) => Results.Ok(new
        {
            total = registro.Todas().Count,
            conexiones = registro.Todas()
        }))
        .RequireAuthorization()
        .WithName("ListarConexiones");

        // Endpoint de prueba: recibe una licencia, localiza el cliente conectado con ella,
        // le pide por el websocket la lista de clientes y devuelve la respuesta por REST.
        grupo.MapGet("/clientes", async (
            [FromQuery] string? licencia,
            [FromQuery] string? motivo,
            [FromQuery] bool? soloActivos,
            [FromQuery] int? limite,
            [FromQuery] int? tiempoEsperaMs,
            ClaimsPrincipal usuario,
            Erp.Server.Licencias.IServicioLicencias licencias,
            Erp.Server.Conexiones.IRegistroConexiones conexiones,
            Erp.Server.Conexiones.ISolicitudDispatcher dispatcher,
            IHubContext<HubConexion> hub,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("EndpointClientes");
            var normalizada = licencias.Normalizar(licencia);

            if (string.IsNullOrEmpty(normalizada))
            {
                return Results.BadRequest(new
                {
                    error = "licencia_requerida",
                    mensaje = "Debe indicar el parametro de query 'licencia'."
                });
            }

            var validacion = licencias.Validar(normalizada);

            if (!validacion.EsValida)
            {
                return Results.Json(new
                {
                    error = $"licencia_{validacion.Resultado.ToString().ToLowerInvariant()}",
                    mensaje = validacion.Mensaje,
                    licencia = normalizada
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            var conexion = conexiones.MasReciente(normalizada);

            if (conexion is null)
            {
                return Results.Json(new
                {
                    error = "sin_conexion",
                    mensaje = $"Ningun cliente conectado tiene la licencia '{normalizada}'.",
                    licencia = normalizada,
                    conexionesConLicencia = 0
                }, statusCode: StatusCodes.Status404NotFound);
            }

            var espera = NormalizarEspera(tiempoEsperaMs);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(espera);

            try
            {
                var solicitud = await dispatcher.CrearAsync(
                    normalizada,
                    usuario.Identity?.Name,
                    motivo,
                    new FiltrosListaClientes { SoloActivos = soloActivos ?? true },
                    (int)espera.TotalMilliseconds,
                    timeout.Token);

                logger.LogInformation(
                    "Solicitando lista de clientes. licencia={Licencia} conexion={Conexion} peticion={PeticionId}",
                    normalizada, conexion.Id, solicitud.PeticionId);

                await hub.Clients.Client(conexion.Id)
                    .SendAsync("SolicitarListaClientes", solicitud, timeout.Token)
                    .ConfigureAwait(false);

                var respuesta = await dispatcher.EsperarAsync(solicitud.PeticionId, timeout.Token).ConfigureAwait(false);

                return Results.Ok(respuesta);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Json(new
                {
                    error = "timeout",
                    mensaje = $"El cliente con licencia '{normalizada}' no ha respondido en {espera.TotalMilliseconds:F0} ms.",
                    licencia = normalizada,
                    conexionId = conexion.Id
                }, statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (OperacionCanceladaException ex)
            {
                return Results.Json(new
                {
                    error = "cancelado",
                    mensaje = ex.Message,
                    licencia = normalizada
                }, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .RequireAuthorization()
        .WithName("ObtenerClientesPorLicencia")
        .Produces<RespuestaListaClientes>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        grupo.MapGet("/token-demo", (
            Configuration.OpcionesJwt opcionesJwt,
            [FromQuery] string usuario = "demo") =>
        {
            // Solo para desarrollo: emite un token de ejemplo.
            var ahora = DateTimeOffset.UtcNow;
            var token = AutenticacionExtensions.CrearToken(
                opcionesJwt, usuario, ["clientes:leer", "conexion:registrar"], ahora, out var expira);

            return Results.Ok(new RespuestaToken
            {
                AccessToken = token,
                ExpiresInSegundos = (int)(expira - ahora).TotalSeconds,
                ExpiraUtc = expira.UtcDateTime
            });
        })
        .AllowAnonymous()
        .WithName("TokenDemo");
    }

    private static TimeSpan NormalizarEspera(int? milliseconds)
    {
        var valor = milliseconds ?? 15000;
        return TimeSpan.FromMilliseconds(Math.Clamp(valor, 500, 120000));
    }
}