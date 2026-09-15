using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 对话面板：剧情/对话/结局渲染（从主视窗剥离，职责分离）。NPC 交谈触发剧情也在此显示。
// 打字机效果：正文逐字显示（TMP maxVisibleCharacters，兼容富文本）；打字期间无选项，点击面板任意处立即完成；
// 完成后才生成选项。
//
// ⚠ 出口（侧边栏「取消」已停用，本项目**没有全局回退键** —— 见 面板基类.回退 的注释）：
//   · 结局模式：自带「走出安全屋（进废城）」按钮 → 不依赖任何全局入口，**不会卡死**。
//   · 打字中：点击面板任意处 = 立即完成 → 也不依赖全局入口。
//   · ★ **潜在风险（目前不可达，但接线时要记得）**：若某天把 `打开对话事件`（NPC 交谈）接上，
//     而那个剧情节点**既没有选项、也没有 `自动目标`**，`生成选项()` 就什么都不生成 →
//     面板上只剩正文、没有任何按钮 —— 而它原本唯一的出口正是侧边栏「取消」。
//     要接这条线就必须**同时**给它一个出口按钮，或保证每个节点都有选项/自动目标。
public sealed class 对话面板 : 面板基类, IPointerClickHandler
{
    [SerializeField] private TMP_Text 正文;
    [SerializeField] private RectTransform 选项区;
    [SerializeField] private float 打字间隔 = 0.03f;  // 每字间隔（秒）
    [SerializeField] private float 自动间隔 = 0.8f;   // 剧情链自动进入下一节点的停顿（秒）

    private bool 来自NPC交谈;        // true = 从 NPC 交谈进来的（取消 = 回上一个面板）
    private bool 显示结局;           // 结局模式：取消 = 回主菜单
    private 显示剧情事件? 打字节点;   // 正在打字的剧情节点（完成后生成选项；struct 用可空标记"无"）
    private Coroutine 打字协程;

    void Awake()
    {
        // 剧情显示由 面板管理器 订阅 显示剧情事件 路由到这里；返回统一走各面板自己的出口按钮
    }

    protected override void 刷新(object 上下文)
    {
        if (上下文 is 打开对话事件 对话)
        {
            // NPC 交谈：进入剧情节点（触发 显示剧情事件 再次渲染）；取消时回上一个面板
            来自NPC交谈 = true;
            ServiceRegistry.Get<DialogueService>().进入节点(对话.剧情节点);
            return;
        }
        if (上下文 is 显示剧情事件 e) { 渲染剧情(e); return; }
        if (上下文 is 打开结局事件) 渲染结局();
    }

    // 渲染剧情：打字机逐字显示，打完后才生成选项
    private void 渲染剧情(显示剧情事件 e)
    {
        显示结局 = false;
        打字节点 = e;
        停打字();
        清空(选项区);
        if (正文 != null)
        {
            正文.text = e.文本 ?? "";
            正文.ForceMeshUpdate();
            正文.maxVisibleCharacters = 0;
            打字协程 = StartCoroutine(打字机(正文.textInfo.characterCount));
        }
        else 生成选项();
    }

    private IEnumerator 打字机(int 总字符)
    {
        while (正文.maxVisibleCharacters < 总字符)
        {
            正文.maxVisibleCharacters++;
            yield return new WaitForSeconds(打字间隔);
        }
        停打字();
        生成选项();
    }

    // 点击面板任意处：立即完成打字并生成选项（仅打字期间有效）
    public void OnPointerClick(PointerEventData 事件)
    {
        if (打字协程 == null || 正文 == null) return;
        停打字();
        正文.maxVisibleCharacters = 正文.textInfo.characterCount;
        生成选项();
    }

    private void 停打字()
    {
        if (打字协程 != null) { StopCoroutine(打字协程); 打字协程 = null; }
    }

    // 打字完成后生成选项（选项只服务剧情分支）；无选项且有自动目标 → 剧情链自动进入下一节点
    private void 生成选项()
    {
        if (打字节点 == null) return;
        var 节点 = 打字节点.Value;
        打字节点 = null;
        if (节点.选项 == null) return;
        foreach (var 选项 in 节点.选项)
        {
            var 目标 = 选项.目标;
            创建行(选项区, 选项.文本, () => ServiceRegistry.Get<DialogueService>().处理选项(目标), true, true);   // 选项：去LayoutElement + 文字居中
        }
        if (节点.选项.Length == 0 && !string.IsNullOrEmpty(节点.自动目标))
        {
            var 目标 = 节点.自动目标;
            StartCoroutine(自动进入下一节点(目标));
        }
    }

    // 剧情链连续播放：文本停顿后自动进入下一节点
    private IEnumerator 自动进入下一节点(string 目标)
    {
        yield return new WaitForSeconds(自动间隔);
        ServiceRegistry.Get<DialogueService>().处理选项(目标);
    }

    // 渲染结局：序章收尾（属剧情内容，故归对话面板）
    private void 渲染结局()
    {
        显示结局 = true;
        停打字();
        // v51：这行原来是奇幻文案（"灰烬镇的余烬还在燃烧。龙在山的深处沉睡。"），改末日正文。
        // 文案口径：《最后87天》——从活过第一天开始倒数。
        设文本(正文, "第一天的太阳落下去的时候，你还在呼吸。\n\n城里的灯不会再亮了。收音机里只剩电流声，"
                   + "偶尔夹着半句听不清的呼叫，念着一串没人在听的地名。\n\n从今天算起，还有八十七天。");
        清空(选项区);
        创建行(选项区, "走出安全屋（进废城）", () => ServiceRegistry.Get<大世界探索服务>()?.打开默认世界(), true, true);
    }

    // 全局取消：结局→回主菜单；NPC 交谈→回上一个面板；主剧情不响应（线性强制，不可取消）
    public override bool 回退()
    {
        if (显示结局) { 面板管理器.实例?.回主菜单(); return true; }
        if (打字协程 != null) { 停打字(); if (正文 != null) 正文.maxVisibleCharacters = 正文.textInfo.characterCount; 生成选项(); return true; }   // 打字中 = 取消 = 立即完成
        if (!来自NPC交谈) return false;
        来自NPC交谈 = false;
        面板管理器.实例?.返回上一面板();
        return true;
    }

    public override string 取消文本
    {
        get
        {
            if (显示结局) return "结束";
            if (打字协程 != null) return "跳过";
            if (来自NPC交谈) return "返回";
            return "取消";
        }
    }
}
