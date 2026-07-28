using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MusicTimeStampController : MonoBehaviour
{
    public static double MusicTime;
    
    private AudioSource m_music;
    private Slider m_slider;
    private double m_roundedMusicTime;
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
        
        m_roundedMusicTime = Math.Round(MusicTime, 2);
    }

    private void Update()
    {
        if (m_music != null && m_slider != null && m_music.isPlaying)
        {
            m_slider.value = (float)(m_music.time / MusicTime);
        }
    }

    public void ValueChange()
    {
        if (m_timeText != null && m_slider != null)
        {
            m_timeText.text = $"{Math.Round(m_slider.value * MusicTime, 2)}/{m_roundedMusicTime}";
        }
    }
}