namespace NexusBooster.Core;

public sealed class FirstLaunchConsent(string consentPath)
{
    public bool EnsureAccepted(Func<bool> requestAcceptance)
    {
        ArgumentNullException.ThrowIfNull(requestAcceptance);

        if (File.Exists(consentPath))
        {
            return true;
        }

        if (!requestAcceptance())
        {
            return false;
        }

        var consentDirectory = Path.GetDirectoryName(Path.GetFullPath(consentPath))
            ?? throw new InvalidOperationException("The consent record directory could not be determined.");
        Directory.CreateDirectory(consentDirectory);
        File.WriteAllText(consentPath, $"Accepted {DateTimeOffset.UtcNow:O}");
        return true;
    }
}
