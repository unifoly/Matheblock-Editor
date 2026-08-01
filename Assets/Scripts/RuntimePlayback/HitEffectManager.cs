using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace RuntimePlayback
{
    /// <summary>
    /// 打击特效管理器：用对象池复用小方块粒子，渲染在方体渲染层（CubeLayer），
    /// 由 CubeCamera 输出到 RawImage，与 3D Note 同坐标系（方体本地空间）。
    ///
    /// 使用方式：
    /// - SpawnBurst：普通 Note 命中时的一次性散射（约 0.2s 消散）。
    /// - EmitHold：Hold 持续期间每帧调用的持续散射，直到 Hold 结束。
    /// </summary>
    public class HitEffectManager : MonoBehaviour
    {
        [Header("对象池")]
        [Tooltip("池最大容量（超过后新建并在归还时销毁，避免峰值卡顿）")]
        [SerializeField] private int m_poolCapacity = 256;

        [Header("普通 Note 散射（Burst）")]
        [Tooltip("单次散射的粒子数量")]
        [SerializeField] private int m_burstCount = 12;
        [Tooltip("Burst 粒子散射速度范围（单位/秒）")]
        [SerializeField] private float m_burstSpeedMin = 0.6f;
        [SerializeField] private float m_burstSpeedMax = 1.4f;
        [Tooltip("Burst 粒子存活时长（秒）")]
        [SerializeField] private float m_burstLife = 0.2f;

        [Header("Hold 持续散射（Emit）")]
        [Tooltip("每帧（每次调用）生成的粒子数量")]
        [SerializeField] private int m_emitPerFrame = 1;
        [Tooltip("Hold 粒子散射速度范围（单位/秒）")]
        [SerializeField] private float m_emitSpeedMin = 0.3f;
        [SerializeField] private float m_emitSpeedMax = 0.8f;
        [Tooltip("Hold 粒子存活时长（秒）")]
        [SerializeField] private float m_emitLife = 0.25f;

        [Header("粒子外观")]
        [Tooltip("粒子颜色（橙色）")]
        [SerializeField] private Color m_particleColor = new Color(1f, 0.62f, 0.1f, 1f);
        [Tooltip("粒子边长（世界单位）")]
        [SerializeField] private float m_particleSize = 0.03f;
        [Tooltip("渲染排序（需高于 Note 的 10）")]
        [SerializeField] private int m_sortingOrder = 20;

        /// <summary>单个粒子的运行时状态</summary>
        private sealed class HitParticle
        {
            public GameObject View;
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Spin;
            public float RemainingLife;
            public float MaxLife;
        }

        private ObjectPool<HitParticle> m_pool;
        private readonly List<HitParticle> m_active = new List<HitParticle>(64);
        private Sprite m_squareSprite;

        private void Awake()
        {
            m_squareSprite = CreateSquareSprite();
            m_pool = new ObjectPool<HitParticle>(
                createFunc: CreateParticle,
                actionOnGet: p => p.View.SetActive(true),
                actionOnRelease: OnReleaseParticle,
                actionOnDestroy: p => Destroy(p.View),
                collectionCheck: false,
                defaultCapacity: 32,
                maxSize: m_poolCapacity);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = m_active.Count - 1; i >= 0; i--)
            {
                HitParticle p = m_active[i];
                p.RemainingLife -= dt;

                if (p.RemainingLife <= 0f)
                {
                    m_pool.Release(p);
                    m_active.RemoveAt(i);
                    continue;
                }

                // 在父级本地空间运动（父级是方体，旋转/移动时粒子跟随）
                p.View.transform.localPosition += p.Velocity * dt;
                p.View.transform.localRotation *= Quaternion.Euler(0f, 0f, p.Spin * dt);

                // 随时间线性淡出
                Color c = p.Renderer.color;
                c.a = Mathf.Clamp01(p.RemainingLife / p.MaxLife);
                p.Renderer.color = c;
            }
        }

        /// <summary>
        /// 普通 Note 命中：在命中点（方体本地坐标）一次性散射若干粒子。
        /// </summary>
        /// <param name="parent">方体 Transform（粒子挂载其下，与 Note 同空间）</param>
        /// <param name="localPos">命中点（方体本地坐标）</param>
        /// <param name="planeAxisA">散射面轴 A（如下落方向）</param>
        /// <param name="planeAxisB">散射面轴 B（如轨道方向）</param>
        /// <param name="layer">目标渲染层（方体层）</param>
        public void SpawnBurst(Transform parent, Vector3 localPos,
            Vector3 planeAxisA, Vector3 planeAxisB, int layer)
        {
            for (int i = 0; i < m_burstCount; i++)
            {
                SpawnOne(parent, localPos, planeAxisA, planeAxisB,
                    m_burstSpeedMin, m_burstSpeedMax, m_burstLife, layer);
            }
        }

        /// <summary>
        /// Hold 持续散射：在 Hold 被"吞入"棱的位置每帧生成少量粒子，
        /// 由外部在 Hold 存活期间每帧调用，Hold 结束停止调用后粒子自然消散。
        /// </summary>
        public void EmitHold(Transform parent, Vector3 localPos,
            Vector3 planeAxisA, Vector3 planeAxisB, int layer)
        {
            for (int i = 0; i < m_emitPerFrame; i++)
            {
                SpawnOne(parent, localPos, planeAxisA, planeAxisB,
                    m_emitSpeedMin, m_emitSpeedMax, m_emitLife, layer);
            }
        }

        /// <summary>立即回收所有活动粒子（退出放映/跳转时调用）</summary>
        public void ClearAll()
        {
            for (int i = m_active.Count - 1; i >= 0; i--)
            {
                m_pool.Release(m_active[i]);
                m_active.RemoveAt(i);
            }
        }

        /// <summary>
        /// 生成单个粒子：在 (planeAxisA, planeAxisB) 张成的面内随机方向散射。
        /// </summary>
        private void SpawnOne(Transform parent, Vector3 localPos,
            Vector3 planeAxisA, Vector3 planeAxisB,
            float speedMin, float speedMax, float life, int layer)
        {
            HitParticle p = m_pool.Get();
            p.View.layer = layer;
            p.View.transform.SetParent(parent, false);
            p.View.transform.localPosition = localPos;
            p.View.transform.localRotation = Quaternion.identity;

            // 面内随机散射方向（粒子保持在 Note 所在面上，不会飞出方体轮廓）
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = (planeAxisA * Mathf.Cos(angle) + planeAxisB * Mathf.Sin(angle)).normalized;
            p.Velocity = dir * Random.Range(speedMin, speedMax);
            p.Spin = Random.Range(-360f, 360f);
            p.RemainingLife = life;
            p.MaxLife = life;
            p.Renderer.color = m_particleColor;

            m_active.Add(p);
        }

        private void OnReleaseParticle(HitParticle p)
        {
            // 归还池前恢复为默认层级并挂回管理器，避免残留脏状态
            p.View.layer = 0;
            p.View.transform.SetParent(transform, false);
            p.View.SetActive(false);
        }

        private HitParticle CreateParticle()
        {
            var go = new GameObject("HitParticle");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * m_particleSize;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = m_squareSprite;
            sr.sortingOrder = m_sortingOrder;

            go.SetActive(false);
            return new HitParticle { View = go, Renderer = sr };
        }

        /// <summary>
        /// 程序化生成 8x8 白色方形贴图（PPU=8 → 世界尺寸 1 单位，缩放即边长）。
        /// 无需外部资源，保证对象池开箱即用。
        /// </summary>
        private static Sprite CreateSquareSprite()
        {
            const int k_size = 8;
            var tex = new Texture2D(k_size, k_size, TextureFormat.RGBA32, false);
            var pixels = new Color[k_size * k_size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.hideFlags = HideFlags.DontSave;

            return Sprite.Create(tex,
                new Rect(0, 0, k_size, k_size),
                new Vector2(0.5f, 0.5f),
                k_size);
        }
    }
}
