using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Side;

// What the side rail tests read and arrange outside the page.
internal static class WorkItemAttachments
{
    // Tom, signed in on a client of his own, attaches a file to the item behind the page's back.
    public static async Task UploadAsTomAsync(string baseUrl, Guid itemId, string name, byte[] bytes)
    {
        using var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        http.DefaultRequestHeaders.Add("X-ShipLean-Client", "teammate-client");
        using var login = await http.PostAsJsonAsync("/api/auth/login", new { email = TestUser.Tom.Email, password = TestUser.Tom.Password });
        login.EnsureSuccessStatusCode();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        form.Add(file, "file", name);
        using var posted = await http.PostAsync($"/api/work-items/{itemId}/attachments", form);
        posted.EnsureSuccessStatusCode();
    }

    public static async Task<(bool Archived, string StorageRef)?> RowAsync(string connectionString, Guid workItemId, string name)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<(bool Archived, string StorageRef)?>(
            "SELECT archived_at IS NOT NULL AS archived, storage_ref AS storageRef FROM attachment WHERE work_item_id = @workItemId AND name = @name",
            new { workItemId, name });
        return row;
    }

    // Marks the item as imported from Jira, the way an import leaves it.
    public static async Task MarkImportedAsync(string connectionString, Guid itemId, string jiraKey, string url)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(
            "UPDATE work_item SET original_system = 'jira', original_system_id = @jiraKey, original_url = @url WHERE id = @itemId",
            new { itemId, jiraKey, url });
    }
}
