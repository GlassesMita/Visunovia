namespace Visunovia.Engine.Plugin;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize();
}
