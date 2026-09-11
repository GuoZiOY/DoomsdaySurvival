using UnityEngine;

// 网格面板配色：网格/装备拖拽 视觉配色统一规范（代码常量，一处定义全局生效）。
// 网格面板（主背包/仓库/容器/穿戴容器）与 装备槽 拖拽高亮 全部从本类读取配色，
// 组件自身不再暴露配色参数（改风格只改这里，全场景同步；改后需 网格重建 或 重新运行 生效）。
public static class 网格面板配色
{
    // 十六进制 RGB + 透明度 0~255
    public static readonly Color 底座色 = new Color(1f, 1f, 1f, 0.95f);                         // 空格底图：纯白 A255（网格底层精灵 原样着色）
    public static readonly Color 底盘色 = new Color(0.05f, 0.05f, 0.07f, 1f);                   // 网格底盘（整块衬底——网格 边框/托盘 感）
    public static readonly float 底盘外扩 = 4f;                                                   // 底盘 比 网格 大 的 像素（四周 合计；中心对称）
    // 三色线条：纯白，仅透明度不同——物品边界 150 / 形状边界 70 / 线条 30（0~255）
    public static readonly Color 形状边界色 = new Color(1f, 1f, 1f, 70f / 255f);    // 容器形状边界线（口袋轮廓框，A70）
    public static readonly Color 线条色 = new Color(1f, 1f, 1f, 30f / 255f);        // 分格线：**统一淡网格**（与物品无关，只给空格做参照）
    public static readonly float 线宽 = 2f;                                                  // 分格线宽（px：淡网格 / 口袋轮廓框）
    // 物品边界线：**画在物品自己身上**（从 线层 剥离）——值与原口径一致（A120 / 2px），只是归属变了
    public static readonly Color 物品边界色 = new Color(1f, 1f, 1f, 120f / 255f);   // 物品边界线（最亮 A150）
    public static readonly float 物品边界线宽 = 2f;                                           // 物品边界线宽（px）
    public static readonly float 物品边距 = 4f;                                              // 物品块四周内缩（不压网格线/不重叠）
    public static readonly Color 物品底色 = new Color(0.2784f, 0.298f, 0.3608f, 0.7843f);   // 物品内容层底色（#474C5C A200）
    public static readonly Color 高光色 = new Color(1f, 1f, 1f, 0.0392f);                    // 悬停高光层（#FFFFFF A10）
    public static readonly float 品质底色透明 = 0.3f;                                        // 品质底色（物品框层）半透明程度
    public static readonly Color 放置可色 = new Color(0.4941f, 0.7725f, 0.5294f, 0.3922f);  // 拖拽投影：可放（#7EC587 A100；装备槽高亮同款）
    public static readonly Color 放置禁色 = new Color(0.7529f, 0.4078f, 0.4078f, 0.3922f);  // 拖拽投影：不可放（#C06868 A100；装备槽高亮同款）
    public static readonly Color 合并色 = new Color(0.451f, 0.749f, 1f, 0.3922f);            // 拖拽投影：可合并（#73BFFF A100）
    // 家具（安全屋 房间网格）：色块区分 用 文本（名称/等级角标），底色 统一——改风格只改这里
    public static readonly Color 家具底色 = new Color(0.18f, 0.18f, 0.22f, 1f);      // 家具框 底色
    public static readonly Color 家具选中色 = new Color(0.32f, 0.32f, 0.4f, 1f);      // 家具框 选中 高亮
    public static readonly Color 家具内容色 = new Color(0.22f, 0.22f, 0.27f, 1f);     // 家具 内容 色块（名称 底）
    // 容器内部形状（塔科夫式独立口袋）：块间 空隙 = 块偏移 自然 露出（无线/无底图/不填色），
    // 每块 四边 亮轮廓 框（独立 闭合）——本类 无 缝隙 配色

    // ===== 房间层（房间网格面板 + 房间图层）：地板/墙/实体/迷雾/路径/效果 用色 =====
    // 材质与实体都靠"底色 + 描边 + 名称"区分（无美术资源阶段的统一表现语言）
    public static readonly Color 房间地板色 = new Color(0.145f, 0.145f, 0.165f, 1f);      // 地格底色
    public static readonly Color 房间地板斑色 = new Color(0.215f, 0.215f, 0.245f, 1f);    // 地格噪点/污渍（程序化贴图用）
    public static readonly Color 房间墙顶色 = new Color(0.40f, 0.38f, 0.36f, 1f);          // 墙顶（加深块）
    public static readonly Color 房间墙身色 = new Color(0.26f, 0.25f, 0.235f, 1f);         // 墙身竖纹
    public static readonly Color 网格实体描边 = new Color(0f, 0f, 0f, 0.60f);              // 实体框黑描边
    public static readonly Color 容器体色 = new Color(0.44f, 0.345f, 0.245f, 1f);          // 家具型容器
    public static readonly Color 容器高柜色 = new Color(0.34f, 0.28f, 0.22f, 1f);          // 高柜（挡视线那类）
    public static readonly Color 尸体体色 = new Color(0.30f, 0.30f, 0.325f, 1f);           // 尸体
    public static readonly Color 敌人令牌色 = new Color(0.62f, 0.20f, 0.20f, 1f);          // 敌人令牌
    public static readonly Color 敌人血条色 = new Color(0.80f, 0.26f, 0.26f, 1f);
    public static readonly Color 玩家令牌色 = new Color(0.24f, 0.72f, 0.62f, 1f);          // 玩家令牌
    public static readonly Color 玩家令牌边 = new Color(0.85f, 1f, 0.95f, 0.9f);
    public static readonly Color 迷雾未探索色 = new Color(0.02f, 0.02f, 0.03f, 1f);        // 视线外：纯黑板（三态柔化按需求取消）
    public static readonly Color 路径格色 = new Color(0.36f, 0.62f, 0.90f, 0.30f);         // 路径格高亮
    public static readonly Color 路径线色 = new Color(0.60f, 0.80f, 1f, 0.85f);            // 路径折线
    public static readonly Color 路径终色 = new Color(1f, 0.88f, 0.42f, 0.95f);            // 终点准星
    public static readonly Color 悬停格色 = new Color(1f, 1f, 1f, 0.08f);                  // 悬停格高亮
    public static readonly Color 不可达色 = new Color(0.85f, 0.30f, 0.30f, 0.45f);         // 目标不可达：闪一下淡红
    public static readonly Color 门框色 = new Color(0.52f, 0.44f, 0.30f, 1f);              // 门框（外墙洞里那圈木框）
    public static readonly Color 门扇色 = new Color(0.62f, 0.50f, 0.33f, 1f);              // 门扇（半开着的那片）
    public static readonly Color 门槛色 = new Color(0.30f, 0.26f, 0.21f, 1f);              // 门槛（洞里的地面）
    public static readonly float 已搜灰化 = 0.35f;                                           // 已搜容器：整体降到该不透明度 + 打勾

    // ===== 区域层（区域网格面板 + 区域图层）：街道 / 楼体 / 障碍 用色 =====
    // 迷雾/阴影相关色：房间层与区域层**共用同一套**（两片格子层都有战争迷雾，读上面房间段那几个）
    public static readonly Color 区域地面色 = new Color(0.10f, 0.105f, 0.12f, 1f);          // 沥青路面底色
    public static readonly Color 区域地面斑色 = new Color(0.155f, 0.16f, 0.18f, 1f);        // 路面斑块 / 裂缝
    public static readonly Color 建筑体色 = new Color(0.235f, 0.21f, 0.195f, 1f);           // 楼体
    public static readonly Color 建筑顶色 = new Color(0.33f, 0.29f, 0.26f, 1f);             // 楼顶压边
    public static readonly Color 建筑窗色 = new Color(0.38f, 0.42f, 0.40f, 1f);             // 窗（暗玻璃）
    public static readonly Color 障碍体色 = new Color(0.30f, 0.285f, 0.27f, 1f);            // 街道障碍（废车 / 砖堆）
    public static readonly Color 障碍深色 = new Color(0.19f, 0.18f, 0.17f, 1f);             // 障碍暗部（轮子 / 阴影）
}
