using System;

namespace AetherXIV.Core.Map.Actors
{
    // Provisional regional distribution, not a recovered retail schedule.
    // One period is eight Eorzean hours (1,400 real seconds).
    internal static class NormalWeatherPolicy
    {
        internal const long PeriodSeconds = 1400;
        // Eleven retail zone-entry packets in the 54-capture corpus use 5.
        internal const ushort EntryTransition = 5;
        // Native argument, not a claim about seconds or ticks. Local client
        // WeatherDirectorBaseClass.processUpdateWork passes 15 for nonzero weather.
        internal const ushort UpdateTransition = 15;

        internal static long GetPeriod(long unixSeconds)
        {
            long period = Math.DivRem(unixSeconds, PeriodSeconds, out long remainder);
            return remainder < 0 ? period - 1 : period;
        }

        public static ushort Select(ushort region, bool outdoors, long unixSeconds)
        {
            if (!outdoors) return 8001;
            long period = GetPeriod(unixSeconds);
            uint hash = unchecked((uint)period * 2654435761u + region * 2246822519u);
            hash ^= hash >> 16;
            int roll = (int)(hash % 100);
            if (region == 104) // Thanalan: 35/25/15/10/10/5 percent.
                return roll < 35 ? (ushort)8001 : roll < 60 ? (ushort)8002 :
                    roll < 75 ? (ushort)8003 : roll < 85 ? (ushort)8005 :
                    roll < 95 ? (ushort)8011 : (ushort)8012;
            if (region == 101) // La Noscea.
                return roll < 35 ? (ushort)8001 : roll < 60 ? (ushort)8002 :
                    roll < 75 ? (ushort)8003 : roll < 85 ? (ushort)8005 : (ushort)8007;
            if (region == 103) // Black Shroud.
                return roll < 25 ? (ushort)8001 : roll < 50 ? (ushort)8002 :
                    roll < 70 ? (ushort)8003 : roll < 80 ? (ushort)8004 : (ushort)8007;
            return 8001; // Unreviewed territories retain the previous clear default.
        }

        public static bool IsNormal(ushort weather) => weather >= 8001 && weather <= 8017 && weather != 8014;
        public static bool IsEvent(ushort weather) => weather == 8014 ||
            (weather >= 8027 && weather <= 8032) || weather == 8065 || weather == 8066;
    }
}
