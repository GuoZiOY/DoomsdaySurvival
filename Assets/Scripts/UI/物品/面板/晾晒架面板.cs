using UnityEngine;

// 晾晒架面板：晾晒架 家具 的 风干/硝制 面板 —— 就地转化面板基类 的 "晾晒" 版。
// 与 种植箱面板 的 差异 只有 文案（其余 全 走 基类：网格绑定 / 进度 / 倒计时 / 状态 / 订阅 / 0.5s 轮询）。
//   生肉 12 游戏时 → 肉干 · 兽皮 24 游戏时 → 皮革（转化 由 世界时间管理器.生长推进 推，按 家具 等级 加速）。
// 网格 4×2（家具数据.容器列/行）：**多件**（8 格 各 晾 各 的）+ **不 定根**（晾晒中 也 能 取下来）——
//   由 基类 按 家具功能类型（"晾晒"）把 单株定根 关掉。
// 场景搭建：Prefab/晾晒架面板（本组件 + 网格 对象 挂 就地转化网格 + 进度 滑条/文本 + 标题/关闭）。
public sealed class 晾晒架面板 : 就地转化面板基类
{
    private const string 预制体路径常量 = "Prefab/晾晒架面板";   // Resources 路径（照 种植箱面板 复制 一个：网格 4×2 更宽更矮）

    protected override string 预制体路径 => 预制体路径常量;

    protected override string 进行中文案 => "晾晒中…";
    protected override string 完成文案 => "已经晾好了";
    protected override string 空闲文案 => "架子空着";
    protected override string 倒计时后缀 => "晾好";

    // 静态 工厂：家具 右键 打开 晾晒架 → 晾晒架面板.创建
    public static 晾晒架面板 创建(RectTransform 挂载父, 物品堆叠 晾晒架)
        => 创建<晾晒架面板>(挂载父, 晾晒架, 预制体路径常量);
}
