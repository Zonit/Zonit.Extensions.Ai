# Integracja MCP — klient zewnętrznych serwerów (v1.21)

> Aktualne dla `Zonit.Extensions.Ai` 1.21+. Klient MCP **wbudowany w core**
> — nie ma osobnego pakietu `Zonit.Extensions.Ai.Mcp` (zlikwidowany w 1.21,
> historia w sekcji [9. Pakiet](#9-pakiet)).
> **Jesteśmy klientem obcych MCP-ów, nie hostem.** Nie piszemy własnego
> serwera MCP — Microsoft ma gotowe rozwiązanie po stronie hostowej.

---

## 1. Zakres

- **In scope:** podłączanie do zewnętrznych serwerów MCP po **HTTP/SSE**
  (Server-Sent Events), pobieranie listy ich narzędzi przez `tools/list`,
  wywoływanie przez `tools/call`. Obsługa tokenu Bearer.
- **Out of scope (świadomie):** stdio, WebSocket, własny serwer MCP,
  auto-reconnect z zaawansowanymi strategiami (prosty retry w core wystarczy).

---

## 2. Jak to wygląda dla programisty

### 2.1 Inline — prosto, bez DI

```csharp
var result = await provider.GenerateAsync(
    new GPT5(),
    "Znajdź otwarte issues w repo zonit-sdk i pogrupuj je po obszarach.",
    mcps: [new Mcp("github", "https://mcp.github.example.com/sse", token)]);
```

### 2.2 Rejestracja w DI (wielokrotne użycie)

```csharp
// Program.cs
builder.Services.AddAiMcp(new Mcp("github", "https://mcp.github.example.com/sse", ghToken));
builder.Services.AddAiMcp(new Mcp("slack",  "https://mcp.slack.example.com/sse",  slackToken));

// Callsite — globalne MCP są dodawane do per-call mcps (additive). Bez
// argumentu mcps agent widzi tylko globalne. Per-call dodaje, nie zastępuje.
var result = await provider.GenerateAsync(new GPT5(), prompt);
```

### 2.3 Z konfiguracji (na razie ręcznie z `IConfiguration`)

Dedykowany helper `AddAiMcpFromConfiguration` jest na liście do dorobienia
([D11](./Agent-Deferred-Decisions.md)). Tymczasem prosty pattern:

```json
{
  "Ai": {
    "Mcp": {
      "Servers": [
        { "Name": "github", "Url": "https://mcp.github.example.com/sse", "Token": "${GITHUB_MCP_TOKEN}" },
        { "Name": "slack",  "Url": "https://mcp.slack.example.com/sse",  "Token": "${SLACK_MCP_TOKEN}" }
      ]
    }
  }
}
```

```csharp
foreach (var s in config.GetSection("Ai:Mcp:Servers").Get<List<McpDto>>() ?? [])
    builder.Services.AddAiMcp(new Mcp(s.Name, s.Url, s.Token));
```

---

## 3. Typ `Mcp`

```csharp
public sealed record Mcp
{
    /// Etykieta serwera — używana jako prefiks nazw narzędzi: "{Name}.{tool}".
    public string Name { get; }

    /// HTTPS endpoint serwera MCP.
    public string Url { get; }

    /// Opcjonalny Bearer token (dodawany jako "Authorization: Bearer {Token}").
    /// null = brak auth.
    public string? Token { get; }

    /// Opcjonalna whitelist remote tool names. null = wszystkie. [] = żadne.
    public IReadOnlyList<string>? AllowedTools { get; }

    public Mcp(
        string name,
        string url,
        string? token = null,
        IReadOnlyList<string>? allowedTools = null)
    { /* validation: name+url not empty, url absolute HTTPS */ }
}
```

**Breaking change vs 1.20:**
- Kolejność parametrów: `(name, url, token, allowedTools)` zamiast `(url, name, password)`.
- `Password` → `Token`.
- Nowy parametr `allowedTools` (whitelist remote tool names).
- Usunięty `Enabled` (zbędny — żeby tymczasowo wyłączyć serwer, zakomentuj
  rejestrację albo nie dołączaj go do `mcps:` w call-site).

---

## 4. Architektura wewnętrzna

```
┌────────────────────────────────────────────────────────┐
│  AgentRunner.ResolveToolsAsync                              │
│    1. Zbiera ITool z parametru `tools` (per-call).          │
│    2. Jeśli AgentOptions.DefaultTools (default true) →       │
│       dorzuca IToolRegistry.GetAll() z DI.                  │
│    3. Zbiera Mcp z parametru `mcps` (per-call).             │
│    4. Jeśli AgentOptions.DefaultMcp (default true) →          │
│       dorzuca IMcpRegistry.GetAll() z DI.                   │
│    5. McpToolFactory.BuildAsync(mcps) → lista McpTool.       │
│    6. Dedup po Name (caller > registry > MCP).              │
└─────────────────┬────────────────────────────────┬──────────┘
                  │                               │
                  ▼                               ▼
     ┌─────────────────────┐       ┌────────────────────────┐
     │ ToolBase<,>          │       │ McpToolFactory           │
     │  (lokalne,           │       │  - McpClient per Mcp     │
     │   z DI lub per-call) │       │  - tools/list per call,  │
     └─────────────────────┘       │    filtrowane AllowedTools│
                                    └─────────┬────────────────┘
                                              │
                                              ▼
                                    ┌────────────────────────┐
                                    │ McpTool : ITool          │
                                    │  InvokeAsync → client.   │
                                    │  CallToolAsync(remote..) │
                                    └────────────────────────┘
```

### 4.1 Kluczowe komponenty

- **`McpClient`** — JSON-RPC 2.0 nad HTTP/SSE (Streamable HTTP transport,
  spec 2025-03-26). Obsługuje `initialize`, `tools/list`, `tools/call`,
  sesję (`Mcp-Session-Id`) i `notifications/initialized`. Akceptuje zarówno
  `application/json` jak i `text/event-stream` w odpowiedzi.
- **`McpToolFactory`** — implementuje `IMcpToolFactory`. Tworzy klient per
  `Mcp` z named `HttpClient` (`McpToolFactory.HttpClientName`), wykonuje
  `tools/list`, aplikuje filtr `AllowedTools` i zwraca `McpTool[]`. Awarie
  per-serwer są logowane, ale **nie przerywają** całego runu — inne serwery
  działają dalej.
- **`McpTool : ITool`** — per-narzędzie wrapper. Eksponuje narzędzie pod
  nazwą `"{server}.{tool}"`.

### 4.2 Lazy connect

Podłączenie do serwera MCP następuje **przy pierwszym wywołaniu agenta**,
który ten serwer wykorzystuje. Nie płacimy kosztu handshake'u przy
starcie aplikacji ani przy zwykłych `GenerateAsync` nieagentowych.

```text
first use:
  client.InitializeAsync(ct)              // JSON-RPC "initialize"
  client.RefreshToolsAsync(ct)            // JSON-RPC "tools/list"
  adapters[] → AgentRunner

subsequent uses:
  client.CallToolAsync(name, args, ct)    // JSON-RPC "tools/call"
```

---

## 5. Nazewnictwo narzędzi

Narzędzia z serwera MCP widoczne są dla modelu pod nazwą `{Name}.{tool}`:

- `Mcp("https://...", "github")` + `list_issues` → `github.list_issues`
- `Mcp("https://...", "slack")`  + `send_message` → `slack.send_message`

Dla modeli nieakceptujących kropki w nazwie funkcji (niektóre wersje
niektórych providerów) fallback: `{Name}__{tool}`. Reguła doboru separatora
znajduje się w adapterze providera, nie w warstwie MCP.

---

## 6. Bezpieczeństwo

- Zezwalamy **tylko na HTTPS** (`Mcp` konstruktor rzuca dla nie-HTTPS).
- **`Token`** jest wkładany w `Authorization: Bearer {...}` i **nigdy nie
  jest logowany** — specjalny filtr w loggerze MCP (spójny z tym, jak
  `OpenAiProvider` maskuje klucze).
- **`Mcp.AllowedTools`** — whitelist na poziomie deskryptora serwera.
  Filtrowanie odbywa się przed wystawieniem narzędzi modelowi.
- **`AgentOptions.AllowedTools`** — dodatkowy filtr po finalnej nazwie
  (`server.tool`) w obrębie pojedynczego wywołania.
- `AgentOptions.OnToolCall` widzi wywołania MCP tak samo jak lokalne —
  jeden punkt autoryzacji dla wszystkiego.

---

## 7. Obsługa błędów

| Sytuacja                                | Zachowanie                                                                   |
|-----------------------------------------|------------------------------------------------------------------------------|
| Serwer MCP nieosiągalny przy pierwszym użyciu | Wyjątek z `GenerateAsync` (`McpConnectionException`). Agent nie rusza.   |
| `tools/list` zwraca błąd                | Jak wyżej — bez schematu narzędzi agent nie ma czego wysłać modelowi.        |
| `tools/call` zwraca błąd JSON-RPC       | `McpToolAdapter` zwraca JSON `{ "error": ..., "type": "mcp:<code>" }`. Model widzi to jak każdy inny błąd narzędzia. Zapisywane w `ToolInvocation`. |
| Serwer padnie w trakcie sesji           | Automatyczny jednorazowy retry z świeżym `initialize`. Gdy nadal źle — błąd do modelu jak wyżej. |
| Odpowiedź nie pasuje do schematu        | Walidacja lokalna (JSON Schema) przed `tools/call` oszczędza round-trip; błąd walidacji = błąd narzędzia. |
| Timeout                                 | `AiAgentOptions.ToolCallTimeout` stosowany per `tools/call`; przekroczenie = błąd narzędzia. |

---

## 8. Diagnostyka

- `ILogger<IMcpClient>` loguje ramki JSON-RPC na poziomie `Trace`
  (bez `Authorization`).
- `ActivitySource` **`Zonit.Ai.Mcp`** emituje spany:
  - `mcp.initialize` — atrybuty: `mcp.server`, `mcp.protocol_version`.
  - `mcp.tools.list` — atrybuty: `mcp.server`, `mcp.tool_count`.
  - `mcp.tools.call` — atrybuty: `mcp.server`, `mcp.tool`, `mcp.duration_ms`.
- Każde wywołanie MCP trafia także do `ResultAgent<T>.ToolCalls` z
  `ToolInvocation.McpServer = "<name>"`. Jedno miejsce do audytu dla
  narzędzi lokalnych i zdalnych.

---

## 9. Pakiet

Klient MCP **został wbudowany w core `Zonit.Extensions.Ai`** w wersji 1.21.
Wcześniej (1.20 i wcześniej) był osobnym pakietem `Zonit.Extensions.Ai.Mcp`.
Powód konsolidacji: jeden `services.AddAi()` wystarczy do pełnej obsługi
agenta + MCP.

- `AddAi()` rejestruje:
  - `IMcpToolFactory` → `McpToolFactory` (singleton),
  - `IMcpRegistry` → `McpRegistry` (singleton, czytany z `IEnumerable<Mcp>`),
  - named `HttpClient` o nazwie `McpToolFactory.HttpClientName`.
- `AddAiMcp(new Mcp(...))` rejestruje deskryptor jako `Singleton` (tylko
  metadata). Klient HTTP jest tworzony **leniwie** — dopiero gdy `AgentRunner`
  faktycznie potrzebuje `tools/list` w czasie rzeczywistego wywołania.
- **Brak** zależności od `Microsoft.Extensions.Hosting` — klient MCP działa
  również w konsolowych / serverless scenariuszach.
- Migracja z 1.20: usuń `using` lub `PackageReference Include="Zonit.Extensions.Ai.Mcp"`
  i `services.AddAiMcpClient()` — obie rzeczy są w 1.21 zbędne.

---

## 10. Co robimy z `ILlm.Tools` a co z MCP

Dla jasności (patrz też §14 w `Agent-Proposal.md`):

| Kategoria                     | Gdzie się pojawia             | Kto wykonuje                      |
|-------------------------------|-------------------------------|-----------------------------------|
| WebSearch, CodeInterpreter... | `ILlm.Tools` (natywne OpenAI) | API providera                     |
| `ToolBase<,>` — nasze custom  | `tools` parametr / DI         | `AgentRunner` lokalnie            |
| Narzędzia z serwera MCP       | `mcps` parametr / DI          | Zdalny serwer MCP, przez HTTP/SSE |

Wszystkie trzy mogą współistnieć w jednym `GenerateAsync`. Pętla agentowa
i tak widzi je przez jeden wspólny interfejs `ITool` (lub dane providera
w przypadku natywnych).

---

## 11. Przykład end-to-end

```csharp
// Program.cs
builder.Services.AddAi();                  // + auto-rejestracja ToolBase<,> + klient MCP
builder.Services.AddAiOpenAi();
builder.Services.AddAiAnthropic();
builder.Services.AddAiMcp(new Mcp(
    "github", "https://mcp.github.example.com/sse", ghToken));   // MCP w DI

// Gdzieś w serwisie:
public class IssueTriageService(IAiProvider provider)
{
    public async Task<TriageReport> RunAsync(CancellationToken ct)
    {
        var result = await provider.GenerateAsync(
            new Claude45Sonnet(),
            new IssueTriagePrompt(),
            ct);

        foreach (var call in result.ToolCalls)
            logger.LogInformation("[{It}] {T} via {Src}: {Ms} ms",
                call.Iteration, call.Name, call.McpServer ?? "local",
                call.Duration.TotalMilliseconds);

        return result.Value;
    }
}
```

MCP to dla programisty **jedna linijka w `Program.cs` lub jeden argument
w wywołaniu**. Wszystko pozostałe — lifecycle, JSON-RPC, mapowanie schematu,
audyt — załatwia biblioteka.
