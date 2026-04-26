using System.Runtime.CompilerServices;

// Allow trusted core + first-party providers to construct internal-only chat
// types (e.g. Tool) when materializing transcripts.
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Anthropic")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.OpenAi")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Google")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.X")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Mistral")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.DeepSeek")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Groq")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Together")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Fireworks")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Perplexity")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Yi")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Zhipu")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Moonshot")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Alibaba")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Cohere")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Baidu")]
[assembly: InternalsVisibleTo("Zonit.Extensions.Ai.Tests")]
