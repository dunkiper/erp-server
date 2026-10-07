namespace Erp.ClienteEjemplo;

public sealed class OpcionesCliente
{
    public string UrlServidor { get; set; } = "http://localhost:8080";

    public string Licencia { get; set; } = "ACME-1111-2222-3333";

    public string? Token { get; set; }

    public string NombreCliente { get; set; } = "erp-nodo";

    public string Version { get; set; } = "1.0.0";

    public int TotalClientes { get; set; } = 7;

    public int TiempoEsperaDesconexionSegundos { get; set; } = 5;
}