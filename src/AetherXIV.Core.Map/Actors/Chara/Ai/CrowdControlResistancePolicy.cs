using System;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.actors.chara.ai
{
    // Patch 1.21 dev1071: per-target/category, full -> half -> quarter -> immune.
    // The numbered cooldown procedure starts a window at the first success;
    // subsequent successes do not extend that window.
    internal sealed class CrowdControlResistancePolicy
    {
        private readonly Dictionary<StatusEffectId, (DateTime Expires, int Successes)> windows = new();

        private static (StatusEffectId Category, int Seconds) GetCategory(uint id) => (StatusEffectId)id switch
        {
            StatusEffectId.Heavy => (StatusEffectId.Heavy, 180),
            StatusEffectId.Slow => (StatusEffectId.Slow, 180),
            StatusEffectId.Petrification => (StatusEffectId.Petrification, 300),
            StatusEffectId.Paralysis => (StatusEffectId.Paralysis, 180),
            StatusEffectId.Silence => (StatusEffectId.Silence, 180),
            StatusEffectId.Blind => (StatusEffectId.Blind, 180),
            StatusEffectId.Pacification => (StatusEffectId.Pacification, 300),
            StatusEffectId.Amnesia => (StatusEffectId.Amnesia, 300),
            StatusEffectId.Stun => (StatusEffectId.Stun, 30),
            StatusEffectId.Bind or StatusEffectId.Bind2 => (StatusEffectId.Bind, 180),
            StatusEffectId.Sleep => (StatusEffectId.Sleep, 180),
            _ => (default, 0)
        };

        // A negative result means immunity. Preview never consumes an application.
        internal double GetDuration(uint statusId, double duration, DateTime now)
        {
            var category = GetCategory(statusId);
            if (category.Seconds == 0 || !windows.TryGetValue(category.Category, out var window) || now >= window.Expires)
                return duration;
            return window.Successes >= 3 ? -1 : duration / (1 << window.Successes);
        }

        internal void RecordSuccess(uint statusId, DateTime now)
        {
            var category = GetCategory(statusId);
            if (category.Seconds == 0) return;
            if (!windows.TryGetValue(category.Category, out var window) || now >= window.Expires)
                windows[category.Category] = (now.AddSeconds(category.Seconds), 1);
            else
                windows[category.Category] = (window.Expires, Math.Min(3, window.Successes + 1));
        }
    }
}
