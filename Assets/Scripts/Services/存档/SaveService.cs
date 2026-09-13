using System;
using UnityEngine;

    // 极简存档（末日版）：PlayerPrefs 存 JSON 快照（v4 键，与旧版 v3 隔离）。
    // v4：饱食/水分 改 float（精确 0.1）+ 世界时间管理器 统一结算——旧 v3 存档 结构 不 兼容，换 键 天然 失效（读档 失败 → 新游戏 重置）。
    public sealed class SaveService
    {
        private const string 存档键 = "last87days_save_v4";

        // 存档结构版本：结构变动时递增；读取时据 版本 做兼容/迁移
        public const int 当前版本 = 4;

        // 存档快照：玩家档案 + 版本 + 保存时间
        [Serializable]
        public class 存档数据
        {
            public int 版本 = 当前版本;
            public 玩家档案 玩家;
            public long 保存时间;
        }

        // 保存完整玩家档案
        public void 保存(玩家档案 玩家)
        {
            var 数据 = new 存档数据
            {
                版本 = 当前版本,
                玩家 = 玩家,
                保存时间 = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            PlayerPrefs.SetString(存档键, JsonUtility.ToJson(数据));
            PlayerPrefs.Save();
        }

        public bool 有存档() => PlayerPrefs.HasKey(存档键);

        // 读取存档；损坏时返回 null 并记日志（不崩）
        public 存档数据 读取()
        {
            if (!有存档()) return null;
            try { return JsonUtility.FromJson<存档数据>(PlayerPrefs.GetString(存档键)); }
            catch (Exception 异常)
            {
                Debug.LogError($"[SaveService] 读取存档失败: {异常.Message}");
                return null;
            }
        }

        public void 删除() => PlayerPrefs.DeleteKey(存档键);
    }
