using System.Security.Cryptography;

namespace DocumentsReporting.Application;

public static class FileValidationHelper
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    // Magic bytes
    private static readonly byte[] PdfMagic = [0x25, 0x50, 0x44, 0x46, 0x2D]; // %PDF-
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static async Task<(MemoryStream ContentStream, string VerifiedContentType, string Sha256Hash, long SizeBytes)> ValidateAndReadAsync(
        Stream inputStream,
        string fileName,
        string? declaredContentType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);

        var ms = new MemoryStream();
        await inputStream.CopyToAsync(ms, ct);

        var size = ms.Length;
        if (size == 0)
        {
            throw new DocumentsException(DocumentsErrorCode.InvalidFileType, "Tệp kết quả rỗng (0 bytes).");
        }

        if (size > MaxFileSizeBytes)
        {
            throw new DocumentsException(DocumentsErrorCode.FileTooLarge, $"Kích thước tệp ({size} bytes) vượt quá giới hạn tối đa 20 MB.");
        }

        var bytes = ms.ToArray();

        var verifiedContentType = DetectContentType(bytes);
        if (verifiedContentType == null)
        {
            throw new DocumentsException(DocumentsErrorCode.InvalidFileType, "Định dạng tệp không hợp lệ. Chỉ chấp nhận tệp PDF, JPG hoặc PNG dựa trên nội dung thực tế (magic bytes).");
        }

        // Tính SHA-256
        var hashBytes = SHA256.HashData(bytes);
        var sha256Hex = Convert.ToHexStringLower(hashBytes);

        // Reset vị trí stream
        ms.Position = 0;

        return (ms, verifiedContentType, sha256Hex, size);
    }

    public static string? DetectContentType(ReadOnlySpan<byte> headerBytes)
    {
        if (headerBytes.Length >= PdfMagic.Length && headerBytes[..PdfMagic.Length].SequenceEqual(PdfMagic))
        {
            return "application/pdf";
        }

        if (headerBytes.Length >= JpegMagic.Length && headerBytes[..JpegMagic.Length].SequenceEqual(JpegMagic))
        {
            return "image/jpeg";
        }

        if (headerBytes.Length >= PngMagic.Length && headerBytes[..PngMagic.Length].SequenceEqual(PngMagic))
        {
            return "image/png";
        }

        return null;
    }
}
