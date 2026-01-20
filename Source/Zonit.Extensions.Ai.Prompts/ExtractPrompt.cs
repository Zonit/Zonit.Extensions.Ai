using System.ComponentModel;

namespace Zonit.Extensions.Ai.Prompts;

/// <summary>
/// Response for extracting structured data.
/// </summary>
/// <typeparam name="T">Type of extracted data.</typeparam>
public class ExtractResponse<T>
{
    /// <summary>
    /// Extracted data.
    /// </summary>
    [Description("Extracted structured data")]
    public required T Data { get; set; }
    
    /// <summary>
    /// Confidence score (0-1).
    /// </summary>
    [Description("Extraction confidence score from 0.0 to 1.0")]
    public double Confidence { get; set; }
    
    /// <summary>
    /// Fields that could not be extracted.
    /// </summary>
    [Description("List of fields that could not be extracted from the source")]
    public List<string>? MissingFields { get; set; }
}

/// <summary>
/// Person information for extraction.
/// </summary>
public class PersonInfo
{
    [Description("Full name of the person")]
    public string? Name { get; set; }
    
    [Description("Email address")]
    public string? Email { get; set; }
    
    [Description("Phone number")]
    public string? Phone { get; set; }
    
    [Description("Company or organization")]
    public string? Company { get; set; }
    
    [Description("Job title or position")]
    public string? Title { get; set; }
}

/// <summary>
/// Prompt for extracting contact information from text.
/// </summary>
/// <example>
/// var result = await ai.GenerateAsync(
///     new GPT51(),
///     new ExtractContactPrompt 
///     { 
///         Content = "Hi, I'm John Smith from Acme Corp. Email me at john@acme.com" 
///     });
/// Console.WriteLine(result.Value.Data.Name); // "John Smith"
/// </example>
public class ExtractContactPrompt : PromptBase<ExtractResponse<PersonInfo>>
{
    /// <summary>
    /// Text to extract contact information from.
    /// </summary>
    public required string Content { get; init; }
    
    /// <inheritdoc />
    public override string Prompt => @"
Extract contact information from the following text.
Find: name, email, phone, company, and job title.
If a field is not present, leave it null.
Provide a confidence score for the overall extraction.

Text:
{{ content }}
";
}

/// <summary>
/// Product information for extraction.
/// </summary>
public class ProductInfo
{
    [Description("Product name")]
    public string? Name { get; set; }
    
    [Description("Product price as decimal")]
    public decimal? Price { get; set; }
    
    [Description("Currency code (USD, EUR, PLN, etc.)")]
    public string? Currency { get; set; }
    
    [Description("Product category")]
    public string? Category { get; set; }
    
    [Description("Key features")]
    public List<string>? Features { get; set; }
}

/// <summary>
/// Prompt for extracting product information from text.
/// </summary>
public class ExtractProductPrompt : PromptBase<ExtractResponse<ProductInfo>>
{
    /// <summary>
    /// Text to extract product information from.
    /// </summary>
    public required string Content { get; init; }
    
    /// <inheritdoc />
    public override string Prompt => @"
Extract product information from the following text.
Find: product name, price (as decimal), currency, category, and key features.
If a field is not present, leave it null.

Text:
{{ content }}
";
}
