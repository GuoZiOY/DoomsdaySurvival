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

    private AudioSource 音乐源;
    private AudioSource 音效源;
    private bool 本帧失败;   // 防重：本帧已判失败，成功音效跳过（失败点击只响错误音）

    // —— 音量/静音（AudioListener.volume 全局缩放；设置面板滑条 + 侧边栏音量开关）——
    public static float 当前音量 { get; private set; } = 1f;
    public bool 已静音 { get; private set; }

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(gameObject); return; }
        实例 = this;
        音乐源 = 配置源(false);
        音效源 = 配置源(true);
        AudioListener.volume = 已静音 ? 0f : 当前音量;
    }

    void Start()
    {
        // 背景音乐：游戏开始即循环（主菜单 → 全程）
        if (背景音乐 != null)
        {
            音乐源.clip = 背景音乐;
            音乐源.loop = true;
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
        音效源.PlayOneShot(按钮成功音效);
    }

    // 按钮错误音效：点击失败时播放（服务失败点调用）
    public void 播放失败()
    {
        本帧失败 = true;
        if (按钮错误音效 == null || 音效源 == null) return;
        音效源.PlayOneShot(按钮错误音效);
    }

    // 通用音效：后续扩展（技能/战斗/事件音效等）用
    public void 播放音效(AudioClip 片段)
    {
        if (片段 == null || 音效源 == null) return;
        音效源.PlayOneShot(片段);
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
}
