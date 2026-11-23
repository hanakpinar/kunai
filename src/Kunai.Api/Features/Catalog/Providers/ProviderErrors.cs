using Kunai.Api.Common.Results;

namespace Kunai.Api.Features.Catalog.Providers;

public static class ProviderErrors
{
    public const string UnavailableCode = "provider_unavailable";
    public const string NotConfiguredCode = "provider_not_configured";
    public const string NotFoundCode = "external_item_not_found";

    public static Error Unavailable(ItemSource source) =>
        Error.Unavailable(UnavailableCode, $"{source} is not answering right now. Try again shortly.");

    public static Error NotConfigured(ItemSource source) =>
        Error.Unavailable(NotConfiguredCode, $"{source} is not configured on this server.");

    public static Error NotFound(ExternalRef reference) =>
        Error.BusinessRule(NotFoundCode, $"{reference} does not exist at the provider.");
}
