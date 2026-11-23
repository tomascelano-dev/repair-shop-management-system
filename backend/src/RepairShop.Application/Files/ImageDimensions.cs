using System.Buffers.Binary;

namespace RepairShop.Application.Files;

/// <summary>
/// Reads the pixel size from the image header without decoding it (PNG, JPEG, WEBP). Security audit 2026-11-23 (M-07):
/// the upload limit is in bytes, but logos and signatures are decoded in every PDF (public ones included), so a small
/// file declaring 20000×20000 pixels would cost gigabytes of memory each time.
/// </summary>
public static class ImageDimensions
{
    /// <summary>Largest picture accepted: well above any phone camera (50 MP ≈ 8160×6120).</summary>
    public const long MaxPixels = 64_000_000;
    public const int MaxSide = 16_000;

    public static bool IsAcceptable(byte[] bytes)
        => TryRead(bytes) is not { } size || (size.Width <= MaxSide && size.Height <= MaxSide && (long)size.Width * size.Height <= MaxPixels);

    /// <summary>Width and height, or null when the format is not one of the three or the header cannot be read.</summary>
    public static (int Width, int Height)? TryRead(ReadOnlySpan<byte> b)
    {
        // PNG: signature + IHDR (width, height big-endian at 16..23).
        if (b.Length >= 24 && b[0] == 0x89 && b[1] == (byte)'P' && b[2] == (byte)'N' && b[3] == (byte)'G')
            return (BinaryPrimitives.ReadInt32BigEndian(b[16..]), BinaryPrimitives.ReadInt32BigEndian(b[20..]));

        // JPEG: walk the markers to the first SOFn.
        if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < b.Length)
            {
                if (b[i] != 0xFF) { i++; continue; }
                var marker = b[i + 1];
                if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) { i += 2; continue; }
                var length = BinaryPrimitives.ReadUInt16BigEndian(b[(i + 2)..]);
                if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                    return (BinaryPrimitives.ReadUInt16BigEndian(b[(i + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(b[(i + 5)..]));
                if (length < 2) return null;
                i += 2 + length;
            }
            return null;
        }

        // WEBP: RIFF....WEBP + VP8 / VP8L / VP8X chunk.
        if (b.Length >= 30 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8))
        {
            var chunk = b[12..16];
            if (chunk.SequenceEqual("VP8X"u8))
                return (1 + (b[24] | b[25] << 8 | b[26] << 16), 1 + (b[27] | b[28] << 8 | b[29] << 16));
            if (chunk.SequenceEqual("VP8L"u8))
            {
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(b[21..]);
                return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
            }
            if (chunk.SequenceEqual("VP8 "u8))
                return (BinaryPrimitives.ReadUInt16LittleEndian(b[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(b[28..]) & 0x3FFF);
        }
        return null;
    }
}
