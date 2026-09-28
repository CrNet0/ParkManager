using System;

namespace ParkManager.Geometry
{
    /// <summary>Seed helpers shared by the deterministic planners.</summary>
    internal static class Seeds
    {
        /// <summary>A fresh positive, non-zero seed for a new variant.</summary>
        internal static int NewSeed()
            => NonZero(Guid.NewGuid().GetHashCode() & int.MaxValue);

        internal static int NonZero(int seed) => seed == 0 ? 1 : seed;

        /// <summary>
        /// Derives an independent, non-zero random stream from one seed.
        /// Each caller uses its own salt so streams do not correlate.
        /// </summary>
        internal static uint Mix(int seed, uint salt)
        {
            var value = unchecked((uint)seed) ^ salt;
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value == 0 ? 1u : value;
        }
    }
}
