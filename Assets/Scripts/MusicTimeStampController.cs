using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RuntimePlayback;

public class MusicTimeStampController : MonoBehaviour
{
    public static double MusicTime;

    private AudioSource m_music;
    private Slider m_slider;
    private TextMeshProUGUI m_timeText;
    // 谱面播放器（提供谱面时钟：offset 前奏等待期音频时间不推进，但谱面时钟持续前进）
    private ChartPlaybackController m_chartPlayback;

    private void Start()
    {
        m_slider = GameObject.Find("MusicTime").GetComponent<Slider>();
        m_music = GameObject.Find("Audio Source").GetComponent<AudioSource>();
        m_chartPlayback = GameObject.Find("PlayScreen")?.GetComponent<ChartPlaybackController>();

        var timeObj = GameObject.Find("Time");
        if (timeObj != null)
        {
            m_timeText = timeObj.GetComponent<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        if (m_music == null || m_slider == null || MusicTime <= 0.0) return;

        // 延迟解析：ChartPlaybackController 由 PlaybackModeController.Start 动态挂载，可能晚于本组件 Start
        if (m_chartPlayback == null)
        {
            m_chartPlayback = GameObject.Find("PlayScreen")?.GetComponent<ChartPlaybackController>();
        }

        // 播放中优先用谱面时钟驱动滑块（含 offset>0 的前奏等待期，此时音频时间尚未推进但谱面已开始滚动）；
        // 否则退回音频时间
        if (m_chartPlayback != null && m_chartPlayback.IsPlaying)
        {
            m_slider.value = (float)(m_chartPlayback.CurrentTime / MusicTime);
        }
        else if (m_music.isPlaying)
        {
            m_slider.value = (float)(m_music.time / MusicTime);
        }
    }

    public void ValueChange()
    {
        if (m_timeText != null && m_slider != null)
        {
            // 总时长实时取 MusicTime（音频异步加载完成后才有效），避免 Start 时捕获到 0
            m_timeText.text = $"{Math.Round(m_slider.value * MusicTime, 2)}/{Math.Round(MusicTime, 2)}";
        }
    }
}