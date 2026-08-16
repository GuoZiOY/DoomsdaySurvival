// 恢复面板：恢复功能（教堂用；当前只有单动作）
public sealed class 恢复面板 : 设施功能面板基类
{
    protected override string 标题文字() => $"{逻辑.名称} · 恢复";

    protected override void 渲染列表()
    {
        if (逻辑 is not 恢复功能 恢复) return;
        创建行(列表区, 恢复.恢复描述(), () => 恢复.尝试恢复());
    }
}
