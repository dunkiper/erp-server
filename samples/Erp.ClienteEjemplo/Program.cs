using Erp.ClienteEjemplo;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var opciones = new OpcionesCliente();

// 1) Fichero appsettings.json
var rutaConfig = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (File.Exists(rutaConfig))
{
    var config = new ConfigurationBuilder()
        .AddJsonFile(rutaConfig, optional: true)
        .Build();

    opciones.UrlServidor = config["UrlServidor"] ?? opciones.UrlServidor;
    opciones.Licencia = config["Licencia"] ?? opciones.Licencia;
    opciones.Token = config["Token"];
    opciones.NombreCliente = config["NombreCliente"] ?? opciones.NombreCliente;
    opciones.Version = config["Version"] ?? opciones.Version;
}

// 2) Variables de entorno
opciones.UrlServidor = Environment.GetEnvironmentVariable("ERP_URL_SERVIDOR") ?? opciones.UrlServidor;
opciones.Licencia = Environment.GetEnvironmentVariable("ERP_LICENCIA") ?? opciones.Licencia;
opciones.Token = Environment.GetEnvironmentVariable("ERP_TOKEN") ?? opciones.Token;
opciones.NombreCliente = Environment.GetEnvironmentVariable("ERP_NOMBRE_CLIENTE") ?? opciones.NombreCliente;

// 3) Argumentos de linea de comandos
for (var i = 0; i < args.Length; i++)
{
    var valor = i + 1 < args.Length ? args[i + 1] : null;

    switch (args[i])
    {
        case "--url" when valor is not null:
            opciones.UrlServidor = valor;
            i++;
            break;
        case "--licencia" when valor is not null:
            opciones.Licencia = valor;
            i++;
            break;
        case "--token" when valor is not null:
            opciones.Token = valor;
            i++;
            break;
        case "--nombre" when valor is not null:
            opciones.NombreCliente = valor;
            i++;
            break;
    }
}

// Sin token propio, se pide uno de demo al propio servidor.
if (string.IsNullOrWhiteSpace(opciones.Token))
{
    Console.WriteLine("No se ha indicado token JWT. Solicitando uno de demo al servidor...");
    opciones.Token = await ObtenerTokenDemoAsync(opciones.UrlServidor);
}

var nivelLog = Environment.GetEnvironmentVariable("ERP_LOG_LEVEL") switch
{
    "Trace" => LogLevel.Trace,
    "Debug" => LogLevel.Debug,
    "Information" => LogLevel.Information,
    _ => LogLevel.Warning
};

var conexion = new HubConnectionBuilder()
    .ConfigurarSignalR(opciones)
    .ConfigureLogging(logging => logging
        .SetMinimumLevel(nivelLog)
        .AddSimpleConsole(consola => consola.SingleLine = true))
    .WithAutomaticReconnect()
    .Build();

var hub = new HubConexionProxy(conexion);
var catalogo = new CatalogoClientesSimulado(opciones);
var manejador = new ManejadorSolicitudLista(hub, catalogo);

hub.Nativo.On("SolicitarListaClientes", async (SolicitudListaClientes solicitud) =>
{
    try
    {
        await manejador.HandleAsync(solicitud);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[cliente] Error desatendido al procesar la peticion: {ex}");
    }
});
hub.Nativo.On("Latido", () => Console.WriteLine("[cliente] Latido recibido"));

conexion.Closed += ex =>
{
    var motivo = ex?.GetBaseException().Message;
    Console.WriteLine($"[cliente] Conexion cerrada{(motivo is null ? "" : $": {motivo}")}");
    return Task.CompletedTask;
};

conexion.Reconnecting += ex =>
{
    var motivo = ex?.GetBaseException().Message;
    Console.WriteLine($"[cliente] Reconectando{(motivo is null ? "" : $": {motivo}")}");
    return Task.CompletedTask;
};

conexion.Reconnected += async id =>
{
    Console.WriteLine($"[cliente] Reconectado con id {id}");

    if (!await RegistrarAsync())
    {
        Console.Error.WriteLine("[cliente] La licencia ya no es valida tras la reconexion. Cerrando.");
        await conexion.StopAsync();
    }
};

Console.WriteLine($"[cliente] Conectando a {opciones.UrlServidor} con licencia {opciones.Licencia}...");

await conexion.StartAsync();

var registrada = await RegistrarAsync();

if (registrada)
{
    Console.WriteLine("[cliente] Conectado. Escuchando peticiones del servidor. Pulse Ctrl+C para salir.");
    Console.WriteLine("[cliente] Puede disparar una peticion REST desde otra terminal con:");
    Console.WriteLine($"  curl -H \"Authorization: Bearer $TOKEN\" \"{opciones.UrlServidor}/api/clientes?licencia={opciones.Licencia}\"");

    try
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await Task.Delay(Timeout.Infinite, cts.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("[cliente] Cerrando...");
    }
}
else
{
    Console.Error.WriteLine("[cliente] Abortando: la conexion no ha quedado registrada.");
    Environment.ExitCode = 1;
}

await conexion.StopAsync();
await conexion.DisposeAsync();

async Task<bool> RegistrarAsync()
{
    ResultadoRegistroConexion resultado;

    try
    {
        resultado = await hub.RegistrarAsync(new RegistroConexionSolicitud
        {
            Licencia = opciones.Licencia,
            NombreCliente = opciones.NombreCliente,
            Version = opciones.Version
        });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[cliente] No se pudo invocar 'RegistrarConexion': {ex.GetBaseException().Message}");
        return false;
    }

    if (!resultado.Exito)
    {
        Console.Error.WriteLine($"[cliente] Error al registrar la conexion: {resultado.Error}");
        return false;
    }

    Console.WriteLine($"[cliente] Conexion registrada: id={resultado.Conexion?.Id} " +
                      $"licencia={resultado.Conexion?.Licencia} cliente={resultado.Conexion?.Cliente} " +
                      $"activas={resultado.Conexion?.ConexionesActivas}/{resultado.Conexion?.MaxConexiones}");

    return true;
}

static async Task<string> ObtenerTokenDemoAsync(string urlServidor)
{
    using var http = new HttpClient();
    try
    {
        var respuesta = await http.GetAsync($"{urlServidor.TrimEnd('/')}/api/token-demo?usuario=cliente-demo");
        respuesta.EnsureSuccessStatusCode();

        var json = await respuesta.Content.ReadAsStringAsync();
        using var documento = System.Text.Json.JsonDocument.Parse(json);
        return documento.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("El servidor no devolvio un accessToken.");
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"No se pudo obtener un token de demo desde '{urlServidor}'. " +
            "Indica uno con --token o la variable de entorno ERP_TOKEN. " +
            $"Detalle: {ex.Message}");
    }
}