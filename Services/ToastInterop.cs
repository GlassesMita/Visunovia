using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;

public class ToastService
{
    private const string AppId = "Visunovia";

    private bool _appIdInitialized;
    private readonly object _initLock = new();

    public void ShowToast(string title, string message)
    {
        if (!_appIdInitialized)
        {
            lock (_initLock)
            {
                if (!_appIdInitialized)
                {
                    SetCurrentProcessExplicitAppUserModelID(AppId);
                    _appIdInitialized = true;
                }
            }
        }

        var escapedTitle = SecurityElement.Escape(title) ?? string.Empty;
        var escapedMessage = SecurityElement.Escape(message) ?? string.Empty;

        var toastXml = $@"
<toast>
    <visual>
        <binding template='ToastGeneric'>
            <text>{escapedTitle}</text>
            <text>{escapedMessage}</text>
        </binding>
    </visual>
</toast>";

        var psScript = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType = WindowsRuntime] | Out-Null
$template = New-Object Windows.Data.Xml.Dom.XmlDocument
$template.LoadXml(@'
{toastXml}
'@)
$toast = [Windows.UI.Notifications.ToastNotification]::new($template)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{AppId}').Show($toast)
";

        var escapedScript = psScript.Replace("\"", "\\\"");
        using var process = new Process();
        process.StartInfo.FileName = "powershell.exe";
        process.StartInfo.Arguments = $"-NoProfile -NonInteractive -Command \"{escapedScript}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            throw new Exception($"PowerShell execution failed: {stderr.Trim()}");
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);
}
