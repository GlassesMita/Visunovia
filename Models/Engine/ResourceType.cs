namespace Visunovia.Models.Engine;

public enum ResourceType
{
    Sprite,
    Background,
    Music,
    Voice,
    Font,
    Other
}

public class ResourceItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public ResourceType Type { get; set; }

    public ResourceItem() { }

    public ResourceItem(string name, string path, ResourceType type)
    {
        Name = name;
        Path = path;
        Type = type;
        ThumbnailPath = type == ResourceType.Music || type == ResourceType.Voice || type == ResourceType.Font || type == ResourceType.Other
            ? null : path;
    }
}
