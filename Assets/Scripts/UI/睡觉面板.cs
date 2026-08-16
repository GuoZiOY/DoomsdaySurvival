// 睡觉面板：睡觉功能（家用；跳过晚上）
public sealed class 睡觉面板 : 设施功能面板基类
{
    protected override string 标题文字() => $"{逻辑.名称} · 休息";

    protected override void 渲染列表()
    {
        if (逻辑 is not 睡觉功能 睡觉) return;
        创建行(内容区, 睡觉.睡觉描述(), () => 睡觉.尝试睡觉());
    }
}
