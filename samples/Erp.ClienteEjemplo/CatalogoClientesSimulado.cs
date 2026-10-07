using Microsoft.AspNetCore.SignalR.Client;

namespace Erp.ClienteEjemplo;

/// <summary>
/// Simula la base de datos del ERP: aqui se generaria la consulta real de clientes.
/// </summary>
public sealed class CatalogoClientesSimulado
{
    private readonly OpcionesCliente _opciones;

    public CatalogoClientesSimulado(OpcionesCliente opciones) => _opciones = opciones;

    public List<ClienteResumen> Obtener(FiltrosListaClientes? filtros, bool incluirMetadatos, int? limite, int? desplazamiento)
    {
        var todos = new List<ClienteResumen>();
        var ahora = DateTime.UtcNow;

        for (var i = 1; i <= _opciones.TotalClientes; i++)
        {
            todos.Add(new ClienteResumen
            {
                Id = $"CLI-{i:D4}",
                Nombre = $"Cliente Demo {i}",
                Email = $"cliente{i}@ejemplo.local",
                Licencia = i % 3 == 0 ? _opciones.Licencia : $"OTRA-{i:D4}-0000-0000",
                Version = $"2.{i}.0",
                Estado = i % 5 == 0 ? EstadoCliente.Inactivo : EstadoCliente.Activo,
                UltimaActividadUtc = ahora.AddMinutes(-i * 7),
                AltaUtc = ahora.AddDays(-i * 30),
                Empresa = $"Empresa {i} S.L.",
                Telefono = $"+34 900 000 {i:D3}",
                Etiquetas = i % 2 == 0 ? ["mayorista", "activo"] : ["minorista"]
            });
        }

        if (filtros is not null)
        {
            if (filtros.SoloActivos)
            {
                todos = todos.Where(c => c.Estado == EstadoCliente.Activo).ToList();
            }

            if (filtros.Estados is { Count: > 0 })
            {
                todos = todos.Where(c => filtros.Estados.Contains(c.Estado)).ToList();
            }

            if (filtros.Licencias is { Count: > 0 })
            {
                todos = todos
                    .Where(c => c.Licencia is not null &&
                                filtros.Licencias.Any(l => string.Equals(l, c.Licencia, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(filtros.Busqueda))
            {
                todos = todos
                    .Where(c => c.Nombre.Contains(filtros.Busqueda, StringComparison.OrdinalIgnoreCase) ||
                                (c.Email?.Contains(filtros.Busqueda, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            if (filtros.ModificadosDesdeUtc is { } desde)
            {
                todos = todos.Where(c => c.UltimaActividadUtc >= desde).ToList();
            }
        }

        var salto = Math.Max(desplazamiento ?? 0, 0);
        if (salto > 0)
        {
            todos = todos.Skip(salto).ToList();
        }

        if (limite is > 0 && todos.Count > limite)
        {
            todos = todos.Take(limite.Value).ToList();
        }

        if (!incluirMetadatos)
        {
            foreach (var cliente in todos)
            {
                cliente.Metadatos = null;
            }
        }
        else
        {
            foreach (var cliente in todos)
            {
                cliente.Metadatos = new Dictionary<string, string>
                {
                    ["origen"] = "catalogo-simulado",
                    ["sincronizadoUtc"] = DateTime.UtcNow.ToString("O")
                };
            }
        }

        return todos;
    }
}

public sealed class ManejadorSolicitudLista(HubConexionProxy hub, CatalogoClientesSimulado catalogo)
{
    public async Task HandleAsync(SolicitudListaClientes solicitud)
    {
        Console.WriteLine($"[cliente] Peticion recibida del servidor: {solicitud.PeticionId} " +
                          $"(solicitante={solicitud.Solicitante}, motivo={solicitud.Motivo ?? "n/d"})");

        RespuestaListaClientes respuesta;

        try
        {
            var clientes = catalogo.Obtener(
                solicitud.Filtros,
                solicitud.IncluirMetadatos,
                solicitud.Limite,
                solicitud.Desplazamiento);

            respuesta = new RespuestaListaClientes
            {
                PeticionId = solicitud.PeticionId,
                Licencia = solicitud.Licencia,
                Exito = true,
                InstanteSolicitudUtc = solicitud.SolicitadoUtc,
                Total = clientes.Count,
                Clientes = clientes,
                HayMas = solicitud.Limite is > 0 && clientes.Count >= solicitud.Limite,
                VersionProtocolo = solicitud.VersionProtocolo
            };
        }
        catch (Exception ex)
        {
            respuesta = new RespuestaListaClientes
            {
                PeticionId = solicitud.PeticionId,
                Licencia = solicitud.Licencia,
                Exito = false,
                Error = ex.Message,
                Total = 0
            };
        }

        Console.WriteLine($"[cliente] Respondiendo {respuesta.Total} clientes para la peticion {respuesta.PeticionId}");

        await hub.EnviarAsync("ResponderListaClientes", respuesta);
    }
}