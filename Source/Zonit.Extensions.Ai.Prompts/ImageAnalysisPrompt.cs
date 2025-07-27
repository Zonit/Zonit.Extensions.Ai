using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai.Prompts;

public class ImageAnalysisPrompt : PromptBase<ImageAnalysisResponse>
{
    public override string Prompt => @"Przeanalizuj za��czone zdj�cie i opisz co widzisz na nim. Podaj szczeg�owy opis tego co jest na zdj�ciu.";
}

public class ImageAnalysisResponse
{
    /// <summary>
    /// Szczeg�owy opis tego co znajduje si� na zdj�ciu.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Lista g��wnych obiekt�w lub element�w widocznych na zdj�ciu.
    /// </summary>
    public List<string> MainObjects { get; set; } = new();
    
    /// <summary>
    /// Og�lna kategoria lub typ zdj�cia (np. "natura", "miasto", "portret", itp.).
    /// </summary>
    public string Category { get; set; } = string.Empty;
}