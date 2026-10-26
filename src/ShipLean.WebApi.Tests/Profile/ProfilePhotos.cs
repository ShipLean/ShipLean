using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShipLean.Contracts;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Profile;

// Sends a photo as multipart form data the way the browser does (ApiClient only sends JSON), and builds image bytes.
internal static class ProfilePhotos
{
    public const int MaxBytes = 2 * 1024 * 1024;

    // Images with a real header that claims the given size (the body after the header is filler, never decoded).
    public static byte[] Png(int length = 64, byte fill = 1, uint width = 1, uint height = 1)
        => Padded([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, .. "IHDR"u8, .. BigEndian(width), .. BigEndian(height), 8, 2, 0, 0, 0], length, fill);

    // SOI, then a start-of-frame segment (SOF0) with height and width.
    public static byte[] Jpeg(int length = 64, byte fill = 2, ushort width = 1, ushort height = 1)
        => Padded([0xFF, 0xD8, 0xFF, 0xC0, 0, 17, 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1], length, fill);

    // RIFF header and a VP8X chunk (24-bit width-1 and height-1).
    public static byte[] WebP(int length = 64, byte fill = 3, uint width = 1, uint height = 1)
        => Padded([.. "RIFF\0\0\0\0WEBPVP8X"u8, 10, 0, 0, 0, 0, 0, 0, 0, .. LittleEndian24(width - 1), .. LittleEndian24(height - 1)], length, fill);

    // A lossy WebP: a VP8 chunk with the frame start code and 14-bit sizes.
    public static byte[] WebPLossy(uint width = 1, uint height = 1)
        => Padded([.. "RIFF\0\0\0\0WEBPVP8 "u8, 10, 0, 0, 0, 0, 0, 0, 0x9D, 0x01, 0x2A, (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8)], 64, 3);

    // A lossless WebP: a VP8L chunk with the 0x2F signature and 14-bit width-1 and height-1 packed together.
    public static byte[] WebPLossless(uint width = 1, uint height = 1)
        => Padded([.. "RIFF\0\0\0\0WEBPVP8L"u8, 5, 0, 0, 0, 0x2F, .. BitConverter.GetBytes(((width - 1) & 0x3FFF) | (((height - 1) & 0x3FFF) << 14))], 64, 3);

    // SOI, JFIF (APP0) and EXIF (APP1) segments, then the frame header: the order a camera or encoder writes.
    public static byte[] EncoderJpeg(ushort width, ushort height)
        => Padded([0xFF, 0xD8, .. Segment(0xE0, "JFIF\0\x01\x01\0\0\x01\0\x01\0\0"u8.ToArray()), .. Segment(0xE1, [.. "Exif\0\0"u8, .. new byte[40]]), .. Frame(width, height)], 64, 2);

    // SOI, a Huffman table (DHT, C4) whose bytes look like a frame header of 5000 wide, then the real frame header.
    public static byte[] JpegWithTableFirst(ushort width, ushort height)
        => Padded([0xFF, 0xD8, .. Segment(0xC4, [0x00, 0x00, 0x01, 0x13, 0x88, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]), .. Frame(width, height)], 64, 2);

    // The decoy of a crafted file: SOI, then FF <marker> 00 10 (a "length" of 16 that a decoder never uses because it skips the
    // pair as garbage), an APP1 that hides a 1x1 frame header exactly where that length lands, and the real 12000x12000 frame.
    public static byte[] JpegWithDecoy(byte marker)
    {
        var app1 = Segment(0xE1, [.. "Exif\0\0"u8, .. new byte[40]]);
        var head = new List<byte> { 0xFF, 0xD8, 0xFF, marker, 0x00, 0x10 };
        head.AddRange(app1);
        var decoy = Frame(1, 1);
        var bytes = head.ToArray();
        decoy.CopyTo(bytes, 2 + 2 + 16);
        return Padded([.. bytes, .. Frame(12000, 12000)], 64, 2);
    }

    private static byte[] Segment(byte marker, byte[] payload) => [0xFF, marker, (byte)((payload.Length + 2) >> 8), (byte)(payload.Length + 2), .. payload];

    private static byte[] Frame(ushort width, ushort height)
        => [0xFF, 0xC0, 0, 11, 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 1, 1, 0x11, 0];

    // A Windows executable's first bytes.
    public static byte[] Exe(int length = 64) => Padded([0x4D, 0x5A, 0x90, 0x00], length, 9);

    public static async Task<HttpResponseMessage> PutAsync(HttpClient http, TestUser user, string declaredType, byte[] bytes, string fileName = "me.png")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(declaredType);
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/me/photo") { Content = form };
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        request.Headers.Add(TestAuthentication.UserHeader, user.Id.ToString());
        return await http.SendAsync(request);
    }

    // A body written by hand, so a test can break the framing: the boundary, a missing closing line, a part that never ends.
    public static async Task<HttpResponseMessage> PutRawAsync(HttpClient http, TestUser user, string boundary, HttpContent body)
    {
        body.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/me/photo") { Content = body };
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        request.Headers.Add(TestAuthentication.UserHeader, user.Id.ToString());
        return await http.SendAsync(request);
    }

    public static string FilePartHead(string boundary) => $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"me.png\"\r\nContent-Type: image/png\r\n\r\n";

    // A well-formed single-file body.
    public static ByteArrayContent Closed(string boundary, byte[] file)
        => new([.. System.Text.Encoding.ASCII.GetBytes(FilePartHead(boundary)), .. file, .. System.Text.Encoding.ASCII.GetBytes($"\r\n--{boundary}--\r\n")]);

    public static async Task<MeDto> UploadAsync(HttpClient http, TestUser user, string declaredType, byte[] bytes)
    {
        using var response = await PutAsync(http, user, declaredType, bytes);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ProfileWire.ReadMeAsync(response);
    }

    private static byte[] BigEndian(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static byte[] LittleEndian24(uint value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16)];

    private static byte[] Padded(byte[] head, int length, byte fill)
    {
        var bytes = new byte[Math.Max(length, head.Length)];
        Array.Fill(bytes, fill);
        head.CopyTo(bytes, 0);
        return bytes;
    }
}
