using Microsoft.AspNetCore.Mvc;
using Visunovia.Services;
using System.Text.Json;

namespace Visunovia.Controllers;

[ApiController]
[Route("api/scenegraphs")]
public class SceneGraphController : ControllerBase
{
    private readonly EditorSessionService _sessionService;

    public SceneGraphController(EditorSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet("{sceneId}")]
    public IActionResult GetSceneGraph(string sceneId)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var sceneGraph = editor.GetSceneGraph(sceneId);
            if (sceneGraph == null)
                return NotFound(new { error = $"场景图 {sceneId} 不存在" });

            return Ok(new { success = true, data = sceneGraph });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPut("{sceneId}")]
    public async Task<IActionResult> SaveSceneGraph(string sceneId, [FromBody] SceneGraphData sceneGraphData)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var json = JsonSerializer.Serialize(sceneGraphData);
            await editor.SaveSceneGraphAsync(sceneId, json);
            return Ok(new { success = true, message = "场景图已保存" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("{sceneId}/nodes")]
    public IActionResult CreateNode(string sceneId, [FromBody] NodeCreateRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var node = editor.CreateNode(sceneId, request);
            return Ok(new { success = true, data = node });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPut("{sceneId}/nodes/{nodeId}")]
    public IActionResult UpdateNode(string sceneId, string nodeId, [FromBody] NodeUpdateRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var node = editor.UpdateNode(sceneId, nodeId, request);
            return Ok(new { success = true, data = node });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("{sceneId}/nodes/{nodeId}")]
    public IActionResult DeleteNode(string sceneId, string nodeId)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            editor.DeleteNode(sceneId, nodeId);
            return Ok(new { success = true, message = "节点已删除" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("{sceneId}/edges")]
    public IActionResult CreateEdge(string sceneId, [FromBody] EdgeCreateRequest request)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var edge = editor.CreateEdge(sceneId, request);
            return Ok(new { success = true, data = edge });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("{sceneId}/edges/{edgeId}")]
    public IActionResult DeleteEdge(string sceneId, string edgeId)
    {
        try
        {
            var editor = _sessionService.GetEditor();
            editor.DeleteEdge(sceneId, edgeId);
            return Ok(new { success = true, message = "连接已删除" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("list")]
    public IActionResult GetSceneGraphList()
    {
        try
        {
            var editor = _sessionService.GetEditor();
            var sceneGraphs = editor.GetSceneGraphList();
            return Ok(new { success = true, data = sceneGraphs });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}
