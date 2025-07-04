using System.ComponentModel;
using Zonit.Extensions.Ai;

namespace Example.Backgrounds;

public class TestPrompt : PromptBase<TestPromptResponse>
{
    public string TestString { get; set; } = "Hello World!";
    public int TestNumber { get; set; } = 42;
    public bool IsEnabled { get; set; } = true;
    public TestEnum StatusLevel { get; set; } = TestEnum.High;
    public TestFlagsEnum Features { get; set; } = TestFlagsEnum.OptionA | TestFlagsEnum.OptionC;
    public string? OptionalNote { get; set; } = null;
    public List<string> Keywords { get; set; } = new() { "AI", "test", "prompt" };
    public List<int> Scores { get; set; } = new() { 1, 2, 3 };

    public override string Prompt => @"
# 🧪 TESTING PROMPT TEMPLATE

**Basic values:**
- Test string: {{ test_string }}
- Test number: {{ test_number }}
- Is enabled: {{ is_enabled }}
- Status level: {{ status_level }}
- Features: {{ features }}
- Optional note: {{ if optional_note != null }}{{ optional_note }}{{ else }}None provided{{ end }}

**Loop through keywords:**
{{ for keyword in keywords }}
- Keyword {{ keyword.index }}: {{ keyword }}
{{ end }}

**Loop through scores with conditionals:**
{{ for score in scores }}
- Score {{ score }}: {{ if score > 1 }}✅ valid{{ else }}❌ too low{{ end }}
{{ end }}

**Conditional check:**
{{ if is_enabled }}
System is enabled.
{{ else }}
System is disabled.
{{ end }}

**Nested logic test:**
{{ for keyword in keywords }}
Keyword: {{ keyword }} - {{ if keyword == 'test' }}Matched test keyword{{ else }}Other{{ end }}
{{ end }}
";
}

public enum TestEnum
{
    Low,
    Medium,
    High
}

[Flags]
public enum TestFlagsEnum
{
    None = 0,
    OptionA = 1 << 0,
    OptionB = 1 << 1,
    OptionC = 1 << 2
}

public class TestPromptResponse
{
    [Description("Final result of the test prompt rendering.")]
    public string? RenderedText { get; set; }
}