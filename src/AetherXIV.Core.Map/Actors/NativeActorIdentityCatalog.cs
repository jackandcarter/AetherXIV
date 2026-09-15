using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AetherXIV.Core.Map.Actors
{
    sealed class NativeActorIdentityDocument
    {
        public string schema;
        public List<NativeActorTerritoryIdentity> territories;
    }

    sealed class NativeActorTerritoryIdentity
    {
        public uint territoryId;
        public NativeActorPublicAreaIdentity publicArea;
        public List<NativeStaticActorIdentity> staticActorAssignments;
        public List<NativeResidentDirectorIdentity> residentDirectors;
    }

    sealed class NativeActorPublicAreaIdentity
    {
        public string privateAreaName;
        public uint privateAreaLevel;
        public uint areaMasterNativeSlot;
    }

    sealed class NativeStaticActorIdentity
    {
        public uint spawnId;
        public uint nativeActorSlot;
    }

    sealed class NativeResidentDirectorIdentity
    {
        public string scriptPath;
        public uint nativeActorSlot;
        public uint nativeClassId;
        public string nativeClassPath;
    }

    /// <summary>
    /// Loads the reviewed actor-identity catalog embedded in the shipping Map
    /// assembly. This is the sole runtime authority for territories whose
    /// native public-area namespace has been verified.
    /// </summary>
    static class NativeActorIdentityCatalog
    {
        internal const string ExpectedSchema = "aetherxiv.native-actor-identities.v2";
        private const string ResourceName = "AetherXIV.NativeActorIdentities.json";

        private static readonly Lazy<IReadOnlyDictionary<uint, NativeActorTerritoryIdentity>> Territories =
            new Lazy<IReadOnlyDictionary<uint, NativeActorTerritoryIdentity>>(LoadAndValidate);

        public static bool TryGetTerritory(
            uint territoryId,
            out NativeActorTerritoryIdentity identity)
        {
            return Territories.Value.TryGetValue(territoryId, out identity);
        }

        public static bool TryGetResidentDirector(
            uint territoryId,
            string scriptPath,
            out NativeResidentDirectorIdentity identity)
        {
            identity = null;
            if (!TryGetTerritory(territoryId, out NativeActorTerritoryIdentity territory))
                return false;

            identity = territory.residentDirectors.FirstOrDefault(
                row => String.Equals(row.scriptPath, scriptPath, StringComparison.Ordinal));
            return identity != null;
        }

        internal static NativeActorIdentityDocument ParseAndValidate(string json)
        {
            NativeActorIdentityDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<NativeActorIdentityDocument>(json);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Native actor identity catalog is not valid JSON.", exception);
            }

            if (document == null
                || !String.Equals(document.schema, ExpectedSchema, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Native actor identity catalog schema is unsupported.");
            }
            if (document.territories == null || document.territories.Count == 0)
                throw new InvalidDataException("Native actor identity catalog contains no territories.");

            HashSet<uint> territoryIds = new HashSet<uint>();
            foreach (NativeActorTerritoryIdentity territory in document.territories)
            {
                if (territory == null
                    || territory.territoryId == 0
                    || territory.territoryId > NativeActorId.MaximumTerritoryId
                    || !territoryIds.Add(territory.territoryId))
                {
                    throw new InvalidDataException("Native actor identity catalog contains an invalid or duplicate territory.");
                }
                if (territory.publicArea == null
                    || !String.IsNullOrEmpty(territory.publicArea.privateAreaName)
                    || territory.publicArea.privateAreaLevel != 0)
                {
                    throw new InvalidDataException(String.Format(
                        "Territory {0} does not define the public-area identity scope.",
                        territory.territoryId));
                }

                Dictionary<uint, string> claimedSlots = new Dictionary<uint, string>();
                ClaimSlot(
                    claimedSlots,
                    territory.publicArea.areaMasterNativeSlot,
                    "area master",
                    territory.territoryId);

                territory.staticActorAssignments ??= new List<NativeStaticActorIdentity>();
                HashSet<uint> spawnIds = new HashSet<uint>();
                foreach (NativeStaticActorIdentity actor in territory.staticActorAssignments)
                {
                    if (actor == null || actor.spawnId == 0 || !spawnIds.Add(actor.spawnId))
                    {
                        throw new InvalidDataException(String.Format(
                            "Territory {0} contains an invalid or duplicate static actor assignment.",
                            territory.territoryId));
                    }
                    ClaimSlot(
                        claimedSlots,
                        actor.nativeActorSlot,
                        "static actor " + actor.spawnId,
                        territory.territoryId);
                }

                territory.residentDirectors ??= new List<NativeResidentDirectorIdentity>();
                HashSet<string> scriptPaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (NativeResidentDirectorIdentity director in territory.residentDirectors)
                {
                    if (director == null
                        || String.IsNullOrWhiteSpace(director.scriptPath)
                        || !scriptPaths.Add(director.scriptPath)
                        || director.nativeClassId == 0
                        || String.IsNullOrWhiteSpace(director.nativeClassPath)
                        || !director.nativeClassPath.StartsWith("/", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(String.Format(
                            "Territory {0} contains an invalid or duplicate resident director.",
                            territory.territoryId));
                    }
                    ClaimSlot(
                        claimedSlots,
                        director.nativeActorSlot,
                        "resident director " + director.scriptPath,
                        territory.territoryId);
                }
            }

            return document;
        }

        private static IReadOnlyDictionary<uint, NativeActorTerritoryIdentity> LoadAndValidate()
        {
            Assembly assembly = typeof(NativeActorIdentityCatalog).Assembly;
            using Stream stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream == null)
                throw new InvalidDataException("Shipping Map assembly is missing its native actor identity catalog.");
            using StreamReader reader = new StreamReader(stream);
            NativeActorIdentityDocument document = ParseAndValidate(reader.ReadToEnd());
            return document.territories.ToDictionary(row => row.territoryId);
        }

        private static void ClaimSlot(
            IDictionary<uint, string> claimedSlots,
            uint nativeSlot,
            string owner,
            uint territoryId)
        {
            if (nativeSlot == 0 || nativeSlot > NativeActorId.MaximumSlot)
            {
                throw new InvalidDataException(String.Format(
                    "Territory {0} has an invalid native actor slot for {1}.",
                    territoryId,
                    owner));
            }
            if (claimedSlots.TryGetValue(nativeSlot, out string existingOwner))
            {
                throw new InvalidDataException(String.Format(
                    "Territory {0} native actor slot 0x{1:X} is claimed by {2} and {3}.",
                    territoryId,
                    nativeSlot,
                    existingOwner,
                    owner));
            }
            claimedSlots.Add(nativeSlot, owner);
        }
    }
}
