# ERP Server

Servidor ASP.NET Core (.NET 9) que expone **WebSocket via SignalR** y una **API REST** en el
mismo proceso. Ambas capas exigen **autenticacion JWT**, y cada conexion WebSocket se valida
ademas contra un **codigo de licencia**.

## Como funciona

```
Cliente REST                Servidor                          Cliente ERP (SignalR)
    |                          |                                        |
    |  GET /api/clientes       |                                        |
    |  ?licencia=ACME-...      |                                        |
    |  Authorization: Bearer   |                                        |
    |------------------------->|                                        |
    |                          |  busca conexion activa                |
    |                          |  con esa licencia                      |
    |                          |                                        |
    |                          |  "SolicitarListaClientes"  ---------->|
    |                          |  (websocket, con peticionId)           |
    |                          |                                        |
    |                          |  "ResponderListaClientes"  <----------|
    |                          |  (websocket, lista de clientes)        |
    |                          |                                        |
    |  200 + lista             |  correlaciona por peticionId           |
    |<-------------------------|                                        |
```

La correlacion REST <-> WebSocket se hace con el `peticionId` y un
`TaskCompletionSource` por peticion (`SolicitudDispatcher`). La llamada HTTP queda a la
espera con timeout; si el cliente no contesta, se devuelve `504`.

## Estructura

```
.
├── Dockerfile                  Build multi-stage (sdk:9.0 -> aspnet:9.0)
├── docker-compose.yml
├── .env.example
├── Erp.Server.sln
├── src/Erp.Server/
│   ├── Program.cs              Composicion de la web (JWT + SignalR + endpoints)
│   ├── Autenticacion/          Emision y validacion de tokens JWT
│   ├── Licencias/              Validacion del codigo de licencia y catalogo
│   ├── Conexiones/             Registro de conexiones + dispatcher REST<->WS
│   ├── Contratos/              DTOs del protocolo (payload del websocket)
│   ├── Endpoints/              Endpoints REST
│   └── Hubs/HubConexion.cs    Hub SignalR
└── samples/Erp.ClienteEjemplo/ Cliente de consola (.NET, sin Docker)
```

## Dos capas de seguridad

1. **JWT** - obligatorio en el hub y en los endpoints REST. En los WebSockets el token
   viaja en la query (`?access_token=`) porque el navegador no permite fijar cabeceras
   en un WebSocket. Se acepta en cualquier ruta `/hubs`.
2. **Licencia** - cada conexion WebSocket debe llamar a `RegistrarConexion` con un codigo
   de licencia. El servidor comprueba formato, existencia en el catalogo, que este activa,
   que no haya expirado y que no se supere el limite de conexiones simultaneas. Solo tras
   esa llamada la conexion queda operativa y puede atender peticiones.

Cada conexion es independiente: tiene su propio `connectionId`, su propio registro y cuenta
por separado contra el limite de la licencia. El limite se comprueba en el registro (dato
de la peticion), no con una lectura previa, para no abrir una condicion de carrera.

## Payloads del WebSocket

Peticion servidor -> cliente (`SolicitarListaClientes`):

```json
{
  "peticionId": "6b0fd50e5985406dada1d417b6bc8f54",
  "licencia": "ACME-1111-2222-3333",
  "solicitante": "admin",
  "motivo": "revision-inicial",
  "filtros": { "soloActivos": true, "estados": null, "licencias": null, "busqueda": null, "modificadosDesdeUtc": null },
  "incluirMetadatos": true,
  "limite": null,
  "desplazamiento": null,
  "solicitadoUtc": "2026-10-07T18:09:34.78Z",
  "tiempoEsperaMaximoMs": 15000,
  "versionProtocolo": 1
}
```

Respuesta cliente -> servidor (`ResponderListaClientes`). Devuelve el mismo `peticionId`
para que el servidor la correlacione con la peticion REST que la origino:

```json
{
  "peticionId": "6b0fd50e5985406dada1d417b6bc8f54",
  "licencia": "ACME-1111-2222-3333",
  "exito": true,
  "error": null,
  "instanteSolicitudUtc": "2026-10-07T18:09:34.78Z",
  "instanteRespuestaUtc": "2026-10-07T18:09:34.93Z",
  "total": 6,
  "clientes": [
    {
      "id": "CLI-0001",
      "nombre": "Cliente Demo 1",
      "email": "cliente1@ejemplo.local",
      "licencia": "OTRA-0001-0000-0000",
      "version": "2.1.0",
      "estado": "Activo",
      "ultimaActividadUtc": "2026-10-07T18:02:34.92Z",
      "altaUtc": "2026-09-07T18:09:34.92Z",
      "empresa": "Empresa 1 S.L.",
      "telefono": "+34 900 000 001",
      "etiquetas": ["minorista"],
      "metadatos": { "origen": "catalogo-simulado", "sincronizadoUtc": "2026-10-07T18:09:34.93Z" }
    }
  ],
  "hayMas": false,
  "versionProtocolo": 1
}
```

`estado` viaja como texto y las fechas en ISO-8601 UTC. **El servidor registra
`JsonStringEnumConverter` tanto en SignalR como en la API REST**: sin esto, un cliente que
serialice enums como texto provoca un fallo de binding de argumentos en el servidor.

El payload lleva suficiente informacion para que la API REST devuelva la lista sin volver a
preguntar: identidad, contacto, licencia, version, estado, marcas temporales, etiquetas y un
diccionario libre de metadatos.

## API REST

| Metodo | Ruta | Auth | Descripcion |
|---|---|---|---|
| GET | `/health` | no | Sonda de salud |
| GET | `/api/saludo` | no | Endpoint de prueba |
| GET | `/api/token-demo?usuario=admin` | no | Emite un JWT de ejemplo (solo desarrollo) |
| GET | `/api/conexiones` | si | Conexiones WebSocket activas |
| GET | `/api/clientes?licencia=...` | si | Pide la lista por WebSocket al cliente de esa licencia |

Parametros de `/api/clientes`: `licencia` (obligatorio), `motivo`, `soloActivos`,
`limite`, `tiempoEsperaMs` (500..120000, por defecto 15000).

Si hay varias conexiones con la misma licencia, responde la **mas reciente**.

### Codigos de respuesta

| Codigo | Cuando |
|---|---|
| 200 | Lista recibida |
| 400 | Falta el parametro `licencia` |
| 401 | Token JWT ausente o invalido |
| 403 | Licencia con formato desconocido, no registrada, inactiva o expirada |
| 404 | Licencia valida pero sin ninguna conexion activa |
| 504 | El cliente no respondio dentro del timeout |

## Ejecucion con Docker

```bash
cp .env.example .env
# edite .env y ponga una clave JWT real de 32+ caracteres
docker compose up -d --build
docker compose logs -f erp-server
```

El servidor escucha en `http://localhost:8080`. La clave JWT es **obligatoria**: si falta,
el arranque falla a proposito en lugar de usar una clave por defecto debil.

Para generar una clave:

```bash
openssl rand -base64 48
```

## Ejecucion local sin Docker

```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/Erp.Server
```

En `Development` la clave JWT de ejemplo ya viene en `appsettings.Development.json`.
En `Production` hay que supplyarla:

```bash
export Jwt__SigningKey="$(openssl rand -base64 48)"
dotnet run --project src/Erp.Server
```

Swagger queda disponible en `/swagger` solo en `Development`.

## Cliente de ejemplo

Consola .NET independiente, sin Docker. Simula un nodo ERP con un catalogo de clientes
de prueba.

```bash
# Si no se indica token, lo pide a /api/token-demo
dotnet run --project samples/Erp.ClienteEjemplo -- \
  --url http://localhost:8080 \
  --licencia ACME-1111-2222-3333
```

Tambien acepta variables de entorno (`ERP_URL_SERVIDOR`, `ERP_LICENCIA`, `ERP_TOKEN`,
`ERP_NOMBRE_CLIENTE`, `ERP_LOG_LEVEL`) y `appsettings.json`.

Para realimentar la respuesta hay que cambiar `CatalogoClientesSimulado.Obtener` por la
consulta real a la base de datos del ERP; el resto del protocolo no cambia.

## Prueba manual del flujo completo

```bash
# 1. Servidor
docker compose up -d --build

# 2. Cliente conectado (terminal 1)
dotnet run --project samples/Erp.ClienteEjemplo -- --licencia ACME-1111-2222-3333

# 3. Peticion REST (terminal 2)
TOKEN=$(curl -s "http://localhost:8080/api/token-demo?usuario=admin" | jq -r .accessToken)
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/clientes?licencia=ACME-1111-2222-3333" | jq
```

## Licencias

Catalogo configurable en `appsettings.json` (`Licencias:Catalogo`) o por variables de
entorno `Licencias__Catalogo__N__*`. El formato por defecto son 4 segmentos de 4
caracteres separados por guion (`ACME-1111-2222-3333`), tambien configurable.

```json
{
  "Codigo": "ACME-1111-2222-3333",
  "Cliente": "ACME Industrial S.L.",
  "Activa": true,
  "ExpiraUtc": null,
  "MaxConexionesSimultaneas": 5
}
```

Para sustituir el catalogo estatico por el sistema de licencias real, implemente
`IServicioLicencias` y registre esa implementacion en `Program.cs`.

## Notas para produccion

- `/api/token-demo` emite un token sin comprobar credenciales. Debe eliminarse, o protegerse,
  antes de exponer el servicio. En un despliegue real el token lo emite el sistema de
  identidad de la empresa.
- `Jwt:SigningKey` es simetrica (HMAC-SHA256). Con varios emisores de token conviene
  migrar a claves asimetricas.
- Sirva el contenedor tras un proxy inverso con TLS: hoy `RequireHttpsMetadata` esta a
  `false` porque el handshake se hace en claro en desarrollo.
- Las claves de Data Protection se regeneran en cada arranque del contenedor. Si anade
  cookies de sesion o cifrado, monte un volumen para `/home/app/.aspnet/DataProtection-Keys`.