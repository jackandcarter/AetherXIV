using System;

namespace AetherXIV.Core.Map.actors.group
{
    /// <summary>
    /// Composes protocol-visible group IDs without allowing one group's type
    /// prefix to leak into a later allocation.
    /// </summary>
    sealed class RuntimeGroupIdAllocator
    {
        public const ulong TypeMask = 0xF000000000000000;
        public const ulong PayloadMask = 0x0FFFFFFFFFFFFFFF;
        public const ulong RelationType = 0x0000000000000000;
        public const ulong GuildleveContentType = 0x2000000000000000;
        public const ulong ContentType = 0x3000000000000000;

        private ulong nextPayload = 1;

        public ulong AllocateRelation()
        {
            return Allocate(RelationType);
        }

        public ulong AllocateContent()
        {
            return Allocate(ContentType);
        }

        public ulong AllocateGuildleveContent()
        {
            return Allocate(GuildleveContentType);
        }

        private ulong Allocate(ulong type)
        {
            if ((type & ~TypeMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(type));
            if (nextPayload == 0 || nextPayload > PayloadMask)
                throw new InvalidOperationException("The runtime group identity space is exhausted.");

            return type | nextPayload++;
        }
    }
}
