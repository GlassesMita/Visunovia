namespace Visunovia.Models.Engine;

public class VNState
{
    public string CurrentScene { get; set; } = string.Empty;
    public string CurrentSpeaker { get; set; } = string.Empty;
    public string CurrentText { get; set; } = string.Empty;
    public string BackgroundImage { get; set; } = string.Empty;
    public VNTransition? BackgroundTransition { get; set; }
    public string? CurrentBgmPath { get; set; }
    public int CurrentBgmVolume { get; set; } = 100;
    public int BgmFadeIn { get; set; }
    public int BgmFadeOut { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsTextComplete { get; set; }
    public Dictionary<string, VNCharacter> Characters { get; set; } = new();
    public List<VNChoice> CurrentChoices { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public Stack<string> History { get; set; } = new();
    public VNTextEffect? CurrentTextEffect { get; set; }
}

public class VNCharacter
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public VNTransition? Transition { get; set; }
}

public class VNChoice
{
    public string Text { get; set; } = string.Empty;
    public string TargetScene { get; set; } = string.Empty;
}

public enum VNTextEffectType
{
    None,
    Typewriter,
    FadeIn,
    FadeOut,
    FadeInOut
}

public class VNTextEffect
{
    public VNTextEffectType Type { get; set; } = VNTextEffectType.None;
    public int Speed { get; set; } = 50;
    public bool Shake { get; set; }
    public int FadeDuration { get; set; } = 500;
    public int DelayBeforeStart { get; set; } = 0;
}

public class VNAnimation
{
    public string Type { get; set; } = "none";
    public int Duration { get; set; } = 300;
}

public class VNBgm
{
    public string Path { get; set; } = string.Empty;
    public int Volume { get; set; } = 80;
    public bool Loop { get; set; } = true;
}

public class VNSoundEffect
{
    public string Path { get; set; } = string.Empty;
    public int Volume { get; set; } = 100;
}

public class VNSprite
{
    public string Path { get; set; } = string.Empty;
    public string Character { get; set; } = string.Empty;
    public string Position { get; set; } = "center";
    public int Layer { get; set; }
    public VNAnimation Animation { get; set; } = new();
}

public class VNVoiceLine
{
    public string Speaker { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class VNCharacterControl
{
    public string Slot { get; set; } = "1";
    public string Character { get; set; } = string.Empty;
    public string Action { get; set; } = "show";
    public string Sprite { get; set; } = string.Empty;
    public string Sfx { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string FromPosition { get; set; } = string.Empty;
    public string ToPosition { get; set; } = "none";
    public string Position { get; set; } = "center";
    public string Animation { get; set; } = "fade";
    public string Easing { get; set; } = "easeOutCubic";
    public double Duration { get; set; } = 0.3;
}

public class VNScene
{
    public string Id { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public VNBgm? Bgm { get; set; }
    public List<VNDialogue> Dialogues { get; set; } = new();
    public List<VNBlueprintNode> Nodes { get; set; } = new();
    public List<VNBlueprintConnection> Connections { get; set; } = new();
}

public class VNBlueprintNode
{
    public string Uuid { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string SubType { get; set; } = string.Empty;
    public VNBlueprintPosition Position { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<string> NextNodeUuids { get; set; } = new();
}

public class VNBlueprintConnection
{
    public string Uuid { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string SourceNodeUuid { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string SourcePort { get; set; } = string.Empty;
    public string TargetNodeUuid { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string TargetPort { get; set; } = string.Empty;
}

public class VNBlueprintPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

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
    WindowEffect,
    Custom,
    SendSystemNotification = 11
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

public class VNTransition
{
    public VNTransitionEffect Effect { get; set; } = VNTransitionEffect.None;
    public int Duration { get; set; } = 500;
}

public class VNChoiceOption
{
    public string Text { get; set; } = string.Empty;
    public string TargetScene { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
}

public class VNBranch
{
    public List<VNChoiceOption> Choices { get; set; } = new();
}

public class VNEvent
{
    public VNEventType EventType { get; set; } = VNEventType.Custom;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public VNTransition? Transition { get; set; }
}

public class VNDialogue
{
    public string Uuid { get; set; } = string.Empty;
    public VNDialogueType Type { get; set; } = VNDialogueType.Dialogue;
    public string Speaker { get; set; } = string.Empty;
    public List<string> Speakers { get; set; } = new();
    public string SpeakerSlot { get; set; } = "1";
    public string Text { get; set; } = string.Empty;
    public List<VNSprite> Sprites { get; set; } = new();
    public string Voice { get; set; } = string.Empty;
    public List<VNVoiceLine> Voices { get; set; } = new();
    public List<VNCharacterControl> CharacterControls { get; set; } = new();
    public VNTextEffect? TextEffect { get; set; }
    public VNAnimation? Animation { get; set; }
    public VNBranch? Branch { get; set; }
    public VNEvent? Event { get; set; }
    public VNTransition? Transition { get; set; }
    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
}
