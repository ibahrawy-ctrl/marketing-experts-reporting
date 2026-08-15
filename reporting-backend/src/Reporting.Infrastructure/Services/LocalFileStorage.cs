using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Documents;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// تنفيذ تخزين محلّيّ على القرص لخدمة المستندات (CPW-R1B2).
/// الجذر من الإعدادات حصرًا وخارج جذر الويب، ولا يُقدَّم عبر Static Files.
/// الكتابة ذرّيّة (ملفّ مؤقّت ثمّ نقل) والبصمة تُحسَب أثناء الكتابة بمرور واحد.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    /// <summary>مقاطع المفتاح مقيَّدة بمعرّفات وامتدادات آمنة — يمنع أيّ محاولة خروج من الجذر.</summary>
    private static readonly Regex SegmentPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.Compiled);

    private readonly IHostEnvironment _env;
    private readonly FileStorageOptions _options;

    public LocalFileStorage(IHostEnvironment env, IOptions<FileStorageOptions> options)
    {
        _env = env;
        _options = options.Value;
    }

    /// <summary>جذر التخزين — من الإعدادات أو افتراضيّ آمن داخل ContentRoot/App_Data.</summary>
    private string Root =>
        string.IsNullOrWhiteSpace(_options.DocumentsRootPath)
            ? Path.Combine(_env.ContentRootPath, "App_Data", "documents")
            : _options.DocumentsRootPath;

    public string BuildStorageKey(string resourceKind, Guid resourceId, Guid documentId, Guid versionId, string safeExtension)
    {
        if (string.IsNullOrWhiteSpace(resourceKind) || !SegmentPattern.IsMatch(resourceKind))
            throw new ArgumentException("نوع المورد غير صالح كمقطع مسار.", nameof(resourceKind));

        var ext = string.IsNullOrWhiteSpace(safeExtension) ? string.Empty : safeExtension.ToLowerInvariant();
        if (ext.Length > 0 && !Regex.IsMatch(ext, "^\\.[a-z0-9]{1,10}$"))
            throw new ArgumentException("الامتداد غير صالح.", nameof(safeExtension));

        // معرّفات فقط — لا أثر لاسم الملفّ الأصليّ في المسار.
        return $"{resourceKind}/{resourceId:D}/{documentId:D}/{versionId:D}{ext}";
    }

    public async Task<StoredFile> SaveAsync(string storageKey, Stream content, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(storageKey);
        if (File.Exists(fullPath))
            throw new IOException("مفتاح التخزين مستخدَم مسبقًا.");

        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        // كتابة ذرّيّة: ملفّ مؤقّت في المجلّد نفسه ثمّ نقل — لا يظهر ملفّ ناقص تحت المفتاح النهائيّ.
        var tempPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        long size;
        string hash;

        try
        {
            using (var sha = SHA256.Create())
            await using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             bufferSize: 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                    total += read;
                }

                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                await target.FlushAsync(ct);

                size = total;
                hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            }

            File.Move(tempPath, fullPath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }

        return new StoredFile(storageKey, size, hash);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(storageKey);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("الملفّ المخزَّن غير موجود.");

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
        => Task.FromResult(File.Exists(ResolveFullPath(storageKey)));

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        TryDelete(ResolveFullPath(storageKey));
        return Task.CompletedTask;
    }

    /// <summary>
    /// يحوّل المفتاح إلى مسار مطلق داخل الجذر مع منع الخروج منه.
    /// يتحقّق من كلّ مقطع بنمط صارم ثمّ يتحقّق من احتواء المسار النهائيّ داخل الجذر.
    /// </summary>
    private string ResolveFullPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("مفتاح التخزين مطلوب.", nameof(storageKey));

        if (storageKey.Contains('\\') || Path.IsPathRooted(storageKey))
            throw new ArgumentException("مفتاح التخزين غير صالح.", nameof(storageKey));

        var segments = storageKey.Split('/', StringSplitOptions.None);
        foreach (var segment in segments)
        {
            if (!SegmentPattern.IsMatch(segment))
                throw new ArgumentException("مفتاح التخزين غير صالح.", nameof(storageKey));
        }

        var rootFull = Path.GetFullPath(Root);
        var combined = Path.GetFullPath(Path.Combine(rootFull, Path.Combine(segments)));

        var rootPrefix = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootPrefix, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("مسار التخزين خارج الجذر المسموح.");

        return combined;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // التنظيف أفضل جهد — لا يُسقِط العمليّة الأصليّة.
        }
    }
}
