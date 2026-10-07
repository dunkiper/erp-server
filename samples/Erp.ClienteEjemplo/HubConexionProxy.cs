using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.ClienteEjemplo;

/// <summary>
/// Proxy tipado sobre la conexion SignalR.
/// </summary>
public sealed class HubConexionProxy(HubConnection conexion)
{
    public HubConnection Nativo => conexion;

    public Task<ResultadoRegistroConexion> RegistrarAsync(RegistroConexionSolicitud solicitud, CancellationToken ct = default) =>
        conexion.InvokeAsync<ResultadoRegistroConexion>("RegistrarConexion", solicitud, ct);

    public Task EnviarAsync(string metodo, object payload, CancellationToken ct = default) =>
        conexion.InvokeAsync(metodo, payload, ct);
}

public static class SignalRExtensions
{
    public static IHubConnectionBuilder ConfigurarSignalR(this IHubConnectionBuilder builder, OpcionesCliente opciones)
    {
        return builder
            .WithUrl($"{opciones.UrlServidor.TrimEnd('/')}/hubs/conexion", op =>
            {
                if (!string.IsNullOrWhiteSpace(opciones.Token))
                {
                    op.AccessTokenProvider = () => Task.FromResult<string?>(opciones.Token);
                }
            })
            .AddJsonProtocol(o =>
            {
                o.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                o.PayloadSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
    }
}