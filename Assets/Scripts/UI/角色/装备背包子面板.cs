using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 装备槽引用：一个装备槽的静态引用（装备文本 + 点击按钮）
[Serializable]
public class 装备槽引用
{
    public TMP_Text 文本;
    public Button 按钮;
}

// 装备/背包子面板：角色面板 [装备/背包] Tab 页的组件（挂在「装备/背包页」物体上）。
// 布局：顶部 8 装备槽（槽位[8] 数组引用，可点击→详情/卸下；双击=快速卸下）
//   + 分类 Tab（分类按钮[5]）+ 背包行列表（品质/名字/数量/选中图，无界克隆）+ 选中详情区（操作按钮）。
// 数据模型：装备 = 转移（换装按 物品.槽位 入槽并回退同槽旧件、卸下回包）；背包列表只显示可用物品。
public sealed class 装备背包子面板 : MonoBehaviour
{
    // —— 装备槽（顶部：槽位[8] 数组引用，索引 = 槽位名顺序；槽名是场景静态标签）——
    [SerializeField] private 装备槽引用[] 槽位;

    // —— 分类 Tab（索引 = 分类表：全部/装备/消耗/材料/任务）——
    [SerializeField] private Button[] 分类按钮;

    // —— 背包列表（垂直列表：品质/名字/数量/选中图，点击行选中）——
    [SerializeField] private RectTransform 列表容器;      // VerticalLayoutGroup 容器（场景搭建）
    [SerializeField] private GameObject 背包行模板;       // 根节点 Button + 背包行（品质/名字/数量/选中图）

    // —— 选中详情区 ——
    [SerializeField] private TMP_Text 详情名称, 详情描述, 详情数值, 详情价格;   // 名称含品质标签
    [SerializeField] private Button 详情操作按钮;
    [SerializeField] private TMP_Text 详情操作文字;

    // 8 槽固定顺序（物品.槽位 声明的 "饰品" 由 饰品目标槽 分配到 饰品1/饰品2）
    private static readonly string[] 全部槽位 = { "主手", "副手", "头盔", "盔甲", "靴子", "手套", "饰品1", "饰品2" };

    private enum 分类 { 全部, 装备, 消耗, 材料, 任务 }
    private static readonly 分类[] 分类表 = { 分类.全部, 分类.装备, 分类.消耗, 分类.材料, 分类.任务 };
    private 分类 当前分类 = 分类.全部;
    private string 选中标识;   // 当前选中物品标识（null=未选中）
    private readonly 双击检测 双击 = new 双击检测();

    void Awake()
    {
        ServiceRegistry.Get<EventBus>()?.订阅<背包变化事件>(_ => 刷新());
        for (int i = 0; i < 分类表.Length && i < (分类按钮?.Length ?? 0); i++)
        {
            var 分类项 = 分类表[i];
            var 按钮 = 分类按钮[i];
            if (按钮 != null) 按钮.onClick.AddListener(() => 设分类(分类项));
        }
        // 装备槽点击 → 选中已装备物品进详情；双击由本级对槽位记录
        for (int i = 0; i < 全部槽位.Length && i < (槽位?.Length ?? 0); i++)
        {
            var 槽 = 全部槽位[i];
            var 组 = 槽位[i];
            if (组?.按钮 != null) 组.按钮.onClick.AddListener(() => 点击装备槽(槽));
        }
        详情操作按钮?.onClick.AddListener(执行操作);
        刷新();
    }

    // 页面激活时兜底刷新一次
    private void OnEnable() => 刷新();

    // ===== 刷新 =====

    public void 刷新()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        if (玩家 == null || 列表容器 == null) return;
        刷新装备槽(玩家);
        刷新列表(玩家);
        刷新详情(玩家);
        刷新分类高亮();
    }

    // 原地刷新 8 个装备槽文本（无克隆）
    private void 刷新装备槽(玩家档案 玩家)
    {
        for (int i = 0; i < 全部槽位.Length && i < (槽位?.Length ?? 0); i++)
        {
            var 组 = 槽位[i];
            if (组 == null) continue;
            面板基类.设文本(组.文本, 槽文本(玩家, 全部槽位[i]));
        }
    }

    // 装备槽文本：只显示物品名；品质非普通时用品质色富文本（效果数值不显示）
    private static string 槽文本(玩家档案 玩家, string 槽)
    {
        string 标识 = 玩家.装备标识(槽);
        if (string.IsNullOrEmpty(标识)) return "";
        var 数据 = ServiceRegistry.Get<DataService>();
        if (!数据.物品.TryGetValue(标识, out var 物品)) return 标识;
        if (物品.品质档 == 品质.普通) return 物品.标识;
        string 色 = ColorUtility.ToHtmlStringRGB(品质工具.颜色(物品.品质档));
        return $"<color=#{色}>{物品.标识}</color>";
    }

    // ===== 装备槽点击（双击 = 快速卸下 / 单击 = 选中进详情） =====

    private void 点击装备槽(string 槽)
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>().档案;
        string 标识 = 玩家.装备标识(槽);
        if (string.IsNullOrEmpty(标识)) return;
        if (双击.点击("槽:" + 槽)) { 选中标识 = 标识; 执行操作(); return; }
        选中标识 = 标识;
        刷新();
    }

    // ===== 背包列表（固定排序：类型 → 品质降序 → 名称） =====

    private void 刷新列表(玩家档案 玩家)
    {
        面板基类.清空(列表容器);
        var 数据 = ServiceRegistry.Get<DataService>();
        var 列表 = new List<物品堆叠>();
        foreach (var 堆叠 in 玩家.所有持有物品())
            if (堆叠.数量 > 0 && 数据.物品.TryGetValue(堆叠.标识, out var 物品) && 匹配分类(物品.类型, 当前分类))
                列表.Add(堆叠);
        列表.Sort((a, b) =>
        {
            var 物品A = 数据.物品[a.标识];
            var 物品B = 数据.物品[b.标识];
            int 序 = 类型序(物品A.类型).CompareTo(类型序(物品B.类型));
            if (序 != 0) return 序;
            序 = 物品B.品质档.CompareTo(物品A.品质档);   // 品质降序（传奇在前）
            if (序 != 0) return 序;
            return string.CompareOrdinal(物品A.标识, 物品B.标识);
        });
        foreach (var 堆叠 in 列表)
        {
            var 物品 = 数据.物品[堆叠.标识];
            var 行 = 创建物品行(堆叠, 物品);
            if (行 == null) continue;
        }
    }

    // 行只承载三样：名字（含品质标签）/ 数量 / 选中图（行本体 Button 只做点击选中；其余信息由右侧详情区承载）
    private 背包行 创建物品行(物品堆叠 堆叠, 物品数据 物品)
    {
        if (背包行模板 == null) return null;
        var 行 = 面板基类.创建模板<背包行>(列表容器, 背包行模板);
        if (行 == null) return 行;
        if (行.名字 != null) 行.名字.text = 物品工具.品质名称(物品.品质档, 物品.标识);
        if (行.数量 != null) 行.数量.text = 堆叠.数量 > 1 ? $"×{堆叠.数量}" : "";
        if (行.选中图 != null) 行.选中图.gameObject.SetActive(选中标识 == 堆叠.标识);
        var 按钮 = 行.GetComponent<Button>();
        if (按钮 != null)
        {
            var 标识 = 堆叠.标识;
            按钮.onClick.AddListener(() => 点击格子(标识));
            音效管理器.实例?.注册按钮(按钮);   // 动态行按钮成功音效
        }
        return 行;
    }

    // 单击 = 选中进详情；双击 = 快速操作（装备/使用/学习）
    private void 点击格子(string 标识)
    {
        if (双击.点击(标识)) { 选中标识 = 标识; 执行操作(); return; }
        选中标识 = 标识;
        刷新();
    }

    // ===== 详情区 =====

    private void 刷新详情(玩家档案 玩家)
    {
        var 数据 = ServiceRegistry.Get<DataService>();
        // 规范写法：out 变量在 if 条件末尾（条件为真时 body 直接 return，条件为假时 out 变量必已赋值）
        if (string.IsNullOrEmpty(选中标识) || !数据.物品.TryGetValue(选中标识, out var 物品))
        {
            显示未选中();
            return;
        }
        bool 在背包 = 玩家.所有持有物品().Exists(s => s.标识 == 选中标识);
        bool 已装备 = 玩家.已装备(选中标识);
        if (!在背包 && !已装备)
        {
            显示未选中();
            return;
        }
        面板基类.设文本(详情名称, 物品工具.品质名称(物品.品质档, 物品.标识));   // 品质+名称 合并一个文本
        面板基类.设文本(详情描述, 物品.描述);
        // 详情数值 = 基础加成 + 实例配件（在背包取该堆叠配件，已装备取装备记录配件）
        var 实例配件 = 已装备 ? 玩家.装备配件(选中标识) : 玩家.背包配件(选中标识);
        面板基类.设文本(详情数值, 物品工具.数值文本(数据, 物品, 实例配件));
        面板基类.设文本(详情价格, 物品.价值 > 0 ? $"价值 {物品.价值}" : "");
        string 操作 = 已装备 ? "卸下" : 操作文本(物品);
        if (详情操作按钮 != null)
        {
            详情操作按钮.gameObject.SetActive(!string.IsNullOrEmpty(操作));
            面板基类.设文本(详情操作文字, 操作);
        }
    }

    private void 显示未选中()
    {
        面板基类.设文本(详情名称, "—— 未选中 ——");
        面板基类.设文本(详情描述, "点击左侧物品查看详情。");
        面板基类.设文本(详情数值, "");
        面板基类.设文本(详情价格, "");
        if (详情操作按钮 != null) 详情操作按钮.gameObject.SetActive(false);
    }

    private static string 操作文本(物品数据 物品)
    {
        switch (物品.类型)
        {
            case "武器":
            case "防具":
            case "饰品": return "装备";
            case "恢复": return "使用";
            case "技能书": return "学习";
            default: return "";
        }
    }

    // 详情区操作按钮：已装备→卸下；装备类→换装；恢复→使用；技能书→学习
    private void 执行操作()
    {
        var 玩家 = ServiceRegistry.Get<PlayerService>()?.档案;
        var 数据 = ServiceRegistry.Get<DataService>();
        if (玩家 == null || string.IsNullOrEmpty(选中标识) || !数据.物品.TryGetValue(选中标识, out var 物品)) return;
        bool 已装备 = 玩家.已装备(选中标识);
        if (已装备)
        {
            string 槽 = "";
            foreach (var s in 全部槽位)
                if (玩家.装备标识(s) == 选中标识) { 槽 = s; break; }
            if (!string.IsNullOrEmpty(槽)) 面板操作.卸下(玩家, 槽);
        }
        else if (物品.类型 == "武器" || 物品.类型 == "防具" || 物品.类型 == "饰品") 面板操作.换装(玩家, 物品);
        else if (物品.类型 == "技能书") 面板操作.学习技能书(玩家, 物品);
        else if (物品.恢复量 > 0) 面板操作.使用恢复(玩家, 数据, 选中标识);   // 恢复/食物/药剂/战斗（带恢复量才可战斗外使用）
        选中标识 = null;
        刷新();
    }

    // ===== 分类 =====

    private void 设分类(分类 分类项)
    {
        当前分类 = 分类项;
        选中标识 = null;
        刷新();
    }

    private void 刷新分类高亮()
    {
        for (int i = 0; i < 分类表.Length && i < (分类按钮?.Length ?? 0); i++)
            面板基类.设选中缩放(分类按钮[i], 当前分类 == 分类表[i]);   // 选中态 = 固定放大（替代颜色高亮）
    }

    private static bool 匹配分类(string 类型, 分类 分类项)
    {
        switch (分类项)
        {
            case 分类.全部: return true;
            case 分类.装备: return 类型 == "武器" || 类型 == "防具";
            case 分类.消耗: return 类型 == "饮食" || 类型 == "医疗" || 类型 == "弹药" || 类型 == "技能书" || 类型 == "图纸";
            case 分类.材料: return 类型 == "材料";
            case 分类.任务: return 类型 == "任务品";
            default: return true;
        }
    }

    private static int 类型序(string 类型)
    {
        switch (类型)
        {
            case "武器": return 0;
            case "防具": return 1;
            case "饮食": return 3;
            case "医疗": return 3;
            case "弹药": return 3;
            case "技能书": return 4;
            case "图纸": return 4;
            case "材料": return 5;
            case "任务品": return 6;
            default: return 9;
        }
    }
}
