using System.IO;
using System.Text.Json;

namespace Visunovia.Controls;

public class RecentProjectsManager
{
    private const int MaxRecentProjects = 10;
    private readonly string _recentProjectsPath;
    private List<RecentProject> _recentProjects = new();

    public RecentProjectsManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Visunovia");

        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        _recentProjectsPath = Path.Combine(appDataPath, "recent_projects.json");
        LoadRecentProjects();
    }

    public IReadOnlyList<RecentProject> RecentProjects => _recentProjects.AsReadOnly();

    public void AddProject(string projectPath, string projectTitle)
    {
        if (string.IsNullOrEmpty(projectPath))
            return;

        var existing = _recentProjects.FirstOrDefault(p =>
            string.Equals(p.Path, projectPath, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            _recentProjects.Remove(existing);
        }

        _recentProjects.Insert(0, new RecentProject
        {
            Path = projectPath,
            Title = projectTitle,
            LastOpened = DateTime.Now
        });

        if (_recentProjects.Count > MaxRecentProjects)
        {
            _recentProjects = _recentProjects.Take(MaxRecentProjects).ToList();
        }

        SaveRecentProjects();
    }

    public void RemoveProject(string projectPath)
    {
        var existing = _recentProjects.FirstOrDefault(p =>
            string.Equals(p.Path, projectPath, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            _recentProjects.Remove(existing);
            SaveRecentProjects();
        }
    }

    public void Clear()
    {
        _recentProjects.Clear();
        SaveRecentProjects();
    }

    private void LoadRecentProjects()
    {
        try
        {
            if (File.Exists(_recentProjectsPath))
            {
                var json = File.ReadAllText(_recentProjectsPath);
                _recentProjects = JsonSerializer.Deserialize<List<RecentProject>>(json) ?? new List<RecentProject>();

                _recentProjects = _recentProjects
                    .Where(p => File.Exists(p.Path))
                    .ToList();

                if (_recentProjects.Count > MaxRecentProjects)
                {
                    _recentProjects = _recentProjects.Take(MaxRecentProjects).ToList();
                }
            }
        }
        catch
        {
            _recentProjects = new List<RecentProject>();
        }
    }

    private void SaveRecentProjects()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentProjects, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_recentProjectsPath, json);
        }
        catch
        {
        }
    }
}

public class RecentProject
{
    public string Path { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; }
}
