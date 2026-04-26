# Agent — przykłady użycia API (v1.21)

> Aktualne dla wydanej wersji **`Zonit.Extensions.Ai` 1.21+**.
> Dokument towarzyszący [`Agent-Proposal.md`](./Agent-Proposal.md) i
> [`Agent-Mcp.md`](./Agent-Mcp.md). Wszystkie przykłady są prawdziwym,
> działającym kodem (nie projektem). Tam gdzie API różni się od propozycji
> z `Agent-Proposal.md`, źródłem prawdy jest ten plik.

API agenta (jawne, nie miesza się ze zwykłym `GenerateAsync`):

```csharp
Task<ResultAgent<T>> GenerateAsync<T>(
    IAgentLlm agent,
    IPrompt<T> prompt,
    IEnumerable<ITool>? tools = null,
    IEnumerable<Mcp>?   mcps  = null,
    AgentOptions? options = null,
    CancellationToken cancellationToken = default);
```

---

## 1. Najprostszy agent — jedno narzędzie, zero konfiguracji

### 1.1 Definicja narzędzia

```csharp
using System.ComponentModel;
using Zonit.Extensions.Ai;

public class GetWeatherTool(IWeatherClient weather)
    : ToolBase<GetWeatherTool.Input, GetWeatherTool.Output>
{
    public override string Name => "get_weather";
    public override string Description =>
        "Zwraca aktualną pogodę dla miasta (temperatura °C, opis słowny).";

    public override async Task<Output> ExecuteAsync(Input input, CancellationToken ct)
    {
        var forecast = await weather.GetCurrentAsync(input.City, ct);
        return new Output
        {
            TemperatureC = forecast.Temperature,
            Description  = forecast.Summary
        };
    }

    [Description("Parametry zapytania o pogodę.")]
    public class Input
    {
        [Description("Nazwa miasta po angielsku, np. 'Warsaw', 'Tokyo'.")]
        public required string City { get; init; }
    }

    [Description("Odpowiedź narzędzia pogodowego.")]
    public class Output
    {
        [Description("Temperatura w stopniach Celsjusza.")]
        public double TemperatureC { get; set; }

        [Description("Krótki opis warunków pogodowych.")]
        public string Description { get; set; } = string.Empty;
    }
}
```

### 1.2 Rejestracja — automatyczna

```csharp
// Program.cs
builder.Services.AddAi();          // rejestruje też wszystkie ToolBase<,> z projektu
builder.Services.AddAiOpenAi();
```

Source generator dorzuca w każdym Twoim assembly `[ModuleInitializer]`,
który zgłasza każdą klasę dziedziczącą po `ToolBase<,>` do statycznego
`ToolDiscovery`. `AddAi()` czyta tę listę i rejestruje typy jako `Scoped`
w DI — bez ręcznej akcji.

Jeśli w starszym kodzie wołasz `services.AddAiTools()` — to teraz no-op
z atrybutem `[Obsolete]`; możesz spokojnie usunąć.

### 1.3 Użycie

```csharp
public class WeatherService(IAiProvider provider)
{
    public async Task<string> AskAsync(string question, CancellationToken ct)
    {
        // Brak parametru tools — używane są wszystkie z DI.
        var result = await provider.GenerateAsync(
            new GPT5(),           // IAgentLlm
            question,
            cancellationToken: ct);

        return result.Value;      // string
    }
}
```

---

## 2. Typowany prompt + typowana odpowiedź

Identycznie jak dziś — `PromptBase<T>` jest niezmienione:

```csharp
public class TripPlannerPrompt : PromptBase<TripPlan>
{
    public required string City { get; init; }
    public required int Days { get; init; }

    public override string Prompt => @"
Zaplanuj {{ days }}-dniową wycieczkę do {{ city }}.
Jeśli musisz — użyj dostępnych narzędzi (pogoda, baza miejsc).
Odpowiedz strukturą TripPlan.";
}

public class TripPlan
{
    public required string City { get; set; }
    public List<DayPlan> Days { get; set; } = [];
    public string? WeatherSummary { get; set; }
}

public class DayPlan
{
    public int Day { get; set; }
    public List<string> Activities { get; set; } = [];
}
```

Wywołanie:

```csharp
var result = await provider.GenerateAsync(
    new GPT5(),
    new TripPlannerPrompt { City = "Kraków", Days = 3 });

Console.WriteLine(result.Value.WeatherSummary);
Console.WriteLine($"Iteracji: {result.Iterations}");
Console.WriteLine($"Wywołań narzędzi: {result.ToolCalls.Count}");
Console.WriteLine($"Total cost: {result.TotalCost}");
```

---

## 3. Audyt wywołań — `ResultAgent<T>.ToolCalls` (feedback pkt 6)

Pełna lista wejść i wyjść narzędzi. Możesz ją zapisać do bazy jako
ścieżkę audytową lub przekazać innemu modelowi do weryfikacji.

```csharp
var result = await provider.GenerateAsync(new GPT5(), prompt);

foreach (var call in result.ToolCalls)
{
    logger.LogInformation(
        "[{Iter}] {Tool} ({Duration} ms){Mcp}\n  IN:  {In}\n  OUT: {Out}{Err}",
        call.Iteration, call.Name, call.Duration.TotalMilliseconds,
        call.McpServer is null ? "" : $" via MCP {call.McpServer}",
        call.Input, call.Output,
        call.Error is null ? "" : $"\n  ERR: {call.ErrorType}: {call.Error}");
}

// Zapis do bazy audytowej:
await auditStore.SaveAsync(new AuditEntry
{
    RequestId = result.MetaData.RequestId!,
    Iterations = result.Iterations,
    TotalCost  = result.TotalCost.Value,
    Calls = result.ToolCalls
               .Select(c => new AuditCall(c.Name, c.Input.ToString(),
                                          c.Output?.ToString(), c.Error))
               .ToList()
});

// Lub: niech inny model zweryfikuje co zrobił poprzedni.
var verifier = await provider.GenerateAsync(
    new ClaudeHaiku45(),
    new VerifyAgentRunPrompt { ToolCalls = result.ToolCalls });
```

---

## 4. Precyzyjny wybór narzędzi per-wywołanie

**Default = additive.** Narzędzia/MCP zarejestrowane globalnie w DI są
**dodawane** do tych podanych per-call (nie nadpisywane). Jeśli chcesz
odciąć defaults dla pojedynczego wywołania, użyj flag w `AgentOptions`:

```csharp
var result = await provider.GenerateAsync(
    new GPT5(),
    new TripPlannerPrompt { City = "Kraków", Days = 3 },
    tools: [weatherTool, placesTool],   // dodawane do globalnych z DI
    options: new AgentOptions
    {
        MaxIterations = 20,
        Timeout       = TimeSpan.FromMinutes(3),
        DefaultTools  = false,           // ← *teraz* tylko weatherTool + placesTool
        DefaultMcp    = false,           // analogicznie dla MCP
    });
```

Alternatywa przez whitelist po nazwie (DI dostarcza pełny zestaw, ograniczamy
po nazwie — przydatne przy MCP):

```csharp
var options = new AgentOptions
{
    AllowedTools = ["get_weather", "github.read_file"]
};
```

> Limit kosztu (`MaxCost`) **został usunięty** w 1.21 — kontrolę budżetu
> realizuj poprzez `MaxIterations`, `Timeout` lub limity tokenów na
> poziomie modelu (`IAgentLlm`).

---

## 5. Mieszanie narzędzi natywnych providera z własnymi (feedback pkt 1)

Natywne (`WebSearchTool`, `CodeInterpreterTool`) trafiają do `ILlm.Tools` —
wykonuje je OpenAI/Anthropic. Nasze `ToolBase<,>` i MCP — my.
Wszystko w tym samym wywołaniu, bez kolizji:

```csharp
var llm = new GPT5
{
    Tools = [new WebSearchTool { ContextSize = WebSearchTool.ContextSizeType.Medium }]
};

var result = await provider.GenerateAsync(
    llm,
    new SimplePrompt<Report>("Znajdź 3 newsy o AI i zapisz je do bazy."),
    tools: [saveToDatabaseTool]);
```

---

## 6. Równoległe wywołania narzędzi (feedback pkt 7)

Gdy Claude/GPT/Gemini zwróci kilka `tool_use` w jednej turze, biblioteka
wykonuje je równolegle automatycznie. `MaxParallelToolCalls` to **rozmiar
kolejki workerów** — model może zwrócić więcej wywołań niż limit; nadmiar
czeka i jest wykonywany gdy zwolni się slot. **Żadne wywołanie nigdy nie
jest odrzucone.**

```csharp
var options = new AgentOptions
{
    MaxParallelToolCalls = 32   // duży workload, wiemy co robimy
};

var result = await provider.GenerateAsync(
    new Claude45Sonnet(), prompt, options: options);

// Albo sekwencyjnie, gdy narzędzia nie są idempotentne:
var options2 = new AgentOptions { MaxParallelToolCalls = 1 };
```

Globalny default (`Ai:Agent:MaxParallelToolCalls`) = 16.

---

## 7. Wyjątki w narzędziach (feedback pkt 8)

Narzędzie może rzucić cokolwiek. Domyślnie **model dostaje błąd i sam
decyduje co dalej** (`AiAgentOptions.OnToolException = ReturnErrorToModel`).

```csharp
public class DeleteRecordTool(IDb db)
    : ToolBase<DeleteRecordTool.Input, DeleteRecordTool.Output>
{
    public override string Name => "delete_record";
    public override string Description => "Usuwa rekord po ID. Rzuca gdy nie istnieje.";

    public override async Task<Output> ExecuteAsync(Input input, CancellationToken ct)
    {
        var row = await db.FindAsync(input.Id, ct)
            ?? throw new InvalidOperationException($"Record {input.Id} not found.");

        await db.DeleteAsync(row, ct);
        return new Output { Deleted = true };
    }

    public class Input  { public required Guid Id { get; init; } }
    public class Output { public bool Deleted { get; set; } }
}
```

Co widzi model:
```json
{ "error": "Record 42 not found.", "type": "InvalidOperationException" }
```

Co widzisz Ty w `ResultAgent<T>.ToolCalls`:
```csharp
call.Output    == null
call.Error     == "Record 42 not found."
call.ErrorType == "System.InvalidOperationException"
```

Jeśli wolisz twardą propagację (wyjątek z `GenerateAsync`), zmień politykę:

```json
{ "Ai": { "Agent": { "OnToolException": "ThrowToCaller" } } }
```

Albo per narzędzie rzuć `ToolFailedException` — traktowane jak zwykły błąd
biznesowy (bez zanieczyszczania logów stack-tracem).

---

## 8. Guard przed wywołaniem (autoryzacja / rate-limit)

```csharp
var options = new AgentOptions
{
    OnToolCall = async (invocation, ct) =>
    {
        // Godzinowy rate-limit na kosztowne narzędzia:
        if (invocation.Name == "expensive_api_call"
            && !await rateLimiter.TryAcquireAsync(ct))
        {
            logger.LogWarning("Rate-limit blocked: {Tool}", invocation.Name);
            return false;   // narzędzie NIE zostanie wykonane
        }

        // Godziny pracy:
        if (invocation.Name == "save_to_database" && !IsBusinessHours())
            return false;

        return true;
    }
};
```

Zablokowane wywołanie trafia do modelu jako `tool_result` z
`error = "blocked by policy"` — model sam decyduje, czy próbować inaczej
(np. innym narzędziem).

---

## 9. Zewnętrzne MCP

Klient MCP jest **wbudowany w core** (od 1.21 nie ma osobnego pakietu
`Zonit.Extensions.Ai.Mcp`). Sygnatura konstruktora:

```csharp
new Mcp(
    name:         "github",
    url:          "https://mcp.github.example.com/sse",
    token:        bearerToken,                          // opcjonalnie
    allowedTools: new[] { "read_file", "create_issue" } // opcjonalna whitelist
);
```

Kolejność: **`name`, `url`, `token`, `allowedTools`** (zmiana z 1.20 — wcześniej
było `(url, name, password)`). Pole `Password` zostało przemianowane na
`Token`.

### 9.1 Inline (bez DI)

```csharp
var result = await provider.GenerateAsync(
    new GPT5(),
    "Przeanalizuj otwarte issues w repo zonit-sdk i pogrupuj po obszarach.",
    mcps: [new Mcp("github", "https://mcp.github.example.com/sse", githubToken)]);
```

Model widzi narzędzia MCP pod nazwami `github.list_issues`,
`github.get_issue` itp. (prefiks = `name` z konstruktora).

### 9.2 MCP bez autoryzacji

```csharp
mcps: [new Mcp("intranet", "https://mcp.intranet.example.com/sse")]
```

### 9.3 Whitelist narzędzi z serwera

Serwer może udostępniać 50 narzędzi, a Ty chcesz tylko dwa — bez
krążenia po `AllowedTools` w `AgentOptions`:

```csharp
new Mcp(
    name: "gold",
    url:  "https://mcp.example.com/sse",
    token: token,
    allowedTools: new[] { "get_gold_price", "get_cot_data" });
```

`null` = wszystkie narzędzia. Pusta lista = żadne (de facto wyłączenie).

### 9.4 Kilka MCP naraz

```csharp
var result = await provider.GenerateAsync(
    new Claude45Sonnet(),
    prompt,
    tools: [saveToDbTool],
    mcps: [
        new Mcp("github", "https://mcp.github.example.com/sse", ghToken),
        new Mcp("slack",  "https://mcp.slack.example.com/sse",  slackToken)
    ]);
```

### 9.5 MCP zarejestrowane w DI (wielokrotne użycie)

```csharp
// Program.cs
builder.Services.AddAiMcp(new Mcp("github", "https://mcp.github.example.com/sse", ghToken));
builder.Services.AddAiMcp(new Mcp("slack",  "https://mcp.slack.example.com/sse",  slackToken));

// Użycie — globalne MCP są dodawane *do* per-call mcps. Bez argumentu mcps
// agent dostaje tylko globalne.
var result = await provider.GenerateAsync(new GPT5(), prompt);

// Tymczasowe wyłączenie globalnych MCP (np. dla wewnętrznego testu):
var resultIsolated = await provider.GenerateAsync(
    new GPT5(), prompt,
    mcps: [new Mcp("local-test", "https://localhost:5000/mcp")],
    options: new AgentOptions { DefaultMcp = false });
```

---

## 10. Streaming agenta (feedback pkt 9)

```csharp
await foreach (var ev in provider.GenerateStreamAsync(new GPT5(), prompt))
{
    switch (ev)
    {
        case AgentIterationStarted s:
            logger.LogInformation("→ Iteracja {N}", s.Iteration);
            break;

        case AgentToolCallStarted call:
            logger.LogInformation("  ⏵ {Tool}({Args})", call.Name, call.Input);
            break;

        case AgentToolCallFinished done:
            logger.LogInformation("  ⏸ {Tool} = {Out} ({Ms} ms){Err}",
                done.Name, done.Output, done.Duration.TotalMilliseconds,
                done.Error is null ? "" : $" ERROR: {done.Error}");
            break;

        case AgentTextDelta text:
            Console.Write(text.Chunk);
            break;

        case AgentCompleted<Report> end:
            logger.LogInformation("✓ Iteracji: {N}, koszt: {Cost}",
                end.Result.Iterations, end.Result.TotalCost);
            break;
    }
}
```

---

## 11. Testowanie narzędzia bez wywoływania modelu

Narzędzie to zwykła klasa C# — testujesz ją bezpośrednio, bez mockowania agenta:

```csharp
[Fact]
public async Task GetWeather_ReturnsForecast()
{
    var client = Substitute.For<IWeatherClient>();
    client.GetCurrentAsync("Warsaw", Arg.Any<CancellationToken>())
          .Returns(new Forecast(18.5, "partly cloudy"));

    var sut = new GetWeatherTool(client);

    var output = await sut.ExecuteAsync(
        new GetWeatherTool.Input { City = "Warsaw" },
        CancellationToken.None);

    Assert.Equal(18.5, output.TemperatureC);
    Assert.Equal("partly cloudy", output.Description);
}
```

Schemat JSON parametrów (generowany z `Input`) można sprawdzić pod kątem
zmian przy code review:

```csharp
var schema = ((ITool)sut).InputSchema;
// snapshot test: zapis do pliku i porównanie w kolejnych PR-ach
```

---

## 12. Narzędzie bez inputu / bez outputu

Gdy narzędzie nie potrzebuje argumentów albo nic nie zwraca, używaj `object`
lub dedykowanych pustych typów:

```csharp
public class GetCurrentTimeTool : ToolBase<GetCurrentTimeTool.Empty, GetCurrentTimeTool.Output>
{
    public override string Name => "get_current_time";
    public override string Description => "Zwraca aktualny czas UTC.";

    public override Task<Output> ExecuteAsync(Empty _, CancellationToken ct)
        => Task.FromResult(new Output { UtcNow = DateTime.UtcNow });

    public sealed class Empty { }
    public class Output { public DateTime UtcNow { get; set; } }
}
```

---

## 13. Porównanie: zwykły request vs agent

| Aspekt                | Zwykły request                                | Agent                                                     |
|-----------------------|-----------------------------------------------|-----------------------------------------------------------|
| Wywołanie             | `provider.GenerateAsync(llm, prompt)`         | `provider.GenerateAsync(agent, prompt, tools?, mcps?, ...)` |
| Typ modelu            | `ILlm`                                        | `IAgentLlm : ILlm`                                        |
| Narzędzia providera   | `llm.Tools` (jak dziś)                        | `llm.Tools` (bez zmian) + nasze `tools`/`mcps`            |
| Custom tools (nasze)  | **brak** (świadoma decyzja — pkt 14 feedbacku)| `ToolBase<,>` — lokalnie lub z MCP                        |
| Pętla tool-call       | brak                                          | ukryta w `AgentRunner` (core)                             |
| Rezultat              | `Result<T>`                                   | `ResultAgent<T> : Result<T>` + `ToolCalls`, `Iterations`  |
| Rejestracja           | `AddAi()` + `AddAiOpenAi()`                   | `AddAi()` (auto-rejestruje narzędzia) + `AddAiMcp(...)` opcjonalnie |
| Breaking changes      | 0                                             | 0 — to jawne, nowe overload'y                             |

Dla programisty dochodzą dwie rzeczy: `ToolBase<,>` jako klasa narzędzia
i `new Mcp(...)` jako obiekt serwera MCP. Reszta mental modelu bez zmian.
