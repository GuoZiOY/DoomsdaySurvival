// 世界沙盒事件：探索沙盒服务 → 探索网格面板（发布方只描述数据，不碰 UI 类型）

// 单格显示码（面板据此取色/取字符；逻辑层不关心怎么画）
public enum 沙盒格显示
{
    空,        // 未探索 / 无内容
    墙,        // 墙（含建筑外墙）
    地板,      // 室内可通行
    街道,      // 室外可通行
    障碍,      // 掩体（不可通行、可看穿）
    出口,      // 撤离点
    门,        // 门（关着/开着由 沙盒格视图.门开 区分）
    建筑入口,   // 楼门（点它进/出楼）
    楼梯,      // 楼梯（点它上/下楼）
    搜索点,    // 可搜刮的家具/容器（占格，点它搜）
    尸体,      // 战斗掉落的尸体容器（占格，点它搜）
    敌人,      // 敌人（在视野内才显示）
    玩家,      // 玩家所在格（面板也读 玩家列/行，这里兜底）
}

// 一格视图：显示码 + 迷雾态（未探索 / 已探索记忆 / 当前可见）+ 门开关
public readonly struct 沙盒格视图
{
    public readonly 沙盒格显示 显示;
    public readonly bool 已探索;
    public readonly bool 可见;
    public readonly bool 门开;     // 仅 显示=门 时有意义

    public 沙盒格视图(沙盒格显示 显示, bool 已探索, bool 可见, bool 门开 = false)
    { this.显示 = 显示; this.已探索 = 已探索; this.可见 = 可见; this.门开 = 门开; }

    public static readonly 沙盒格视图 未探索 = new 沙盒格视图(沙盒格显示.空, false, false);
}

// 沙盒显示事件：一次完整快照（面板全量重画；网格只有几百格，不做增量）
public readonly struct 沙盒显示事件
{
    public readonly string 信息条;      // 地区 · 危险度 · 楼层 · 行动点 · 时间
    public readonly string 提示;        // 脚下/相邻的可交互提示（"脚边有 衣柜（点击搜索）" 等）
    public readonly int 列, 行;
    public readonly 沙盒格视图[] 格;    // 行 * 列 + 列
    public readonly int 玩家列, 玩家行;
    public readonly bool 在建筑内;      // 面板据此切换按钮组（进入/离开、上楼/下楼）
    // 脚下可做的事（服务判定，面板只负责按钮启停——避免 UI 猜规则）
    public readonly bool 可搜刮;
    public readonly bool 可进入;
    public readonly bool 可上下;
    public readonly bool 可撤离;

    public 沙盒显示事件(string 信息条, string 提示, int 列, int 行, 沙盒格视图[] 格,
        int 玩家列, int 玩家行, bool 在建筑内,
        bool 可搜刮 = false, bool 可进入 = false, bool 可上下 = false, bool 可撤离 = false)
    {
        this.信息条 = 信息条; this.提示 = 提示;
        this.列 = 列; this.行 = 行; this.格 = 格;
        this.玩家列 = 玩家列; this.玩家行 = 玩家行; this.在建筑内 = 在建筑内;
        this.可搜刮 = 可搜刮; this.可进入 = 可进入; this.可上下 = 可上下; this.可撤离 = 可撤离;
    }
}

// 打开世界沙盒面板：地区模板标识（由 大地图/快速测试/安全屋出发 触发）
public readonly struct 打开世界沙盒事件
{
    public readonly string 地区标识;
    public 打开世界沙盒事件(string 地区标识) { this.地区标识 = 地区标识; }
}

// 沙盒动作项：点击世界物件后弹出的小菜单里的一条。
// 分工：服务判定「这个目标能做什么」（含可用性），UI 只负责把条目列出来 + 回传选了哪条。
public readonly struct 沙盒动作项
{
    public readonly string 文本;   // 显示文字："推开" / "带上" / "上楼" / "下楼" / "进去" / "出来" / "搜刮" / "撤离" / "走过去"
    public readonly string 动作;   // 动作标识："开门"/"关门"/"上楼"/"下楼"/"进入"/"离开"/"搜刮"/"撤离"/"移动"/"查看"

    public 沙盒动作项(string 文本, string 动作) { this.文本 = 文本; this.动作 = 动作; }
    public bool 有效 => !string.IsNullOrEmpty(文本) && !string.IsNullOrEmpty(动作);
}

// 沙盒日志文案事件（走既有 日志事件 也可以；这条留给"仅在沙盒面板内提示"的短句）
public readonly struct 沙盒提示事件
{
    public readonly string 文本;
    public 沙盒提示事件(string 文本) { this.文本 = 文本; }
}

// 打开沙盒容器（沙盒服务 → UI：搜索面板接管；实例 id 由生成器确定性派生）
public readonly struct 打开沙盒容器事件
{
    public readonly string 容器标识;   // 容器实例 id
    public 打开沙盒容器事件(string 容器标识) { this.容器标识 = 容器标识; }
}
