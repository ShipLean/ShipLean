using System.Net.Http.Json;
using System.Text.Json;

namespace ShipLean.Application.Features.Holidays;

// Reads Nager.Date over its named HttpClient. A failure throws (HttpRequestException, JsonException, TaskCanceledException);
// the refresher catches it and keeps the old data.
public sealed class NagerClient(IHttpClientFactory clients, HolidaySettings settings)
{
    public const string HttpClientName = "holidays-nager";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public bool Enabled => settings.BaseUrl is not null;

    public Task<IReadOnlyList<NagerCountry>> CountriesAsync(CancellationToken cancellationToken)
        => GetAsync<NagerCountry>("AvailableCountries", cancellationToken);

    // Only the national (global) holidays, with a name; one per date and name.
    public async Task<IReadOnlyList<NagerHoliday>> HolidaysAsync(int year, string countryCode, CancellationToken cancellationToken)
    {
        var rows = await GetAsync<NagerHoliday>($"PublicHolidays/{year}/{Uri.EscapeDataString(countryCode)}", cancellationToken);
        return [.. rows.Where(h => h.Global && !string.IsNullOrWhiteSpace(h.Name)).DistinctBy(h => (h.Date, h.Name))];
    }

    private async Task<IReadOnlyList<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var baseUrl = settings.BaseUrl ?? throw new InvalidOperationException("The holiday feed is switched off.");
        using var response = await clients.CreateClient(HttpClientName).GetAsync(new Uri(string.Join('/', baseUrl.AbsoluteUri.TrimEnd('/'), path)), cancellationToken);
        response.EnsureSuccessStatusCode();
        // 204 (Nager's answer for a country it has no data for) has no body.
        if (response.Content.Headers.ContentLength == 0)
        {
            return [];
        }

        return await response.Content.ReadFromJsonAsync<List<T>>(Wire, cancellationToken) ?? [];
    }
}
