namespace Visunovia.Models.Engine;

public class VNProject
{
    public VNMetadata Metadata { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public List<VNScene> Scenes { get; set; } = new();
}

public class VNMetadata
{
    public string Title { get; set; } = "未命名项目";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0";
}

public class VNCustomMethod
{
    public string Name { get; set; } = "";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string? Script { get; set; }
    public string Language { get; set; } = "csharp";
}
