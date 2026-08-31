using System;

// 与 数据模型.cs 中 容器形状块 一致（纯数据类，验证 用 独立 定义）
[Serializable]
public class 容器形状块
{
    public int 列;
    public int 行;
    public int 宽;
    public int 高;
    public 容器形状块() { }
    public 容器形状块(int 列, int 行, int 宽, int 高) { this.列 = 列; this.行 = 行; this.宽 = 宽; this.高 = 高; }
}
