namespace Zonit.Extensions.Ai.Prompts;

public class AnimalPrompt : PromptBase<IFile>
{
    public required string Animal { get; set; }

    public override string Prompt => @"
Narysuj zwierzę które przedstawia ``{{ animal }}``
";
}