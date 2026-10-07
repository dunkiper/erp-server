using System.Text.Json.Serialization;

namespace Erp.Server.Contratos;

public enum EstadoCliente
{
    Desconocido = 0,
    Activo = 1,
    Inactivo = 2,
    Bloqueado = 3,
    Eliminado = 4
}

/// <summary>
/// Payload de la peticion que el servidor envia al cliente por el websocket.
/// </summary>
public sealed class SolicitudListaClientes
{
    [JsonPropertyName("peticionId")]
    public string PeticionId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("licencia")]
    public string Licencia { get; set; } = string.Empty;

    [JsonPropertyName("solicitante")]
    public string? Solicitante { get; set; }

    [JsonPropertyName("motivo")]
    public string? Motivo { get; set; }

    [JsonPropertyName("filtros")]
    public FiltrosListaClientes? Filtros { get; set; }

    [JsonPropertyName("incluirMetadatos")]
    public bool IncluirMetadatos { get; set; } = true;

    [JsonPropertyName("limite")]
    public int? Limite { get; set; }

    [JsonPropertyName("desplazamiento")]
    public int? Desplazamiento { get; set; }

    [JsonPropertyName("solicitadoUtc")]
    public DateTime SolicitadoUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tiempoEsperaMaximoMs")]
    public int TiempoEsperaMaximoMs { get; set; } = 15000;

    [JsonPropertyName("versionProtocolo")]
    public int VersionProtocolo { get; set; } = 1;
}

public sealed class FiltrosListaClientes
{
    [JsonPropertyName("soloActivos")]
    public bool SoloActivos { get; set; } = true;

    [JsonPropertyName("estados")]
    public List<EstadoCliente>? Estados { get; set; }

    [JsonPropertyName("licencias")]
    public List<string>? Licencias { get; set; }

    [JsonPropertyName("busqueda")]
    public string? Busqueda { get; set; }

    [JsonPropertyName("modificadosDesdeUtc")]
    public DateTime? ModificadosDesdeUtc { get; set; }
}

/// <summary>
/// Payload que el cliente devuelve por el websocket. Contiene la lista y el id de peticion
/// para que el servidor pueda correlacionarla con la llamada REST que la origino.
/// </summary>
public sealed class RespuestaListaClientes
{
    [JsonPropertyName("peticionId")]
    public string PeticionId { get; set; } = string.Empty;

    [JsonPropertyName("licencia")]
    public string Licencia { get; set; } = string.Empty;

    [JsonPropertyName("exito")]
    public bool Exito { get; set; } = true;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("instanteSolicitudUtc")]
    public DateTime? InstanteSolicitudUtc { get; set; }

    [JsonPropertyName("instanteRespuestaUtc")]
    public DateTime InstanteRespuestaUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("clientes")]
    public List<ClienteResumen> Clientes { get; set; } = [];

    [JsonPropertyName("hayMas")]
    public bool HayMas { get; set; }

    [JsonPropertyName("versionProtocolo")]
    public int VersionProtocolo { get; set; } = 1;
}

/// <summary>
/// Informacion de cada cliente que devuelve el ERP. Incluye identidad, licencia y
/// metadatos para que la API REST pueda mostrarla sin volver a preguntar.
/// </summary>
public sealed class ClienteResumen
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("licencia")]
    public string? Licencia { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("estado")]
    public EstadoCliente Estado { get; set; } = EstadoCliente.Desconocido;

    [JsonPropertyName("ultimaActividadUtc")]
    public DateTime? UltimaActividadUtc { get; set; }

    [JsonPropertyName("altaUtc")]
    public DateTime? AltaUtc { get; set; }

    [JsonPropertyName("empresa")]
    public string? Empresa { get; set; }

    [JsonPropertyName("telefono")]
    public string? Telefono { get; set; }

    [JsonPropertyName("etiquetas")]
    public List<string> Etiquetas { get; set; } = [];

    [JsonPropertyName("metadatos")]
    public Dictionary<string, string>? Metadatos { get; set; }
}