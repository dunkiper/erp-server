using Erp.Server.Conexiones;
using Erp.Server.Contratos;
using Erp.Server.Licencias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Erp.Server.Hubs;

public sealed class HubConexion : Hub
{
    public const string Ruta = "/hubs/conexion";

    private readonly IServicioLicencias _licencias;
    private readonly IRegistroConexiones _registro;
    private readonly ISolicitudDispatcher _dispatcher;
    private readonly ILogger<HubConexion> _logger;

    public HubConexion(
        IServicioLicencias licencias,
        IRegistroConexiones registro,
        ISolicitudDispatcher dispatcher,
        ILogger<HubConexion> logger)
    {
        _licencias = licencias;
        _registro = registro;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Autentica la conexion contra la licencia. Sin este paso la conexion no queda operativa.
    /// </summary>
    public async Task<ResultadoRegistroConexion> RegistrarConexion(RegistroConexionSolicitud solicitud)
    {
        var identity = Context.User?.Identity?.Name ?? "desconocido";
        var licencia = _licencias.Normalizar(solicitud.Licencia);

        if (string.IsNullOrEmpty(licencia))
        {
            return new ResultadoRegistroConexion(false, null, "Debe indicar un codigo de licencia.");
        }

        var conexionesActivas = _registro.ContarPorLicencia(licencia);
        var validacion = _licencias.Validar(licencia, conexionesActivas);

        if (!validacion.EsValida)
        {
            _logger.LogWarning(
                "Rechazo de conexion. usuario={Usuario} licencia={Licencia} motivo={Motivo} resultado={Resultado}",
                identity, licencia, validacion.Mensaje, validacion.Resultado);

            return new ResultadoRegistroConexion(false, null, validacion.Mensaje);
        }

        if (Context.ConnectionId is not { Length: > 0 } connectionId)
        {
            return new ResultadoRegistroConexion(false, null, "No se ha podido determinar el identificador de conexion.");
        }

        var registro = _registro.Registrar(connectionId, licencia, solicitud.NombreCliente, solicitud.Version);

        _logger.LogInformation(
            "Conexion registrada. id={Id} usuario={Usuario} licencia={Licencia} cliente={Cliente}Connections={Conexiones}/{Maximo}",
            connectionId, identity, licencia, solicitud.NombreCliente, conexionesActivas + 1, validacion.MaxConexiones);

        await Groups.AddToGroupAsync(connectionId, $"licencia:{licencia}").ConfigureAwait(false);

        return new ResultadoRegistroConexion(true, new InfoConexion(
            connectionId,
            licencia,
            validacion.Licencia?.Cliente,
            registro.ConectadaUtc,
            validacion.MaxConexiones,
            conexionesActivas + 1), null);
    }

    /// <summary>
    /// Responde a la peticion REST devolviendo la lista de clientes del ERP.
    /// </summary>
    public Task ResponderListaClientes(RespuestaListaClientes respuesta)
    {
        _registro.ContarMensajeRecibido(Context.ConnectionId);

        if (string.IsNullOrWhiteSpace(respuesta.PeticionId))
        {
            _logger.LogWarning("Respuesta de lista sin peticionId. conexion={Id}", Context.ConnectionId);
            return Task.CompletedTask;
        }

        if (string.IsNullOrEmpty(respuesta.Licencia))
        {
            respuesta.Licencia = _licencias.Normalizar(
                _registro.Todas().FirstOrDefault(c => c.Id == Context.ConnectionId)?.Licencia);
        }

        _logger.LogInformation(
            "Lista de clientes recibida. conexion={Id} peticion={PeticionId} licencia={Licencia} total={Total} exito={Exito}",
            Context.ConnectionId, respuesta.PeticionId, respuesta.Licencia, respuesta.Total, respuesta.Exito);

        _dispatcher.Resolver(respuesta.PeticionId, respuesta);
        return Task.CompletedTask;
    }

    public Task Latido()
    {
        _registro.MarcarActividad(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public override Task OnConnectedAsync()
    {
        var query = Context.GetHttpContext()?.Request.Query;
        var licencia = query is not null && query.TryGetValue("licencia", out var valor)
            ? _licencias.Normalizar(valor.ToString())
            : string.Empty;

        _logger.LogInformation(
            "Handshake websocket. conexion={Id} usuario={Usuario} licencia={Licencia} ip={Ip} protocolo={Protocolo}",
            Context.ConnectionId,
            Context.User?.Identity?.Name,
            string.IsNullOrEmpty(licencia) ? "(sin licencia en query)" : licencia,
            Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString(),
            Context.GetHttpContext()?.Request.Protocol);

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var conexion = _registro.Todas().FirstOrDefault(c => c.Id == Context.ConnectionId);
        _registro.DarDeBaja(Context.ConnectionId);

        if (conexion is not null)
        {
            _logger.LogInformation(
                "Conexion cerrada. id={Id} licencia={Licencia} error={Error}",
                Context.ConnectionId, conexion.Licencia, exception?.Message ?? "ninguno");
        }

        return base.OnDisconnectedAsync(exception);
    }
}

public sealed record RegistroConexionSolicitud
{
    public string Licencia { get; init; } = string.Empty;

    public string? NombreCliente { get; init; }

    public string? Version { get; init; }
}

public sealed record ResultadoRegistroConexion(
    bool Exito,
    InfoConexion? Conexion,
    string? Error);

public sealed record InfoConexion(
    string Id,
    string Licencia,
    string? Cliente,
    DateTime ConectadaUtc,
    int MaxConexiones,
    int ConexionesActivas);