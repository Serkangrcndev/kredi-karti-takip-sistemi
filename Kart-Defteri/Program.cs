using System.Globalization;

namespace kredi_karti_takip;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        bool background = args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        using var instance = new Mutex(true, @"Local\Erpilic.HasmetGida.KartDefteri", out bool firstInstance);
        using var activation = new EventWaitHandle(false, EventResetMode.AutoReset,
            @"Local\Erpilic.HasmetGida.KartDefteri.Open");
        if (!firstInstance)
        {
            if (!background) activation.Set();
            return;
        }

        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
        {
            LogError(args.Exception);
            MessageBox.Show("Beklenmeyen bir hata oluştu. Son işlemin sonucunu defterden kontrol edin.",
                "Kart Defteri", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        Application.Run(new Form1(background, activation));
    }
    internal static void LogError(Exception exception)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KartDefteri");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "hatalar.log"),
                $"{DateTimeOffset.Now:O} {exception.GetType().FullName} (0x{exception.HResult:X8}){Environment.NewLine}" +
                $"{exception.StackTrace}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
