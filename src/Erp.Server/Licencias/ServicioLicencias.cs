using System.Text.RegularExpressions;
using Erp.Server.Configuration;
using Microsoft.Extensions.Options;

namespace Erp.Server.Licencias;

public enum ResultadoLicencia
{
    Valida,
    FormatoInvalido,
    Desconocida,
    Inactiva,
    Expirada,
    LimiteConexionesAlcanzado
}

public sealed record ResultadoValidacionLicencia(
    ResultadoLicencia Resultado,
    string CodigoNormalizado,
    LicenciaCatalogo? Licencia,
    int ConexionesActivas,
    int MaxConexiones,
    string Mensaje)
{
    public bool EsValida => Resultado == ResultadoLicencia.Valida;
}

public interface IServicioLicencias
{
    string Normalizar(string? codigo);

    ResultadoValidacionLicencia Validar(string? codigo, int conexionesActivas = 0);

    IReadOnlyCollection<LicenciaCatalogo> Listar();
}

public sealed class ServicioLicencias : IServicioLicencias
{
    private readonly OpcionesLicencias _opciones;
    private readonly Regex _regex;
    private readonly Dictionary<string, LicenciaCatalogo> _catalogo;

    public ServicioLicencias(IOptions<OpcionesLicencias> opciones, ILogger<ServicioLicencias> logger)
    {
        _opciones = opciones.Value;
        _regex = new Regex(_opciones.Formato.Regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        _catalogo = new Dictionary<string, LicenciaCatalogo>(StringComparer.OrdinalIgnoreCase);

        foreach (var licencia in _opciones.Catalogo)
        {
            var codigo = Normalizar(licencia.Codigo);
            if (string.IsNullOrEmpty(codigo))
            {
                continue;
            }

            _catalogo[codigo] = licencia;
            logger.LogInformation("Licencia registrada: {Codigo} -> cliente {Cliente}", codigo, licencia.Cliente);
        }

        if (_catalogo.Count == 0)
        {
            logger.LogWarning(
                "El catalogo de licencias esta vacio: todas las conexiones seran rechazadas. Revise la seccion '{Seccion}' de appsettings.json o las variables de entorno Licencias__Catalogo__*.",
                OpcionesLicencias.Seccion);
        }
    }

    public string Normalizar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return string.Empty;
        }

        var normalizado = codigo.Trim();

        var separador = _opciones.Formato.Separador;
        var inicio = 0;
        var fin = normalizado.Length;

        while (inicio < fin && separador.Contains(normalizado[inicio]))
        {
            inicio++;
        }

        while (fin > inicio && separador.Contains(normalizado[fin - 1]))
        {
            fin--;
        }

        return normalizado[inicio..fin].ToUpperInvariant();
    }

    public ResultadoValidacionLicencia Validar(string? codigo, int conexionesActivas = 0)
    {
        var normalizado = Normalizar(codigo);

        if (string.IsNullOrEmpty(normalizado))
        {
            return new ResultadoValidacionLicencia(
                ResultadoLicencia.FormatoInvalido, normalizado, null, conexionesActivas, 0,
                "No se ha indicado codigo de licencia.");
        }

        if (!_regex.IsMatch(normalizado))
        {
            return new ResultadoValidacionLicencia(
                ResultadoLicencia.FormatoInvalido, normalizado, null, conexionesActivas, 0,
                $"El codigo '{normalizado}' no cumple el formato esperado ({_opciones.Formato.Segmentos} segmentos de {_opciones.Formato.LongitudSegmento} caracteres separados por '{_opciones.Formato.Separador}').");
        }

        if (!_catalogo.TryGetValue(normalizado, out var licencia))
        {
            return new ResultadoValidacionLicencia(
                ResultadoLicencia.Desconocida, normalizado, null, conexionesActivas, 0,
                $"La licencia '{normalizado}' no esta registrada.");
        }

        if (!licencia.Activa)
        {
            return new ResultadoValidacionLicencia(
                ResultadoLicencia.Inactiva, normalizado, licencia, conexionesActivas, licencia.MaxConexionesSimultaneas,
                $"La licencia '{normalizado}' esta desactivada.");
        }

        if (licencia.ExpiraUtc is { } expira && DateTimeOffset.UtcNow >= expira)
        {
            return new ResultadoValidacionLicencia(
                ResultadoLicencia.Expirada, normalizado, licencia, conexionesActivas, licencia.MaxConexionesSimultaneas,
                $"La licencia '{normalizado}' expiro el {expira:yyyy-MM-dd}.");
        }

        var maximo = licencia.MaxConexionesSimultaneas;
        if (conexionesActivas >= maximo)
        {
            return new ResultadoValidacionLicencia(
                ResultadoLicencia.LimiteConexionesAlcanzado, normalizado, licencia, conexionesActivas, maximo,
                $"La licencia '{normalizado}' ha alcanzado su limite de {maximo} conexiones simultaneas.");
        }

        return new ResultadoValidacionLicencia(
            ResultadoLicencia.Valida, normalizado, licencia, conexionesActivas, maximo,
            "Licencia valida.");
    }

    public IReadOnlyCollection<LicenciaCatalogo> Listar() => _catalogo.Values.ToList();
}