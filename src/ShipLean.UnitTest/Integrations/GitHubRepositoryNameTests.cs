using ShipLean.Application.Integrations;
using Shouldly;

namespace ShipLean.UnitTest.Integrations;

[TestFixture]
public class GitHubRepositoryNameTests
{
    [TestCase("https://github.com/Acme/Widgets", "Acme/Widgets")]
    [TestCase("https://GitHub.com/Acme/Widgets", "Acme/Widgets")]
    [TestCase("https://github.com/Acme/Widgets.git", "Acme/Widgets")]
    [TestCase("https://github.com/Acme/Widgets/", "Acme/Widgets")]
    [TestCase("https://github.com/Acme/Widgets.git/", "Acme/Widgets")]
    [TestCase("  https://github.com/Acme/Widgets  ", "Acme/Widgets")]
    [TestCase("https://user:secret@github.com/Acme/Widgets", "Acme/Widgets")]
    [TestCase("ssh://git@github.com/Acme/Widgets.git", "Acme/Widgets")]
    [TestCase("git@github.com:Acme/Widgets.git", "Acme/Widgets")]
    [TestCase("GIT@GITHUB.COM:Acme/Widgets", "Acme/Widgets")]
    public void FromRepoUrl_WhenGitHubAddress_ShouldReturnOwnerAndName(string url, string expected)
    {
        GitHubRepositoryName.FromRepoUrl(url).ShouldBe(expected);
    }

    [TestCase("https://github.example.com/Acme/Widgets")]
    [TestCase("https://ghe.corp.net/Acme/Widgets")]
    [TestCase("git@github.example.com:Acme/Widgets.git")]
    [TestCase("https://evilgithub.com/Acme/Widgets")]
    [TestCase("https://github.com.evil.com/Acme/Widgets")]
    [TestCase("http://github.com/Acme/Widgets")]
    [TestCase("https://github.com/Acme")]
    [TestCase("https://github.com/Acme/Widgets/issues")]
    [TestCase("https://github.com/Acme/..")]
    [TestCase("")]
    [TestCase(null)]
    public void FromRepoUrl_WhenNotGitHubDotCom_ShouldReturnNull(string? url)
    {
        GitHubRepositoryName.FromRepoUrl(url).ShouldBeNull();
    }
}
