using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 角色面板：全局面板（HUD 打开、返回上一面板）——薄编排器。
// 只负责：标题信息栏、Tab 切换（[属性] / [装备/背包]）、返回上一面板；内容由 子面板组件 自刷新。
//   属性子面板 / 装备背包子面板 各自挂在对应 Tab 页物体上，自订阅事件刷新，父面板不参与内容。
// 未接线子面板（旧场景）时回退旧列表样式（内容区 + 创建行），保证场景改造前可玩。
public sealed class 角色面板 : 面板基类
{

    [SerializeField] private TMP_Text 标题;
    [SerializeField] private Button 属性Tab, 装备背包Tab;         // Tab 子按钮
    [SerializeField] private 属性子面板 属性页;                   // [属性] Tab 页（组件，挂在页物体上）
    [SerializeField] private 装备背包子面板 装备背包页;           // [装备/背包] Tab 页

    private bool 显示属性页 = true;

    // 角色面板 = 侧边式：从屏右滑入（向左到位），退出向右滑出
    protected override 面板过渡样式 过渡样式 => 面板过渡样式.右侧滑入右滑出;

    void Awake()
    {
        var 事件 = ServiceRegistry.Get<EventBus>();
        事件.订阅<属性变化事件>(_ => 刷新(null));   // 回退模式：加点后重绘
        事件.订阅<背包变化事件>(_ => 刷新(null));   // 回退模式：背包变化重绘
        属性Tab?.onClick.AddListener(() => 切换页(true));
        装备背包Tab?.onClick.AddListener(() => 切换页(false));
    }

    protected override void 刷新(object 上下文)
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        if (标题 != null) 标题.text = $"角色  Lv.{玩家.等级}    自由属性点 {玩家.自由属性点}";
        切换页(显示属性页);
    }

    // 全局取消 = 返回上一面板（无上一面板则不可取消）
    public override bool 回退()
    {
        if (面板管理器.实例?.上一个面板 == null) return false;
        面板管理器.实例.返回上一面板();
        return true;
    }

    public override string 取消文本 => "关闭";

    private void 切换页(bool 属性)
    {
        显示属性页 = 属性;
        if (属性页 != null) 属性页.gameObject.SetActive(属性);
        if (装备背包页 != null) 装备背包页.gameObject.SetActive(!属性);
        设选中缩放(属性Tab, 属性);        // 选中态 = 固定放大（替代颜色高亮）
        设选中缩放(装备背包Tab, !属性);
    }

    // ===== 旧样式整体回退（子面板未接线时） =====

    
}
