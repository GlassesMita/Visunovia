namespace Visunovia.Player.WPF.Player;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize();
}
