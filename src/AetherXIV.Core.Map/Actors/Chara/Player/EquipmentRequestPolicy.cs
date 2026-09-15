namespace AetherXIV.Core.Map.actors.chara.player
{
    internal enum EquipmentRequestRejection
    {
        None,
        SlotOutOfRange,
        WrongActor,
        WrongPackage,
        MissingItem,
        ItemReferenceMismatch,
        OwnershipMismatch,
        NotEquipment,
        ItemTypeMismatch,
        EquipPointMismatch,
        AlreadyEquipped
    }

    // Server-authoritative checks for the fields carried by retail
    // EquipCommand. The policy is deliberately data-only so the recovered
    // wire contract can be tested without constructing a live Player.
    internal static class EquipmentRequestPolicy
    {
        public const ushort EquipCommandId = 12009;
        public const uint EquipAnimationId = 0x7C000062;
        public const byte EquipSubstateWaste = 12;

        public static EquipmentRequestRejection Validate(
            int requestedEquipPoint,
            int equipmentCapacity,
            uint playerActorId,
            uint referenceActorId,
            ushort referencePackage,
            ushort normalPackage,
            ushort referenceSlot,
            bool itemExists,
            ulong echoedItemId,
            ulong actualItemId,
            bool ownershipMatches,
            ushort actualPackage,
            ushort actualSlot,
            bool isEquipment,
            bool itemTypeMatchesSlot,
            bool requiresEquipPointMatch,
            int itemEquipPoint,
            bool equippedElsewhere)
        {
            if (requestedEquipPoint <= 0 || requestedEquipPoint > equipmentCapacity)
                return EquipmentRequestRejection.SlotOutOfRange;
            if (referenceActorId != playerActorId)
                return EquipmentRequestRejection.WrongActor;
            if (referencePackage != normalPackage)
                return EquipmentRequestRejection.WrongPackage;
            if (!itemExists)
                return EquipmentRequestRejection.MissingItem;
            if (echoedItemId != actualItemId)
                return EquipmentRequestRejection.ItemReferenceMismatch;
            if (!ownershipMatches || actualPackage != referencePackage || actualSlot != referenceSlot)
                return EquipmentRequestRejection.OwnershipMismatch;
            if (!isEquipment)
                return EquipmentRequestRejection.NotEquipment;
            if (!itemTypeMatchesSlot)
                return EquipmentRequestRejection.ItemTypeMismatch;
            if (requiresEquipPointMatch && itemEquipPoint != requestedEquipPoint)
                return EquipmentRequestRejection.EquipPointMismatch;
            if (equippedElsewhere)
                return EquipmentRequestRejection.AlreadyEquipped;

            return EquipmentRequestRejection.None;
        }

        public static uint PackAppearance(
            uint weaponId,
            uint equipmentId,
            uint variantId,
            uint colorId)
        {
            uint mixedVariantId = weaponId == 0
                ? ((variantId & 0x1F) << 5) | colorId
                : variantId;

            return (weaponId & 0x3FF) << 20
                | (equipmentId & 0x3FF) << 10
                | (mixedVariantId & 0x3FF);
        }
    }
}
