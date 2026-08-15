using System;
using UnityEngine;

    // 极简存档：PlayerPrefs 存 JSON 快照（v2 键，与旧框架 v1 隔离，互不覆盖）。
    public sealed class SaveService
    {
        private const string 存档键 = "fantasy_text_rpg_save_v2";

        // 存档快照：玩家档案 + 保存时间
        [Serializable]
        public class 存档数据
        {
            public 玩家档案 玩家;
            public long 保存时间;
        }

        // 保存完整玩家档案
        public void 保存(玩家档案 玩家)
        {
            var 数据 = new 存档数据
            {
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
