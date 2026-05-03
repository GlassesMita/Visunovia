using Visunovia.Engine.Core;

namespace Visunovia.Engine.Events;

public class EventExecutor
{
    private readonly VNEngine _engine;
    private readonly Action<string>? _statusCallback;

    public EventExecutor(VNEngine engine, Action<string>? statusCallback = null)
    {
        _engine = engine;
        _statusCallback = statusCallback;
    }

    public async Task<bool> ExecuteEventAsync(VNEvent eventItem)
    {
        if (eventItem == null)
        {
            return false;
        }

        try
        {
            switch (eventItem.EventType)
            {
                case VNEventType.JumpScene:
                    return await ExecuteJumpSceneAsync(eventItem);

                case VNEventType.SetVariable:
                    return await ExecuteSetVariableAsync(eventItem);

                case VNEventType.PlaySound:
                    return await ExecutePlaySoundAsync(eventItem);

                case VNEventType.ChangeBackground:
                    return await ExecuteChangeBackgroundAsync(eventItem);

                case VNEventType.ChangeBgm:
                    return await ExecuteChangeBgmAsync(eventItem);

                case VNEventType.ShowCharacter:
                    return await ExecuteShowCharacterAsync(eventItem);

                case VNEventType.HideCharacter:
                    return await ExecuteHideCharacterAsync(eventItem);

                case VNEventType.Pause:
                    return await ExecutePauseAsync(eventItem);

                case VNEventType.WaitSeconds:
                    return await ExecuteWaitSecondsAsync(eventItem);

                case VNEventType.WindowEffect:
                    return await ExecuteWindowEffectAsync(eventItem);

                case VNEventType.Custom:
                    return await ExecuteCustomAsync(eventItem);

                default:
                    _statusCallback?.Invoke($"未知事件类型: {eventItem.EventType}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            _statusCallback?.Invoke($"执行事件失败: {ex.Message}");
            return false;
        }
    }

    private Task<bool> ExecuteJumpSceneAsync(VNEvent eventItem)
    {
        if (eventItem.Parameters.TryGetValue("TargetScene", out var targetSceneObj) && targetSceneObj is string targetScene)
        {
            _engine.State.CurrentScene = targetScene;
            _statusCallback?.Invoke($"跳转场景: {targetScene}");
            return Task.FromResult(true);
        }
        _statusCallback?.Invoke("JumpScene 事件缺少 TargetScene 参数");
        return Task.FromResult(false);
    }

    private Task<bool> ExecuteSetVariableAsync(VNEvent eventItem)
    {
        if (eventItem.Parameters.TryGetValue("VariableName", out var varNameObj) &&
            eventItem.Parameters.TryGetValue("Value", out var valueObj))
        {
            var varName = varNameObj?.ToString() ?? "";
            _engine.SetVariable(varName, valueObj ?? "");
            _statusCallback?.Invoke($"设置变量: {varName} = {valueObj}");
            return Task.FromResult(true);
        }
        _statusCallback?.Invoke("SetVariable 事件缺少参数");
        return Task.FromResult(false);
    }

    private Task<bool> ExecutePlaySoundAsync(VNEvent eventItem)
    {
        if (eventItem.Parameters.TryGetValue("SoundPath", out var soundPathObj) && soundPathObj is string soundPath)
        {
            var volume = 100;
            if (eventItem.Parameters.TryGetValue("Volume", out var volObj) && volObj is int vol)
            {
                volume = vol;
            }
            _statusCallback?.Invoke($"播放音效: {soundPath} (音量: {volume}%)");
            return Task.FromResult(true);
        }
        _statusCallback?.Invoke("PlaySound 事件缺少 SoundPath 参数");
        return Task.FromResult(false);
    }

    private Task<bool> ExecuteChangeBackgroundAsync(VNEvent eventItem)
    {
        if (eventItem.Parameters.TryGetValue("BackgroundPath", out var bgPathObj) && bgPathObj is string bgPath)
        {
            var effect = VNTransitionEffect.None;
            var duration = 500;

            if (eventItem.Transition != null)
            {
                effect = eventItem.Transition.Effect;
                duration = eventItem.Transition.Duration;
            }

            _engine.SetBackground(bgPath, effect, duration);
            _statusCallback?.Invoke($"更改背景: {bgPath} (效果: {effect})");
            return Task.FromResult(true);
        }
        _statusCallback?.Invoke("ChangeBackground 事件缺少 BackgroundPath 参数");
        return Task.FromResult(false);
    }

    private Task<bool> ExecuteChangeBgmAsync(VNEvent eventItem)
    {
        if (eventItem.Parameters.TryGetValue("BgmPath", out var bgmPathObj) && bgmPathObj is string bgmPath)
        {
            var volume = 100;
            var fadeIn = 0;

            if (eventItem.Parameters.TryGetValue("Volume", out var volObj) && volObj is int vol)
            {
                volume = vol;
            }
            if (eventItem.Parameters.TryGetValue("FadeIn", out var fadeObj) && fadeObj is int fade)
            {
                fadeIn = fade;
            }

            _engine.PlayBgm(bgmPath, volume, fadeIn);
            _statusCallback?.Invoke($"更改 BGM: {bgmPath} (音量: {volume}%, 淡入: {fadeIn}ms)");
            return Task.FromResult(true);
        }
        _statusCallback?.Invoke("ChangeBgm 事件缺少 BgmPath 参数");
        return Task.FromResult(false);
    }

    private Task<bool> ExecuteShowCharacterAsync(VNEvent eventItem)
    {
        if (eventItem.Parameters.TryGetValue("CharacterId", out var charIdObj) && charIdObj is string charId)
        {
            var expression = "default";
            if (eventItem.Parameters.TryGetValue("Expression", out var exprObj) && exprObj is string expr)
            {
                expression = expr;
            }

            var effect = VNTransitionEffect.FadeIn;
            var duration = 300;
            if (eventItem.Transition != null)
            {
                effect = eventItem.Transition.Effect;
                duration = eventItem.Transition.Duration;
            }

            _engine.SetCharacter(charId, expression, effect, duration);
            _statusCallback?.Invoke($"显示角色: {charId} ({expression})");
            return Task.FromResult(true);
        }
        _statusCallback?.Invoke("ShowCharacter 事件缺少 CharacterId 参数");
        return Task.FromResult(false);
    }

    private Task<bool> ExecuteHideCharacterAsync(VNEvent eventItem)
    {
        if (eventItem.Parameters.TryGetValue("CharacterId", out var charIdObj) && charIdObj is string charId)
        {
            if (_engine.State.Characters.TryGetValue(charId, out var character))
            {
                character.IsVisible = false;
                _statusCallback?.Invoke($"隐藏角色: {charId}");
                return Task.FromResult(true);
            }
            _statusCallback?.Invoke($"角色不存在: {charId}");
            return Task.FromResult(false);
        }
        _statusCallback?.Invoke("HideCharacter 事件缺少 CharacterId 参数");
        return Task.FromResult(false);
    }

    private Task<bool> ExecutePauseAsync(VNEvent eventItem)
    {
        var duration = 1000;
        if (eventItem.Parameters.TryGetValue("Duration", out var durObj) && durObj is int dur)
        {
            duration = dur;
        }
        _statusCallback?.Invoke($"暂停: {duration}ms");
        return Task.FromResult(true);
    }

    private async Task<bool> ExecuteWaitSecondsAsync(VNEvent eventItem)
    {
        var seconds = 1.0;
        if (eventItem.Parameters.TryGetValue("Seconds", out var secObj))
        {
            if (secObj is int secInt)
            {
                seconds = secInt;
            }
            else if (secObj is double secDouble)
            {
                seconds = secDouble;
            }
            else if (secObj is string secStr && double.TryParse(secStr, out var secParsed))
            {
                seconds = secParsed;
            }
        }

        _statusCallback?.Invoke($"等待: {seconds}秒");
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        return true;
    }

    private Task<bool> ExecuteCustomAsync(VNEvent eventItem)
    {
        if (eventItem.Parameters.TryGetValue("Script", out var scriptObj) && scriptObj is string script)
        {
            _statusCallback?.Invoke($"执行自定义脚本: {script}");
            return Task.FromResult(true);
        }
        _statusCallback?.Invoke("Custom 事件缺少 Script 参数");
        return Task.FromResult(false);
    }

    private Task<bool> ExecuteWindowEffectAsync(VNEvent eventItem)
    {
        if (eventItem.Parameters.TryGetValue("EffectType", out var effectTypeObj) && effectTypeObj is string effectTypeStr)
        {
            if (Enum.TryParse<VNWindowEffectType>(effectTypeStr, out var effectType))
            {
                WindowController.ExecuteWindowEffect(effectType, eventItem.Parameters);
                _statusCallback?.Invoke($"执行窗口效果: {effectType}");
                return Task.FromResult(true);
            }
        }
        _statusCallback?.Invoke("WindowEffect 事件缺少 EffectType 参数");
        return Task.FromResult(false);
    }
}
