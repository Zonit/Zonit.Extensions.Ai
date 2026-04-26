# Agent — decyzje odroczone

> Lista świadomie odroczonych decyzji i pomysłów, do których wrócimy po
> wdrożeniu fazy 1–5 z [`Agent-Proposal.md`](./Agent-Proposal.md).
> Każdy wpis ma powód odroczenia i kryteria powrotu.

---

## D1. Automatyczne udostępnianie narzędzi dla zwykłego `GenerateAsync` (feedback pkt 14)

**Rozważany pomysł:** `provider.GenerateAsync(llm, prompt)` (nie-agentowe
overload'y) bierze automatycznie wszystkie zarejestrowane `ToolBase<,>`, tak
że po aktualizacji biblioteki każde istniejące zapytanie staje się agentem.

**Decyzja (v2 RFC): NIE robimy tego.**

**Powody odroczenia:**

- **Breaking change semantyczny.** Istniejące wywołania `GenerateAsync(ILlm, prompt)`
  nagle po `dotnet update` zaczęłyby korzystać z narzędzi, zmieniać zachowanie,
  zużywać więcej tokenów, wydłużać czas odpowiedzi. Kompletnie niewidoczne
  w kodzie.
- **Zaskakujące koszty.** Proste prompt → odpowiedź nagle generuje 5–10
  iteracji tool-call i rachunek rośnie kilkukrotnie.
- **Niespójna semantyka.** `GenerateAsync(ILlm, ...)` nie ma `IAgentLlm`,
  więc nie jest jasne co się dzieje gdy model nie wspiera tool-calli
  (cichy no-op? wyjątek?). Teraz kontrakt jest jednoznaczny.
- **Utrudnia testy.** Test, który dziś nie potrzebuje DI tooli, po zmianie
  zaczyna ich wymagać.

**Co sprawdzić przed ewentualnym powrotem:**

1. Czy istnieje niezawodny sposób włączenia „auto tools” tylko per
   zapytanie (np. `[AiUseTools]` na klasie `PromptBase<T>`), bez wpływu
   globalnego?
2. Czy dałoby się to zrobić via opt-in flag globalną w `AiOptions.EnableAutoTools`,
   domyślnie `false` i dobrze udokumentowaną jako breaking?
3. Czy istnieje rzeczywista potrzeba tego (pain point z produkcji), czy
   to tylko wygoda teoretyczna?

**Kryteria powrotu:** min. 3 realne przypadki z użycia biblioteki, w których
wywoływanie `GenerateAsync(IAgentLlm, prompt, tools: ...)` jest odczuwalnie
uciążliwe i nie da się tego skompensować wrapperem po stronie aplikacji.

---

## D2. Własny serwer MCP hostowany przez `Zonit.Extensions.Ai`

**Rozważany pomysł:** pakiet `Zonit.Extensions.Ai.Mcp.Server` — hostujemy
własny serwer MCP, który udostępnia narzędzia aplikacji do zewnętrznych
klientów (Claude Desktop itp.).

**Decyzja: NIE implementujemy w najbliższym czasie.**

**Powody:**

- Microsoft ma oficjalne rozwiązanie hostowe MCP dla .NET — nie ma sensu
  konkurować z OOB.
- Nasz use case to LLM w aplikacji, który korzysta z narzędzi — czyli
  strona kliencka. Strona hostowa to zupełnie inny scenariusz.
- Dodatkowa powierzchnia bezpieczeństwa (auth, rate-limiting, transport)
  — nieproporcjonalna do wartości.

**Kryteria powrotu:** zapotrzebowanie od co najmniej dwóch projektów
zonitowych na eksponowanie narzędzi jako serwera MCP, którego rozwiązanie
Microsoftu nie pokrywa.

---

## D3. MCP przez stdio (lokalny proces)

**Rozważany pomysł:** uruchamianie procesu (np. `npx @modelcontextprotocol/...`)
i komunikacja przez JSON-RPC na stdio.

**Decyzja: NIE w fazie 5. Tylko HTTP/SSE.**

**Powody:**

- Dodaje zależność od hostingu procesów (environment, permissions, timeout,
  zombie processes, I/O redirection).
- Główne praktyczne serwery MCP (GitHub, Slack, enterprise) są dostępne
  po HTTP.
- Stdio jest dobre w Claude Desktop / IDE — nie tam gdzie typowo żyje
  nasza biblioteka (web app, worker service).

**Kryteria powrotu:** realny case (nie „na wszelki wypadek”) — np. CLI
tool integrujący się z lokalnymi narzędziami developerskimi.

---

## D4. Multi-turn sesje agenta (`IAgentSession`)

**Rozważany pomysł:** obiekt sesji zachowujący historię konwersacji między
wywołaniami `GenerateAsync`, żeby agent mógł pamiętać poprzednie tury bez
duplikowania kontekstu w prompcie.

**Decyzja: ODROCZONE do fazy 6+.**

**Powody:**

- Zwiększa powierzchnię API znacząco (sesje → lifecycle → persistence →
  concurrency → TTL).
- Da się obejść po stronie aplikacji: prompt zawiera historię.
- Większość praktycznych agentów to „jeden strzał z dużą ilością narzędzi”,
  nie „rozmowa z agentem”.

**Kryteria powrotu:** 2+ projekty, w których `IAgentSession` da wymierną
przewagę (mniej tokenów, prostszy kod) vs. ręczne zarządzanie historią.

---

## D5. Różne modele dla planowania i egzekucji (`ExecutionModel`)

**Rozważany pomysł:** `AgentOptions.ExecutionModel` — planowanie (reasoning)
robi `GPT5`, wywołania tool-call robi `GPT5Mini` (tańszy).

**Decyzja: ODROCZONE do fazy 7+.**

**Powody:**

- Wymaga bardzo dokładnej definicji „co jest planem, a co egzekucją”
  w kontekście OpenAI Responses API / Anthropic / Gemini. Nie ma jednego
  spójnego wzorca w branży.
- Obecnie można to zasymulować: najpierw `GenerateAsync(GPT5Pro, planPrompt)`,
  potem `GenerateAsync(GPT5Mini, execPrompt)`.

**Kryteria powrotu:** gdy providery ustandaryzują pojęcie „subagenta”
w swoich API (na razie jest to eksperymentalne).

---

## D6. Walidacja argumentów narzędzi przez `DataAnnotations`

> Uwaga (1.21): właściwość `ITool.Strict` została usunięta. Dla narzędzi
> wystawianych przez OpenAI Responses API runtime hardkoduje `strict: true`
> w schemacie. Walidacja przez `DataAnnotations` wciąż jest odroczona
> zgodnie z poniższą decyzją.

**Rozważany pomysł:** oprócz `strict` JSON Schema, uruchamiać też
`Validator.TryValidateObject` na zdeserializowanym `TInput`, żeby atrybuty
jak `[Range]`, `[StringLength]` były egzekwowane.

**Decyzja: ODROCZONE do zebrania feedbacku.**

**Powody:**

- JSON Schema `strict` + `[Description]` pokrywa zdecydowaną większość
  przypadków.
- `DataAnnotations` duplikuje regułę w dwóch miejscach (schema dla modelu +
  adnotacje dla deserializacji).
- Programista zawsze może walidować ręcznie w `ExecuteAsync`.

**Kryteria powrotu:** 3+ realne przypadki, w których model generuje wartość
zgodną ze schematem, ale niezgodną z logiką biznesową, a walidacja
w `ExecuteAsync` jest niewygodna.

---

## D7. Observability — metryki (Prometheus/OpenTelemetry)

**Rozważany pomysł:** fabryka metryk z licznikami (agent iterations,
tool calls success/failure), histogramami (duration), gauge (koszt).

**Decyzja: ODROCZONE do fazy 8.**

**Powody:**

- `ActivitySource` w fazie 8 (patrz plan) da i tak traces — w większości
  projektów to wystarcza.
- Dodawanie metryk bez jasnego odbiorcy to przedwczesna optymalizacja.

**Kryteria powrotu:** wdrożenie agenta w produkcji zonitowej na skalę,
gdzie traces nie wystarczają (potrzebne dashboardy agregujące).

---

## D8. Zaawansowane strategie reconnect do MCP

**Rozważany pomysł:** circuit breaker, exponential backoff, health
checks dla klienta MCP.

**Decyzja: ODROCZONE. W fazie 5 robimy tylko jednorazowy retry z świeżym
`initialize`.**

**Powody:**

- Microsoft.Extensions.Http.Resilience (już używane dla providerów LLM)
  pokryje większość przypadków, gdy opakujemy `IMcpClient` typowanym
  `HttpClient`.
- Nie chcemy pisać własnego Polly wrapper'a.

**Kryteria powrotu:** realny przypadek niestabilnego serwera MCP
w produkcji.

---

## D9. Limity kosztu — miękki vs twardy ~~(otwarte)~~ **(zamknięte w 1.21: nie implementujemy)**

**Decyzja końcowa (1.21):** `AgentOptions.MaxCost` został **usunięty**.
`AgentCostLimitException` też nie istnieje.

**Powód:** kontrolę budżetu lepiej egzekwować na poziomie modelu
(`IAgentLlm` ma `MaxTokens` / equivalent w każdym providerze) i przez
`AgentOptions.MaxIterations` + `AgentOptions.Timeout`. Liczenie kosztu
w locie w runnerze duplikowało logikę cenników (które i tak musimy znać
w `Result.TotalCost`), a precyzja była niepełna (rozliczenie tokenów przez
providerow nie zawsze jest dostępne mid-stream).

**Kryteria powrotu:** realny przypadek, w którym `MaxIterations` /
`Timeout` / `MaxTokens` na modelu nie pokrywają potrzeby (np. SLA na
co-month-budget agenta dla pojedynczego use case).

---

## D11. `AddAiMcpFromConfiguration(...)` helper

**Rozważany pomysł:** dedykowane DI extension, które czyta sekcję
`Ai:Mcp:Servers` z `IConfiguration` i woła `AddAiMcp(new Mcp(...))` dla
każdego wpisu — tak żeby konfiguracja MCP żyła w `appsettings.json`.

**Decyzja: ODROCZONE.**

**Powody:**

- 95% projektów używa 1–3 serwerów MCP — trzy linijki `AddAiMcp(...)` w
  `Program.cs` to nie jest problem.
- DTO + binding + walidacja URL/secret to dużo kodu jak na cukier syntaktyczny.
- Tymczasowy pattern z `IConfiguration` + `foreach` z `Agent-Mcp.md` §2.3
  pokrywa potrzebę bez nowego API.

**Kryteria powrotu:** projekt z 5+ MCP-ami, gdzie konfiguracja przez kod
staje się odczuwalnie uciążliwa, lub potrzeba przeładowania konfiguracji
MCP bez restartu (hot-reload przez `IOptionsMonitor<>`).

---

## D10. Streaming strukturalnych odpowiedzi (JSON)

**Rozważany pomysł:** `GenerateStreamAsync<TResponse>` emituje progresywny
delta w JSON (np. pierwsze pola struktury pojawiają się zanim całość jest
gotowa).

**Decyzja: ODROCZONE do fazy 8.**

**Powody:**

- Providerzy różnie implementują streaming strukturalnych outputów
  (OpenAI Responses obsługuje, Anthropic nie zawsze).
- W fazie 7 pokrywamy `AgentTextDelta` — to rozwiązuje 80% przypadków UX.

**Kryteria powrotu:** ustabilizowanie się specyfikacji providerów.

---

## Format dodawania nowych wpisów

```
## D<N>. Tytuł

**Rozważany pomysł:** ...

**Decyzja:** ...

**Powody:**
- ...

**Kryteria powrotu:** ...
```

Każdy nowy wpis powinien mieć datę w historii git i link do konkretnej dyskusji
(issue / PR / konwersacja), jeśli taki istnieje.
