using System.Buffers.Binary;
using System.Text;
using SkiaSharp;

namespace ComTest.IntegrationTests.Support;

/// <summary>Real image bytes for tests, including some deliberately hostile ones.</summary>
public static class TestImages
{
    public const string HiddenMetadata = "SECRET-GPS-37.9601N-58.3261E";

    public static byte[] Png(int width = 400, int height = 300) => Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width = 400, int height = 300) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    /// <summary>A JPEG carrying EXIF metadata: a description with "location" text and an orientation flag.</summary>
    public static byte[] JpegWithExif(int orientation = 1, int width = 400, int height = 300)
    {
        var jpeg = Jpeg(width, height);
        var description = Encoding.ASCII.GetBytes(HiddenMetadata + "\0");

        using var tiff = new MemoryStream();

        void Write16(int value)
        {
            tiff.WriteByte((byte)(value >> 8));
            tiff.WriteByte((byte)value);
        }

        void Write32(int value)
        {
            Write16(value >> 16);
            Write16(value & 0xFFFF);
        }

        tiff.Write("MM\0*"u8);
        Write32(8);
        Write16(2);

        // ImageDescription (ASCII), stored after the directory.
        Write16(0x010E);
        Write16(2);
        Write32(description.Length);
        Write32(8 + 2 + (2 * 12) + 4);

        // Orientation (SHORT), stored inline.
        Write16(0x0112);
        Write16(3);
        Write32(1);
        Write16(orientation);
        Write16(0);

        Write32(0);
        tiff.Write(description);

        using var app1 = new MemoryStream();
        app1.Write("Exif\0\0"u8);
        tiff.WriteTo(app1);

        using var result = new MemoryStream();
        result.Write(jpeg, 0, 2);
        result.WriteByte(0xFF);
        result.WriteByte(0xE1);

        var length = (int)app1.Length + 2;
        result.WriteByte((byte)(length >> 8));
        result.WriteByte((byte)length);
        app1.WriteTo(result);
        result.Write(jpeg, 2, jpeg.Length - 2);

        return result.ToArray();
    }

    /// <summary>A valid PNG with a script appended after its end, the classic polyglot trick.</summary>
    public static byte[] PngWithAppendedScript() =>
        [.. Png(), .. "<script>alert('stolen')</script>"u8];

    /// <summary>A tiny PNG whose header claims enormous dimensions, to exhaust memory when decoded.</summary>
    public static byte[] DecompressionBomb(int width = 40_000, int height = 40_000)
    {
        using var stream = new MemoryStream();

        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2;

        WriteChunk(stream, "IHDR", header);
        WriteChunk(stream, "IDAT", [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01]);
        WriteChunk(stream, "IEND", []);

        return stream.ToArray();
    }

    /// <summary>Reads pixels back, to check how an image really turned out.</summary>
    public static SKBitmap Decode(byte[] bytes) => SKBitmap.Decode(bytes);

    public static bool IsWebp(byte[] bytes) =>
        bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Red);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);

        return encoded.ToArray();
    }

    /// <summary>The checksum PNG chunks need, so that a decoder gets as far as reading the header.</summary>
    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in bytes)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, Crc32([.. typeBytes, .. data]));
        stream.Write(checksum);
    }
}
