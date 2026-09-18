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
            if (requiresEquipPointMatch && !FitsPoint(itemEquipPoint, requestedEquipPoint))
                return EquipmentRequestRejection.EquipPointMismatch;
            if (equippedElsewhere)
                return EquipmentRequestRejection.AlreadyEquipped;

            return EquipmentRequestRejection.None;
        }

        // Installed 1.23b ItemBaseClass_common.getEquipmentEquipPointDetail,
        // source lines 2117-2288. Points are one-based, unlike package slots.
        public static bool FitsPoint(int itemPoint, int requestedPoint)
        {
            if (requestedPoint < 1 || requestedPoint > 27) return false;
            if (itemPoint >= 1 && itemPoint <= 27) return itemPoint == requestedPoint;
            switch (itemPoint)
            {
                case 34: return requestedPoint == 6 || requestedPoint == 7;
                case 35: return requestedPoint == 6;
                case 36: return requestedPoint == 1 || requestedPoint == 2;
                case 37: case 38: case 39: return requestedPoint == 1;
                case 40: return requestedPoint == 2;
                case 41: case 42: case 43: return requestedPoint == 11;
                case 44: return requestedPoint == 13;
                case 45: return requestedPoint == 20 || requestedPoint == 21;
                case 46: return requestedPoint == 20;
                case 47: return requestedPoint == 18 || requestedPoint == 19;
                case 48: return requestedPoint == 18;
                case 49: return requestedPoint >= 22 && requestedPoint <= 25;
                case 50: return requestedPoint == 22;
                case 51: return requestedPoint == 24 || requestedPoint == 25;
                case 53: return requestedPoint == 26 || requestedPoint == 27;
                case 54: return true;
                default: return false;
            }
        }

        public static int[] OccupiedPoints(int itemPoint, int requestedPoint)
        {
            switch (itemPoint)
            {
                case 35: return new[] { 6, 7 };
                case 37: case 38: return new[] { 1, 2 };
                case 39: return new[] { 1, 3 };
                case 40: return new[] { 2, 4 };
                case 41: return new[] { 9, 11, 13, 14, 15, 16 };
                case 42: return new[] { 9, 11 };
                case 43: return new[] { 11, 13, 14, 15 };
                case 44: return new[] { 13, 15 };
                case 46: return new[] { 20, 21 };
                case 48: return new[] { 18, 19 };
                case 50: return new[] { 22, 23 };
                default: return new[] { requestedPoint };
            }
        }

        public static bool MeetsRequiredLevel(int levelType, int itemLevel, int playerLevel)
            => levelType != 1 || playerLevel >= itemLevel;

        // ItemBaseClass_common.isConformTribe, source lines 2297-2389.
        public static bool FitsTribe(int restriction, int tribe)
        {
            if (restriction == 0) return false;
            if (restriction >= 1 && restriction <= 15) return restriction == tribe;
            switch (restriction)
            {
                case 16: return tribe == 1 || tribe == 3;
                case 17: return tribe == 2;
                case 18: return tribe == 4 || tribe == 6;
                case 19: return tribe == 5 || tribe == 7;
                case 20: return tribe == 8 || tribe == 10;
                case 21: return tribe == 9 || tribe == 11;
                case 22: return tribe == 14 || tribe == 15;
                case 23: return tribe == 12 || tribe == 13;
                case 24: return tribe == 1 || tribe == 2 || tribe == 3;
                case 25: return tribe >= 4 && tribe <= 7;
                case 26: return tribe >= 8 && tribe <= 11;
                case 27: return tribe == 1 || tribe == 3 || tribe == 4 || tribe == 6 || tribe == 8 || tribe == 10 || tribe == 14 || tribe == 15;
                case 28: return tribe == 2 || tribe == 5 || tribe == 7 || tribe == 9 || tribe == 11 || tribe == 12 || tribe == 13;
                default: return true;
            }
        }

        // Same catalog families previously selected in EquipCommand.lua.
        public static byte WeaponClass(uint catalogId)
        {
            switch (catalogId / 10000)
            {
                case 402: return 2; case 403: return 3; case 404: return 4;
                case 407: return 7; case 408: return 8;
                case 502: return 22; case 503: return 23;
                case 601: return 29; case 602: return 30; case 603: return 31;
                case 604: return 32; case 605: return 33; case 606: return 34;
                case 607: return 35; case 608: return 36;
                case 701: return 39; case 702: return 40; case 703: return 41;
                default: return 0;
            }
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
