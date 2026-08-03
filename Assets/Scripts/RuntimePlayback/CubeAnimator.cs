using System.Collections.Generic;
using UnityEngine;

namespace RuntimePlayback
{
    /// <summary>
    /// 方体动画组件：挂载到方体 GameObject 上，根据播放时间实时驱动方体的
    /// 缩放(lx/ly/lz)、旋转(rx/ry/rz)、位置(px/py/pz)、颜色(RGBA)。
    /// 数据来源为 PlaybackCubeData 中的 13 个方体级缓动槽。
    /// </summary>
    public class CubeAnimator : MonoBehaviour
    {
        private PlaybackCubeData m_cubeData;
        private Vector3 m_basePosition;
        private Vector3 m_baseScale;
        private Quaternion m_baseRotation;
        private Renderer[] m_renderers;
        private float[] m_baseAlphas;
        private MaterialPropertyBlock m_propertyBlock;
        private bool m_initialized;

        // 缓存的槽位值（避免每帧 GC）
        private readonly float[] m_slotValues = new float[PlaybackSlotConfigs.CubeSlotCount];

        /// <summary>
        /// 初始化：缓存原始 Transform 数据、Renderer 引用和材质基础 Alpha。
        /// 若已初始化过，先恢复原始 Transform，避免将动画状态缓存为 base。
        /// </summary>
        public void Initialize(PlaybackCubeData data)
        {
            // 已初始化过时，先恢复原始 Transform，避免缓存动画中间状态
            if (m_initialized)
            {
                RestoreOriginal();
            }

            m_cubeData = data;
            m_basePosition = transform.localPosition;
            m_baseScale = transform.localScale;
            m_baseRotation = transform.localRotation;
            m_renderers = GetComponentsInChildren<Renderer>(true);
            m_propertyBlock = new MaterialPropertyBlock();

            // 缓存每个 Renderer 材质的原始 Alpha（棱=1, 面=0.4）
            m_baseAlphas = new float[m_renderers.Length];
            for (int i = 0; i < m_renderers.Length; i++)
            {
                if (m_renderers[i] != null && m_renderers[i].sharedMaterial != null)
                {
                    m_baseAlphas[i] = m_renderers[i].sharedMaterial.color.a;
                }
                else
                {
                    m_baseAlphas[i] = 1f;
                }
            }

            m_initialized = true;
        }

        /// <summary>
        /// 在指定时间点更新方体所有属性
        /// </summary>
        public void UpdateAtTime(float time)
        {
            if (!m_initialized || m_cubeData == null) return;

            EvaluateCubeSlots(time);
            ApplyTransform();
            ApplyColor();
        }

        /// <summary>
        /// 恢复方体到播放前的原始状态
        /// </summary>
        public void RestoreOriginal()
        {
            if (!m_initialized) return;

            transform.localPosition = m_basePosition;
            transform.localScale = m_baseScale;
            transform.localRotation = m_baseRotation;

            // 清除 PropertyBlock 颜色覆盖
            if (m_renderers != null)
            {
                foreach (var r in m_renderers)
                {
                    if (r != null)
                    {
                        r.GetPropertyBlock(m_propertyBlock);
                        m_propertyBlock.Clear();
                        r.SetPropertyBlock(m_propertyBlock);
                    }
                }
            }
        }

        /// <summary>
        /// 求值 13 个方体级缓动槽
        /// </summary>
        private void EvaluateCubeSlots(float time)
        {
            for (int i = 0; i < PlaybackSlotConfigs.CubeSlotCount; i++)
            {
                var config = PlaybackSlotConfigs.Slots[i];

                if (m_cubeData.easingSlots != null && i < m_cubeData.easingSlots.Count)
                {
                    m_slotValues[i] = EasingEvaluator.EvaluateSlot(
                        m_cubeData.easingSlots[i], time, config);
                }
                else
                {
                    m_slotValues[i] = config.defaultValue;
                }
            }
        }

        /// <summary>
        /// 将槽位值应用到 Transform
        /// </summary>
        private void ApplyTransform()
        {
            // lx/ly/lz -> localScale（百分比值，100=原始大小）
            float sx = m_slotValues[0];
            float sy = m_slotValues[1];
            float sz = m_slotValues[2];
            transform.localScale = new Vector3(
                m_baseScale.x * sx / 100f,
                m_baseScale.y * sy / 100f,
                m_baseScale.z * sz / 100f);

            // rx/ry/rz -> localEulerAngles
            transform.localRotation = m_baseRotation * Quaternion.Euler(
                m_slotValues[3], m_slotValues[4], m_slotValues[5]);

            // px/py/pz -> localPosition（叠加基础位置）
            transform.localPosition = m_basePosition + new Vector3(
                m_slotValues[6], m_slotValues[7], m_slotValues[8]);
        }

        /// <summary>
        /// 将 RGBA 值应用到所有子 Renderer 的材质。
        /// Alpha = 材质基础Alpha（棱=1, 面=0.4）× A槽位值
        /// </summary>
        private void ApplyColor()
        {
            if (m_renderers == null || m_renderers.Length == 0) return;

            float r = m_slotValues[9];
            float g = m_slotValues[10];
            float b = m_slotValues[11];
            float a = m_slotValues[12];

            for (int i = 0; i < m_renderers.Length; i++)
            {
                var rend = m_renderers[i];
                if (rend == null) continue;

                float finalAlpha = m_baseAlphas[i] * a;
                Color color = new Color(r, g, b, finalAlpha);

                rend.GetPropertyBlock(m_propertyBlock);
                m_propertyBlock.SetColor("_Color", color);
                rend.SetPropertyBlock(m_propertyBlock);
            }
        }

        /// <summary>当前方体数据</summary>
        public PlaybackCubeData CubeData => m_cubeData;

        /// <summary>是否已初始化</summary>
        public bool IsInitialized => m_initialized;
    }
}
