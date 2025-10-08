namespace GIFleziPT.App.Configs;

public class AppSettings
{
    public static AppSettings Instance { get; set; } = new AppSettings();
    public static IConfiguration Configs { get; set; } = null!;

    public string PythonScriptPath { get; set; } = string.Empty;
}
