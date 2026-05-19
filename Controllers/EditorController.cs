using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Visunovia.Models.Engine;
using Visunovia.Services;

namespace Visunovia.Controllers;

/// <summary>
/// 场景/对话 CRUD API，处理场景和对话的增删改查及撤销/重做操作
/// </summary>
[ApiController]
[Route("api/editor")]
public class EditorController : ControllerBase
{
    private readonly EditorSessionService _sessionService;

    public EditorController(EditorSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>
    /// 添加新场景到当前项目
    /// </summary>
    /// <param name="request">包含场景 ID 的请求体</param>
    [HttpPost("add-scene")]
    public IActionResult AddScene([FromBody] AddSceneRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return BadRequest(new { error = "没有打开的项目" });

            var scene = new VNScene
            {
                Id = request.SceneId,
                Bgm = new VNBgm(),
                Dialogues = new List<VNDialogue>()
            };
            editor.AddScene(scene);
            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"添加场景失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 从当前项目移除指定索引的场景
    /// </summary>
    /// <param name="request">包含场景索引的请求体</param>
    [HttpPost("remove-scene")]
    public IActionResult RemoveScene([FromBody] RemoveSceneRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return BadRequest(new { error = "没有打开的项目" });

            if (request.SceneIndex < 0 || request.SceneIndex >= editor.CurrentProject.Scenes.Count)
                return BadRequest(new { error = "无效的场景索引" });

            var sceneId = editor.CurrentProject.Scenes[request.SceneIndex].Id;
            editor.RemoveScene(sceneId);
            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"移除场景失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 重命名指定索引的场景
    /// </summary>
    /// <param name="request">包含场景索引和新名称的请求体</param>
    [HttpPost("rename-scene")]
    public IActionResult RenameScene([FromBody] RenameSceneRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return BadRequest(new { error = "没有打开的项目" });

            if (request.SceneIndex < 0 || request.SceneIndex >= editor.CurrentProject.Scenes.Count)
                return BadRequest(new { error = "无效的场景索引" });

            if (string.IsNullOrWhiteSpace(request.NewName))
                return BadRequest(new { error = "场景名称不能为空" });

            editor.CurrentProject.Scenes[request.SceneIndex].Id = request.NewName;
            editor.MarkAsModified();
            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"重命名场景失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 向指定场景添加对话，支持指定插入位置和对话类型
    /// </summary>
    /// <param name="request">包含场景索引、对话类型和插入位置的请求体</param>
    [HttpPost("add-dialogue")]
    public IActionResult AddDialogue([FromBody] AddDialogueRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return BadRequest(new { error = "没有打开的项目" });

            if (request.SceneIndex < 0 || request.SceneIndex >= editor.CurrentProject.Scenes.Count)
                return BadRequest(new { error = "无效的场景索引" });

            var scene = editor.CurrentProject.Scenes[request.SceneIndex];

            var dialogue = new VNDialogue();
            if (Enum.TryParse<VNDialogueType>(request.Type, true, out var dialogueType))
            {
                dialogue.Type = dialogueType;
            }

            if (dialogue.Type == VNDialogueType.Branch)
            {
                dialogue.Branch = new VNBranch();
            }
            else if (dialogue.Type == VNDialogueType.Event)
            {
                dialogue.Event = new VNEvent();
            }

            var insertIndex = request.InsertAfterIndex + 1;
            if (insertIndex < 0) insertIndex = 0;
            if (insertIndex > scene.Dialogues.Count) insertIndex = scene.Dialogues.Count;

            scene.Dialogues.Insert(insertIndex, dialogue);
            editor.MarkAsModified();
            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"添加对话失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 从指定场景移除对话
    /// </summary>
    /// <param name="request">包含场景索引和对话索引的请求体</param>
    [HttpPost("remove-dialogue")]
    public IActionResult RemoveDialogue([FromBody] RemoveDialogueRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return BadRequest(new { error = "没有打开的项目" });

            if (request.SceneIndex < 0 || request.SceneIndex >= editor.CurrentProject.Scenes.Count)
                return BadRequest(new { error = "无效的场景索引" });

            var scene = editor.CurrentProject.Scenes[request.SceneIndex];
            if (request.DialogueIndex < 0 || request.DialogueIndex >= scene.Dialogues.Count)
                return BadRequest(new { error = "无效的对话索引" });

            var sceneId = scene.Id;
            editor.RemoveDialogue(sceneId, request.DialogueIndex);
            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"移除对话失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 复制指定对话并在其后插入副本
    /// </summary>
    /// <param name="request">包含场景索引和对话索引的请求体</param>
    [HttpPost("copy-dialogue")]
    public IActionResult CopyDialogue([FromBody] CopyDialogueRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return BadRequest(new { error = "没有打开的项目" });

            if (request.SceneIndex < 0 || request.SceneIndex >= editor.CurrentProject.Scenes.Count)
                return BadRequest(new { error = "无效的场景索引" });

            var scene = editor.CurrentProject.Scenes[request.SceneIndex];
            if (request.DialogueIndex < 0 || request.DialogueIndex >= scene.Dialogues.Count)
                return BadRequest(new { error = "无效的对话索引" });

            var original = scene.Dialogues[request.DialogueIndex];
            var json = JsonSerializer.Serialize(original);
            var clone = JsonSerializer.Deserialize<VNDialogue>(json)!;

            scene.Dialogues.Insert(request.DialogueIndex + 1, clone);
            editor.MarkAsModified();
            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"复制对话失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 上下移动对话位置
    /// </summary>
    /// <param name="request">包含场景索引、对话索引和移动方向的请求体</param>
    [HttpPost("move-dialogue")]
    public IActionResult MoveDialogue([FromBody] MoveDialogueRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return BadRequest(new { error = "没有打开的项目" });

            if (request.SceneIndex < 0 || request.SceneIndex >= editor.CurrentProject.Scenes.Count)
                return BadRequest(new { error = "无效的场景索引" });

            var scene = editor.CurrentProject.Scenes[request.SceneIndex];
            if (request.DialogueIndex < 0 || request.DialogueIndex >= scene.Dialogues.Count)
                return BadRequest(new { error = "无效的对话索引" });

            var direction = request.Direction?.ToLowerInvariant();
            if (direction != "up" && direction != "down")
                return BadRequest(new { error = "方向参数必须为 up 或 down" });

            if (direction == "up" && request.DialogueIndex > 0)
            {
                var dialogue = scene.Dialogues[request.DialogueIndex];
                scene.Dialogues.RemoveAt(request.DialogueIndex);
                scene.Dialogues.Insert(request.DialogueIndex - 1, dialogue);
                editor.MarkAsModified();
            }
            else if (direction == "down" && request.DialogueIndex < scene.Dialogues.Count - 1)
            {
                var dialogue = scene.Dialogues[request.DialogueIndex];
                scene.Dialogues.RemoveAt(request.DialogueIndex);
                scene.Dialogues.Insert(request.DialogueIndex + 1, dialogue);
                editor.MarkAsModified();
            }

            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"移动对话失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 更新指定对话的属性
    /// </summary>
    /// <param name="request">包含场景索引、对话索引和对话对象的请求体</param>
    [HttpPost("update-dialogue")]
    public IActionResult UpdateDialogue([FromBody] UpdateDialogueRequest request)
    {
        try
        {
            Console.WriteLine($"[DEBUG] UpdateDialogue - Scene:{request.SceneIndex} Dialogue:{request.DialogueIndex} EventType:{request.Dialogue?.Event?.EventType} Params:{System.Text.Json.JsonSerializer.Serialize(request.Dialogue?.Event?.Parameters)}");
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return BadRequest(new { error = "没有打开的项目" });

            if (request.SceneIndex < 0 || request.SceneIndex >= editor.CurrentProject.Scenes.Count)
                return BadRequest(new { error = "无效的场景索引" });

            var scene = editor.CurrentProject.Scenes[request.SceneIndex];
            if (request.DialogueIndex < 0 || request.DialogueIndex >= scene.Dialogues.Count)
                return BadRequest(new { error = "无效的对话索引" });

            scene.Dialogues[request.DialogueIndex] = request.Dialogue;
            editor.MarkAsModified();
            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"更新对话失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 撤销上一步操作
    /// </summary>
    [HttpPost("undo")]
    public IActionResult Undo()
    {
        try
        {
            var editor = _sessionService.GetEditor();
            editor.Undo();
            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"撤销失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 重做上一步撤销的操作
    /// </summary>
    [HttpPost("redo")]
    public IActionResult Redo()
    {
        try
        {
            var editor = _sessionService.GetEditor();
            editor.Redo();
            return Ok(editor.CurrentProject);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"重做失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 查询当前是否可以撤销/重做
    /// </summary>
    [HttpGet("can-undo")]
    public IActionResult CanUndo()
    {
        try
        {
            var editor = _sessionService.GetEditor();
            return Ok(new { canUndo = editor.CanUndo, canRedo = editor.CanRedo });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"查询撤销状态失败: {ex.Message}" });
        }
    }
}

public record AddSceneRequest(string SceneId);
public record RemoveSceneRequest(int SceneIndex);
public record RenameSceneRequest(int SceneIndex, string NewName);
public record AddDialogueRequest(int SceneIndex, string Type, int InsertAfterIndex);
public record RemoveDialogueRequest(int SceneIndex, int DialogueIndex);
public record CopyDialogueRequest(int SceneIndex, int DialogueIndex);
public record MoveDialogueRequest(int SceneIndex, int DialogueIndex, string Direction);
public record UpdateDialogueRequest(int SceneIndex, int DialogueIndex, VNDialogue Dialogue);
