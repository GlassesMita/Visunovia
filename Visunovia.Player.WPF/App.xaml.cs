using System.IO;
using System.Windows;

namespace Visunovia.Player.WPF;

public partial class App : Application
{
    private static string? _password;

    public static string? Password => _password;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var cryptoJsPath = Path.Combine(exeDir, "Security", "simple-crypto.js");

        if (File.Exists(cryptoJsPath))
        {
            var lines = File.ReadAllLines(cryptoJsPath);
            foreach (var line in lines)
            {
                if (line.Contains("OBFUSCATED_KEY_STRING"))
                {
                    var parts = line.Split('=');
                    if (parts.Length >= 2)
                    {
                        _password = parts[1].Trim().Trim('\'', '"', ',', ';');
                    }
                    break;
                }
            }
        }

        var lorePath = Path.Combine(exeDir, "Game.lore");
        if (File.Exists(lorePath))
        {
            var mainWindow = new MainWindow();
            mainWindow.LoadGame(lorePath, _password ?? "");
            mainWindow.Show();
        }
        else
        {
            MessageBox.Show("未找到 Game.lore 文件，请确保该文件与 Player.exe 在同一目录下。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}
