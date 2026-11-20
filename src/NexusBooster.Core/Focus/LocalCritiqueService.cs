using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NexusBooster.Core.Focus;

public enum CritiqueMode
{
    CodeAuditor,
    LogicHunter,
    BloatCutter,
    GoalAlignment
}

public sealed record CritiqueFeedback(bool Passed, IReadOnlyList<string> Bullets);

public sealed class LocalCritiqueService : IDisposable
{
    public const string DefaultModel = "qwen2.5:1.5b";
    public const string LocalEndpoint = "http://127.0.0.1:11434/";
    private static readonly Uri BaseUri = new(LocalEndpoint);
    private readonly HttpClient _client;

    public LocalCritiqueService(HttpMessageHandler? handler = null)
    {
        var localOnlyHandler = handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        };
        _client = new HttpClient(localOnlyHandler, disposeHandler: true)
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public async Task<CritiqueFeedback> CritiqueAsync(
        CritiqueMode mode,
        string task,
        string work,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(task, work);
        var payload = new
        {
            model = DefaultModel,
            prompt = BuildPrompt(mode, task.Trim(), work.Trim()),
            stream = false,
            format = "json"
        };

        using var response = await _client.PostAsJsonAsync("api/generate", payload, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.MovedPermanently ||
            response.StatusCode == HttpStatusCode.TemporaryRedirect ||
            response.StatusCode == HttpStatusCode.PermanentRedirect)
        {
            throw new HttpRequestException("The local Ollama endpoint redirected the request; redirects are not followed.");
        }

        response.EnsureSuccessStatusCode();
        try
        {
            using var responseDocument = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            if (!responseDocument.RootElement.TryGetProperty("response", out var responseText) ||
                responseText.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(responseText.GetString()))
            {
                throw new InvalidDataException("Ollama returned no critique response.");
            }

            return ParseFeedback(responseText.GetString()!, mode == CritiqueMode.BloatCutter);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Ollama returned malformed critique data.", error);
        }
    }

    public static CritiqueFeedback ParseFeedback(string modelResponse, bool requireCutPercent = false)
    {
        using var document = JsonDocument.Parse(modelResponse);
        var root = document.RootElement;
        if (!root.TryGetProperty("verdict", out var verdictElement) ||
            verdictElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("The critique did not include a PASS or FAIL verdict.");
        }

        var verdict = verdictElement.GetString()?.Trim();
        var passed = verdict?.Equals("PASS", StringComparison.OrdinalIgnoreCase) == true;
        var failed = verdict?.Equals("FAIL", StringComparison.OrdinalIgnoreCase) == true;
        if (!passed && !failed)
        {
            throw new InvalidDataException("The critique verdict must be PASS or FAIL.");
        }

        if (!root.TryGetProperty("bullets", out var bulletsElement) ||
            bulletsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The critique did not include its findings.");
        }

        var bullets = bulletsElement.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Length > 400 ? item[..400] : item)
            .Take(5)
            .ToArray();
        if (bullets.Length == 0)
        {
            throw new InvalidDataException("The critique returned no findings.");
        }

        if (requireCutPercent && !bullets.Any(HasCutPercent))
        {
            throw new InvalidDataException("The Bloat Cutter result must include a Cut: N% score.");
        }

        return new CritiqueFeedback(passed, bullets);
    }

    public void Dispose() => _client.Dispose();

    private static string BuildPrompt(CritiqueMode mode, string task, string work)
    {
        var specialty = mode switch
        {
            CritiqueMode.CodeAuditor =>
                "Audit code for concrete logic bugs, edge cases, and unnecessary loops. Identify issues only; never supply replacement code.",
            CritiqueMode.LogicHunter =>
                "Inspect the reasoning for unsupported assumptions, missing steps, and contradictions. Do not rewrite the plan.",
            CritiqueMode.BloatCutter =>
                "Identify redundant or low-value material and include one finding formatted exactly as Cut: NN% (an integer from 0 to 100). Do not rewrite the material.",
            CritiqueMode.GoalAlignment =>
                "Judge only whether the submitted work fulfills the one-line task. Flag missing requirements and unrelated scope.",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        return $$"""
            You are a terse, offline critique gate. {{specialty}}
            Compare the work against the exact task below. PASS only when no material blocker remains; otherwise FAIL.
            Return one JSON object with exactly these fields: {"verdict":"PASS or FAIL","bullets":["..."]}.
            Return 1 to 5 short, blunt findings. Never provide copy-paste answers, replacement code, a conversation, or a question.
            Task: {{task}}
            Work:
            {{work}}
            """;
    }

    private static bool HasCutPercent(string bullet)
    {
        var match = Regex.Match(bullet, @"\bCut\s*:\s*(\d{1,3})\s*%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, out var score) && score <= 100;
    }

    private static void ValidateInput(string task, string work)
    {
        if (string.IsNullOrWhiteSpace(task) || task.Trim().Length > 120)
        {
            throw new ArgumentException("Enter a task of 1 to 120 characters.", nameof(task));
        }

        if (string.IsNullOrWhiteSpace(work) || work.Length > 100_000)
        {
            throw new ArgumentException("Enter work to critique (up to 100,000 characters).", nameof(work));
        }
    }
}
