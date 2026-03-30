namespace Visunovia.Engine.Script;

public class VNScriptCommand
{
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public class VNScriptParser
{
    public List<VNScriptCommand> Parse(string script)
    {
        var commands = new List<VNScriptCommand>();
        if (string.IsNullOrEmpty(script))
            return commands;

        var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            var command = ParseLine(line);
            if (command != null)
                commands.Add(command);
        }

        return commands;
    }

    private VNScriptCommand? ParseLine(string line)
    {
        if (line.StartsWith("@"))
        {
            return ParseCommand(line);
        }
        else if (line.StartsWith("{"))
        {
            return new VNScriptCommand { Type = "label", Parameters = new() { { "name", line.Trim('{', '}').Trim() } } };
        }
        else if (line.StartsWith("["))
        {
            var content = line.Trim('[', ']');
            var parts = content.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
            return new VNScriptCommand
            {
                Type = "choice",
                Parameters = new()
                {
                    { "text", parts[0].Trim() },
                    { "target", parts.Length > 1 ? parts[1].Trim() : string.Empty }
                }
            };
        }

        var speakerText = line.Split(':', 2);
        if (speakerText.Length == 2)
        {
            return new VNScriptCommand
            {
                Type = "dialog",
                Parameters = new()
                {
                    { "speaker", speakerText[0].Trim() },
                    { "text", speakerText[1].Trim() }
                }
            };
        }

        return new VNScriptCommand { Type = "dialog", Parameters = new() { { "text", line } } };
    }

    private VNScriptCommand ParseCommand(string line)
    {
        var content = line.TrimStart('@');
        var parts = content.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        var command = new VNScriptCommand { Type = parts[0].ToLower() };

        if (parts.Length > 1)
        {
            command.Parameters["value"] = parts[1].Trim('"');
        }

        return command;
    }
}