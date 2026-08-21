using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HexMap;

public class ButtonFuctionManager : MonoBehaviour
{
    // 播放倍速允许的范围（下限避免静音，上限防止过快）
    private const float k_minPlaybackSpeed = 0.05f;
    private const float k_maxPlaybackSpeed = 10f;

    // 播放/暂停切换快捷键的 Action 名（与 Settings 页面中一致，用于 KeyBindingsStore 持久化）
    private const string k_actionPlaybackToggle = "Playback_Toggle";

    private AudioSource m_music;
    private Slider m_slider;
    private GameObject m_pauseButton;
    private GameObject m_playButton;
    private PlaybackModeController m_playbackModeController;
    private TMP_InputField m_speedInput;

    // 播放/暂停切换组合键（默认空格，可从 Settings 重绑）
    private KeyCombo m_playbackToggleCombo;

    private void Start()
    {
        m_slider = GameObject.Find("MusicTime").GetComponent<Slider>();
        m_music = GameObject.Find("Audio Source").GetComponent<AudioSource>();
        m_playbackModeController = GameObject.Find("PlayScreen")?.GetComponent<PlaybackModeController>();

        // 缓存按钮引用，避免重复查找
        var buttonContainer = GameObject.Find("TmpDataChanger").transform;
        m_pauseButton = buttonContainer.Find("Pause").gameObject;
        m_playButton = buttonContainer.Find("Play").gameObject;

        // Display 按钮未在场景中绑定 onClick，此处以代码绑定（避免场景中重复绑定时被调用两次）
        var displayButton = buttonContainer.Find("Display")?.GetComponent<Button>();
        if (displayButton != null && displayButton.onClick.GetPersistentEventCount() == 0)
        {
            displayButton.onClick.AddListener(Display);
        }

        // 倍速输入框：默认值为 1，结束编辑时应用到播放速度（AudioSource.pitch）
        m_speedInput = buttonContainer.Find("Speed/Input")?.GetComponent<TMP_InputField>();
        if (m_speedInput != null)
        {
            if (string.IsNullOrWhiteSpace(m_speedInput.text))
            {
                m_speedInput.text = "1";
            }
            ApplyPlaybackSpeedFromInput();
            m_speedInput.onEndEdit.AddListener(HandleSpeedInputEndEdit);
        }

        // 加载播放/暂停切换快捷键（默认空格，可重绑）
        m_playbackToggleCombo = KeyBindingsStore.GetKeyCombo(k_actionPlaybackToggle, KeyCombo.Parse("Space"));
    }

    private void Update()
    {
        // 文本输入框获焦时跳过，避免与 TMP_InputField 的空格输入冲突
        if (UndoRedoManager.IsTextInputFocused())
        {
            return;
        }

        // 空格键切换播放/暂停
        if (m_playbackToggleCombo.IsValid && m_playbackToggleCombo.IsPressed())
        {
            TogglePlayPause();
        }
    }

    /// <summary>
    /// 倍速输入结束：解析并应用播放速度，无效输入回退为当前倍速
    /// </summary>
    private void HandleSpeedInputEndEdit(string value)
    {
        if (float.TryParse(value, out float speed))
        {
            ApplyPlaybackSpeed(Mathf.Clamp(speed, k_minPlaybackSpeed, k_maxPlaybackSpeed));
        }
        else
        {
            // 无效输入：回退显示当前倍速
            m_speedInput.text = m_music.pitch.ToString("0.##");
        }
    }

    /// <summary>
    /// 将输入框中的倍速值应用到音频（供启动时同步默认值）
    /// </summary>
    private void ApplyPlaybackSpeedFromInput()
    {
        if (m_speedInput != null && float.TryParse(m_speedInput.text, out float speed))
        {
            ApplyPlaybackSpeed(Mathf.Clamp(speed, k_minPlaybackSpeed, k_maxPlaybackSpeed));
        }
    }

    /// <summary>
    /// 应用播放倍速并回写输入框显示
    /// </summary>
    private void ApplyPlaybackSpeed(float speed)
    {
        m_music.pitch = speed;
        m_speedInput.text = speed.ToString("0.##");
    }

    /// <summary>
    /// 回到最开头并开始放映（正常放映模式，淡出网格）
    /// </summary>
    public void Replay()
    {
        m_slider.value = 0f;
        m_playbackModeController?.SetKeepGridDuringPlayback(false);
        Play();
    }

    /// <summary>
    /// 放映谱面但不隐藏网格（Display 模式：网格保持可见并跟随播放自动滚动）
    /// </summary>
    public void Display()
    {
        m_playbackModeController?.SetKeepGridDuringPlayback(true);
        Play();
    }

    public void Play()
    {
        // 不在此处重置放映模式：由具体按钮（Display/Replay）决定是否保留网格
        float seekTime = (float)(m_slider.value * MusicTimeStampController.MusicTime);

        if (m_playbackModeController != null)
        {
            // 经由放映控制器启动：刷新最新 offset 并按 offset 调整音频起始位置
            m_playbackModeController.PlayMusic(seekTime);
        }
        else
        {
            // 兜底：无放映控制器时直接播放
            m_music.Stop();
            m_music.time = seekTime;
            m_music.Play();
        }

        m_pauseButton.SetActive(true);
        m_playButton.SetActive(false);
    }

    public void Pause()
    {
        if (m_playbackModeController != null)
        {
            m_playbackModeController.PauseMusic();
        }
        else
        {
            m_music.Pause();
        }

        m_pauseButton.SetActive(false);
        m_playButton.SetActive(true);
    }

    /// <summary>
    /// 切换播放/暂停状态（供空格快捷键调用）。
    /// 播放中则暂停，未播放则从当前位置继续播放。
    /// </summary>
    public void TogglePlayPause()
    {
        if (m_playbackModeController != null && m_playbackModeController.IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }
}