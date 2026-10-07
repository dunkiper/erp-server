namespace Erp.Server.Configuration;

public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";

    public string Issuer { get; set; } = "erp-server";

    public string Audience { get; set; } = "erp-clientes";

    public string SigningKey { get; set; } = string.Empty;

    public int ExpirationMinutos { get; set; } = 60;

    public int ClockSkewSegundos { get; set; } = 30;
}