using System.Collections.Generic;
using UnityEngine;

namespace RuntimePlayback
{
    /// <summary>
    /// 谱面播放控制器：加载 JSON 谱面数据，驱动方体动画。
    /// 独立于编辑器运行，可同时用于编辑器放映模式和游戏运行时。
    ///
    /// 使用流程：
    /// 1. LoadChart(jsonPath) 加载谱面
    /// 2. DiscoverCubes() 或 RegisterCube() 关联方体 GameObject
    /// 3. 绑定 AudioSource，调用 Play() 开始播放
    /// 4. 每帧自动根据 AudioSource.time 更新方体动画
    /// </summary>
    public class ChartPlaybackController : MonoBehaviour
    {
        [Header("播放设置")]
        [Tooltip("音频源（驱动播放时间轴）")]
        [SerializeField] private AudioSource m_audioSource;

        [Tooltip("方体根节点（在其下搜索 Cube_{id}，留空则全局搜索）")]
        [SerializeField] private Transform m_cubeParent;

        // ---- 内部状态 ----
        private PlaybackChartData m_chartData;
        private readonly Dictionary<int, CubeAnimator> m_cubeAnimators = new Dictionary<int, CubeAnimator>();
        private bool m_isPlaying;

        /// <summary>是否正在播放</summary>
        public bool IsPlaying => m_isPlaying;

        /// <summary>谱面数据</summary>
        public PlaybackChartData ChartData => m_chartData;

        /// <summary>已注册的方体动画组件数量</summary>
        public int CubeAnimatorCount => m_cubeAnimators.Count;

        /// <summary>当前播放时间（秒）</summary>
        public float CurrentTime => m_audioSource != null ? m_audioSource.time : 0f;

        /// <summary>设置音频源（驱动播放时间轴）</summary>
        public void SetAudioSource(AudioSource source)
        {
            m_audioSource = source;
        }

        /// <summary>设置方体搜索根节点</summary>
        public void SetCubeParent(Transform parent)
        {
            m_cubeParent = parent;
        }

        #region 谱面加载

        /// <summary>
        /// 从 JSON 文件加载谱面数据
        /// </summary>
        public bool LoadChart(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath) || !System.IO.File.Exists(jsonPath))
            {
                Debug.LogWarning($"[ChartPlaybackController] 谱面文件不存在: {jsonPath}");
                return false;
            }

            string json = System.IO.File.ReadAllText(jsonPath);
            return LoadChartFromString(json);
        }

        /// <summary>
        /// 从 JSON 字符串加载谱面数据
        /// </summary>
        public bool LoadChartFromString(string json)
        {
            try
            {
                m_chartData = JsonUtility.FromJson<PlaybackChartData>(json);
                if (m_chartData?.cubes == null || m_chartData.cubes.Count == 0)
                {
                    Debug.LogWarning("[ChartPlaybackController] 谱面中无方体数据");
                    return false;
                }

                // 诊断日志：输出加载的缓动槽数量和锚点总数
                var sb = new System.Text.StringBuilder();
                sb.Append($"加载谱面: {m_chartData.cubes.Count} 个方体");
                foreach (var cube in m_chartData.cubes)
                {
                    int totalAnchors = 0;
                    if (cube.easingSlots != null)
                    {
                        foreach (var slot in cube.easingSlots)
                            totalAnchors += slot?.bars?.Count ?? 0;
                    }
                    sb.Append($"\n  Cube#{cube.cubeId}: slots={cube.easingSlots?.Count ?? 0}, bars={totalAnchors}");
                }
                Debug.Log($"[ChartPlaybackController] {sb}");

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ChartPlaybackController] 加载谱面失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重新加载谱面数据并更新已注册方体的动画数据（不重新搜索 GameObject）
        /// </summary>
        public void ReloadChartData(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath) || !System.IO.File.Exists(jsonPath)) return;

            try
            {
                string json = System.IO.File.ReadAllText(jsonPath);
                m_chartData = JsonUtility.FromJson<PlaybackChartData>(json);
                if (m_chartData?.cubes == null) return;

                // 更新已注册 CubeAnimator 的数据引用（先恢复原始 Transform 再重新初始化）
                foreach (var cube in m_chartData.cubes)
                {
                    if (m_cubeAnimators.TryGetValue(cube.cubeId, out var animator) && animator != null)
                    {
                        animator.RestoreOriginal();
                        animator.Initialize(cube);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ChartPlaybackController] 重新加载谱面失败: {ex.Message}");
            }
        }

        #endregion

        #region 方体关联

        /// <summary>
        /// 自动发现场景中的方体 GameObject（按 "Cube_{id}" 名称匹配）
        /// </summary>
        public void DiscoverCubes()
        {
            if (m_chartData?.cubes == null) return;

            foreach (var cube in m_chartData.cubes)
            {
                string name = $"Cube_{cube.cubeId}";
                GameObject go = FindCubeByName(name);
                if (go != null)
                {
                    RegisterCubeInternal(cube.cubeId, go);
                }
                else
                {
                    Debug.LogWarning($"[ChartPlaybackController] 未找到方体: {name}");
                }
            }
        }

        /// <summary>
        /// 手动注册方体 GameObject
        /// </summary>
        public void RegisterCube(int cubeId, GameObject cubeGo)
        {
            if (cubeGo == null) return;
            RegisterCubeInternal(cubeId, cubeGo);
        }

        /// <summary>
        /// 注册所有方体（需先 LoadChart）
        /// </summary>
        public void RegisterAllCubes(Dictionary<int, GameObject> cubes)
        {
            if (cubes == null) return;
            foreach (var pair in cubes)
            {
                RegisterCube(pair.Key, pair.Value);
            }
        }

        private void RegisterCubeInternal(int cubeId, GameObject go)
        {
            // 已存在则移除旧组件
            if (m_cubeAnimators.TryGetValue(cubeId, out var existing))
            {
                if (existing != null)
                {
                    existing.RestoreOriginal();
                    Destroy(existing);
                }
                m_cubeAnimators.Remove(cubeId);
            }

            // 查找对应的谱面数据
            PlaybackCubeData cubeData = null;
            if (m_chartData?.cubes != null)
            {
                foreach (var c in m_chartData.cubes)
                {
                    if (c.cubeId == cubeId) { cubeData = c; break; }
                }
            }

            if (cubeData == null)
            {
                Debug.LogWarning($"[ChartPlaybackController] 谱面中无 cubeId={cubeId}");
                return;
            }

            var animator = go.GetComponent<CubeAnimator>();
            if (animator == null)
                animator = go.AddComponent<CubeAnimator>();

            animator.Initialize(cubeData);
            m_cubeAnimators[cubeId] = animator;
        }

        private GameObject FindCubeByName(string name)
        {
            if (m_cubeParent != null)
            {
                var child = m_cubeParent.Find(name);
                if (child != null) return child.gameObject;
            }
            return GameObject.Find(name);
        }

        #endregion

        #region 播放控制

        /// <summary>开始播放</summary>
        public void Play()
        {
            if (m_audioSource != null && !m_audioSource.isPlaying)
                m_audioSource.Play();
            m_isPlaying = true;
        }

        /// <summary>暂停播放</summary>
        public void Pause()
        {
            if (m_audioSource != null && m_audioSource.isPlaying)
                m_audioSource.Pause();
            m_isPlaying = false;
        }

        /// <summary>停止播放并恢复方体原始状态</summary>
        public void Stop()
        {
            if (m_audioSource != null)
            {
                m_audioSource.Stop();
                m_audioSource.time = 0f;
            }
            m_isPlaying = false;
            RestoreAllCubes();
        }

        /// <summary>跳转到指定时间（秒）</summary>
        public void Seek(float time)
        {
            if (m_audioSource != null)
                m_audioSource.time = Mathf.Max(0f, time);
            UpdateAllCubes(time);
        }

        #endregion

        #region 每帧更新

        private void Update()
        {
            if (!m_isPlaying) return;
            if (m_audioSource == null || m_chartData == null) return;

            // 音频结束自动停止（clip 未分配时直接跳过，避免空引用）
            if (m_audioSource.clip != null
                && !m_audioSource.isPlaying
                && m_audioSource.time >= m_audioSource.clip.length - 0.05f)
            {
                Pause();
                return;
            }

            UpdateAllCubes(m_audioSource.time);
        }

        /// <summary>
        /// 更新所有方体动画到指定时间
        /// </summary>
        public void UpdateAllCubes(float time)
        {
            foreach (var pair in m_cubeAnimators)
            {
                if (pair.Value != null && pair.Value.IsInitialized)
                {
                    pair.Value.UpdateAtTime(time);
                }
            }
        }

        /// <summary>
        /// 恢复所有方体到播放前状态
        /// </summary>
        public void RestoreAllCubes()
        {
            foreach (var pair in m_cubeAnimators)
            {
                if (pair.Value != null)
                    pair.Value.RestoreOriginal();
            }
        }

        #endregion

        #region 清理

        private void OnDisable()
        {
            m_isPlaying = false;
        }

        private void OnDestroy()
        {
            RestoreAllCubes();
        }

        #endregion
    }
}
