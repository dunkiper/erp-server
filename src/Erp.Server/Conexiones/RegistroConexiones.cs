using System.Collections.Concurrent;

namespace Erp.Server.Conexiones;

public sealed class RegistroConexion
{
    public required string Id { get; init; }

    public required string Licencia { get; init; }

    public string? NombreCliente { get; init; }

    public string? Version { get; init; }

    public required DateTime ConectadaUtc { get; init; }

    public DateTime UltimaActividadUtc { get; set; }

    public DateTime DesconectadaUtc { get; set; }

    public long MensajesRecibidos { get; set; }

    public long MensajesEnviados { get; set; }
}

public sealed class VistaConexion
{
    public required string Id { get; init; }

    public required string Licencia { get; init; }

    public string? NombreCliente { get; init; }

    public string? Version { get; init; }

    public required DateTime ConectadaUtc { get; init; }

    public required DateTime UltimaActividadUtc { get; init; }

    public long MensajesRecibidos { get; init; }

    public long MensajesEnviados { get; init; }

    public TimeSpan Duracion => DateTime.UtcNow - ConectadaUtc;
}

public interface IRegistroConexiones
{
    /// <summary>
    /// Registra una conexion nueva y devuelve la vista publica. Lanza si se supera el limite de la licencia.
    /// </summary>
    RegistroConexion Registrar(string id, string licencia, string? nombreCliente, string? version);

    void MarcarActividad(string id);

    void ContarMensajeRecibido(string id);

    void ContarMensajeEnviado(string id);

    void DarDeBaja(string id);

    int ContarPorLicencia(string licencia);

    IReadOnlyCollection<VistaConexion> Todas();

    IReadOnlyCollection<VistaConexion> PorLicencia(string licencia);

    bool Existe(string id);

    /// <summary>
    /// Selecciona la conexion mas reciente de una licencia (null si no hay ninguna).
    /// </summary>
    VistaConexion? MasReciente(string licencia);
}

public sealed class RegistroConexiones : IRegistroConexiones
{
    private readonly ConcurrentDictionary<string, RegistroConexion> _conexiones = new(StringComparer.Ordinal);

    public RegistroConexion Registrar(string id, string licencia, string? nombreCliente, string? version)
    {
        var ahora = DateTime.UtcNow;
        var registro = new RegistroConexion
        {
            Id = id,
            Licencia = licencia,
            NombreCliente = nombreCliente,
            Version = version,
            ConectadaUtc = ahora,
            UltimaActividadUtc = ahora
        };

        _conexiones[id] = registro;
        return registro;
    }

    public void MarcarActividad(string id)
    {
        if (_conexiones.TryGetValue(id, out var registro))
        {
            registro.UltimaActividadUtc = DateTime.UtcNow;
        }
    }

    public void ContarMensajeRecibido(string id)
    {
        if (_conexiones.TryGetValue(id, out var registro))
        {
            registro.MensajesRecibidos++;
            registro.UltimaActividadUtc = DateTime.UtcNow;
        }
    }

    public void ContarMensajeEnviado(string id)
    {
        if (_conexiones.TryGetValue(id, out var registro))
        {
            registro.MensajesEnviados++;
            registro.UltimaActividadUtc = DateTime.UtcNow;
        }
    }

    public void DarDeBaja(string id)
    {
        if (_conexiones.TryRemove(id, out var registro))
        {
            registro.DesconectadaUtc = DateTime.UtcNow;
        }
    }

    public int ContarPorLicencia(string licencia) =>
        _conexiones.Values.Count(c => string.Equals(c.Licencia, licencia, StringComparison.OrdinalIgnoreCase));

    public bool Existe(string id) => _conexiones.ContainsKey(id);

    public IReadOnlyCollection<VistaConexion> Todas() =>
        _conexiones.Values.Select(Proyectar).ToList();

    public IReadOnlyCollection<VistaConexion> PorLicencia(string licencia) =>
        _conexiones.Values
            .Where(c => string.Equals(c.Licencia, licencia, StringComparison.OrdinalIgnoreCase))
            .Select(Proyectar)
            .ToList();

    public VistaConexion? MasReciente(string licencia) =>
        _conexiones.Values
            .Where(c => string.Equals(c.Licencia, licencia, StringComparison.OrdinalIgnoreCase))
            .MaxBy(c => c.ConectadaUtc) is { } masReciente
            ? Proyectar(masReciente)
            : null;

    private static VistaConexion Proyectar(RegistroConexion registro) => new()
    {
        Id = registro.Id,
        Licencia = registro.Licencia,
        NombreCliente = registro.NombreCliente,
        Version = registro.Version,
        ConectadaUtc = registro.ConectadaUtc,
        UltimaActividadUtc = registro.UltimaActividadUtc,
        MensajesRecibidos = registro.MensajesRecibidos,
        MensajesEnviados = registro.MensajesEnviados
    };
}