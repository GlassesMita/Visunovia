using System.Collections.Generic;

namespace Visunovia.Player.WPF.Player;

public enum VNDialogueType
{
    Dialogue,
    Branch,
    Event
}

public enum VNEventType
{
    JumpScene,
    SetVariable,
    PlaySound,
    ChangeBackground,
    ChangeBgm,
    ShowCharacter,
    HideCharacter,
    Pause,
    WaitSeconds,
    Custom,
    InvokePlugin,
    InvokeCode
}

public enum VNTransitionEffect
{
    None,
    Instant,
    FadeIn,
    FadeOut,
    CrossFade,
    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown
}

public class VNBgm
{
    public string Path { get; set; } = "";
    public int Volume { get; set; } = 80;
    public bool Loop { get; set; } = true;
}

public class VNSprite
{
    public string Path { get; set; } = "";
    public string Position { get; set; } = "center";
    public int Layer { get; set; }
}

public class VNTextEffect
{
    public string Type { get; set; } = "none";
    public int Speed { get; set; } = 50;
    public bool Shake { get; set; }
    public int FadeDuration { get; set; } = 500;
    public int DelayBeforeStart { get; set; } = 0;
}

public class VNTransition
{
    public string Effect { get; set; } = "none";
    public int Duration { get; set; } = 500;
}

public class VNChoiceOption
{
    public string Text { get; set; } = "";
    public string TargetScene { get; set; } = "";
    public string Condition { get; set; } = "";
}

public class VNBranch
{
    public List<VNChoiceOption> Choices { get; set; } = new();
}

public class VNEvent
{
    public string EventType { get; set; } = "Custom";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public VNTransition? Transition { get; set; }

    public VNEventType GetEventType()
    {
        return EventType switch
        {
            "JumpScene" => VNEventType.JumpScene,
            "SetVariable" => VNEventType.SetVariable,
            "PlaySound" => VNEventType.PlaySound,
            "ChangeBackground" => VNEventType.ChangeBackground,
            "ChangeBgm" => VNEventType.ChangeBgm,
            "ShowCharacter" => VNEventType.ShowCharacter,
            "HideCharacter" => VNEventType.HideCharacter,
            "Pause" => VNEventType.Pause,
            "WaitSeconds" => VNEventType.WaitSeconds,
            "InvokePlugin" => VNEventType.InvokePlugin,
            "InvokeCode" => VNEventType.InvokeCode,
            _ => VNEventType.Custom
        };
    }
}

public class VNDialogue
{
    public string Type { get; set; } = "Dialogue";
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    public List<VNSprite> Sprites { get; set; } = new();
    public string Voice { get; set; } = "";
    public VNTextEffect? TextEffect { get; set; }
    public VNBranch? Branch { get; set; }
    public VNEvent? Event { get; set; }
    public VNTransition? Transition { get; set; }

    public VNDialogueType GetDialogueType()
    {
        return Type switch
        {
            "Branch" => VNDialogueType.Branch,
            "Event" => VNDialogueType.Event,
            _ => VNDialogueType.Dialogue
        };
    }
}

public class VNScene
{
    public string Id { get; set; } = "";
    public string Background { get; set; } = "";
    public VNBgm? Bgm { get; set; }
    public List<VNDialogue> Dialogues { get; set; } = new();
}
