namespace Visunovia.Services;

/// <summary>
/// 编辑器会话服务，作为单例注册在 DI 容器中，管理 EditorService 实例的生命周期。
/// 当前为单用户模式，后续可扩展为多用户会话管理。
/// </summary>
public class EditorSessionService
{
    private EditorService? _editorService;

    /// <summary>
    /// 获取当前编辑器实例，如不存在则自动创建
    /// </summary>
    public EditorService GetEditor()
    {
        _editorService ??= new EditorService();
        return _editorService;
    }

    /// <summary>
    /// 重置编辑器实例，创建新的 EditorService（用于新建/切换项目等场景）
    /// </summary>
    public void ResetEditor()
    {
        _editorService = new EditorService();
    }
}
