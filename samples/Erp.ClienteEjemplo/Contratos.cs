using System.Text.Json.Serialization;

namespace Erp.ClienteEjemplo;

public enum EstadoCliente
{
    Desconocido = 0,
    Activo = 1,
    Inactivo = 2,
    Bloqueado = 3,
    Eliminado = 4
}

public sealed class RegistroConexionSolicitud
{
    [JsonPropertyName("licencia")]
    public string Licencia { get; set; } = string.Empty;

    [JsonPropertyName("nombreCliente")]
    public string? NombreCliente { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public sealed class ResultadoRegistroConexion
{
    [JsonPropertyName("exito")]
    public bool Exito { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("conexion")]
    public InfoConexion? Conexion { get; set; }
}

public sealed class InfoConexion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("licencia")]
    public string Licencia { get; set; } = string.Empty;

    [JsonPropertyName("cliente")]
    public string? Cliente { get; set; }

    [JsonPropertyName("conectadaUtc")]
    public DateTime ConectadaUtc { get; set; }

    [JsonPropertyName("maxConexiones")]
    public int MaxConexiones { get; set; }

    [JsonPropertyName("conexionesActivas")]
    public int ConexionesActivas { get; set; }
}

public sealed class SolicitudListaClientes
{
    [JsonPropertyName("peticionId")]
    public string PeticionId { get; set; } = string.Empty;

    [JsonPropertyName("licencia")]
    public string Licencia { get; set; } = string.Empty;

    [JsonPropertyName("solicitante")]
    public string? Solicitante { get; set; }

    [JsonPropertyName("motivo")]
    public string? Motivo { get; set; }

    [JsonPropertyName("filtros")]
    public FiltrosListaClientes? Filtros { get; set; }

    [JsonPropertyName("incluirMetadatos")]
    public bool IncluirMetadatos { get; set; }

    [JsonPropertyName("limite")]
    public int? Limite { get; set; }

    [JsonPropertyName("desplazamiento")]
    public int? Desplazamiento { get; set; }

    [JsonPropertyName("solicitadoUtc")]
    public DateTime SolicitadoUtc { get; set; }

    [JsonPropertyName("tiempoEsperaMaximoMs")]
    public int TiempoEsperaMaximoMs { get; set; }

    [JsonPropertyName("versionProtocolo")]
    public int VersionProtocolo { get; set; }
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
    public EstadoCliente Estado { get; set; }

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