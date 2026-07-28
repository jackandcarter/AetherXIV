using System;

namespace AetherXIV.Core.Map.actors.chara.ai.utils
{
    /// <summary>
    /// Selects the authoritative raw input for an autoattack. The installed
    /// 1.23b item sheet supplies weapon and virtual-ammo damage, while battle
    /// NPC data supplies Attack. The retail class-bonus coefficients remain
    /// deliberately deferred until they can be recovered from official
    /// captures; this policy prevents the old universal 100-potency placeholder
    /// from overriding all of those source values.
    /// </summary>
    static class AutoAttackPotencyPolicy
    {
        public static ushort Calculate(int weaponDamagePower, int ammoVirtualDamagePower, double attack)
        {
            int weaponBase = Math.Max(0, weaponDamagePower) + Math.Max(0, ammoVirtualDamagePower);
            if (weaponBase > 0)
                return (ushort)Math.Min(weaponBase, 9999);

            int actorBase = (int)Math.Round(Math.Max(0, attack));
            return (ushort)Math.Max(1, Math.Min(actorBase, 9999));
        }
    }
}
