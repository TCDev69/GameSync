using System.Text.RegularExpressions;
using GameSync.Core.Errors;
using GameSync.Core.Models;

namespace GameSync.Core.GitHub;

/// <summary>
/// Validates GitHub owner/name/branch/clone URLs before any network or filesystem use.
/// </summary>
public static class GitHubRepositoryValidator
{
    private static readonly Regex OwnerNameRegex = new(
        @"^[a-zA-Z0-9](?:[a-zA-Z0-9]|-(?=[a-zA-Z0-9])){0,38}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RepoNameRegex = new(
        @"^[a-zA-Z0-9._-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BranchRegex = new(
        @"^[a-zA-Z0-9._/-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Validate(RepositoryConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ValidateOwner(configuration.Owner);
        ValidateRepositoryName(configuration.Name);
        ValidateBranch(configuration.DefaultBranch);
        if (!string.IsNullOrWhiteSpace(configuration.CloneUrl))
        {
            ValidateCloneUrl(configuration.CloneUrl, configuration.Owner, configuration.Name);
        }
    }

    public static void ValidateOwner(string owner)
    {
        if (string.IsNullOrWhiteSpace(owner) || !OwnerNameRegex.IsMatch(owner) || owner.Contains("..", StringComparison.Ordinal))
        {
            throw new RepositoryUnavailableException($"Invalid GitHub owner '{owner}'.");
        }
    }

    public static void ValidateRepositoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Length > 100
            || !RepoNameRegex.IsMatch(name)
            || name.Contains("..", StringComparison.Ordinal))
        {
            throw new RepositoryUnavailableException($"Invalid GitHub repository name '{name}'.");
        }
    }

    public static void ValidateBranch(string branch)
    {
        if (string.IsNullOrWhiteSpace(branch)
            || branch.Length > 255
            || !BranchRegex.IsMatch(branch)
            || branch.Contains("..", StringComparison.Ordinal)
            || branch.StartsWith('/')
            || branch.StartsWith('-'))
        {
            throw new RepositoryUnavailableException($"Invalid branch name '{branch}'.");
        }
    }

    public static void ValidateCloneUrl(string cloneUrl, string owner, string name)
    {
        if (!Uri.TryCreate(cloneUrl, UriKind.Absolute, out var uri))
        {
            throw new RepositoryUnavailableException("Clone URL is not an absolute URI.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new RepositoryUnavailableException("Clone URL must use HTTPS.");
        }

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new RepositoryUnavailableException("Clone URL host must be github.com.");
        }

        var expectedPath = $"/{owner}/{name}.git";
        var altPath = $"/{owner}/{name}";
        if (!string.Equals(uri.AbsolutePath, expectedPath, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.AbsolutePath, altPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new RepositoryUnavailableException("Clone URL does not match the selected owner/repository.");
        }
    }
}
