using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
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

    // -- 内部状态 --

    private static readonly Lock Lock = new();
    private static readonly Dictionary<string, Modification> Modifications = new();

    // -- 视觉池缓存（供 VisualCardPool getter 高性能查询） --

    private static readonly ConcurrentDictionary<ModelId, CardPoolModel> VisualCache = new();
    public static CardPoolModel? GetVisualPool(ModelId cardId) => VisualCache.GetValueOrDefault(cardId);

    // -- 公共命令（供 CardPoolCmd 转发） --

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

    // -- Patch 查询 API --

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

    // -- 序列化 --

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

    // -- 私有辅助（调用方必须持有 Lock） --

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

    public static void InjectModsIntoSave(bool isMultiplayer)
    {
        try
        {
            string path = GetSavePath(isMultiplayer);
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            JsonNode? root = JsonNode.Parse(json);
            if (root is not JsonObject obj) return;

            var mods = CardPoolModStore.Serialize();
            if (mods != null)
                obj[ModsJsonProperty] = JsonSerializer.SerializeToNode(mods);
            else
                obj.Remove(ModsJsonProperty);

            File.WriteAllText(path, obj.ToJsonString());
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
            string path = GetSavePath(isMultiplayer);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                JsonNode? root = JsonNode.Parse(json);
                if (root is JsonObject obj
                    && obj.TryGetPropertyValue(ModsJsonProperty, out var modsNode)
                    && modsNode != null)
                {
                    var data = modsNode.Deserialize<CardPoolModStore.SerializableMods>();
                    if (data != null)
                    {
                        CardPoolModStore.Deserialize(data);
                        return;
                    }
                }
            }
            CardPoolModStore.ClearAll();
        }
        catch (Exception ex)
        {
            Log.Error($"[CardPoolSavePersistence] Failed to extract card pool mods from save: {ex.Message}");
        }
    }

    // I think this thing is kinda... dangerous. Who knows.
    public static string GetSavePath(bool isMultiplayer)
    {
        int profileId = SaveManager.Instance.CurrentProfileId;
        string fileName = isMultiplayer ? "current_run_mp.save" : "current_run.save";
        string relativePath = RunSaveManager.GetRunSavePath(profileId, fileName);
        string godotPath = UserDataPathProvider.GetAccountScopedBasePath(null) + "/" + relativePath;
        return Godot.ProjectSettings.GlobalizePath(godotPath);
    }
}


[HarmonyPatch]
public static class CardPoolPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
    private static void GetUnlockedCards_Postfix(
        CardPoolModel __instance,
        ref IEnumerable<CardModel> __result)
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

    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), typeof(SerializableRun), typeof(bool))]
    public static void RunSaveManager_SaveRun_Postfix(ref Task __result, bool isMultiplayer)
    {
        __result = SaveRun_Postfix_Awaited(__result, isMultiplayer);
    }

    private static async Task SaveRun_Postfix_Awaited(Task originalTask, bool isMultiplayer)
    {
        await originalTask;
        CardPoolSavePersistence.InjectModsIntoSave(isMultiplayer);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave))]
    private static void RunSaveManager_LoadRunSave_Postfix(ref ReadSaveResult<SerializableRun> __result)
    {
        if (__result?.Success == true)
            CardPoolSavePersistence.RestoreModsFromSave(isMultiplayer: false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadMultiplayerRunSave))]
    private static void RunSaveManager_LoadMultiplayerRunSave_Postfix(ref ReadSaveResult<SerializableRun> __result)
    {
        if (__result?.Success == true)
            CardPoolSavePersistence.RestoreModsFromSave(isMultiplayer: true);
    }

    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    private static void RunManager_CleanUp_Postfix()
        => CardPoolModStore.ClearAll();

}