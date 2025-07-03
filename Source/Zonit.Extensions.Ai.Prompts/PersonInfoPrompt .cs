using System.ComponentModel;

namespace Zonit.Extensions.Ai.Prompts;

public class PersonInfoPrompt : PromptBase<PersonInfoResponse>
{
    public required string Name { get; set; }
    public int Age { get; set; }

    public override string Prompt => @"
Create a detailed character profile for a person named {{ name }} who is {{ age }} years old.

Generate realistic information including:
- Brief personality description
- Likely profession based on age
- Hobbies and interests
- Family situation
- Educational background
- Fun fact about them

Make it creative but believable. Return the information in a structured format.
";
}

public class PersonInfoResponse
{
    [Description("Brief 2-3 sentence description of the person's personality traits")]
    public string? Personality { get; set; }

    [Description("Most likely profession or job title based on the person's age")]
    public string? Profession { get; set; }

    [Description("List of 3-4 hobbies or interests this person might have")]
    public List<string>? Hobbies { get; set; }

    [Description("Description of family situation - married, single, kids, etc.")]
    public string? FamilyStatus { get; set; }

    [Description("Educational background - degree, school, field of study")]
    public string? Education { get; set; }

    [Description("An interesting or unusual fact about this person")]
    public string? FunFact { get; set; }

    [Description("City or region where this person likely lives")]
    public string? Location { get; set; }

    [Description("Person's biggest goal or aspiration")]
    public string? LifeGoal { get; set; }
}