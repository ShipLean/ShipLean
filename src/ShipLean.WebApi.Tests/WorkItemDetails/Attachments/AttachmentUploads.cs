using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// Posts a file as multipart form data the way the browser does (ApiClient only sends JSON).
internal static class AttachmentUploads
{
    public static async Task<HttpResponseMessage> PostAsync(HttpClient http, string workItemUrl, string name, string contentType, byte[] bytes, TestUser? actingAs = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", name);
        using var request = new HttpRequestMessage(HttpMethod.Post, workItemUrl) { Content = form };
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        if (actingAs is not null)
        {
            request.Headers.Add(TestAuthentication.UserHeader, actingAs.Id.ToString());
        }

        return await http.SendAsync(request);
    }

    public static async Task<AttachmentDto> UploadAsync(HttpClient http, string workItemUrl, string name, string contentType, byte[] bytes, TestUser? actingAs = null)
    {
        using var response = await PostAsync(http, workItemUrl, name, contentType, bytes, actingAs);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AttachmentDto>())!;
    }

    // Opens a socket, declares a body of 100 MiB (or `declaredLength`), sends only the start of a file and then goes quiet, as a slow or endless upload
    // does. Returns the status line the server answers with while the body is still unfinished (null when it stays silent for
    // `wait`): a server that reads the whole body before it answers cannot answer here. (HttpClient holds a response back
    // until its own request body has been written, so it cannot see this.)
    public static async Task<string?> StatusLineWhileStalledAsync(Uri baseAddress, string path, TestUser? actingAs, int firstBytes, TimeSpan wait, long declaredLength = 100L * 1024 * 1024)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(baseAddress.Host, baseAddress.Port);
        await using var stream = tcp.GetStream();
        var user = actingAs is null ? string.Empty : $"{TestAuthentication.UserHeader}: {actingAs.Id}\r\n";
        var head = $"POST /{path} HTTP/1.1\r\nHost: {baseAddress.Authority}\r\n{ClientHeader.Name}: {ApiClient.ClientId}\r\n{user}"
            + $"Content-Type: multipart/form-data; boundary=BOUND\r\nContent-Length: {declaredLength}\r\n\r\n"
            + "--BOUND\r\nContent-Disposition: form-data; name=\"file\"; filename=\"big.bin\"\r\nContent-Type: application/octet-stream\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(new byte[firstBytes]);
        using var timeout = new CancellationTokenSource(wait);
        var line = new StringBuilder();
        var buffer = new byte[1];
        try
        {
            while (await stream.ReadAsync(buffer, timeout.Token) == 1 && buffer[0] != '\n')
            {
                line.Append((char)buffer[0]);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return line.ToString().TrimEnd('\r');
    }

    public static async Task<HttpResponseMessage> PostRawAsync(HttpClient http, string workItemUrl, HttpContent content, TestUser? actingAs = null)
    {
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=BOUND");
        using var request = new HttpRequestMessage(HttpMethod.Post, workItemUrl) { Content = content };
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        if (actingAs is not null)
        {
            request.Headers.Add(TestAuthentication.UserHeader, actingAs.Id.ToString());
        }

        return await http.SendAsync(request);
    }
}
