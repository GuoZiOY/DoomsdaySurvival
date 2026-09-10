using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 沙盒冒烟测试（Editor，命令行可跑）：把世界沙盒**真的跑起来**——装配服务、进地区、走格子、搜容器、
// 进楼上下楼、遭遇开战、撤离、同种子重建——逐步断言。不需要场景，因为它直接驱动服务层。
//
// 用法：
//   Unity.exe -batchmode -quit -projectPath "<项目>" -executeMethod SandboxSmokeTest.Run -logFile -
// 退出码：0 = 全部通过；1 = 有断言失败（日志里以 [冒烟][失败] 开头）。
//
// 注意：它真的会推进游戏时间/消耗行动点/发起战斗，并可能触发自动存档写盘——不要在有价值的存档上跑。
public static class SandboxSmokeTest
{
    private const string 测试地区 = "老城区";

    private static int 失败;
    private static int 通过;
    private static readonly List<string> 痕迹 = new List<string>();
    private static 沙盒显示事件 最近显示;
    private static int 营地事件数;
    private static int 容器事件数;
    private static string 最近容器标识;
    private static int 胜利事件数;
    private static int 遭遇次数;

    // 编辑器菜单入口（不用关编辑器也能跑：结果看 Console 的 [冒烟] 行；会打完一场仗，跑完会还原存档快照）
    [MenuItem("工具/世界沙盒/运行冒烟测试（装配→进区→搜刮→门/楼梯→遭遇→撤离）")]
    public static void 菜单冒烟() => Run();

    public static void Run()
    {
        // 存档保护：自动存档器 会在 战斗结束 时写 PlayerPrefs（与 SaveService 同键）——
        // 冒烟测试会真的打完一场仗，所以先快照、结束再还原，别把玩家手头的档冲掉。
        const string 存档键 = "last87days_save_v4";
        bool 有原档 = PlayerPrefs.HasKey(存档键);
        string 原档 = 有原档 ? PlayerPrefs.GetString(存档键) : null;
        try
        {
            try
            {
                跑();
            }
            catch (Exception e)
            {
                失败++;
                Debug.LogError($"[冒烟][失败] 异常：{e.GetType().Name} {e.Message}\n{e.StackTrace}");
            }
            if (痕迹.Count > 0) Debug.Log("[冒烟] 明细：\n" + string.Join("\n", 痕迹));
            Debug.Log($"[冒烟] 通过 {通过} 项，失败 {失败} 项，途中遭遇 {遭遇次数} 次");
        }
        finally
        {
            if (有原档) PlayerPrefs.SetString(存档键, 原档); else PlayerPrefs.DeleteKey(存档键);
            PlayerPrefs.Save();
            Debug.Log("[冒烟] 存档已还原（PlayerPrefs 快照回写）");
        }
        if (Application.isBatchMode) EditorApplication.Exit(失败 == 0 ? 0 : 1);
    }

    private static void 跑()
    {
        GameBootstrap.装配();
        断(!GameBootstrap.装配失败, "服务装配成功（数据校验通过）");
        if (GameBootstrap.装配失败) return;

        var 事件 = ServiceRegistry.Get<EventBus>();
        var 沙盒 = ServiceRegistry.Get<探索沙盒服务>();
        var 战斗 = ServiceRegistry.Get<BattleService>();
        var 搜索 = ServiceRegistry.Get<搜索服务>();
        var 玩家 = ServiceRegistry.Get<PlayerService>();
        断(沙盒 != null && 战斗 != null && 搜索 != null && 玩家?.档案 != null, "核心服务齐备（沙盒/战斗/搜索/玩家）");
        if (沙盒 == null || 战斗 == null || 搜索 == null || 玩家?.档案 == null) return;

        事件.订阅<沙盒显示事件>(e => 最近显示 = e);
        事件.订阅<打开营地事件>(_ => 营地事件数++);
        事件.订阅<打开沙盒容器事件>(e => { 容器事件数++; 最近容器标识 = e.容器标识; });
        事件.订阅<战斗结束事件>(e => { if (e.胜利) 胜利事件数++; });

        // ===== ① 进入地区 =====
        断(沙盒.进入地区(测试地区), $"进入地区 {测试地区}");
        断(沙盒.探索中, "探索中 = true（挂机时间驱动随之暂停）");
        断(最近显示.格 != null && 最近显示.列 >= 10 && 最近显示.行 >= 8, $"收到街道网格快照（{最近显示.列}×{最近显示.行}）");
        var 街道 = 沙盒.当前层数据;
        断(街道 != null && 街道.取(沙盒.玩家格.列, 沙盒.玩家格.行)?.地形 == 沙盒地形.出口, "出生点 = 撤离出口格");
        断(沙盒.当前片区.建筑.Count >= 1, $"片区生成了建筑（{沙盒.当前片区.建筑.Count} 栋）");
        断(数可见(最近显示) > 0, $"初始视野已点亮（{数可见(最近显示)} 格可见）");
        string 结构甲 = 指纹(沙盒.当前片区);

        // ===== ② 走到家具旁边 → 点它搜刮（家具占格，人站不上去）=====
        var 到容器 = 找最近(街道, 沙盒.玩家格.列, 沙盒.玩家格.行, 沙盒对象.搜索点, 走到相邻: true);
        断(到容器 != null, "街道上存在可搜刮的家具（占格）");
        if (到容器 != null)
        {
            int 行动点前 = 玩家.档案.行动点, 步数 = 到容器.Count - 1;
            走路径(沙盒, 战斗, 到容器);
            断(玩家.档案.行动点 <= 行动点前 - 步数, $"移动扣行动点（{行动点前} → {玩家.档案.行动点}，计划走 {步数} 步）");
            var 家具格 = 相邻家具格(街道, 沙盒.玩家格.列, 沙盒.玩家格.行);
            断(家具格 != null, "家具就在相邻格（占格，走不进去）");
            if (家具格 != null)
            {
                var 家具 = 街道.取(家具格.Value.列, 家具格.Value.行);
                string 实例 = 家具?.标识;
                断(!沙盒.移动(家具格.Value.列, 家具格.Value.行), "尝试走进家具格被拒绝（占格）");
                断(!string.IsNullOrEmpty(实例), "家具格上带容器实例 id");
                int 事件前 = 容器事件数;
                断(菜单里有(沙盒, 家具格.Value.列, 家具格.Value.行, "搜刮"), "点家具弹出的菜单里有「搜刮」");
                沙盒.执行动作("搜刮", 家具格.Value.列, 家具格.Value.行);   // 菜单里选「搜刮」
                断(容器事件数 == 事件前 + 1 && 最近容器标识 == 实例, "从菜单搜刮：实例 id 正确");
                var 定义 = 搜索.查找容器(实例);
                断(定义 != null, "搜索服务按 实例 id 反查到定义");
                var 内容 = 定义 != null ? 搜索.打开(定义) : null;
                断(内容 != null && 内容.网格物品 != null && 内容.网格物品.Count > 0, $"容器首次打开生成了物品（{内容?.网格物品?.Count ?? 0} 件）");
                断(搜索.已注册实例(实例), "实例容器已登记（重开内容一致的前提）");
                var 内容二 = 搜索.打开(定义);
                断(ReferenceEquals(内容, 内容二), "同一实例二次打开返回同一份内容（不重复随机）");
            }
        }

        // ===== ③ 楼层规则（结构层：单层楼无楼梯、多层楼每层有楼梯）=====
        bool 单层无梯 = true, 多层有梯 = true;
        int 多层楼数 = 0;
        foreach (var 楼 in 沙盒.当前片区.建筑)
        {
            if (楼?.楼层 == null || 楼.楼层.Count == 0) continue;
            if (楼.楼层.Count == 1)
            {
                if (有楼梯(楼.楼层[0])) 单层无梯 = false;
            }
            else
            {
                多层楼数++;
                foreach (var 层 in 楼.楼层) if (!有楼梯(层)) 多层有梯 = false;
            }
        }
        断(单层无梯, "单层建筑（便利店/药店）里没有楼梯");
        断(多层楼数 == 0 || 多层有梯, $"多层建筑每层都有楼梯（多层 {多层楼数} 栋）");

        // ===== ④ 点楼门进屋 → 房门开关 → 点楼梯上下 → 点出口门出楼 =====
        var 多层 = 沙盒.当前片区.建筑.Find(x => x != null && x.楼层.Count > 1);
        断(多层 != null, "片区里存在多层建筑");
        if (多层 != null)
        {
            var 楼门 = 街道.取(多层.入口列, 多层.入口行);
            断(楼门 != null && 楼门.是门 && 楼门.对象 == 沙盒对象.建筑入口, $"楼是一扇真门（@{多层.入口列},{多层.入口行}）");
            断(楼门 == null || !楼门.门开, "楼门初始关着");
            var 到楼门 = 找路径到格(街道, 沙盒.玩家格.列, 沙盒.玩家格.行, 多层.入口列, 多层.入口行, 停在相邻: true);
            断(到楼门 != null, "能走到楼门旁边");
            if (到楼门 != null)
            {
                走路径(沙盒, 战斗, 到楼门);
                断(菜单里有(沙盒, 多层.入口列, 多层.入口行, "进入"), "点楼门弹出的菜单里有「进入」");
                沙盒.执行动作("进入", 多层.入口列, 多层.入口行);   // 菜单里选「进去」
                断(最近显示.在建筑内, "从菜单进入建筑（进出切换）");
                断(沙盒.当前楼层索引 == 0 && 沙盒.当前层数据?.名称 == "1F", "落脚在 1F");
            }
            var 一层 = 沙盒.当前层数据;
            if (最近显示.在建筑内 && 一层 != null)
            {
                // 房门：点它推开 → 走过去 → 站在门洞里点自己那格带上（关门）
                var 房门 = 找一个门(一层, 沙盒.玩家格.列, 沙盒.玩家格.行);
                断(房门 != null, "1F 里存在可走到的房门");
                if (房门 != null)
                {
                    走路径(沙盒, 战斗, 找路径到格(一层, 沙盒.玩家格.列, 沙盒.玩家格.行, 房门.Value.列, 房门.Value.行, 停在相邻: true));
                    var 房门格 = 一层.取(房门.Value.列, 房门.Value.行);
                    int 可见前 = 数可见(最近显示);
                    断(!沙盒.移动(房门.Value.列, 房门.Value.行), "关着的门挡路（走不过去）");
                    断(菜单里有(沙盒, 房门.Value.列, 房门.Value.行, "开门"), "点关着的门：菜单里有「开门」");
                    沙盒.执行动作("开门", 房门.Value.列, 房门.Value.行);
                    断(房门格 != null && 房门格.门开, "从菜单推开 = 门开");
                    断(数可见(最近显示) > 可见前, "开门后视野穿过门口（关着的门挡视线）");
                    断(沙盒.移动(房门.Value.列, 房门.Value.行), "开着的门能走过去");
                    断(菜单里有(沙盒, 房门.Value.列, 房门.Value.行, "关门"), "站在门洞里：菜单里有「关门」");
                    沙盒.执行动作("关门", 房门.Value.列, 房门.Value.行);
                    断(房门格 != null && !房门格.门开, "从菜单带上 = 门关");
                    沙盒.执行动作("开门", 房门.Value.列, 房门.Value.行);   // 再推开，方便继续走
                }
                // 楼梯：站旁边点它就能上楼（不必先站上去）
                var 到楼梯 = 找最近(一层, 沙盒.玩家格.列, 沙盒.玩家格.行, 沙盒对象.楼梯, 走到相邻: true);
                断(到楼梯 != null, "1F 存在可走到的楼梯");
                if (到楼梯 != null)
                {
                    走路径(沙盒, 战斗, 到楼梯);
                    var 梯格 = 相邻梯格(一层, 沙盒.玩家格.列, 沙盒.玩家格.行);
                    断(梯格 != null, "楼梯就在相邻格");
                    if (梯格 != null)
                    {
                        断(菜单里有(沙盒, 梯格.Value.列, 梯格.Value.行, "上楼"), "点楼梯：菜单里有「上楼」");
                        沙盒.执行动作("上楼", 梯格.Value.列, 梯格.Value.行);
                        断(沙盒.当前楼层索引 == 1 && 沙盒.当前层数据?.名称 == "2F", "从菜单上楼");
                        断(数可见(最近显示) > 0, "新楼层视野已点亮（迷雾按层独立）");
                        断(菜单里有(沙盒, 沙盒.玩家格.列, 沙盒.玩家格.行, "下楼"), "2F 楼梯口：菜单里有「下楼」");
                        沙盒.执行动作("下楼", 沙盒.玩家格.列, 沙盒.玩家格.行);
                        断(沙盒.当前楼层索引 == 0, "从菜单下楼");
                    }
                }
                // 出楼：1F 墙上的出口门（对象=建筑入口）→ 点它出去
                var 出口门 = 找最近(一层, 沙盒.玩家格.列, 沙盒.玩家格.行, 沙盒对象.建筑入口, 走到相邻: true);
                断(出口门 != null, "1F 墙上有出口门");
                if (出口门 != null)
                {
                    走路径(沙盒, 战斗, 出口门);
                    var 门内 = 相邻入口格(一层, 沙盒.玩家格.列, 沙盒.玩家格.行);
                    断(门内 != null, "出口门在相邻格");
                    if (门内 != null)
                    {
                        断(菜单里有(沙盒, 门内.Value.列, 门内.Value.行, "离开"), "点楼里的出口门：菜单里有「离开」");
                        沙盒.执行动作("离开", 门内.Value.列, 门内.Value.行);
                        断(!最近显示.在建筑内, "从菜单出楼（进出切换）");
                    }
                }
            }
        }

        // ===== ⑤ 走回出口撤离（且不触发睡觉满状态） =====
        var 到出口 = 找最近(街道, 沙盒.玩家格.列, 沙盒.玩家格.行, 沙盒对象.无, 只要出口: true);
        断(到出口 != null, "能走回撤离出口");
        if (到出口 != null)
        {
            走路径(沙盒, 战斗, 到出口);
            int 生命前 = 玩家.档案.生命, 营地前 = 营地事件数;
            断(菜单里有(沙盒, 沙盒.玩家格.列, 沙盒.玩家格.行, "撤离"), "站在撤离点上：菜单里有「撤离」");
            断(沙盒.执行动作("撤离", 沙盒.玩家格.列, 沙盒.玩家格.行), "从菜单撤离成功");
            断(营地事件数 == 营地前 + 1, "撤离发出 打开营地事件（回安全屋）");
            断(!沙盒.探索中, "撤离后 探索中 = false");
            断(玩家.档案.生命 == 生命前, $"撤离不改生命（{生命前} → {玩家.档案.生命}；不触发睡觉满状态）");
        }

        // ===== ⑥ 二次进区 → 撞敌人 → 棋盘按场景传入 → 胜利清格 =====
        玩家.档案.行动点 = 玩家.档案.最大行动点;
        断(沙盒.进入地区(测试地区), "二次进入同一地区");
        var 街2 = 沙盒.当前层数据;
        bool 开战 = false;
        for (int 尝试 = 0; 尝试 < 4 && !开战; 尝试++)
        {
            var 到敌人 = 找最近(街2, 沙盒.玩家格.列, 沙盒.玩家格.行, 沙盒对象.敌人, 走到相邻: true);
            if (到敌人 == null) break;
            走路径(沙盒, 战斗, 到敌人, 自动解决: false);   // 留着这场战斗，供下面逐条断言
            if (!沙盒.遭遇中) 沙盒.交互();
            if (沙盒.遭遇中) 开战 = true;
        }
        断(遭遇次数 > 0, $"行走途中触发了遭遇（共 {遭遇次数} 次）");
        if (开战)
        {
            断(战斗.战斗中, "战斗服务已开战");
            断(战斗.当前战斗棋盘 == 街2.战斗棋盘, $"战斗棋盘按遭遇场景传入（实际 {战斗.当前战斗棋盘}，期望 {街2.战斗棋盘}）");
            断(战斗.敌方.Count > 0, $"敌方单位已生成（{战斗.敌方.Count} 个）");
            var 敌格 = 交战中敌格(街2, 沙盒.玩家格.列, 沙盒.玩家格.行);
            int 胜利前 = 胜利事件数;
            击杀全部敌人(战斗);
            推战斗直到结束(战斗);
            断(!战斗.战斗中, "敌人清空后战斗结束（胜利判定）");
            断(胜利事件数 == 胜利前 + 1, "收到 战斗结束事件（胜利）");
            战斗.返回();   // __沙盒胜利 → 沙盒回写
            断(!沙盒.遭遇中 && !沙盒.本次战斗来自沙盒, "战斗 [继续] 回调沙盒并复位状态");
            if (敌格 != null)
            {
                var 格 = 街2.取(敌格.Value.列, 敌格.Value.行);
                断(格 != null && 格.对象 != 沙盒对象.敌人, $"胜利后原地不再有敌人（现为 {格?.对象}）");
            }
        }

        // ===== ⑦ 确定性：同种子重建结构一致 =====
        断(沙盒.进入地区(测试地区), "三次进入同一地区");
        断(指纹(沙盒.当前片区) == 结构甲, "同种子重建的结构与首次一致（确定性）");
        沙盒.撤离();
        断(!沙盒.探索中, "收尾：撤离完成");
    }

    // ===== 断言 =====

    // 菜单里有某个动作？（点世界物件 → 服务判定动作列表 → UI 列出来）
    private static bool 菜单里有(探索沙盒服务 沙盒, int 列, int 行, string 动作)
    {
        var 列表 = 沙盒.目标动作(列, 行);
        foreach (var 项 in 列表) if (项.动作 == 动作) return true;
        断(false, $"菜单里应有「{动作}」@({列},{行})，实际：[{菜单文本(列表)}]");
        return false;
    }

    private static string 菜单文本(List<沙盒动作项> 列表)
    {
        if (列表 == null || 列表.Count == 0) return "空";
        var 段 = new List<string>();
        foreach (var 项 in 列表) 段.Add(项.文本);
        return string.Join(" / ", 段);
    }

    private static void 断(bool 条件, string 描述)
    {
        if (条件) { 通过++; 痕迹.Add($"  ✓ {描述}"); }
        else { 失败++; 痕迹.Add($"  ✗ {描述}"); Debug.LogError($"[冒烟][失败] {描述}"); }
    }

    // ===== 驱动工具 =====

    // 走一条路径（逐格移动）；路上遇到关着的门先推开；途中若触发遭遇 ——
    // 自动解决 = true 时速战速决继续走，false 时立刻停在遭遇状态（把战斗留给调用方断言）
    private static void 走路径(探索沙盒服务 沙盒, BattleService 战斗, List<(int 列, int 行)> 路径, bool 自动解决 = true)
    {
        if (路径 == null) return;
        var 层 = 沙盒.当前层数据;
        for (int i = 1; i < 路径.Count; i++)
        {
            if (沙盒.遭遇中)
            {
                遭遇次数++;
                if (!自动解决) return;
                速战速决(战斗); 沙盒.战斗胜利(); continue;
            }
            var 目标 = 层?.取(路径[i].列, 路径[i].行);
            if (目标 == null) return;
            if (目标.是门 && !目标.门开) 沙盒.点格(路径[i].列, 路径[i].行);   // 关着的门：点它推开
            if (!沙盒.移动(路径[i].列, 路径[i].行)) return;                  // 走不动就停（家具/墙/其它）
            if (沙盒.遭遇中)
            {
                遭遇次数++;
                if (!自动解决) return;
                速战速决(战斗); 沙盒.战斗胜利();
            }
        }
    }

    private static void 速战速决(BattleService 战斗)
    {
        if (!战斗.战斗中) return;
        击杀全部敌人(战斗);
        推战斗直到结束(战斗);
        战斗.返回();
    }

    // 击杀：生命归零 + 从敌方队列移除（战斗内正常的死亡会由 检查单位死亡 做移除；
    // 这里直接清列表，等价于「敌方已被打光」，这样 检查战斗结束 才判得出胜利）
    private static void 击杀全部敌人(BattleService 战斗)
    {
        foreach (var 敌 in 战斗.敌方) 敌.生命 = 0;
        战斗.敌方.Clear();
    }

    // 清空敌方血量后推进战斗：判定发生在「单位行动后」与「轮次结算（60 战斗分钟）」，
    // 所以要给足时间（行动间隔基准 5 秒/次）——240 × 0.5 = 120 战斗秒。
    private static void 推战斗直到结束(BattleService 战斗)
    {
        for (int i = 0; i < 240 && 战斗.战斗中; i++) 战斗.推进战斗(0.5f);
    }

    // 当前这一层里还剩的敌人格（取最近的）
    private static (int 列, int 行)? 当前敌人格(沙盒层数据 层)
    {
        if (层 == null) return null;
        foreach (var (列, 行, 格) in 层.全部())
            if (格 != null && 格.对象 == 沙盒对象.敌人) return (列, 行);
        return null;
    }

    // 正在交战的敌人格：玩家自己踩着的，或四邻里的那个敌人
    private static (int 列, int 行)? 交战中敌格(沙盒层数据 层, int 玩家列, int 玩家行)
    {
        if (层 == null) return null;
        var 脚下 = 层.取(玩家列, 玩家行);
        if (脚下 != null && 脚下.对象 == 沙盒对象.敌人) return (玩家列, 玩家行);
        foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
        {
            int 列 = 玩家列 + 偏列, 行 = 玩家行 + 偏行;
            var 格 = 层.取(列, 行);
            if (格 != null && 格.对象 == 沙盒对象.敌人) return (列, 行);
        }
        return null;
    }

    private static bool 有楼梯(沙盒层数据 层)
    {
        if (层 == null) return false;
        foreach (var (_, _, 格) in 层.全部()) if (格 != null && 格.对象 == 沙盒对象.楼梯) return true;
        return false;
    }

    // 寻路可走：地形可通行 且 不是家具/尸体占格（门算可走——推得开）
    private static bool 可走(沙盒格 格)
        => 格 != null && 沙盒格工具.可通行(格.地形) && !沙盒格工具.占位物(格);

    // BFS：走到指定坐标格（停在相邻 = 只走到它旁边，用于点门/点家具）
    private static List<(int 列, int 行)> 找路径到格(沙盒层数据 层, int 起列, int 起行, int 目标列, int 目标行, bool 停在相邻 = false)
    {
        if (层 == null || !层.在界内(目标列, 目标行)) return null;
        var 父 = new Dictionary<int, int>();
        var 队列 = new Queue<(int 列, int 行)>();
        父[起行 * 层.列 + 起列] = -1;
        队列.Enqueue((起列, 起行));
        while (队列.Count > 0)
        {
            var (列, 行) = 队列.Dequeue();
            if (列 == 目标列 && 行 == 目标行)
            {
                var 路径 = 回溯(父, 层, 列, 行);
                if (停在相邻 && 路径 != null && 路径.Count > 1) 路径.RemoveAt(路径.Count - 1);
                return 路径;
            }
            foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
            {
                int 新列 = 列 + 偏列, 新行 = 行 + 偏行;
                if (!可走(层.取(新列, 新行))) continue;
                int 键 = 新行 * 层.列 + 新列;
                if (父.ContainsKey(键)) continue;
                父[键] = 行 * 层.列 + 列;
                队列.Enqueue((新列, 新行));
            }
        }
        return null;
    }

    // BFS：找最近的「对象=目标 / 地形=出口」格；走到相邻 = 停在它的邻格（不踩上去）
    private static List<(int 列, int 行)> 找最近(沙盒层数据 层, int 起列, int 起行, 沙盒对象 目标, bool 走到相邻 = false, bool 只要出口 = false)
    {
        if (层 == null) return null;
        var 父 = new Dictionary<int, int>();
        var 队列 = new Queue<(int 列, int 行)>();
        父[起行 * 层.列 + 起列] = -1;
        队列.Enqueue((起列, 起行));
        while (队列.Count > 0)
        {
            var (列, 行) = 队列.Dequeue();
            var 格 = 层.取(列, 行);
            bool 命中 = 只要出口 ? 格?.地形 == 沙盒地形.出口 : 格?.对象 == 目标;
            if (命中 && !(列 == 起列 && 行 == 起行))
            {
                var 路径 = 回溯(父, 层, 列, 行);
                if (!走到相邻) return 路径;
                if (路径 != null && 路径.Count > 1) 路径.RemoveAt(路径.Count - 1);   // 停在相邻格
                return 路径;
            }
            foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
            {
                int 新列 = 列 + 偏列, 新行 = 行 + 偏行;
                var 新格 = 层.取(新列, 新行);
                // 目标格本身要能入队（家具/尸体 占格走不进去，但「走到相邻」需要先找到它）
                bool 是目标 = 只要出口 ? 新格?.地形 == 沙盒地形.出口 : 新格?.对象 == 目标;
                if (!可走(新格) && !是目标) continue;
                int 键 = 新行 * 层.列 + 新列;
                if (父.ContainsKey(键)) continue;
                父[键] = 行 * 层.列 + 列;
                队列.Enqueue((新列, 新行));
            }
        }
        return null;
    }

    // 相邻的家具/尸体格（点它搜刮）
    private static (int 列, int 行)? 相邻家具格(沙盒层数据 层, int 列, int 行)
    {
        foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
        {
            int 邻列 = 列 + 偏列, 邻行 = 行 + 偏行;
            if (沙盒格工具.占位物(层.取(邻列, 邻行))) return (邻列, 邻行);
        }
        return null;
    }

    // 相邻的楼梯格 / 相邻的楼门格
    private static (int 列, int 行)? 相邻梯格(沙盒层数据 层, int 列, int 行)
        => 相邻对象格(层, 列, 行, 沙盒对象.楼梯);

    private static (int 列, int 行)? 相邻入口格(沙盒层数据 层, int 列, int 行)
        => 相邻对象格(层, 列, 行, 沙盒对象.建筑入口);

    private static (int 列, int 行)? 相邻对象格(沙盒层数据 层, int 列, int 行, 沙盒对象 目标)
    {
        foreach (var (偏列, 偏行) in 沙盒格工具.四方向)
        {
            int 邻列 = 列 + 偏列, 邻行 = 行 + 偏行;
            if (层.取(邻列, 邻行)?.对象 == 目标) return (邻列, 邻行);
        }
        return null;
    }

    // 找一层里某个「可走到的门」：门本身在结构上可通行，取它旁边能站的格做路径终点
    private static (int 列, int 行)? 找一个门(沙盒层数据 层, int 起列, int 起行)
    {
        if (层 == null) return null;
        foreach (var (列, 行, 格) in 层.全部())
        {
            if (格 == null || !格.是门 || 格.对象 != 沙盒对象.无) continue;   // 只找房间门（楼门另有测试）
            if (找路径到格(层, 起列, 起行, 列, 行, 停在相邻: true) != null) return (列, 行);
        }
        return null;
    }

    private static List<(int 列, int 行)> 回溯(Dictionary<int, int> 父, 沙盒层数据 层, int 列, int 行)
    {
        var 路径 = new List<(int 列, int 行)>();
        int 键 = 行 * 层.列 + 列;
        while (键 >= 0)
        {
            路径.Add((键 % 层.列, 键 / 层.列));
            if (!父.TryGetValue(键, out var 上)) break;
            键 = 上;
        }
        路径.Reverse();
        return 路径;
    }

    private static int 数可见(沙盒显示事件 显示)
    {
        int 数 = 0;
        if (显示.格 == null) return 0;
        foreach (var 格 in 显示.格) if (格.可见) 数++;
        return 数;
    }

    private static string 指纹(片区数据 片区)
    {
        if (片区?.街道 == null) return "null";
        var 缓冲 = new System.Text.StringBuilder();
        foreach (var (_, _, 格) in 片区.街道.全部()) 缓冲.Append((int)格.地形).Append(格.标识);
        foreach (var 楼 in 片区.建筑)
            foreach (var 层 in 楼.楼层)
                foreach (var (_, _, 格) in 层.全部()) 缓冲.Append((int)格.地形).Append(格.标识);
        return 缓冲.ToString();
    }
}
