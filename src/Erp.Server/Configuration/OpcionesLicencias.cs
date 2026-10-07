namespace Erp.Server.Configuration;

public sealed class OpcionesLicencias
{
    public const string Seccion = "Licencias";

    public FormatoLicencia Formato { get; set; } = new();

    public List<LicenciaCatalogo> Catalogo { get; set; } = [];

    /// <summary>
    /// Parametro de licencia admitido en el handshake del hub (query string) o en la cabecera.
    /// </summary>
    public string NombreParametroHandshake { get; set; } = "licencia";
}

public sealed class FormatoLicencia
{
    public string Separador { get; set; } = "-";

    public int Segmentos { get; set; } = 4;

    public int LongitudSegmento { get; set; } = 4;

    public string Regex => string.Concat(
        "^",
        string.Join($@"{Separador}", Enumerable.Repeat($"[A-Z0-9]{{{LongitudSegmento}}}", Segmentos)),
        "$");
}

public sealed class LicenciaCatalogo
{
    public string Codigo { get; set; } = string.Empty;

    public string Cliente { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    public DateTimeOffset? ExpiraUtc { get; set; }

    public int MaxConexionesSimultaneas { get; set; } = 5;
}