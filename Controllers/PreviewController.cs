using Microsoft.AspNetCore.Mvc;
using Visunovia.Services;

namespace Visunovia.Controllers;

/// <summary>
/// 预览数据 API，提供项目预览所需的数据，支持从指定场景和对话位置开始
/// </summary>
[ApiController]
[Route("api/preview")]
public class PreviewController : ControllerBase
{
    private readonly EditorSessionService _sessionService;

    public PreviewController(EditorSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>
    /// 获取当前项目的预览数据，从场景 0、对话 0 开始
    /// </summary>
    [HttpGet]
    public IActionResult GetPreview()
    {
        return GetPreviewFromPosition(0, 0);
    }

    /// <summary>
    /// 获取当前项目的预览数据，从指定场景和对话位置开始
    /// </summary>
    /// <param name="sceneIndex">起始场景索引</param>
    /// <param name="dialogueIndex">起始对话索引</param>
    [HttpGet("{sceneIndex}/{dialogueIndex}")]
    public IActionResult GetPreviewFromPosition(int sceneIndex, int dialogueIndex)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            if (editor.CurrentProject == null)
                return NotFound(new { error = "没有打开的项目" });

            if (sceneIndex < 0 || sceneIndex >= editor.CurrentProject.Scenes.Count)
                return BadRequest(new { error = "无效的场景索引" });

            var scene = editor.CurrentProject.Scenes[sceneIndex];
            if (dialogueIndex < 0 || dialogueIndex >= scene.Dialogues.Count)
                return BadRequest(new { error = "无效的对话索引" });

            return Ok(new
            {
                scenes = editor.CurrentProject.Scenes,
                projectPath = editor.CurrentProjectPath,
                startSceneIndex = sceneIndex,
                startDialogueIndex = dialogueIndex
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"获取预览数据失败: {ex.Message}" });
        }
    }
}
