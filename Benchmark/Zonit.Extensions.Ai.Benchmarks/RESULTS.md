# Zonit.Extensions.Ai — wyniki benchmarków

> Mikrobenchmarki lokalnej pracy CPU wokół wywołania AI. **Nie** wykonują żadnego
> requestu do providera — mierzą tylko narzut, który dzieje się na maszynie.

## Środowisko

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200)
AMD Ryzen 9 5900X 3.70GHz, 24 logical / 12 physical cores
.NET SDK 10.0.300 — runtime .NET 10.0.8, X64 RyuJIT AVX2
Job: ShortRun (LaunchCount=1, WarmupCount=3, IterationCount=3)
```

> `ShortRun` daje szybki, ale niżej-rozdzielczy obraz (stąd miejscami wysoki Error).
> Do oficjalnego pomiaru / wykrywania regresji uruchom `dotnet run -c Release` bez
> `--job short`.

## Schema structured-output (`SchemaBenchmarks`)

| Metoda | Mean | Allocated |
|--------|-----:|----------:|
| Reflection (`JsonSchemaGenerator`) | **18 066 ns** | 17 825 B |
| Source-gen + cache (`AiSchemaRegistry`) | **3.8 ns** | 0 B |

Ścieżka source-gen + cache jest **~4 700× szybsza** i zero-alokacyjna. To
potwierdza, że budowanie schematu w czasie kompilacji (zamiast refleksją per request)
realnie działa. Refleksyjny `JsonSchemaGenerator` jest tylko fallbackiem dla typów,
których generator nie widział — i to jedyne miejsce, gdzie ~18 µs / ~17 KB byłoby
płacone per request.

## Renderowanie promptu (`PromptRenderBenchmarks`)

| Metoda | Mean | Allocated |
|--------|-----:|----------:|
| Render typed `PromptBase` (Scriban) | **18 105 ns** | 40 935 B |
| Render `SimplePrompt` (bez templatingu) | **0.99 ns** | 0 B |

To **najcięższy krok lokalny**: ~18 µs i ~40 KB alokacji na każdy render. Powód jest
w `ScribanPromptRenderer.Render` — `Template.Parse(raw)` jest wołane **przy każdym
wywołaniu**, więc szablon jest parsowany od nowa za każdym razem. Patrz „Rekomendacje”.

## Parsowanie odpowiedzi (`ResponseParseBenchmarks`)

| Metoda | Mean | Allocated |
|--------|-----:|----------:|
| `Parse<string>` (ekstrakcja tekstu) | 14.7 ns | 0 B |
| `Parse<int>` (ekstrakcja liczby) | 88.9 ns | 240 B |
| `Parse<bool>` | 17.0 ns | 0 B |
| `Parse<T>` typ złożony (stringowe enumy) | 1 522 ns | 1 432 B |
| `DeserializeStructured` czysty JSON | 2 508 ns | 1 480 B |
| `DeserializeStructured` koperta `{result:…}` | 1 884 ns | 1 896 B |
| `ExtractJson` z markdown | 3 128 ns | 1 312 B |

Wszystko w zakresie pojedynczych µs — w pełni akceptowalne. Obie ścieżki
(`Parse<T>` i `DeserializeStructured`) parsują ten sam JSON od modelu, w tym
stringowe enumy (patrz „Naprawione” niżej).

## Liczenie kosztu (`CostBenchmarks`)

| Metoda | Mean | Allocated |
|--------|-----:|----------:|
| `CalculateCost` (bez cache) | 90.1 ns | 0 B |
| `CalculateCost` (cache read+write) | 160.6 ns | 0 B |
| `CalculateBatchCost` | 90.7 ns | 0 B |
| `EstimateCost` (z tekstu promptu) | 79.9 ns | 0 B |

Czysta arytmetyka `decimal`, zero alokacji. Bez zastrzeżeń.

---

## Wnioski

**Skala odniesienia:** wywołanie modelu to setki–tysiące **ms**. Cały lokalny narzut
zmierzony powyżej (nawet pesymistycznie ~18 µs render + ~3 µs parse + schema + koszt)
to **< 0.05 ms** na request — czyli **rzędy wielkości** poniżej szumu sieci. Z punktu
widzenia latencji **nic nie wymaga pilnej optymalizacji**.

Te benchmarki służą więc głównie do **wychwytywania regresji** (np. przypadkowa
alokacja na ścieżce parsowania, utrata cache schematu) niż do gonienia za czasem.

### Rekomendacje

Brak rekomendacji wydajnościowych — nic na ścieżce lokalnej nie jest wąskim gardłem
wobec sieci. Najcięższy krok (render Scriban, ~18 µs/~40 KB) świadomie zostawiamy bez
cache: AI woła się rzadko, a parsowanie szablonu raz na wywołanie jest nieistotne
wobec round-tripu do modelu.

### Naprawione: `Parse<T>` wywracał się na stringowych enumach

Benchmarki ujawniły realny bug. `JsonResponseParser.Parse<T>` **rzucał wyjątkiem** dla
typu złożonego ze stringowym enumem (`"sentiment": "neutral"`):

```
JsonException: The JSON value could not be converted to ...Sentiment. Path: $.sentiment
```

**Przyczyna:** wygenerowane `JsonTypeInfo` POCO zostawia property enuma z
`Converter = null`, więc konwerter enuma jest rozwiązywany z przekazanych
`JsonSerializerOptions`. `Parse<T>` używał `DefaultOptions` **bez**
`CaseInsensitiveEnumConverterFactory`/`DateTimeConverterFactory`, podczas gdy
`DeserializeStructured<T>` używa `ProviderResponseOptions`, które te konwertery ma.
Ten sam JSON od modelu przechodził przez `DeserializeStructured<T>`, ale wywracał się
na `Parse<T>` — a modele standardowo zwracają enumy jako stringi.

**Naprawa:** ujednolicono oba zestawy opcji w
`Source/Zonit.Extensions.Ai/JsonResponseParser.cs` — `DefaultOptions` dostało te same
konwertery (enum + DateTime) oraz `Encoder`, a `ProviderResponseOptions` dostało
`AllowTrailingCommas` + `ReadCommentHandling`. Obie ścieżki są teraz tak samo odporne.
Pokrycie regresyjne: `Test/Zonit.Extensions.Ai.Tests/Schema/JsonResponseParserTests.cs`.
