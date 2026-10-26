using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Import.Jira;

namespace ShipLean.WebApi.Tests.Import.Jira;

// Wraps the app's data protection so that the Jira token protector, and only it, throws when it is used: a test that passes with this pinned never decrypted (or encrypted) a token.
internal sealed class ThrowingTokenProtection(IDataProtectionProvider inner) : IDataProtectionProvider
{
    public static void Pin(IServiceCollection services)
    {
        var original = services.Last(d => d.ServiceType == typeof(IDataProtectionProvider));
        services.Replace(ServiceDescriptor.Singleton<IDataProtectionProvider>(sp => new ThrowingTokenProtection((IDataProtectionProvider)original.ImplementationFactory!(sp))));
    }

    public IDataProtector CreateProtector(string purpose) => purpose == JiraTokens.Purpose ? new ThrowingProtector() : inner.CreateProtector(purpose);

    private sealed class ThrowingProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => throw new InvalidOperationException("The Jira token protector was used.");

        public byte[] Unprotect(byte[] protectedData) => throw new InvalidOperationException("The Jira token protector was used.");
    }
}
