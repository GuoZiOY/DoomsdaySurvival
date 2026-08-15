using NUnit.Framework;

// 冒烟测试：确认测试程序集能跑起来
public class 冒烟测试
{
    [Test]
    public void 测试框架可用()
    {
        Assert.That(1 + 1, Is.EqualTo(2));
    }
}
