namespace ShipLean.WebApi.Tests.Backups.EndToEnd;

// The first 36 bytes of an SLBK1 object as C-FORMAT pins them. Reading them from the bucket is how a test knows what was
// uploaded was encrypted, not plain: a pass-through cipher fails here.
internal static class Slbk1Header
{
    public static async Task CheckAsync(string file)
    {
        var header = new byte[36];
        await using (var stream = File.OpenRead(file))
        {
            (await stream.ReadAsync(header)).ShouldBe(36, $"{file} is shorter than an SLBK1 header");
        }

        System.Text.Encoding.ASCII.GetString(header, 0, 4).ShouldBe("SLBK", file);
        header[4].ShouldBe((byte)0x01, $"{file}: version");
        header[5].ShouldBe((byte)0x01, $"{file}: KDF");
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(6, 4)).ShouldBe(600_000u, $"{file}: iterations");
        header[34].ShouldBe((byte)0, $"{file}: reserved");
        header[35].ShouldBe((byte)0, $"{file}: reserved");
    }
}
