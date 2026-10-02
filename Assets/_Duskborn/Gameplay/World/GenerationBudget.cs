using System.Collections;
using System.Diagnostics;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Per-frame time budget manager (Frame Budgeting).
    /// Allows heavy operations (such as fractal terrain, raycasts, and mesh assembly) to be sliced
    /// across multiple frames to eliminate stuttering and maintain consistent 60+ FPS.
    /// </summary>
    public class GenerationBudget
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly float _maxMillisecondsPerFrame;

        /// <summary>
        /// Initialize the budget controller with a per-frame millisecond limit (default: 8ms, half a frame at 60 FPS).
        /// </summary>
        /// <param name="maxMillisecondsPerFrame">Maximum time in ms before yielding the frame to Unity.</param>
        public GenerationBudget(float maxMillisecondsPerFrame = 8f)
        {
            _maxMillisecondsPerFrame = maxMillisecondsPerFrame;
            _stopwatch.Start();
        }

        /// <summary>
        /// Restart the timer for the current frame.
        /// </summary>
        public void Reset()
        {
            _stopwatch.Restart();
        }

        /// <summary>
        /// Check whether the current frame's time limit has been reached.
        /// If reached, restart the timer and return true, signaling the routine to use 'yield return null'.
        /// </summary>
        public bool ShouldYield()
        {
            if (_stopwatch.ElapsedMilliseconds >= _maxMillisecondsPerFrame)
            {
                _stopwatch.Restart();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Helper yielding execution to Unity for one frame and resetting the timer.
        /// </summary>
        public IEnumerator YieldFrame()
        {
            yield return null;
            Reset();
        }
    }
}
