# Propozycja: AI Agent w `Zonit.Extensions.Ai` (v2)

> Status: **wdrożone w 1.21** — dokument zachowany jako RFC z historią
> projektowych decyzji. Tam, gdzie aktualne API różni się od propozycji,
> obowiązuje [`Agent-Examples.md`](./Agent-Examples.md) i
> [`Agent-Mcp.md`](./Agent-Mcp.md) (źródło prawdy dla użytkowników).
> Powiązane: [`Agent-Examples.md`](./Agent-Examples.md), [`Agent-Mcp.md`](./Agent-Mcp.md), [`Agent-Deferred-Decisions.md`](./Agent-Deferred-Decisions.md).

## Changelog vs propozycja — co się zmieniło przy wdrożeniu

| Element propozycji                                | Stan w 1.21                                                                                |
|---------------------------------------------------|--------------------------------------------------------------------------------------------|
| `AddAiTools()` (osobne wywołanie)                  | **Usunięte.** `AddAi()` auto-rejestruje narzędzia przez `[ModuleInitializer]` source generatora. Stary call to no-op `[Obsolete]`. |
| `Mcp(url, name, password?)`                       | **Zmienione na `Mcp(name, url, token?, allowedTools?)`.** `Password` → `Token`.            |
| `Mcp.Enabled`                                     | **Nie istnieje.** Wyłączenie = nie rejestruj / nie dodawaj do `mcps:`.                     |
| `ITool.Strict` / `ToolBase<,>.Strict`             | **Usunięte.** OpenAI adapter hardkoduje `strict: true` w schemacie funkcji.                |
| `AgentOptions.MaxCost` + `AgentCostLimitException`| **Usunięte.** Patrz [D9](./Agent-Deferred-Decisions.md#d9-limity-kosztu--miękki-vs-twardy-otwarte-zamknięte-w-121-nie-implementujemy). |
| Pakiet `Zonit.Extensions.Ai.Mcp`                  | **Zlikwidowany.** Klient MCP wbudowany w core; `AddAiMcpClient()` nie istnieje.            |
| Tools/MCP per-call **nadpisują** DI               | **Zmienione na additive** — per-call jest dodawane do globalnych. Opt-out: `AgentOptions.DefaultTools = false` / `DefaultMcp = false`. |
| `AgentOptions.MaxParallelToolCalls`               | Bez zmian, ale doprecyzowane: to **rozmiar kolejki**, nigdy nie odrzuca tool-callów.       |

Reszta propozycji (patrz dalej) odpowiada implementacji 1:1, modulo
standardowe drobiazgi nazewnicze.

---

## 1. Cel

Dodać do biblioteki obsługę **AI Agenta** — modelu, który w pętli wywołuje
narzędzia (własne + zewnętrzne MCP) aż do finalnej odpowiedzi — bez zmian
w dotychczasowym prostym API pojedynczego requestu i bez obciążania
programisty zarządzaniem pętlą, dispatcherem narzędzi czy protokołem MCP.

Dwie żelazne zasady:

1. **Prostota dla programisty aplikacji** — `provider.GenerateAsync(...)` ze
   znanych sygnatur, narzędzia pisane jak `PromptBase<T>` (klasa z
   `ExecuteAsync`), wynik zwraca ścieżkę audytu wywołań.
2. **Prostota dla programisty providera** — cała pętla, równoległość, MCP,
   limity, trace żyją w core (`AgentRunner`). Provider dostaje **mały
   adapter** (`IAgentProviderAdapter`). Dodanie agenta do nowego modelu
   (OpenAI, Claude, Gemini, Grok) ≈ 100 LOC.

---

## 2. Co zostaje, co dochodzi

**Zostaje bez zmian:**

- `IAiProvider` jako fasada — dostaje tylko nowe overload'y.
- `ILlm`, `IPrompt<T>`, `PromptBase<T>`, `SimplePrompt<T>`, `Result<T>`,
  `MetaData`, `JsonSchemaGenerator`.
- `ILlm.Tools` — **pozostaje wyłącznie dla narzędzi wbudowanych providera**
  (WebSearch, CodeInterpreter, FileSearch, XSearch). To są narzędzia
  wykonywane po stronie OpenAI/Anthropic. Niezwiązane z naszym systemem
  custom tools.
- `AiProviderRegistrationGenerator` — wzorzec rozszerzymy dla narzędzi.

**Dochodzi:**

- `ToolBase<TInput, TOutput>` — bazowa klasa **własnych** narzędzi.
- `ITool` — interfejs niegeneryczny dla dynamicznych scenariuszy.
- `IAgentLlm : ILlm` — capability marker „ten model potrafi być agentem”.
- `Mcp(url, name, password?)` — wartość opisująca **zewnętrzny** serwer MCP
  (HTTP/SSE). Jesteśmy klientem, nie hostem.
- `ResultAgent<T> : Result<T>` — wynik z listą `ToolInvocation`.
- `AgentOptions` — opcje per-wywołanie (timeout, budżet kosztu, whitelist,
  guard hook).
- `AiAgentOptions` (sekcja `Ai:Agent` w `appsettings.json`) — opcje globalne
  (MaxIterations, MaxParallelToolCalls, ToolCallTimeout, policy na wyjątki).
- `AgentRunner` (core) + `IAgentProviderAdapter` (per-provider).
- `IToolRegistry` + source generator `AiToolRegistrationGenerator` — auto
  rejestracja `ToolBase<,>` w DI (jedno `AddAiTools()`).
- Streaming: `GenerateStreamAsync` (formerly `StreamAgentAsync`) → `IAsyncEnumerable<AgentEvent>`.

**Odrzucone (patrz [`Agent-Deferred-Decisions.md`](./Agent-Deferred-Decisions.md)):**

- Automatyczne dostarczanie narzędzi dla zwykłego (nie-agentowego)
  `GenerateAsync` — breaking change, świadomie poza zakresem.
- Własny serwer MCP hostowany przez nas — Microsoft ma gotowe rozwiązanie.
- Obsługa stdio MCP jako first-class — tylko HTTP.

---

## 3. Narzędzie — `ToolBase<TInput, TOutput>`

Analogia 1:1 do `PromptBase<T>`: dziedziczysz, uzupełniasz `Name`,
`Description`, `ExecuteAsync`. Schemat JSON parametrów generuje się z
typu `TInput` przez istniejący `JsonSchemaGenerator`.

```csharp
public abstract class ToolBase<TInput, TOutput> : ITool
    where TInput : class
{
    /// Nazwa widoczna dla modelu (unikalna w obrębie agenta).
    public abstract string Name { get; }

    /// Opis dla modelu — co robi narzędzie i kiedy go użyć.
    public abstract string Description { get; }

    /// Właściwe wykonanie. Dostajesz zdeserializowany TInput, zwracasz TOutput.
    /// Możesz rzucić wyjątek — AgentRunner go złapie i przekaże modelowi jako
    /// wynik tool_result (pkt 8 feedbacku, Claude to rozumie i reaguje).
    public abstract Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken);

    // Strict został usunięty w 1.21 — OpenAI adapter hardkoduje strict:true.
}
```

Interfejs minimalny (dla dynamicznych narzędzi, pluginów, MCP adaptera):

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }

    /// Schemat JSON parametrów (generowany automatycznie dla ToolBase<,>).
    JsonElement InputSchema { get; }

    /// Generyczne wywołanie — surowy JSON in/out. Używane przez AgentRunner.
    Task<JsonElement> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken);
}
```

`ToolBase<,>` implementuje `ITool.InvokeAsync` za programistę: deserializacja
`TInput` → wywołanie `ExecuteAsync` → serializacja `TOutput` → łapanie wyjątku.

### 3.1 Przykład

```csharp
public class SaveToDatabaseTool(IMyDb db)
    : ToolBase<SaveToDatabaseTool.Input, SaveToDatabaseTool.Output>
{
    public override string Name => "save_to_database";
    public override string Description =>
        "Zapisuje rekord do bazy i zwraca nadany identyfikator.";

    public override async Task<Output> ExecuteAsync(Input input, CancellationToken ct)
    {
        var id = await db.SaveAsync(input.Key, input.Value, ct);
        return new Output { Id = id };
    }

    public class Input
    {
        [Description("Klucz rekordu, unikalny.")]
        public required string Key { get; init; }

        [Description("Wartość do zapisania.")]
        public required string Value { get; init; }
    }

    public class Output
    {
        public Guid Id { get; set; }
    }
}
```

### 3.2 Wyjątki w narzędziach (feedback pkt 8)

- Narzędzie może rzucić dowolny wyjątek — **nie trzeba try/catchować**.
- `AgentRunner` łapie, serializuje do `{ "error": "...", "type": "..." }`
  i odsyła modelowi jako `tool_result` / `function_call_output`.
- Model (Claude 4.5, GPT-5, Gemini 2.5) widzi błąd i reaguje: retry z innymi
  parametrami, fallback albo zgłoszenie użytkownikowi.
- Do `ResultAgent<T>.ToolCalls` trafia `ToolInvocation.Error` + `ErrorType`.
- Domyślną politykę można przełączyć na „rzucaj do wywołującego” przez
  `AiAgentOptions.OnToolException = ThrowToCaller` (patrz §10).

---

## 4. Capability: `IAgentLlm` (feedback pkt 3, 4)

```csharp
/// Marker: „ten LLM potrafi wywoływać narzędzia w pętli agentowej”.
public interface IAgentLlm : ILlm
{
    /// Domyślny limit iteracji (może być nadpisany przez AgentOptions
    /// albo globalne AiAgentOptions). Default: 100 — agenci często robią
    /// po kilkadziesiąt wywołań, więc dajemy duży ale bezpieczny sufit.
    int DefaultMaxIterations => 100;
}
```

Modele wspierające function calling (GPT-5, GPT-4.1, Claude 4.5, Gemini 2.5,
Grok 4, DeepSeek-V3, Mistral Large, …) dostają `IAgentLlm`. Modele audio,
embedding, image — nie. Kompilator zapewnia, że overload'y agentowe
`GenerateAsync` nie zostaną wywołane na niewłaściwym modelu.

---

## 5. Zewnętrzne MCP — `Mcp` (feedback pkt 5)

Jesteśmy **klientem** obcych serwerów MCP. Nie hostujemy, nie uruchamiamy
procesów lokalnie. Prosty, serializowalny typ wartości:

```csharp
public sealed record Mcp
{
    public string Name { get; }                          // prefiks narzędzi: "{Name}.{tool}"
    public string Url  { get; }                          // HTTPS endpoint
    public string? Token { get; }                        // null = bez auth; inaczej Bearer
    public IReadOnlyList<string>? AllowedTools { get; }  // whitelist remote tool names; null = wszystkie

    public Mcp(string name, string url, string? token = null,
               IReadOnlyList<string>? allowedTools = null) { /* guard */ }
}
```

Użycie punktowe (bez rejestracji w DI):

```csharp
var result = await provider.GenerateAsync(
    new GPT5(),
    prompt,
    tools: [new SaveToDatabaseTool(db)],
    mcps:  [new Mcp("github",   "https://mcp.github.example.com/sse", token),
            new Mcp("intranet", "https://mcp.intranet.example.com/sse")]);
```

Opcjonalna rejestracja w DI (gdy używasz tego samego MCP w wielu miejscach):

```csharp
services.AddAiMcp(new Mcp("github", "https://mcp.github.example.com/sse", token));
```

Szczegóły protokołu i lifecycle w [`Agent-Mcp.md`](./Agent-Mcp.md).

---

## 6. Rejestracja narzędzi — automatyczna (feedback pkt 11) [zaktualizowane w 1.21]

Developer pisze same klasy `ToolBase<,>`, nigdzie nie wylistowuje ich ręcznie.

```csharp
public class SaveToDatabaseTool : ToolBase<...> { ... }
public class GetWeatherTool    : ToolBase<...> { ... }
```

W `Program.cs` jedno wywołanie:

```csharp
builder.Services.AddAi();          // ← *to* auto-rejestruje wszystkie ToolBase<,>
builder.Services.AddAiOpenAi();
builder.Services.AddAiAnthropic();
```

Mechanizm (1.21): `AiToolRegistrationGenerator` emituje w każdym
konsumenckim assembly `[ModuleInitializer]`, który zgłasza każdą
nie-abstrakcyjną klasę dziedziczącą z `ToolBase<,>` do statycznego
`ToolDiscovery`. Przy `AddAi()` runtime czyta `ToolDiscovery.RegisteredTypes`
i rejestruje typy jako `Scoped` przez `TryAddEnumerable<ITool>` —
idempotentnie.

Dla scenariuszy dynamicznych (pluginy, narzędzia budowane runtime, gotowe
instancje):

```csharp
services.AddAiTools<SaveToDatabaseTool>();            // typ
services.AddAiTools(new ReportBugTool(githubClient)); // instancja (singleton)
services.AddAiTools(sp => new CustomInlineTool(...)); // factory (scoped)
```

> Stare API `AddAiTool<T>()` / `AddAiTools()` (bez parametrów) wciąż istnieje
> jako alias — ale nie musisz go już wołać.

---

## 7. Wejście — nowe overload'y `IAiProvider.GenerateAsync` (feedback pkt 14)

Dotychczasowe overload'y **nie zmieniają zachowania** — zwykły
`provider.GenerateAsync(new GPT5(), prompt)` dalej **nie** korzysta z narzędzi
ani agenta. To świadoma decyzja (pkt 14 feedbacku), zapisana w
[`Agent-Deferred-Decisions.md`](./Agent-Deferred-Decisions.md).

Agent to **jawna** ścieżka przez nowe overload'y z `IAgentLlm`:

```csharp
// Typowany prompt + typowana odpowiedź.
Task<ResultAgent<TResponse>> GenerateAsync<TResponse>(
    IAgentLlm agent,
    IPrompt<TResponse> prompt,
    IEnumerable<ITool>? tools = null,
    IEnumerable<Mcp>?   mcps  = null,
    AgentOptions?       options = null,
    CancellationToken   cancellationToken = default);

// String in / string out (quick path).
Task<ResultAgent<string>> GenerateAsync(
    IAgentLlm agent,
    string prompt,
    IEnumerable<ITool>? tools = null,
    IEnumerable<Mcp>?   mcps  = null,
    AgentOptions?       options = null,
    CancellationToken   cancellationToken = default);
```

Rozwiązywanie narzędzi i MCP **(zmienione w 1.21 na additive)**:

| Parametr | `null` (domyślnie)                                            | Wartość jawna                                                       |
|----------|---------------------------------------------------------------|--------------------------------------------------------------------|
| `tools`  | wszystkie `ToolBase<,>` zarejestrowane w DI                   | **dodawane** do globalnych (additive). Opt-out: `AgentOptions.DefaultTools = false`. |
| `mcps`   | wszystkie `Mcp` zarejestrowane w DI                           | **dodawane** do globalnych. Opt-out: `AgentOptions.DefaultMcp = false`.              |

`AgentOptions.AllowedTools` może dodatkowo zawęzić wybór po nazwie
(np. `"github.read_file"` dla narzędzia z MCP).

---

## 8. Wynik — `ResultAgent<T>` (feedback pkt 6)

Nowy typ, dedykowany dla agenta — nie zaśmiecamy `Result<T>`.

```csharp
public class ResultAgent<T> : Result<T>
{
    /// Ile tur (model call → tools → model call → ...) wykonał agent.
    public required int Iterations { get; init; }

    /// Pełna lista wywołań narzędzi z inputami i outputami — w kolejności wykonania.
    public required IReadOnlyList<ToolInvocation> ToolCalls { get; init; }

    /// Suma tokenów po wszystkich iteracjach.
    public required TokenUsage TotalUsage { get; init; }

    /// Suma kosztu po wszystkich iteracjach.
    public required Price TotalCost { get; init; }
}

public sealed record ToolInvocation
{
    public int          Iteration  { get; init; }
    public string       Name       { get; init; } = "";
    public JsonElement  Input      { get; init; } // dokładnie to, co dostało narzędzie
    public JsonElement? Output     { get; init; } // null gdy wyjątek
    public string?      Error      { get; init; } // message wyjątku
    public string?      ErrorType  { get; init; } // pełny typ wyjątku
    public TimeSpan     Duration   { get; init; }
    public string?      McpServer  { get; init; } // nazwa MCP, null dla lokalnych
}
```

Dzięki temu developer może:

- zrzucić `ToolCalls` do bazy audytowej / SIEM,
- przekazać je innemu modelowi do weryfikacji („czy agent zrobił dobrze?”),
- wyrenderować timeline wywołań w UI,
- policzyć czas spędzony w narzędziach vs w modelu.

`ResultAgent<T>.MetaData` zawiera dane **ostatniej** iteracji modelu
(kompatybilne z kodem, który już patrzy w `Result.MetaData`).

---

## 9. Opcje per-wywołanie — `AgentOptions`

```csharp
public sealed class AgentOptions
{
    /// Nadpisanie globalnego MaxIterations (z AiAgentOptions / IAgentLlm.DefaultMaxIterations).
    public int? MaxIterations { get; init; }

    /// Nadpisanie globalnego MaxParallelToolCalls.
    public int? MaxParallelToolCalls { get; init; }

    /// Twardy timeout całego wywołania agenta.
    public TimeSpan? Timeout { get; init; }

    /// Czy dołączyć globalnie zarejestrowane narzędzia (z `services.AddAiTools(...)`).
    /// Default: true. Per-call `tools:` jest *dodawane* do tego zbioru.
    public bool DefaultTools { get; init; } = true;

    /// Czy dołączyć globalnie zarejestrowane MCP (z `services.AddAiMcp(...)`).
    /// Default: true. Per-call `mcps:` jest *dodawane*.
    public bool DefaultMcp { get; init; } = true;

    // MaxCost został usunięty w 1.21 — patrz Agent-Deferred-Decisions D9.

    /// Whitelist nazw narzędzi (po nazwie zewnętrznej, np. "github.read_file"). Null = wszystkie.
    public IReadOnlyCollection<string>? AllowedTools { get; init; }

    /// Hook wywoływany przed każdym tool-call. Zwrócenie false blokuje wywołanie.
    /// Model dostaje tool_result z błędem "blocked by policy" i decyduje co dalej.
    public Func<ToolInvocation, CancellationToken, ValueTask<bool>>? OnToolCall { get; init; }
}
```

---

## 10. Opcje globalne — `AiAgentOptions` (feedback pkt 13)

Nowa sekcja w istniejącym `AiOptions`, bindowana z `appsettings.json`:

```csharp
public sealed class AiAgentOptions
{
    /// Domyślny limit iteracji, gdy nikt inny go nie ustawi.
    /// Duży, ale bezpieczny — agenci często robią po kilkadziesiąt tool-calli.
    public int MaxIterations { get; set; } = 100;

    /// Limit jednoczesnych wywołań narzędzi w jednej turze.
    /// Duży, ale nie unlimited — chroni przed "DDOS-em własnej bazy".
    public int MaxParallelToolCalls { get; set; } = 16;

    /// Domyślny timeout pojedynczego wywołania narzędzia.
    public TimeSpan ToolCallTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// Co robić gdy narzędzie rzuci wyjątek:
    ///  - ReturnErrorToModel (default) — model dostaje JSON błędu i decyduje,
    ///  - ThrowToCaller — wyjątek propaguje się z GenerateAsync.
    public ToolExceptionPolicy OnToolException { get; set; }
        = ToolExceptionPolicy.ReturnErrorToModel;
}

public enum ToolExceptionPolicy { ReturnErrorToModel, ThrowToCaller }
```

`appsettings.json`:

```json
{
  "Ai": {
    "Agent": {
      "MaxIterations": 100,
      "MaxParallelToolCalls": 16,
      "ToolCallTimeout": "00:02:00",
      "OnToolException": "ReturnErrorToModel"
    }
  }
}
```

Kolejność ważności (od najsilniejszej):
`AgentOptions` (per-call) → `AiAgentOptions` (globalne) → `IAgentLlm` (model).

---

## 11. Równoległe wywołania narzędzi (feedback pkt 7)

Claude 4.5, GPT-5 Responses API, Gemini 2.5 i Grok 4 potrafią w jednej
odpowiedzi zwrócić **kilka bloków tool_use / function_call**. Wszystkie
muszą zostać wykonane i odesłane **jako spójny batch** w jednej kolejnej
wiadomości — inaczej Claude rzuca błąd „unused tool_use blocks”.

`AgentRunner` zawsze wykonuje je równolegle:

```text
Tura N:  model → [call_A, call_B, call_C]
AgentRunner:
  using var gate = new SemaphoreSlim(MaxParallelToolCalls);
  var results = await Task.WhenAll(calls.Select(async c => {
      await gate.WaitAsync(ct);
      try  { return await Invoke(c, ct); }
      finally { gate.Release(); }
  }));
  session.AppendToolResults(results);   // wszystkie naraz
Tura N+1: kontynuacja z modelu.
```

Wnioski:

- Równoległość jest **obligatoryjna** (nie opcjonalna).
- Limit konfigurowalny: `AgentOptions.MaxParallelToolCalls` → `AiAgentOptions.MaxParallelToolCalls` (default 16).
- `0` / `1` = sekwencyjnie. `int.MaxValue` = bez limitu (świadoma decyzja).
- Kolejność `ToolInvocation` w `ResultAgent.ToolCalls` = kolejność wywołania, nie ukończenia.

---

## 12. Architektura — core vs provider (feedback pkt 12)

**Cel:** dołożenie agenta do nowego providera nie wymaga znajomości MCP,
pętli, równoległości ani walidacji schematu. Provider wie tylko, jak
**rozmawiać z własnym API w turach**.

```
┌────────────────────────────────────────────────────────────────────┐
│  IAiProvider.GenerateAsync(IAgentLlm, prompt, tools, mcps, options)│
│                           │                                         │
│                           ▼                                         │
│                    AgentRunner (core)                               │
│   1. Zbuduj listę ITool:                                            │
│        - ToolBase<,> z tools lub DI,                                │
│        - McpToolAdapter z mcps lub DI (tools/list po connect).      │
│   2. Zbuduj AgentSession (prompt, tools, responseType, history).    │
│   3. Pętla:                                                         │
│        a) adapter.SendAsync(session) → AgentTurnResult              │
│        b) ToolCalls? → Task.WhenAll z SemaphoreSlim → dopisz batch  │
│        c) Final? → parse TResponse → return ResultAgent<T>          │
│        d) budget check (MaxIterations, Timeout)                     │
└────────────────────────────────────────────────────────────────────┘
                           ▲
                           │ mały kontrakt
                           ▼
     ┌────────────────────IAgentProviderAdapter────────────────────┐
     ▼                     ▼                                        ▼
OpenAiAgentAdapter  AnthropicAgentAdapter               GoogleAgentAdapter...
(~100 LOC)          (~100 LOC)                          (~100 LOC)
```

Interfejs adaptera:

```csharp
public interface IAgentProviderAdapter
{
    /// Czy ten adapter obsługuje dany model.
    bool Supports(IAgentLlm llm);

    /// Wyślij bieżącą konwersację do modelu, zwróć co odpowiedział.
    Task<AgentTurnResult> SendAsync(
        AgentSession session,
        CancellationToken cancellationToken);

    /// Opcjonalnie — strumieniowa wersja tury (dla GenerateStreamAsync).
    /// null = provider nie wspiera streamingu agenta.
    IAsyncEnumerable<AgentTurnChunk>? StreamAsync(
        AgentSession session,
        CancellationToken cancellationToken) => null;
}

public sealed class AgentSession
{
    public IAgentLlm                Llm          { get; init; } = default!;
    public IPrompt                  Prompt       { get; init; } = default!;
    public IReadOnlyList<ITool>     Tools        { get; init; } = [];
    public Type                     ResponseType { get; init; } = typeof(string);
    public List<AgentMessage>       History      { get; } = [];  // dopisywane przez runner
}

public abstract record AgentTurnResult(TokenUsage Usage, string? RequestId)
{
    public sealed record ToolCalls(
        IReadOnlyList<ToolCallRequest> Calls,
        TokenUsage Usage, string? RequestId) : AgentTurnResult(Usage, RequestId);

    public sealed record Final(
        string Text,
        TokenUsage Usage, string? RequestId) : AgentTurnResult(Usage, RequestId);
}

public sealed record ToolCallRequest(string Id, string Name, JsonElement Arguments);
```

Co robi adapter OpenAI (szkic):

- Mapuje `session.History + session.Tools` → payload Responses API.
- Mapuje `output[].type == "function_call"` → `AgentTurnResult.ToolCalls`.
- Mapuje `output[].type == "message"` → `AgentTurnResult.Final`.
- Dopisuje `function_call_output` w kolejnej turze (to robi runner przez
  `AgentSession.History.Add(...)`).

Tyle. Pętla, MCP, równoległość, limity, wyjątki, trace — wszystko w core.

---

## 13. Streaming agenta (feedback pkt 9)

```csharp
IAsyncEnumerable<AgentEvent> GenerateStreamAsync<TResponse>(
    IAgentLlm agent,
    IPrompt<TResponse> prompt,
    IEnumerable<ITool>? tools = null,
    IEnumerable<Mcp>?   mcps  = null,
    AgentOptions? options = null,
    CancellationToken cancellationToken = default);
```

`AgentEvent` to zamknięta hierarchia:

```csharp
public abstract record AgentEvent;

public sealed record AgentIterationStarted(int Iteration) : AgentEvent;
public sealed record AgentTextDelta(string Chunk) : AgentEvent;
public sealed record AgentToolCallStarted(int Iteration, string ToolCallId,
    string Name, JsonElement Input) : AgentEvent;
public sealed record AgentToolCallFinished(int Iteration, string ToolCallId,
    string Name, JsonElement? Output, string? Error, TimeSpan Duration) : AgentEvent;
public sealed record AgentCompleted<T>(ResultAgent<T> Result) : AgentEvent;
```

Provider, który nie wspiera natywnego streamingu, dostaje **fallback z core**:
zdarzenia emitowane są na podstawie nieblokującego `SendAsync` — UI i tak
widzi live timeline tool-calli.

---

## 14. Wbudowane narzędzia providera vs własne (feedback pkt 1)

Jasna separacja w typach:

- **`ILlm.Tools` (`IToolBase[]`)** → natywne narzędzia providera: `WebSearchTool`,
  `CodeInterpreterTool`, `FileSearchTool`, `XSearchTool`. Wykonuje je API
  providera. **Nie są** naszymi `ITool` i nie trafiają do pętli `AgentRunner`.
- **`ITool` / `ToolBase<,>`** → nasze custom tools, wykonywane lokalnie.
- **`Mcp` + `McpToolAdapter : ITool`** → narzędzia zdalne po protokole MCP.

W jednym wywołaniu mogą współistnieć:

```csharp
var llm = new GPT5 { Tools = [new WebSearchTool()] };   // natywne (provider)
await provider.GenerateAsync(
    llm,
    prompt,
    tools: [new SaveToDatabaseTool(db)],                 // custom (my)
    mcps:  [new Mcp("https://mcp.github.example.com/sse", "github")]);
```

Adapter OpenAI sam mapuje `llm.Tools` na swoje `web_search` itp.; runnera
to nie obchodzi. Runner widzi wyłącznie `function_call` dla naszych tooli.

---

## 15. Plan wdrożenia — fazowo

| Faza | Zakres | Pakiet |
|------|--------|--------|
| 0 | Dokumenty (ten + Examples + Mcp + Deferred) | `Docs/` |
| 1 | `ITool`, `ToolBase<,>`, `IAgentLlm`, `AgentOptions`, `AiAgentOptions`, `ResultAgent<T>`, `ToolInvocation`, `Mcp` (typ), `IToolRegistry`, `IMcpRegistry` | `Zonit.Extensions.Ai.Abstractions` |
| 2 | `AgentRunner`, `IAgentProviderAdapter`, extensions `AddAiTool<T>`, `AddAiTools()` (source gen), `AddAiMcp(Mcp)` | `Zonit.Extensions.Ai` + `Zonit.Extensions.Ai.SourceGenerators` |
| 3 | `OpenAiAgentAdapter` — OpenAI Responses API (function_call / function_call_output) | `Zonit.Extensions.Ai.OpenAi` |
| 4 | `AnthropicAgentAdapter` — Messages API (tool_use / tool_result, multi-block) | `Zonit.Extensions.Ai.Anthropic` |
| 5 | Klient MCP (HTTP/SSE) + `McpTool` + auto `tools/list` po connect | **wbudowany w core w 1.21** (uprzednio osobny pakiet) |
| 6 | `GoogleAgentAdapter`, `XAgentAdapter`, `MistralAgentAdapter`, `DeepSeekAgentAdapter` | odpowiednie pakiety |
| 7 | `GenerateStreamAsync` (fallback w core, natywne w OpenAI/Anthropic) | core + providery |
| 8 | Observability: `ActivitySource` `Zonit.Ai.Agent`, metryki (iterations, duration, errors) | core |

Każda faza jest osobno mergowalna. Żadna nie zmienia zachowania istniejącego
kodu (żaden call-site `GenerateAsync(ILlm, prompt)` nie zmienia semantyki).

---

## 16. TL;DR

```csharp
// 1. Narzędzie — jak PromptBase, tylko z Input/Output.
public class SaveToDbTool(IDb db) : ToolBase<SaveToDbTool.In, SaveToDbTool.Out>
{
    public override string Name => "save_to_db";
    public override string Description => "Zapis rekordu do bazy.";
    public override async Task<Out> ExecuteAsync(In i, CancellationToken ct)
        => new Out { Id = await db.SaveAsync(i.Key, i.Value, ct) };

    public class In  { public required string Key { get; init; }
                       public required string Value { get; init; } }
    public class Out { public Guid Id { get; set; } }
}

// 2. Rejestracja — automatyczna przez source generator + module initializer.
services.AddAi();          // auto-rejestruje wszystkie ToolBase<,> z projektu
services.AddAiOpenAi();

// 3. Użycie agenta — jawny overload z IAgentLlm.
var result = await provider.GenerateAsync(
    new GPT5(),                                      // IAgentLlm
    new SimplePrompt<Report>("Zbadaj X i zapisz."),
    mcps: [new Mcp("github", "https://mcp.github.example.com/sse", token)]);

Console.WriteLine($"Iteracji: {result.Iterations}");
foreach (var call in result.ToolCalls)
    Console.WriteLine($"{call.Name} ({call.Duration.TotalMilliseconds:F0} ms)");
```
