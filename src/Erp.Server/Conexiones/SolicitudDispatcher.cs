using System.Collections.Concurrent;
using Erp.Server.Contratos;

namespace Erp.Server.Conexiones;

/// <summary>
/// Coordina la peticion REST -> websocket -> respuesta REST. Correlaciona por peticionId.
/// </summary>
public interface ISolicitudDispatcher
{
    Task<SolicitudListaClientes> CrearAsync(string licencia, string? solicitante, string? motivo,
        FiltrosListaClientes? filtros, int tiempoEsperaMaximoMs, CancellationToken cancellationToken);

    Task<RespuestaListaClientes> EsperarAsync(string peticionId, CancellationToken cancellationToken);

    void Resolver(string peticionId, RespuestaListaClientes respuesta);

    void Cancelar(string peticionId, string motivo);
}

public sealed class SolicitudDispatcher : ISolicitudDispatcher
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RespuestaListaClientes>> _pendientes = new(StringComparer.Ordinal);

    public Task<SolicitudListaClientes> CrearAsync(string licencia, string? solicitante, string? motivo,
        FiltrosListaClientes? filtros, int tiempoEsperaMaximoMs, CancellationToken cancellationToken)
    {
        var solicitud = new SolicitudListaClientes
        {
            Licencia = licencia,
            Solicitante = solicitante,
            Motivo = motivo,
            Filtros = filtros,
            TiempoEsperaMaximoMs = tiempoEsperaMaximoMs,
            IncluirMetadatos = filtros?.SoloActivos != false
        };

        var tcs = new TaskCompletionSource<RespuestaListaClientes>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendientes[solicitud.PeticionId] = tcs;

        return Task.FromResult(solicitud);
    }

    public async Task<RespuestaListaClientes> EsperarAsync(string peticionId, CancellationToken cancellationToken)
    {
        if (!_pendientes.TryGetValue(peticionId, out var tcs))
        {
            throw new InvalidOperationException($"No hay ninguna solicitud pendiente con el id '{peticionId}'.");
        }

        try
        {
            return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pendientes.TryRemove(peticionId, out _);
        }
    }

    public void Resolver(string peticionId, RespuestaListaClientes respuesta)
    {
        if (_pendientes.TryRemove(peticionId, out var tcs))
        {
            tcs.TrySetResult(respuesta);
        }
    }

    public void Cancelar(string peticionId, string motivo)
    {
        if (_pendientes.TryRemove(peticionId, out var tcs))
        {
            tcs.TrySetException(new OperacionCanceladaException(motivo));
        }
    }
}

public sealed class OperacionCanceladaException(string mensaje) : Exception(mensaje);