using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace AspectsColorless.AspectsColorlessCode.Patches;


public static class CardPoolModStore
{
    private sealed record Modification
    {
        public HashSet<ModelId> AddedCards { get; init; } = new();
        public HashSet<ModelId> RemovedCards { get; init; } = new();
        public bool IsEmpty => AddedCards.Count == 0 && RemovedCards.Count == 0;
    }
    public sealed record SerializableMods { public Dictionary<string, SerializablePoolMod> Pools { get; set; } = new(); }
    public sealed record SerializablePoolMod { public List<string> AddedCardIds { get; set; } = new(); public List<string> RemovedCardIds { get; set; } = new(); }

    private static readonly Lock Lock = new();
    private static readonly Dictionary<string, Modification> Modifications = new();
    private static readonly ConcurrentDictionary<ModelId, CardPoolModel> VisualCache = new();

    public static CardPoolModel? GetVisualPool(ModelId cardId) => VisualCache.GetValueOrDefault(cardId);

    public static void AddToCardPool(CardModel card, CardPoolModel pool)
    {
        lock (Lock)
        {
            var mod = GetOrCreateLocked(pool.GetType().FullName!);
            mod.RemovedCards.Remove(card.Id);
            mod.AddedCards.Add(card.Id);
            VisualCache[card.Id] = pool;
        }
    }

    public static void RemoveFromCardPool(CardModel card, CardPoolModel pool)
    {
        lock (Lock)
        {
            var mod = GetOrCreateLocked(pool.GetType().FullName!);
            mod.AddedCards.Remove(card.Id);
            mod.RemovedCards.Add(card.Id);
            VisualCache.TryRemove(card.Id, out _);
        }
    }

    public static void ClearAll()
    {
        lock (Lock)
        {
            Modifications.Clear();
            VisualCache.Clear();
        }
    }

    public static void ApplyModifications(CardPoolModel pool, List<CardModel> cards)
    {
        Modification? mod;
        lock (Lock)
        {
            if (!Modifications.TryGetValue(pool.GetType().FullName!, out mod)) return;
        }

        if (mod.RemovedCards.Count > 0)
            cards.RemoveAll(c => mod.RemovedCards.Contains(c.Id));

        if (mod.AddedCards.Count > 0)
        {
            var existingIds = new HashSet<ModelId>(cards.Select(c => c.Id));
            foreach (var id in mod.AddedCards)
            {
                if (existingIds.Contains(id)) continue;
                try
                {
                    var canonical = ModelDb.GetByIdOrNull<CardModel>(id);
                    if (canonical != null)
                        cards.Add(canonical);
                    else
                        Log.Warn($"[CardPoolModStore] Cannot resolve card id '{id}' — skipped");
                }
                catch (Exception ex)
                {
                    Log.Error($"[CardPoolModStore] Error resolving card id '{id}': {ex.Message}");
                }
            }
        }
    }

    public static SerializableMods? Serialize()
    {
        lock (Lock)
        {
            var result = new SerializableMods();
            foreach (var (poolType, mod) in Modifications)
            {
                if (mod.IsEmpty) continue;
                result.Pools[poolType] = new SerializablePoolMod
                {
                    AddedCardIds = mod.AddedCards.Select(id => id.ToString()).ToList(),
                    RemovedCardIds = mod.RemovedCards.Select(id => id.ToString()).ToList(),
                };
            }
            return result.Pools.Count > 0 ? result : null;
        }
    }

    public static void Deserialize(SerializableMods? data)
    {
        lock (Lock)
        {
            Modifications.Clear();
            VisualCache.Clear();
            if (data == null || data.Pools.Count == 0) return;

            foreach (var (poolType, serMod) in data.Pools)
            {
                try
                {
                    var addedCards = new HashSet<ModelId>();
                    foreach (var s in serMod.AddedCardIds)
                    {
                        try { addedCards.Add(ModelId.Deserialize(s)); }
                        catch (Exception ex) { Log.Warn($"[CardPoolModStore] Skipping invalid added card id '{s}': {ex.Message}"); }
                    }
                    var removedCards = new HashSet<ModelId>();
                    foreach (var s in serMod.RemovedCardIds)
                    {
                        try { removedCards.Add(ModelId.Deserialize(s)); }
                        catch (Exception ex) { Log.Warn($"[CardPoolModStore] Skipping invalid removed card id '{s}': {ex.Message}"); }
                    }
                    Modifications[poolType] = new Modification { AddedCards = addedCards, RemovedCards = removedCards };

                    CardPoolModel? resolvedPool = null;
                    foreach (var p in ModelDb.AllCardPools)
                    {
                        if (p.GetType().FullName == poolType) { resolvedPool = p; break; }
                    }
                    if (resolvedPool != null)
                    {
                        foreach (var cardId in addedCards)
                            VisualCache[cardId] = resolvedPool;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CardPoolModStore] Error deserializing mods for pool '{poolType}': {ex.Message}");
                }
            }
        }
    }

    private static Modification GetOrCreateLocked(string poolTypeName)
    {
        if (!Modifications.TryGetValue(poolTypeName, out var mod))
        {
            mod = new Modification();
            Modifications[poolTypeName] = mod;
        }
        return mod;
    }
}


public static class CardPoolSavePersistence
{
    private const string ModsJsonProperty = "ac_card_pool_mods";

    public static async Task InjectModsIntoSave(bool isMultiplayer)
    {
        try
        {
            var saveStore = GetSaveStore();
            if (saveStore == null) return;

            int profileId = SaveManager.Instance.CurrentProfileId;
            string fileName = isMultiplayer ? "current_run_mp.save" : "current_run.save";
            string relativePath = RunSaveManager.GetRunSavePath(profileId, fileName);

            string? json = await saveStore.ReadFileAsync(relativePath);
            if (json == null) return;

            JsonNode? root = JsonNode.Parse(json);
            if (root is not JsonObject obj) return;

            var mods = CardPoolModStore.Serialize();
            if (mods != null)
                obj[ModsJsonProperty] = JsonSerializer.SerializeToNode(mods);
            else
                obj.Remove(ModsJsonProperty);

            await saveStore.WriteFileAsync(relativePath, obj.ToJsonString());
        }
        catch (Exception ex)
        {
            Log.Error($"[CardPoolSavePersistence] Failed to inject mods into save: {ex.Message}");
        }
    }

    public static void RestoreModsFromSave(bool isMultiplayer)
    {
        try
        {
            var saveStore = GetSaveStore();
            if (saveStore == null) return;

            int profileId = SaveManager.Instance.CurrentProfileId;
            string fileName = isMultiplayer ? "current_run_mp.save" : "current_run.save";
            string relativePath = RunSaveManager.GetRunSavePath(profileId, fileName);

            if (!saveStore.FileExists(relativePath)) return;

            string? json = saveStore.ReadFile(relativePath);
            if (json == null) return;

            JsonNode? root = JsonNode.Parse(json);
            if (root is not JsonObject obj) return;

            if (!obj.TryGetPropertyValue(ModsJsonProperty, out var modsNode) || modsNode == null)
                return;

            var data = modsNode.Deserialize<CardPoolModStore.SerializableMods>();
            if (data != null)
                CardPoolModStore.Deserialize(data);
        }
        catch (Exception ex)
        {
            Log.Error($"[CardPoolSavePersistence] Failed to extract card pool mods from save: {ex.Message}");
        }
    }

    private static ISaveStore? GetSaveStore()
    {
        var field = typeof(SaveManager).GetField("_saveStore",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(SaveManager.Instance) as ISaveStore;
    }
}


public static class CardPoolNetSync
{
    /// <summary>"ACMP" — magic marker for detecting postfix ordering conflicts.</summary>
    private const int NetMagic = 0x41434D50;
    private static CardPoolModStore.SerializableMods? _pendingNetworkMods;

    public static void Write(PacketWriter writer)
    {
        var mods = CardPoolModStore.Serialize();
        writer.WriteBool(mods != null);
        if (mods != null)
        {
            writer.WriteInt(NetMagic);
            writer.WriteString(JsonSerializer.Serialize(mods));
        }
    }

    public static void Read(PacketReader reader)
    {
        if (!reader.ReadBool())
        {
            Store(null);
            return;
        }

        int magic = reader.ReadInt();
        if (magic != NetMagic)
        {
            Log.Error(
                $"[CardPoolNetSync] Network magic mismatch! " +
                $"Expected 0x{NetMagic:X8} (\"ACMP\"), got 0x{magic:X8}. " +
                "Possible Harmony postfix ordering conflict with another mod — " +
                "card pool mods disabled for this session.");
            Store(null);
            return;
        }

        string json = reader.ReadString();
        try
        {
            Store(JsonSerializer.Deserialize<CardPoolModStore.SerializableMods>(json));
        }
        catch (JsonException ex)
        {
            Log.Error(
                $"[CardPoolNetSync] Failed to parse network mod JSON: {ex.Message}. " +
                $"Raw (first 200 chars): {json[..Math.Min(json.Length, 200)]}");
            Store(null);
        }
    }

    private static void Store(CardPoolModStore.SerializableMods? mods)
    {
        if (_pendingNetworkMods != null)
            Log.Warn("[CardPoolNetSync] Overwriting non-null pending mods — was RestorePending() not called?");
        _pendingNetworkMods = mods;
    }

    public static bool RestorePending()
    {
        if (_pendingNetworkMods == null) return false;
        CardPoolModStore.Deserialize(_pendingNetworkMods);
        _pendingNetworkMods = null;
        return true;
    }

    public static void ClearPending()
        => _pendingNetworkMods = null;
}


[HarmonyPatch]
public static class CardPoolPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
    private static void GetUnlockedCards_Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        var cards = __result as List<CardModel> ?? __result.ToList();
        CardPoolModStore.ApplyModifications(__instance, cards);
        __result = cards;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.VisualCardPool), MethodType.Getter)]
    private static void VisualCardPool_Getter_Postfix(CardModel __instance, ref CardPoolModel __result)
    {
        var pool = CardPoolModStore.GetVisualPool(__instance.Id);
        if (pool != null)
            __result = pool;
    }

    
    // Save
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), typeof(SerializableRun), typeof(bool))]
    public static void RunSaveManager_SaveRun_Postfix(ref Task __result, bool isMultiplayer)
    {
        __result = SaveRun_Postfix_Awaited(__result, isMultiplayer);
    }

    private static async Task SaveRun_Postfix_Awaited(Task originalTask, bool isMultiplayer)
    {
        await originalTask;
        await CardPoolSavePersistence.InjectModsIntoSave(isMultiplayer);
    }

    
    // New game
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
    private static void SetUpNewSingleplayer_Prefix()
        => CardPoolModStore.ClearAll();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
    private static void SetUpNewMultiplayer_Prefix()
        => CardPoolModStore.ClearAll();

    
    // Singleplayer Loading
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))]
    private static void SetUpSavedSingleplayer_Prefix()
        => CardPoolSavePersistence.RestoreModsFromSave(isMultiplayer: false);

    
    // Multiplayer Loading, Host (Early)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadAndCanonicalizeMultiplayerRunSave))]
    private static void LoadAndCanonicalizeMultiplayerRunSave_Postfix(ReadSaveResult<SerializableRun> __result)
    {
        if (__result?.Success == true)
            CardPoolSavePersistence.RestoreModsFromSave(isMultiplayer: true);
    }

    
    // Multiplayer Loading, Client (Late)
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
    private static void SetUpSavedMultiplayer_Prefix(LoadRunLobby lobby)
    {
        if (lobby.NetService.Type == NetGameType.Client)
            CardPoolNetSync.RestorePending();
    }

    
    // Multiplayer Loading, Sync (Between)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientLoadJoinResponseMessage), nameof(ClientLoadJoinResponseMessage.Serialize))]
    private static void ClientLoadJoinResponse_Serialize_Postfix(PacketWriter writer)
        => CardPoolNetSync.Write(writer);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientLoadJoinResponseMessage), nameof(ClientLoadJoinResponseMessage.Deserialize))]
    private static void ClientLoadJoinResponse_Deserialize_Postfix(PacketReader reader)
        => CardPoolNetSync.Read(reader);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientRejoinResponseMessage), nameof(ClientRejoinResponseMessage.Serialize))]
    private static void ClientRejoinResponse_Serialize_Postfix(PacketWriter writer)
        => CardPoolNetSync.Write(writer);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientRejoinResponseMessage), nameof(ClientRejoinResponseMessage.Deserialize))]
    private static void ClientRejoinResponse_Deserialize_Postfix(PacketReader reader)
        => CardPoolNetSync.Read(reader);

    
    // Cleanup
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    private static void RunManager_CleanUp_Postfix()
    {
        CardPoolModStore.ClearAll();
        CardPoolNetSync.ClearPending();
    }
}