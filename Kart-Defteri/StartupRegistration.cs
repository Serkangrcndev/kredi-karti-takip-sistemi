using Microsoft.Win32;

namespace kredi_karti_takip;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string SettingsKey = @"Software\KartDefteri";
    private const string AppName = "KartDefteri";

    private static RegistryKey OpenUserRegistry() => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
        Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

    public static bool IsPackaged => Environment.ProcessPath is string path &&
        !string.Equals(Path.GetFileNameWithoutExtension(path), "dotnet", StringComparison.OrdinalIgnoreCase) &&
        !File.Exists(Path.ChangeExtension(path, ".dll"));

    public static bool IsEnabled()
    {
        using var registry = OpenUserRegistry();
        using var settings = registry.OpenSubKey(SettingsKey);
        return settings?.GetValue("StartWithWindows") is not 0;
    }

    public static void SetEnabled(bool enabled)
    {
        Apply(enabled);
        using var registry = OpenUserRegistry();
        using var settings = registry.CreateSubKey(SettingsKey);
        settings.SetValue("StartWithWindows", enabled ? 1 : 0, RegistryValueKind.DWord);
    }

    public static void Apply(bool enabled)
    {
        using var registry = OpenUserRegistry();
        using var run = registry.CreateSubKey(RunKey);
        if (!enabled)
        {
            run.DeleteValue(AppName, throwOnMissingValue: false);
            return;
        }

        string path = Environment.ProcessPath ?? throw new InvalidOperationException("Uygulama dosyası bulunamadı.");
        string command = $"\"{path}\" --background";
        if (command.Length > 260)
            throw new InvalidOperationException("Otomatik başlatma için uygulamayı daha kısa bir klasör yoluna taşıyın.");
        if (!string.Equals(run.GetValue(AppName) as string, command, StringComparison.Ordinal))
            run.SetValue(AppName, command, RegistryValueKind.String);
    }
}
