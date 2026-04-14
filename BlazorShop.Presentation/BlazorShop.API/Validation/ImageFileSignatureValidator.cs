namespace BlazorShop.API.Validation;

using System.Text;

public static class ImageFileSignatureValidator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] BmpSignature = [0x42, 0x4D];
    private static readonly byte[] Gif87aSignature = Encoding.ASCII.GetBytes("GIF87a");
    private static readonly byte[] Gif89aSignature = Encoding.ASCII.GetBytes("GIF89a");
    private static readonly byte[] RiffSignature = Encoding.ASCII.GetBytes("RIFF");
    private static readonly byte[] WebpSignature = Encoding.ASCII.GetBytes("WEBP");
    private const int MaxSignatureLength = 12;

    public static async Task<bool> IsValidAsync(Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        if (stream == null || !stream.CanRead || string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var header = new byte[MaxSignatureLength];
        var bytesRead = await ReadHeaderAsync(stream, header, cancellationToken);
        if (bytesRead == 0)
        {
            return false;
        }

        var headerSpan = header.AsSpan(0, bytesRead);

        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => headerSpan.StartsWith(JpegSignature),
            "image/png" => headerSpan.StartsWith(PngSignature),
            "image/gif" => headerSpan.StartsWith(Gif87aSignature) || headerSpan.StartsWith(Gif89aSignature),
            "image/bmp" => headerSpan.StartsWith(BmpSignature),
            "image/webp" => IsWebp(headerSpan),
            _ => false
        };
    }

    private static async Task<int> ReadHeaderAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }

    private static bool IsWebp(ReadOnlySpan<byte> header)
    {
        return header.Length >= MaxSignatureLength
            && header[..4].SequenceEqual(RiffSignature)
            && header.Slice(8, 4).SequenceEqual(WebpSignature);
    }
}