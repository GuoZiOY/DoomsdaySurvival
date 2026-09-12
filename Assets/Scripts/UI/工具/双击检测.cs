using UnityEngine;

// 双击检测器：按"标识"记上次点击时刻，阈值（0.3 秒）内同标识再点一次 = 双击。
// 用法：每次点击调 点击(标识)；返回 true = 这次是双击（单击/双击分流由调用方决定）。
// 来历：原来长在 地图渲染.cs 里（奇幻版地图节点用），地图那套删掉后它还有人在用（装备背包子面板），
//       所以搬到 UI/工具 下当通用小工具。
public sealed class 双击检测
{
    private string 上次标识;
    private float 上次时间;
    private const float 间隔 = 0.3f;

    public bool 点击(string 标识)
    {
        var 现在 = Time.time;
        bool 命中 = 标识 == 上次标识 && 现在 - 上次时间 < 间隔;
        上次标识 = 标识;
        上次时间 = 现在;
        return 命中;
    }
}
