// 验证器专用桩：数据模型.cs 里少数几个「引用了别处枚举」的字段类型。
// 只为了让 数据模型.cs 能在无 Unity 的命令行工程里编译——沙盒生成器用不到这些类型。
// 真身：Assets/Scripts/Domain/品质.cs（品质）、Assets/Scripts/Domain/玩家档案.cs（伤病类型）。
public enum 品质 { 普通, 优秀, 稀有, 史诗, 英雄, 传奇 }

public enum 伤病类型 { 疲劳, 中毒, 感冒, 流血, 骨折, 发烧 }
