using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

[assembly: InternalsVisibleToAttribute("TimersManager")]

namespace Timers
{
    public class Timer
    {
        public const uint INFINITE_LOOPS = uint.MaxValue;
        private readonly WeakReference m_Owner;
        private readonly bool m_UnscaledTime;

        public Timer(object owner, float interval, uint loopsCount, bool unscaledTime, Action action)
        {
            if (owner == null)
            {
                Debug.LogException(new Exception("Timer requre a valid owner, got null"));
                return;
            }

            if (interval < 0)
                interval = 0;

            m_UnscaledTime = unscaledTime;
            m_Owner = new WeakReference(owner);
            Interval = interval;
            LoopsCount = Math.Max(loopsCount, 1);
            m_Action = action;
        }

        /// <summary>
        ///     Timer ID
        /// </summary>
        public int Id => GetHashCode();

        /// <summary>
        ///     Get interval
        /// </summary>
        public object Owner => m_Owner.Target;

        /// <summary>
        ///     Get interval
        /// </summary>
        public float Interval { get; private set; }

        /// <summary>
        ///     Get total loops count (INFINITE (which is uint.MaxValue) if is constantly looping)
        /// </summary>
        public uint LoopsCount { get; } = 1;

        /// <summary>
        ///     Get how many loops were completed
        /// </summary>
        public uint CurrentLoopsCount { get; private set; }

        /// <summary>
        ///     Get how many loops remained to completion
        /// </summary>
        public uint RemainingLoopsCount => LoopsCount - CurrentLoopsCount;

        /// <summary>
        ///     Get total duration, (INFINITE if it's constantly looping)
        /// </summary>
        public float Duration => LoopsCount == INFINITE_LOOPS ? Mathf.Infinity : LoopsCount * Interval;

        /// <summary>
        ///     Get the delegate to execute
        /// </summary>
        public Action Action => m_Action;

        /// <summary>
        ///     Get total remaining time
        /// </summary>
        public float RemainingTime => LoopsCount == INFINITE_LOOPS && Interval > 0f
            ? Mathf.Infinity
            : Mathf.Max(LoopsCount * Interval - ElapsedTime, 0f);

        /// <summary>
        ///     Get total elapsed time
        /// </summary>
        public float ElapsedTime { get; private set; }

        /// <summary>
        ///     Get elapsed time in current loop
        /// </summary>
        public float CurrentCycleElapsedTime { get; private set; }

        /// <summary>
        ///     Get remaining time in current loop
        /// </summary>
        public float CurrentCycleRemainingTime => Mathf.Max(Interval - CurrentCycleElapsedTime, 0);

        /// <summary>
        ///     Checks whether this timer is ok to be removed
        /// </summary>
        public bool ShouldClear => m_Action == null || RemainingTime == 0 || !IsValid(Owner);

        /// <summary>
        ///     Checks if the timer is paused
        /// </summary>
        public bool IsPaused { get; private set; }

        private event Action m_Action;

        internal void Update()
        {
            if (IsPaused || !IsValid(Owner))
                return;

            if (m_Action == null || Interval < 0)
            {
                Interval = 0;
                return;
            }

            if (CurrentLoopsCount >= LoopsCount && LoopsCount != INFINITE_LOOPS)
            {
                ElapsedTime = Interval * LoopsCount;
                CurrentCycleElapsedTime = Interval;
            }
            else
            {
                ElapsedTime += m_UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                if (LoopsCount != INFINITE_LOOPS)
                    ElapsedTime = Mathf.Min(ElapsedTime, Interval * LoopsCount);

                CurrentCycleElapsedTime = Mathf.Min(Interval, ElapsedTime - CurrentLoopsCount * Interval);
                if (CurrentCycleElapsedTime == Interval)
                {
                    CurrentLoopsCount++;
                    CurrentCycleElapsedTime = 0f;
                    m_Action?.Invoke();
                }
            }
        }

        public static Timer FromDescriptor(object owner, Descriptor descriptor)
        {
            return new Timer(
                owner,
                descriptor.Interval,
                Math.Max(1, descriptor.LoopsCount),
                descriptor.UnscaledTime,
                () => descriptor.Event?.Invoke()
            );
        }

        ~Timer()
        {
            m_Action = null;
        }

        /// <summary>
        ///     Pause / Inpause timer
        /// </summary>
        public void SetPaused(bool bPause)
        {
            IsPaused = bPause;
        }


        /// <summary>
        ///     Compare frequency (calls per second)
        /// </summary>
        public static bool operator >(Timer A, Timer B)
        {
            return A == null || B == null ? true : A.Interval < B.Interval;
        }

        /// <summary>
        ///     Compare frequency (calls per second)
        /// </summary>
        public static bool operator <(Timer A, Timer B)
        {
            return A == null || B == null ? true : A.Interval > B.Interval;
        }

        /// <summary>
        ///     Compare frequency (calls per second)
        /// </summary>
        public static bool operator >=(Timer A, Timer B)
        {
            return A == null || B == null ? true : A.Interval <= B.Interval;
        }

        /// <summary>
        ///     Compare frequency (calls per second)
        /// </summary>
        public static bool operator <=(Timer A, Timer B)
        {
            return A == null || B == null ? true : A.Interval >= B.Interval;
        }

        private static bool IsValid(object obj)
        {
            return obj is Object unityObj
                ? unityObj
                : obj != null;
        }

        [Serializable]
        public struct Descriptor
        {
            [SerializeField] private bool m_InfiniteLoops;

            [SerializeField] private uint m_LoopsCount;

            [SerializeField] private float m_Interval;

            [SerializeField] private bool m_UnscaledTime;

            [SerializeField] private UnityEvent m_Event;

            public float Interval => m_Interval;
            public bool UnscaledTime => m_UnscaledTime;
            public uint LoopsCount => m_InfiniteLoops ? INFINITE_LOOPS : m_LoopsCount;
            public UnityEvent Event => m_Event;
        }
    }
}