using DG.Tweening;
using TMPro;
using UnityEngine;

// 战斗面板 · 演出分部：集中 飘字 / 演出片段（受击/治疗/阵亡/敌方行动/结束）。
// 与 战斗面板.cs 同为 partial，直接访问主文件字段/方法（卡牌表、演出参数、刷新信息条等）。
// 关注点分离：交互状态机与布局在主文件，战斗演出统一收拢于此，便于集中调参与维护。
public sealed partial class 战斗面板
{
    // ===== 演出片段（经 入队演出 压入 演出队列核心，串行播放） =====

    // 敌方/助战回合演出片段：先行动画（强调+跳），停顿让玩家看清，再触发 战斗.继续() 执行行动
    private System.Collections.IEnumerator 演出_敌方行动(战斗单位 敌人)
    {
        if (卡牌表.TryGetValue(敌人, out var 卡牌)) { 卡牌.强调行动(); 卡牌.行动跳跃(); }
        yield return new WaitForSeconds(敌方行动停顿);
        // 面板激活才走演出；继续 内部会同步处理敌人行动（其伤害事件将作为新片段入队），并推进到下一单位
        if (gameObject.activeInHierarchy) 战斗.继续();
    }

    // 阵亡：先播倒下退场，播完再隐藏；从可选表移除避免点中已死单位
    private System.Collections.IEnumerator 演出_阵亡(战斗单位 单位)
    {
        if (!卡牌表.TryGetValue(单位, out var 卡牌)) yield break;
        卡牌表.Remove(单位);
        卡牌.阵亡();          // 倒下+右移+淡出
        刷新信息条();
        yield return new WaitForSeconds(战斗单位卡牌.阵亡动画时长);   // 等阵亡动画放完再隐藏
        if (卡牌 != null) 卡牌.gameObject.SetActive(false);
    }

    // 受击：卡牌震颤 + 伤害飘字，随后停顿让玩家看清。暴击/真实伤害 更狠、暴击字放大金红，闪避半个灰字
    private System.Collections.IEnumerator 演出_受击(伤害事件 e)
    {
        刷新卡牌(e.目标);
        if (卡牌表.TryGetValue(e.目标, out var 卡牌))
        {
            bool 严重 = e.暴击 || e.类型 == 伤害类型.真实;
            卡牌.受击(严重);
            yield return new WaitForSeconds(受击停顿);
        }
        if (e.闪避) { 飘字(e.目标, "闪避", 游戏主题.暗淡, 0.7f); yield break; }
        string 文字 = e.数值 > 0 ? "-" + e.数值 : "0";
        Color 色 = e.暴击 ? 游戏主题.危险 : (e.类型 == 伤害类型.魔法 ? 游戏主题.魔法 : Color.white);
        飘字(e.目标, 文字, 色, e.暴击 ? 1.5f : 1f);
    }

    // 治疗：卡牌脉冲 + 绿字上浮，随后停顿
    private System.Collections.IEnumerator 演出_治疗(治疗事件 e)
    {
        刷新卡牌(e.目标);
        if (卡牌表.TryGetValue(e.目标, out var 卡牌))
        {
            卡牌.治疗脉冲();
            yield return new WaitForSeconds(受击停顿);
        }
        飘字(e.目标, "+" + e.数值, 游戏主题.治疗, 1f);
    }

    // 结束：等最后的阵亡/倒下动画播完（队列已保证排在阵亡之后），再弹结算面板
    private System.Collections.IEnumerator 演出_结束(战斗结束事件 e)
    {
        主行动区.gameObject.SetActive(false);
        子菜单区?.gameObject.SetActive(false);
        yield return new WaitForSeconds(结算延迟);
        结算覆盖层?.SetActive(true);
        结算文字.text = e.结算文本;
    }

    // ===== 飘字 =====

    // 施放技能：在玩家卡牌上方飘一行技能名（暖金大字），先于伤害/受击字幕出现
    private void 飘技能名(string 技能名)
    {
        飘字(战斗.玩家, $"「{技能名}」", 技能名色, 1.2f);
    }

    // 飘字：在单位卡牌上方生成一个上浮放大淡出的数字/文字
    private void 飘字(战斗单位 单位, string 文字, Color 颜色, float 缩放倍率)
    {
        if (!卡牌表.TryGetValue(单位, out var 卡牌) || 卡牌 == null) return;
        if (飘字模板 == null) return;   // 无模板则跳过（TMP_Text 是抽象类不能运行时创建，需在场景提供 飘字模板）
        var 父 = 飘字层 != null ? 飘字层 : (RectTransform)卡牌.transform.parent;
        var 物体 = Instantiate(飘字模板, 父, false);
        var 文本 = 物体.GetComponent<TMP_Text>();
        if (文本 == null) { Destroy(物体); return; }
        物体.SetActive(true);
        文本.text = 文字;
        文本.color = 颜色;
        文本.fontSize = 文本.fontSize * 缩放倍率;
        var 矩形 = (RectTransform)文本.transform;
        矩形.DOKill();
        // 定位到卡牌右上方：置于卡牌右缘之上（与飘字层父子关系无关，兼容 Canvas 缩放）
        var 卡牌矩形 = (RectTransform)卡牌.transform;
        float 右偏 = 卡牌矩形.rect.width * 0.5f * Mathf.Abs(卡牌矩形.lossyScale.x) * 0.6f;   // 卡牌右缘偏内
        float 上移 = 卡牌矩形.rect.height * 0.5f * Mathf.Abs(卡牌矩形.lossyScale.y) + 26f;    // 比卡牌顶更高
        Vector3 基 = 卡牌矩形.position + new Vector3(右偏, 上移, 0f);
        矩形.position = 基;
        矩形.localScale = Vector3.one;
        // 上浮 + 缩放 + 淡出（用 world DOMove 位移，不受父布局影响）。飘字时长 可调，让玩家看清数值
        文本.alpha = 1f;
        文本.DOFade(0f, 飘字时长).SetEase(Ease.OutCubic).SetUpdate(true)
            .OnComplete(() => { if (文本 != null) Destroy(文本.gameObject); });   // 淡出结束才销毁，让"飘字停留时长"真正生效
        矩形.DOMove(基 + new Vector3(0f, 飘字上浮, 0f), 飘字时长).SetEase(Ease.OutCubic).SetUpdate(true);
        矩形.DOScale(Vector3.one * 缩放倍率, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
    }
}