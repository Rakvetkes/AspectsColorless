using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace AspectsColorless.AspectsColorlessCode.Patches;

// ═══════════════════════════════════════════════════════════════
// CardPoolModStore — 状态存储 + 锁 + CRUD + 序列化引擎
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// 卡牌池修改的全局状态管理器。
/// 所有对 <see cref="CardPoolModel"/> 的运行时增删都通过此类完成，
/// 包含线程安全保护和完整的序列化支持。
/// </summary>
public static class CardPoolModStore
{
    // -- 内嵌数据模型 --

    /// <summary>单个玩家在单个卡牌池上的增删记录。</summary>
    public sealed class Modification
    {
        public HashSet<ModelId> AddedCards { get; init; } = new();
        public HashSet<ModelId> RemovedCards { get; init; } = new();
        public bool IsEmpty => AddedCards.Count == 0 && RemovedCards.Count == 0;
    }

    /// <summary>单个玩家全部卡牌池修改的 JSON 序列化容器。</summary>
    public sealed class SerializableMods
    {
        public Dictionary<string, SerializablePoolMod> Pools { get; set; } = new();
    }

    /// <summary>单个卡牌池修改的 JSON 序列化容器。</summary>
    public sealed class SerializablePoolMod
    {
        public List<string> AddedCardIds { get; set; } = new();
        public List<string> RemovedCardIds { get; set; } = new();
    }

    /// <summary><see cref="ConditionalWeakTable{TKey, TValue}"/> 要求引用类型值，用于包装 ulong。</summary>
    private sealed class NetIdBox(ulong netId)
    {
        public readonly ulong NetId = netId;
    }

    // -- 内部状态 --

    /// <summary>Key: Player.NetId → (Key: CardPoolModel.FullName → Modification).</summary>
    private static readonly Dictionary<ulong, Dictionary<string, Modification>> _modifications = new();

    /// <summary>UnlockState → NetId 映射，使用引用相等比较。</summary>
    private static readonly ConditionalWeakTable<UnlockState, NetIdBox> _unlockStateMap = new();

    /// <summary>全局锁：保护 _modifications 和 _unlockStateMap 的所有读写。</summary>
    private static readonly object _lock = new();

    // -- 视觉池缓存（供 VisualCardPool getter 高性能查询） --

    /// <summary>哪些 player.NetId 有动态卡牌池修改。</summary>
    private static readonly ConcurrentDictionary<ulong, byte> _playersWithMods = new();

    /// <summary>(netId, cardId) → 动态视觉池。修改时定点写入，查询时 O(1) 无锁读。</summary>
    private static readonly ConcurrentDictionary<(ulong NetId, ModelId CardId), CardPoolModel> _visualCache = new();

    /// <summary>O(1) 前置短路：该玩家是否有任何动态池修改。</summary>
    public static bool HasModifications(ulong netId) => _playersWithMods.ContainsKey(netId);

    /// <summary>O(1) 查询某张牌是否被动态分配到了其他池。</summary>
    public static CardPoolModel? GetVisualPool(ulong netId, ModelId cardId) =>
        _visualCache.TryGetValue((netId, cardId), out var pool) ? pool : null;

    /// <summary>根据类型的 FullName 反查 CardPoolModel 实例。</summary>
    private static CardPoolModel? ResolvePool(string poolTypeName)
    {
        foreach (var pool in ModelDb.AllCardPools)
        {
            if (pool.GetType().FullName == poolTypeName)
                return pool;
        }
        return null;
    }

    // -- 公共命令（供 CardPoolCmd 转发） --

    public static void AddToCardPool(Player player, CardModel card, string poolTypeName)
    {
        var resolvedPool = ResolvePool(poolTypeName);

        lock (_lock)
        {
            RegisterPlayerLocked(player);
            var mod = GetOrCreateLocked(player.NetId, poolTypeName);
            mod.RemovedCards.Remove(card.Id);
            mod.AddedCards.Add(card.Id);

            if (resolvedPool != null)
                _visualCache[(player.NetId, card.Id)] = resolvedPool;
            _playersWithMods.TryAdd(player.NetId, 0);
        }
    }

    public static void RemoveFromCardPool(Player player, CardModel card, string poolTypeName)
    {
        lock (_lock)
        {
            RegisterPlayerLocked(player);
            var mod = GetOrCreateLocked(player.NetId, poolTypeName);
            mod.AddedCards.Remove(card.Id);
            mod.RemovedCards.Add(card.Id);

            _visualCache.TryRemove((player.NetId, card.Id), out _);
            _playersWithMods.TryAdd(player.NetId, 0);
        }
    }

    public static void ClearAll()
    {
        lock (_lock)
        {
            _modifications.Clear();
            _visualCache.Clear();
            _playersWithMods.Clear();
        }
    }

    // -- Patch 查询 API --

    public static bool TryGetNetId(UnlockState unlockState, out ulong netId)
    {
        lock (_lock)
        {
            if (_unlockStateMap.TryGetValue(unlockState, out var holder))
            {
                netId = holder.NetId;
                return true;
            }
        }
        netId = 0;
        return false;
    }

    /// <summary>将修改应用到卡牌列表（原地修改：先删后加）。</summary>
    public static void ApplyModifications(ulong netId, string poolTypeName, List<CardModel> cards)
    {
        Modification? mod;
        lock (_lock)
        {
            if (!_modifications.TryGetValue(netId, out var playerMods)) return;
            if (!playerMods.TryGetValue(poolTypeName, out mod)) return;
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

    public static void RegisterUnlockStateMapping(Player player)
    {
        lock (_lock)
        {
            _unlockStateMap.Remove(player.UnlockState);
            _unlockStateMap.Add(player.UnlockState, new NetIdBox(player.NetId));
        }
    }

    // -- 序列化 --

    public static SerializableMods? SerializeForPlayer(ulong netId)
    {
        lock (_lock)
        {
            if (!_modifications.TryGetValue(netId, out var playerMods))
                return null;

            var result = new SerializableMods();
            foreach (var (poolType, mod) in playerMods)
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

    public static void DeserializeForPlayer(ulong netId, SerializableMods? data)
    {
        lock (_lock)
        {
            // Remove old cache entries for this player
            var oldKeys = _visualCache.Keys.Where(k => k.NetId == netId).ToList();
            foreach (var key in oldKeys)
                _visualCache.TryRemove(key, out _);

            _modifications.Remove(netId);
            if (data == null || data.Pools.Count == 0)
            {
                _playersWithMods.TryRemove(netId, out _);
                return;
            }

            var restored = new Dictionary<string, Modification>();
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
                    restored[poolType] = new Modification { AddedCards = addedCards, RemovedCards = removedCards };

                    // Rebuild visual cache for this pool's added cards
                    var resolvedPool = ResolvePool(poolType);
                    if (resolvedPool != null)
                    {
                        foreach (var cardId in addedCards)
                            _visualCache[(netId, cardId)] = resolvedPool;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CardPoolModStore] Error deserializing mods for pool '{poolType}': {ex.Message}");
                }
            }
            if (restored.Count > 0)
            {
                _modifications[netId] = restored;
                _playersWithMods[netId] = 0;
            }
        }
    }

    public static Dictionary<ulong, SerializableMods> SerializeAll()
    {
        lock (_lock)
        {
            var result = new Dictionary<ulong, SerializableMods>();
            foreach (var netId in _modifications.Keys)
            {
                var data = SerializeForPlayer(netId);
                if (data != null) result[netId] = data;
            }
            return result;
        }
    }

    public static void DeserializeAll(Dictionary<ulong, SerializableMods> data)
    {
        lock (_lock)
        {
            _modifications.Clear();
            foreach (var (netId, playerData) in data)
                DeserializeForPlayer(netId, playerData);
        }
    }

    // -- 私有辅助（调用方必须持有 _lock） --

    private static void RegisterPlayerLocked(Player player)
    {
        _unlockStateMap.Remove(player.UnlockState);
        _unlockStateMap.Add(player.UnlockState, new NetIdBox(player.NetId));
    }

    private static Modification GetOrCreateLocked(ulong netId, string poolTypeName)
    {
        if (!_modifications.TryGetValue(netId, out var playerMods))
        {
            playerMods = new Dictionary<string, Modification>();
            _modifications[netId] = playerMods;
        }
        if (!playerMods.TryGetValue(poolTypeName, out var mod))
        {
            mod = new Modification();
            playerMods[poolTypeName] = mod;
        }
        return mod;
    }
}

// ═══════════════════════════════════════════════════════════════
// CardPoolSavePersistence — 存档文件读写
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// 将卡牌池修改注入到运行存档文件中，支持单机和多人两条路径。
/// 游戏的 JSON 反序列化器忽略未知属性，因此嵌入的额外字段对游戏透明。
/// </summary>
public static class CardPoolSavePersistence
{
    private const string ModsJsonProperty = "ac_card_pool_mods";

    /// <summary>将当前修改序列化后注入/清除存档 JSON。</summary>
    public static void InjectModsIntoSave(bool isMultiplayer)
    {
        try
        {
            string path = GetSavePath(isMultiplayer);
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            JsonNode? root = JsonNode.Parse(json);
            if (root is not JsonObject obj) return;

            var allMods = CardPoolModStore.SerializeAll();
            if (allMods.Count > 0)
                obj[ModsJsonProperty] = JsonSerializer.SerializeToNode(allMods);
            else
                obj.Remove(ModsJsonProperty);

            File.WriteAllText(path, obj.ToJsonString());
        }
        catch (Exception ex)
        {
            Log.Error($"[CardPoolSavePersistence] Failed to inject mods into save: {ex.Message}");
        }
    }

    /// <summary>从存档 JSON 中提取并恢复修改。</summary>
    public static void RestoreModsFromSave(bool isMultiplayer)
    {
        try
        {
            string path = GetSavePath(isMultiplayer);
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            JsonNode? root = JsonNode.Parse(json);
            if (root is not JsonObject obj) return;

            if (obj.TryGetPropertyValue(ModsJsonProperty, out var modsNode) && modsNode != null)
            {
                var data = modsNode.Deserialize<Dictionary<ulong, CardPoolModStore.SerializableMods>>();
                if (data != null && data.Count > 0)
                {
                    CardPoolModStore.DeserializeAll(data);
                    Log.Info($"[CardPoolSavePersistence] Restored {data.Count} player(s) card pool mods from save.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[CardPoolSavePersistence] Failed to extract card pool mods from save: {ex.Message}");
        }
    }

    /// <summary>计算运行存档的绝对文件路径。<see cref="RunSaveManager"/> 相关部分的复制。</summary>
    public static string GetSavePath(bool isMultiplayer)
    {
        int profileId = SaveManager.Instance.CurrentProfileId;
        string fileName = isMultiplayer ? "current_run_mp.save" : "current_run.save";
        return RunSaveManager.GetRunSavePath(profileId, fileName);
    }
}

// ═══════════════════════════════════════════════════════════════
// PendingBinarySync — 网络包桥接器（线程安全）
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// 在 <see cref="SerializablePlayer.Deserialize"/>（生产者，可能在网络线程）
/// 和 <see cref="Player.FromSerializable"/> / <see cref="Player.SyncWithSerializedPlayer"/>（消费者，主线程）
/// 之间传递卡牌池修改数据。
/// 每条数据只消费一次，消费后自动从存储中移除。
/// </summary>
public static class PendingBinarySync
{
    private static readonly Dictionary<ulong, CardPoolModStore.SerializableMods> _pending = new();
    private static readonly Lock _lock = new();

    public static void Store(ulong netId, CardPoolModStore.SerializableMods? mods)
    {
        lock (_lock)
        {
            if (mods != null)
                _pending[netId] = mods;
            else
                _pending.Remove(netId);
        }
    }

    public static CardPoolModStore.SerializableMods? Consume(ulong netId)
    {
        lock (_lock)
        {
            if (_pending.Remove(netId, out var mods))
                return mods;
            return null;
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// CardPoolPatch — Harmony 胶水（纯转发）
// ═══════════════════════════════════════════════════════════════

[HarmonyPatch]
public static class CardPoolPatch
{
    // -- 核心：拦截卡池查询 --

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
    private static void GetUnlockedCards_Postfix(
        CardPoolModel __instance,
        UnlockState unlockState,
        ref IEnumerable<CardModel> __result)
    {
        if (!CardPoolModStore.TryGetNetId(unlockState, out ulong netId))
            return;

        var cards = __result as List<CardModel> ?? __result.ToList();
        CardPoolModStore.ApplyModifications(netId, __instance.GetType().FullName!, cards);
        __result = cards;
    }

    // -- 视觉池重定向：让动态改池的卡牌显示目标池颜色 --

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.VisualCardPool), MethodType.Getter)]
    private static void VisualCardPool_Getter_Postfix(CardModel __instance, ref CardPoolModel __result)
    {
        Player? owner;
        try { owner = __instance.Owner; } catch { return; }
        if (owner == null) return;

        if (!CardPoolModStore.HasModifications(owner.NetId))
            return;

        var pool = CardPoolModStore.GetVisualPool(owner.NetId, __instance.Id);
        if (pool != null)
            __result = pool;
    }

    // -- JSON 存档持久化 --

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), typeof(SerializableRun), typeof(bool))]
    private static void RunSaveManager_SaveRun_Postfix(SerializableRun save, bool isMultiplayer)
        => CardPoolSavePersistence.InjectModsIntoSave(isMultiplayer);

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

    // -- 运行结束清理 --

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.DeleteCurrentRun))]
    private static void RunSaveManager_DeleteCurrentRun_Prefix()
    {
        CardPoolModStore.ClearAll();
        Log.Info("[CardPoolPatch] Cleared all card pool modifications (run ended).");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.DeleteCurrentMultiplayerRun))]
    private static void RunSaveManager_DeleteCurrentMultiplayerRun_Prefix()
    {
        CardPoolModStore.ClearAll();
        Log.Info("[CardPoolPatch] Cleared all card pool modifications (multiplayer run ended).");
    }

    // -- 二进制包序列化（网络同步） --

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializablePlayer), nameof(SerializablePlayer.Serialize))]
    private static void SerializablePlayer_Serialize_Postfix(SerializablePlayer __instance, PacketWriter writer)
    {
        var mods = CardPoolModStore.SerializeForPlayer(__instance.NetId);
        writer.WriteBool(mods != null);
        if (mods != null)
            writer.WriteString(JsonSerializer.Serialize(mods));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SerializablePlayer), nameof(SerializablePlayer.Deserialize))]
    private static void SerializablePlayer_Deserialize_Postfix(SerializablePlayer __instance, PacketReader reader)
    {
        if (!reader.ReadBool()) return;

        string json = reader.ReadString();
        try
        {
            var mods = JsonSerializer.Deserialize<CardPoolModStore.SerializableMods>(json);
            PendingBinarySync.Store(__instance.NetId, mods);
        }
        catch (Exception ex)
        {
            Log.Error($"[CardPoolPatch] Failed to deserialize card pool mods from packet: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.FromSerializable))]
    private static void Player_FromSerializable_Postfix(ref Player __result)
        => RestoreFromBinarySync(__result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.SyncWithSerializedPlayer))]
    private static void Player_SyncWithSerializedPlayer_Postfix(Player __instance)
        => RestoreFromBinarySync(__instance);

    // -- 私有辅助 --

    private static void RestoreFromBinarySync(Player player)
    {
        // 始终注册映射，确保存档加载后 _unlockStateMap 也有记录
        CardPoolModStore.RegisterUnlockStateMapping(player);

        var mods = PendingBinarySync.Consume(player.NetId);
        if (mods == null) return;

        CardPoolModStore.DeserializeForPlayer(player.NetId, mods);
    }
}
