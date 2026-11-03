using System.IO.Compression;
using System.Security.Cryptography;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using TV_Player.Updates;
using Application = Android.App.Application;

namespace TV_Player.Maui.Services;

public sealed class AndroidAppUpdater
{
    public static AndroidAppUpdater Current { get; } = new();
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly GitHubUpdateService _service;
    private static Context Context => Application.Context;
    private static PackageManager Manager => Context.PackageManager!;
    public string CacheDirectory => Path.Combine(FileSystem.CacheDirectory, "updates");
    public string InstalledVersion => AppInfo.Current.VersionString;
    public long InstalledVersionCode => GetVersionCode(Manager.GetPackageInfo(Context.PackageName!, PackageInfoFlags.MetaData)!);
    public UpdateChannel Channel
    {
        get => Preferences.Default.Get("UpdateChannel", "Release") == "Dev" ? UpdateChannel.Dev : UpdateChannel.Release;
        set => Preferences.Default.Set("UpdateChannel", value.ToString());
    }
    public bool IsStoreBuild => (OperatingSystem.IsAndroidVersionAtLeast(30)
        ? Manager.GetInstallSourceInfo(Context.PackageName!).InstallingPackageName
        : Manager.GetInstallerPackageName(Context.PackageName!)) == "com.android.vending";
    public bool CanInstall => !OperatingSystem.IsAndroidVersionAtLeast(26) || Manager.CanRequestPackageInstalls();

    private AndroidAppUpdater() => _service = new(_http);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        if (IsStoreBuild) throw new InvalidOperationException("Обновите приложение через Google Play.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        return await _service.CheckAsync(Channel, InstalledVersionCode, (int)Build.VERSION.SdkInt, timeout.Token);
    }

    public async Task<string> DownloadAsync(AvailableUpdate update, IProgress<DownloadProgress> progress, CancellationToken token)
    {
        if (IsStoreBuild) throw new InvalidOperationException("Обновите приложение через Google Play.");
        Directory.CreateDirectory(CacheDirectory);
        using var storage = new StatFs(CacheDirectory);
        if (storage.AvailableBytes < update.Manifest.Size + 50L * 1024 * 1024)
            throw new IOException("Недостаточно свободного места для обновления.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        var path = await _service.DownloadAsync(update, CacheDirectory, progress, timeout.Token);
        try { ValidateApk(path, update.Manifest); return path; }
        catch { File.Delete(path); throw; }
    }

    public void ValidateApk(string path, AndroidUpdateManifest manifest)
    {
        if (manifest.VersionCode <= InstalledVersionCode) throw new InvalidDataException("Понижение или повторная установка версии запрещены.");
        var flags = OperatingSystem.IsAndroidVersionAtLeast(28) ? PackageInfoFlags.SigningCertificates : PackageInfoFlags.Signatures;
        using var installed = Manager.GetPackageInfo(Context.PackageName!, flags)!;
        using var candidate = Manager.GetPackageArchiveInfo(path, flags)
            ?? throw new InvalidDataException("Android не распознал APK.");
        if (candidate.PackageName != AndroidUpdatePolicy.PackageId || GetVersionCode(candidate) != manifest.VersionCode ||
            candidate.VersionName != manifest.VersionName)
            throw new InvalidDataException("Пакет или версия APK не совпадает с релизом.");
        if (!Certificates(installed).SequenceEqual(Certificates(candidate)))
            throw new InvalidDataException("Подпись APK отличается от установленного приложения.");
        if (candidate.ApplicationInfo == null || (OperatingSystem.IsAndroidVersionAtLeast(24) &&
            (int)candidate.ApplicationInfo.MinSdkVersion > (int)Build.VERSION.SdkInt))
            throw new InvalidDataException("APK несовместим с версией Android.");
        using var zip = ZipFile.OpenRead(path);
        var abis = zip.Entries.Where(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal))
            .Select(entry => entry.FullName.Split('/')).Where(parts => parts.Length > 2).Select(parts => parts[1]).ToHashSet();
        if (abis.Count > 0 && !(Build.SupportedAbis ?? Array.Empty<string>()).Any(abis.Contains))
            throw new InvalidDataException("APK несовместим с процессором устройства.");
    }

    private static string[] Certificates(PackageInfo info)
    {
        var signatures = OperatingSystem.IsAndroidVersionAtLeast(28)
            ? info.SigningInfo?.GetApkContentsSigners() : info.Signatures;
        if (signatures == null || signatures.Count == 0) throw new InvalidDataException("Не удалось проверить подпись APK.");
        return signatures.Select(signature => Convert.ToHexString(SHA256.HashData(signature.ToByteArray()!)))
            .Order(StringComparer.Ordinal).ToArray();
    }

    private static long GetVersionCode(PackageInfo info) => OperatingSystem.IsAndroidVersionAtLeast(28) ? info.LongVersionCode : info.VersionCode;

    public void OpenInstallPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var intent = new Intent(Android.Provider.Settings.ActionManageUnknownAppSources,
            Android.Net.Uri.Parse("package:" + Context.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        Context.StartActivity(intent);
    }

    public void Install(string path, AndroidUpdateManifest manifest)
    {
        if (IsStoreBuild) throw new InvalidOperationException("Обновите приложение через Google Play.");
        ValidateApk(path, manifest);
        if (!CanInstall) throw new InvalidOperationException("Разрешите установку обновлений для IPTV Player.");
        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(Context, Context.PackageName + ".updates", new Java.IO.File(path));
        var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(uri, "application/vnd.android.package-archive");
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
        Context.StartActivity(intent);
    }

    public void CleanOldDownloads()
    {
        try
        {
            if (!Directory.Exists(CacheDirectory)) return;
            foreach (var file in Directory.EnumerateFiles(CacheDirectory, "*.apk"))
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-1)) File.Delete(file);
        }
        catch (IOException) { }
    }
}
