using System.ComponentModel;
using Zonit.Extensions.Ai;

namespace Example.Backgrounds;

/// <summary>
/// Example prompt using Scriban templating.
/// The response type TestPromptResponse is strongly typed!
/// </summary>
public class TestPrompt : PromptBase<TestPromptResponse>
{
    public string TestString { get; set; } = "Hello World!";
    public int TestNumber { get; set; } = 42;
    public bool IsEnabled { get; set; } = true;
    public TestEnum StatusLevel { get; set; } = TestEnum.High;
    public List<string> Keywords { get; set; } = ["AI", "test", "prompt"];

    /// <summary>
    /// Scriban template - properties are available as snake_case.
    /// </summary>
    public override string Prompt => @"
# Testing Prompt

**Values:**
- Test string: {{ test_string }}
- Test number: {{ test_number }}
- Is enabled: {{ is_enabled }}
- Status level: {{ status_level }}

**Keywords:**
{{ for keyword in keywords }}
- {{ keyword }}
{{ end }}

Please summarize these test values in JSON format.
";
}

public enum TestEnum
{
    Low,
    Medium,
    High
}

/// <summary>
/// Response type - auto-generates JSON Schema for structured output.
/// </summary>
[Description("Test prompt response with summary.")]
public class TestPromptResponse
{
    [Description("Summary of the test prompt values.")]
    public string? Summary { get; set; }
    
    [Description("The test number that was provided.")]
    public int TestNumber { get; set; }
    
    [Description("Whether the system was enabled.")]
    public bool WasEnabled { get; set; }
}
