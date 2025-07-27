using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai.Prompts;

public class ImageAnalysisPrompt : PromptBase<ImageAnalysisResponse>
{
    public override string Prompt => @"Przeanalizuj za³¹czone zdjêcie i opisz co widzisz na nim. Podaj szczegó³owy opis tego co jest na zdjêciu.";
}

public class ImageAnalysisResponse
{
    /// <summary>
    /// Szczegó³owy opis tego co znajduje siê na zdjêciu.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Lista g³ównych obiektów lub elementów widocznych na zdjêciu.
    /// </summary>
    public List<string> MainObjects { get; set; } = new();
    
    /// <summary>
    /// Ogólna kategoria lub typ zdjêcia (np. "natura", "miasto", "portret", itp.).
    /// </summary>
    public string Category { get; set; } = string.Empty;
}