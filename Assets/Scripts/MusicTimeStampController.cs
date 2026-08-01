using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MusicTimeStampController : MonoBehaviour
{
    public static double MusicTime;
    
    private AudioSource m_music;
    private Slider m_slider;
    private TextMeshProUGUI m_timeText;

    private void Start()
    {
        m_slider = GameObject.Find("MusicTime").GetComponent<Slider>();
        m_music = GameObject.Find("Audio Source").GetComponent<AudioSource>();
        
        var timeObj = GameObject.Find("Time");
        if (timeObj != null)
        {
            m_timeText = timeObj.GetComponent<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        if (m_music != null && m_slider != null && m_music.isPlaying && MusicTime > 0.0)
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