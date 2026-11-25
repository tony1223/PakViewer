using Lin.Helper.Core.Sprite;

// Synthetic fixtures avoid distributing client assets.
// Run: dotnet run --project tools/TestSpxSignedOffsets -p:TargetFrameworks=net10.0
foreach (bool paletted in new[] { false, true })
{
    Check(paletted, negativeOrigin: true);
    Check(paletted, negativeOrigin: false);
}
Console.WriteLine("PASS: signed/unsigned origins, palette/RGB555, Read/ReadRaw and SPR round trips");

static void Check(bool paletted, bool negativeOrigin)
{
    byte[] data = CreateFixture(paletted, negativeOrigin);
    var raw = L1SPX.ReadRaw(data);
    var frames = L1SPX.Read(data);
    if (raw.Length != 1 || frames.Length != 1) throw new Exception("Unexpected frame count");
    int x = negativeOrigin ? 0 : 2;
    AssertPixel(raw[0].Pixels, raw[0].Width, x, 1, 255, 0, 0, 255);
    AssertPixel(raw[0].Pixels, raw[0].Width, x + 1, 1, 0, 255, 0, 255);
    // This row begins at x=-1 for the signed fixture. Clip that pixel, but
    // consume its color and preserve the following in-bounds pixel at x=0.
    AssertPixel(raw[0].Pixels, raw[0].Width, x, 2, 0, 255, 0, 255);
    int opaque = 0;
    for (int i = 3; i < raw[0].Pixels.Length; i += 4)
        if (raw[0].Pixels[i] != 0) opaque++;
    if (opaque != 3) throw new Exception($"Expected 3 opaque pixels, got {opaque}");
    var pixels = new byte[raw[0].Width * raw[0].Height * 4];
    frames[0].Image.CopyPixelDataTo(pixels);
    frames[0].Image.Dispose();
    if (!pixels.AsSpan().SequenceEqual(raw[0].Pixels)) throw new Exception("Read and ReadRaw disagree");
    var roundTrip = SprReader.LoadRaw(L1SPX.ToSpr(data, scale: 1));
    // SPR crops the empty lower tile row, but preserves the decoded pixels.
    AssertPixel(roundTrip[0].Pixels, roundTrip[0].Width, x, 1, 255, 0, 0, 255);
    AssertPixel(roundTrip[0].Pixels, roundTrip[0].Width, x + 1, 1, 0, 255, 0, 255);
    AssertPixel(roundTrip[0].Pixels, roundTrip[0].Width, x, 2, 0, 255, 0, 255);
    Console.WriteLine($"PASS: palette={paletted}, negativeOrigin={negativeOrigin}");
}

static void AssertPixel(byte[] pixels, int width, int x, int y, byte r, byte g, byte b, byte a)
{
    int offset = (y * width + x) * 4;
    if (!pixels.AsSpan(offset, 4).SequenceEqual(new byte[] { r, g, b, a }))
        throw new Exception($"Unexpected RGBA at ({x},{y}): {Convert.ToHexString(pixels.AsSpan(offset, 4))}");
}

static byte[] CreateFixture(bool paletted, bool negativeOrigin)
{
    using var blockStream = new MemoryStream();
    using (var block = new BinaryWriter(blockStream, System.Text.Encoding.UTF8, leaveOpen: true))
    {
        block.Write(negativeOrigin ? (byte)0xFE : (byte)2);
        block.Write((byte)1); // startY
        block.Write((byte)50); // encoded block width
        block.Write((byte)2); // rows
        block.Write((byte)1); // segments, row 0
        block.Write(negativeOrigin ? (byte)4 : (byte)0); // skip in two-byte units
        block.Write((byte)2);
        WriteColor(block, paletted, 0);
        WriteColor(block, paletted, 1);
        block.Write((byte)1); // segments, row 1
        block.Write(negativeOrigin ? (byte)2 : (byte)0);
        block.Write(negativeOrigin ? (byte)2 : (byte)1);
        if (negativeOrigin) WriteColor(block, paletted, 0);
        WriteColor(block, paletted, 1);
    }
    byte[] blockData = blockStream.ToArray();
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    if (paletted)
    {
        writer.Write((byte)255);
        writer.Write((byte)2);
        writer.Write((ushort)0x7C00); // red
        writer.Write((ushort)0x03E0); // green
    }
    writer.Write((byte)1); // frames
    writer.Write((short)0); writer.Write((short)0);
    writer.Write((short)47); writer.Write((short)47);
    writer.Write((ushort)0); writer.Write((ushort)0);
    writer.Write(1); // block references
    writer.Write((sbyte)0); writer.Write((sbyte)0);
    writer.Write((byte)0); writer.Write(0);
    writer.Write(1); // block table size
    writer.Write(0); writer.Write(blockData.Length);
    writer.Write(blockData);
    return stream.ToArray();
}

static void WriteColor(BinaryWriter writer, bool paletted, byte index)
{
    if (paletted) writer.Write(index);
    else writer.Write(index == 0 ? (ushort)0x7C00 : (ushort)0x03E0);
}
