using UnityEngine;
using UnityEngine.UI;

// 音效管理器：统一音乐/音效播放（场景持久单例）。
// 背景音乐循环；按钮成功/错误音效由 注册按钮 自动挂钩 + 服务失败点驱动。
// 接线：场景建 音效管理器 物体挂本组件，Inspector 拖入 背景音乐/按钮成功音效/按钮错误音效 三个 AudioClip。
// 按钮挂钩全代码化：Start 时自动扫描场景所有 Button 注册成功音效（免手动逐个配置）；
// 运行时动态创建的按钮在创建点（创建行/地图渲染/战斗面板）直接 注册按钮。注册幂等，可重复调用。
public sealed class 音效管理器 : MonoBehaviour
{
    public static 音效管理器 实例 { get; private set; }

    [SerializeField] private AudioClip 背景音乐;
    [SerializeField] private AudioClip 按钮成功音效;
    [SerializeField] private AudioClip 按钮错误音效;
    [SerializeField] private AudioClip 拿起音效;   // 物品拖拽拿起
    [SerializeField] private AudioClip 放下音效;   // 物品拖拽放下
    [SerializeField] private AudioClip 搜索音效;   // 搜索容器：物品 搜索 出来（黑块 揭开）
    [SerializeField] private AudioClip 搜索中音效; // 搜索容器：搜索 进行中（容器/物品 倒计时 循环）
    [SerializeField] private AudioClip 装备音效;   // 装备槽 放下 装备（穿戴 成功）
    [SerializeField] private AudioClip 家具放下音效; // 家具 拖拽 放下 / 建造 落格 / 升级 完成

    // —— 各音效独立音量（0~10 整数，默认 10=满；全局音量仍由 AudioListener.volume 控制）——
    [SerializeField, Range(0, 10)] private int 背景音乐音量 = 10;
    [SerializeField, Range(0, 10)] private int 按钮成功音量 = 10;
    [SerializeField, Range(0, 10)] private int 按钮错误音量 = 10;
    [SerializeField, Range(0, 10)] private int 通用音效音量 = 10;
    [SerializeField, Range(0, 10)] private int 拿起音量 = 10;
    [SerializeField, Range(0, 10)] private int 放下音量 = 10;
    [SerializeField, Range(0, 10)] private int 搜索音量 = 10;
    [SerializeField, Range(0, 10)] private int 搜索中音量 = 10;
    [SerializeField, Range(0, 10)] private int 装备音量 = 10;
    [SerializeField, Range(0, 10)] private int 家具放下音量 = 10;

    private AudioSource 音乐源;
    private AudioSource 音效源;
    private AudioSource 搜索源;   // 搜索 进行中 循环（独立源，loop）
    private bool 本帧失败;   // 防重：本帧已判失败，成功音效跳过（失败点击只响错误音）

    // Inspector 改动即时生效：修改 背景音乐音量 立即同步到音乐源（音效为逐次读取，天然即时）
    void OnValidate()
    {
        背景音乐音量 = Mathf.Clamp(背景音乐音量, 0, 10);
        按钮成功音量 = Mathf.Clamp(按钮成功音量, 0, 10);
        按钮错误音量 = Mathf.Clamp(按钮错误音量, 0, 10);
        通用音效音量 = Mathf.Clamp(通用音效音量, 0, 10);
        拿起音量 = Mathf.Clamp(拿起音量, 0, 10);
        放下音量 = Mathf.Clamp(放下音量, 0, 10);
        搜索音量 = Mathf.Clamp(搜索音量, 0, 10);
        搜索中音量 = Mathf.Clamp(搜索中音量, 0, 10);
        装备音量 = Mathf.Clamp(装备音量, 0, 10);
        家具放下音量 = Mathf.Clamp(家具放下音量, 0, 10);
        if (音乐源 != null) 音乐源.volume = 换算(背景音乐音量);
        if (搜索源 != null && 搜索源.isPlaying) 搜索源.volume = 换算(搜索中音量);   // 搜索循环 音量 即时生效
    }

    // —— 音量/静音（AudioListener.volume 全局缩放；设置面板滑条 + 侧边栏音量开关）——
    public static float 当前音量 { get; private set; } = 1f;
    public bool 已静音 { get; private set; }

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(gameObject); return; }
        实例 = this;
        音乐源 = 配置源(false);
        音效源 = 配置源(true);
        搜索源 = 配置源(true);
        搜索源.loop = true;   // 搜索 进行中 循环（Play 时 设 clip）
        AudioListener.volume = 已静音 ? 0f : 当前音量;
    }

    void Start()
    {
        // 背景音乐：游戏开始即循环（主菜单 → 全程）
        if (背景音乐 != null)
        {
            音乐源.clip = 背景音乐;
            音乐源.loop = true;
            音乐源.volume = 换算(背景音乐音量);
            音乐源.Play();
        }
        // 自动给场景里所有现有按钮挂成功音效
        HookSceneButtons();
    }

    // 每帧末清除防重标记（点击事件同帧内有效）
    void LateUpdate() { 本帧失败 = false; }

    private AudioSource 配置源(bool 一次性)
    {
        var 源 = gameObject.AddComponent<AudioSource>();
        源.playOnAwake = false;
        源.spatialBlend = 0f;
        源.loop = !一次性;
        return 源;
    }

    // 自动扫描并注册场景内所有 Button（跳过预制体资产，只挂场景实例）
    private void HookSceneButtons()
    {
        foreach (var 按钮 in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (按钮 == null || 按钮.gameObject.scene.name == null) continue;
            注册按钮(按钮);
        }
    }

    // 注册单个按钮：点击播放成功音效（移除再添加，幂等，重复注册不叠音）；同时自动挂 悬停反馈（已有则跳过）
    public void 注册按钮(Button 按钮)
    {
        if (按钮 == null) return;
        按钮.onClick.RemoveListener(播放成功);
        按钮.onClick.AddListener(播放成功);
        悬停反馈.注册按钮(按钮);
    }

    // 按钮成功音效：按钮点击时播放；本帧已判失败则跳过
    public void 播放成功()
    {
        if (本帧失败) return;
        if (按钮成功音效 == null || 音效源 == null) return;
        音效源.PlayOneShot(按钮成功音效, 换算(按钮成功音量));
    }

    // 按钮错误音效：点击失败时播放（服务失败点调用）
    public void 播放失败()
    {
        本帧失败 = true;
        if (按钮错误音效 == null || 音效源 == null) return;
        音效源.PlayOneShot(按钮错误音效, 换算(按钮错误音量));
    }

    // 通用音效：后续扩展（技能/战斗/事件音效等）用
    public void 播放音效(AudioClip 片段)
    {
        if (片段 == null || 音效源 == null) return;
        音效源.PlayOneShot(片段, 换算(通用音效音量));
    }

    // 物品拖拽：拿起 / 放下音效
    public void 播放拿起()
    {
        if (拿起音效 == null || 音效源 == null) return;
        音效源.PlayOneShot(拿起音效, 换算(拿起音量));
    }

    public void 播放放下()
    {
        if (放下音效 == null || 音效源 == null) return;
        音效源.PlayOneShot(放下音效, 换算(放下音量));
    }

    // 搜索容器：物品 搜索 出来（黑块 揭开）音效
    public void 播放搜索()
    {
        if (搜索音效 == null || 音效源 == null) return;
        音效源.PlayOneShot(搜索音效, 换算(搜索音量));
    }

    // 搜索 进行中：循环 播放（容器/物品 倒计时 期间；幂等——已在播 不重开）
    public void 开始搜索中()
    {
        if (搜索中音效 == null || 搜索源 == null) return;
        if (搜索源.isPlaying && 搜索源.clip == 搜索中音效) return;
        搜索源.clip = 搜索中音效;
        搜索源.volume = 换算(搜索中音量);
        搜索源.Play();
    }

    // 搜索 结束：停止 循环（全部 搜完 / 关闭 面板）
    public void 停止搜索中()
    {
        if (搜索源 == null) return;
        搜索源.Stop();
    }

    // 装备槽 放下 装备（穿戴 成功）音效
    public void 播放装备()
    {
        if (装备音效 == null || 音效源 == null) return;
        音效源.PlayOneShot(装备音效, 换算(装备音量));
    }

    // 家具 放下 音效（拖拽 移动/换位 放下 / 建造 落格 / 升级 完成）
    public void 播放家具放下()
    {
        if (家具放下音效 == null || 音效源 == null) return;
        音效源.PlayOneShot(家具放下音效, 换算(家具放下音量));
    }

    // 音量开关：静音/恢复（侧边栏 音量按钮）
    public void 切换静音()
    {
        已静音 = !已静音;
        AudioListener.volume = 已静音 ? 0f : 当前音量;
    }

    // 设置音量（0~1，设置面板滑条）；静音状态下不生效，取消静音后恢复
    public void 设置音量(float 音量)
    {
        当前音量 = Mathf.Clamp01(音量);
        if (!已静音) AudioListener.volume = 当前音量;
    }

    // 整数 0~10 → 音量 0~1（各音效独立音量换算）
    private static float 换算(int 整数值) => Mathf.Clamp(整数值, 0, 10) / 10f;

    // —— 各音效独立音量设置（0~10 整数），供设置面板等运行时调整 ——
    public void 设置背景音乐音量(int 音量)
    {
        背景音乐音量 = Mathf.Clamp(音量, 0, 10);
        if (音乐源 != null) 音乐源.volume = 换算(背景音乐音量);
    }

    public void 设置按钮成功音量(int 音量) { 按钮成功音量 = Mathf.Clamp(音量, 0, 10); }
    public void 设置按钮错误音量(int 音量) { 按钮错误音量 = Mathf.Clamp(音量, 0, 10); }
    public void 设置通用音效音量(int 音量) { 通用音效音量 = Mathf.Clamp(音量, 0, 10); }
    public void 设置拿起音量(int 音量) { 拿起音量 = Mathf.Clamp(音量, 0, 10); }
    public void 设置放下音量(int 音量) { 放下音量 = Mathf.Clamp(音量, 0, 10); }
    public void 设置搜索音量(int 音量) { 搜索音量 = Mathf.Clamp(音量, 0, 10); }
    public void 设置搜索中音量(int 音量) { 搜索中音量 = Mathf.Clamp(音量, 0, 10); }
    public void 设置装备音量(int 音量) { 装备音量 = Mathf.Clamp(音量, 0, 10); }
    public void 设置家具放下音量(int 音量) { 家具放下音量 = Mathf.Clamp(音量, 0, 10); }

    // 读初值用（设置面板打开时把滑条同步成当前值）—— 写入口仍然只有上面那些 `设置XX音量`。
    public int 背景音乐音量当前 => 背景音乐音量;
    public int 通用音效音量当前 => 通用音效音量;
}
