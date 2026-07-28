using UnityEngine;
using UnityEngine.UI;

public class ButtonFuctionManager : MonoBehaviour
{
    private AudioSource m_music;
    private Slider m_slider;
    private GameObject m_pauseButton;
    private GameObject m_playButton;

    private void Start()
    {
        m_slider = GameObject.Find("MusicTime").GetComponent<Slider>();
        m_music = GameObject.Find("Audio Source").GetComponent<AudioSource>();
        
        // 缓存按钮引用，避免重复查找
        var buttonContainer = GameObject.Find("TmpDataChanger").transform;
        m_pauseButton = buttonContainer.Find("Pause").gameObject;
        m_playButton = buttonContainer.Find("Play").gameObject;
    }

    public void Replay()
    {
        m_slider.value = 0;
    }

    public void Play()
    {
        m_music.time = (float)(m_slider.value * MusicTimeStampController.MusicTime);
        m_music.Play();
        m_pauseButton.SetActive(true);
        m_playButton.SetActive(false);
    }

    public void Pause()
    {
        m_music.Pause();
        m_pauseButton.SetActive(false);
        m_playButton.SetActive(true);
    }
}