/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Match.Get5.Events;

namespace Match;

public partial class LiveState : ActiveMatchState
{
    public override string Name => "live";
    public static readonly List<string> PauseCmds = ["css_pause", "css_p", "css_pausar"];
    public static readonly List<string> UnpauseCmds = ["css_unpause", "css_up", "css_despausar"];
    public static readonly List<string> SurrenderCmds = ["css_gg", "css_desistir"];
    public long RoundStartedAt = 0;
    public int Round = -1;
    private bool _isForfeiting = false;
    private uint _lastThrownSmokegrenade = 0;
    private long _bombPlantedAt = 0;
    private int? _lastPlantedBombZone = null;
    private readonly Dictionary<uint, ThrownUtility> _thrownUtilities = [];
    private readonly List<CancellationTokenSource> _utilityDetonateTimers = [];

    // smokegrenade_detonate -> inferno_extinguish -> inferno_expire (always called)
    private readonly Dictionary<uint, bool> _didSmokeExtinguishMolotov = [];

    public override void Load()
    {
        Rules.SynchronizeBots();
        RegisterCommand(SurrenderCmds, "Vote to surrender the match.", OnSurrenderCommand);
        RegisterCommand(PauseCmds, "Request to pause the match.", OnPauseCommand);
        RegisterCommand(UnpauseCmds, "Request to unpause the match.", OnUnpauseCommand);
        RegisterCommand(
            ["css_restore"],
            "Restore match state from backup (available during live match).",
            OnRestoreCommand
        );
        RegisterListener<Listeners.OnTick>(OnTick);
        AddCommandListener("mp_backup_restore_load_file", OnBackupRestoreLoadFilePre);
        HookGameEvent<EventPlayerConnect>(OnPlayerConnect);
        HookGameEvent<EventPlayerConnectFull>(OnPlayerConnectFull);
        HookGameEvent<EventRoundPrestart>(OnRoundPrestart);
        HookGameEvent<EventRoundStart>(OnRoundStart);
        HookGameEvent<EventRoundStart>(Stats_OnRoundStart);
        HookGameEvent<EventWeaponFire>(Stats_OnWeaponFire);
        HookGameEvent<EventGrenadeThrown>(OnGrenadeThrown);
        HookGameEvent<EventDecoyStarted>(OnDecoyStarted);
        HookGameEvent<EventHegrenadeDetonate>(OnHegrenadeDetonate);
        HookGameEvent<EventSmokegrenadeDetonate>(OnSmokegrenadeDetonate);
        HookGameEvent<EventInfernoStartburn>(OnInfernoStartburn);
        HookGameEvent<EventInfernoExtinguish>(OnInfernoExtinguish);
        HookGameEvent<EventInfernoExpire>(OnInfernoExpire);
        HookGameEvent<EventFlashbangDetonate>(OnFlashbangDetonate);
        HookGameEvent<EventPlayerBlind>(Stats_OnPlayerBlind);
        RegisterListener<Listeners.OnPlayerTakeDamagePost>(OnPlayerTakeDamagePost);
        HookGameEvent<EventPlayerDeath>(Stats_OnPlayerDeath);
        HookGameEvent<EventBombPlanted>(Stats_OnBombPlanted);
        HookGameEvent<EventBombDefused>(Stats_OnBombDefused);
        HookGameEvent<EventBombExploded>(OnBombExploded);
        HookGameEvent<EventRoundMvp>(Stats_OnRoundMvp);
        HookGameEvent<EventRoundEnd>(OnRoundEndPre, HookMode.Pre);
        HookGameEvent<EventRoundEnd>(Stats_OnRoundEnd);
        HookGameEvent<EventCsWinPanelMatch>(OnCsWinPanelMatch);
        HookGameEvent<EventPlayerDisconnect>(OnPlayerDisconnect);
        HookGameEvent<EventPlayerDisconnect>(Stats_OnPlayerDisconnect);
        Runtime.Log("Executing live match configuration");
        Rules.SendEvent(OnGoingLiveEvent.Create());
        Config.ExecLive(
            maxRounds: ConVars.MaxRounds.Value,
            otMaxRounds: ConVars.OtMaxRounds.Value,
            isFriendlyPause: ConVars.IsFriendlyPause.Value,
            backupPath: Rules.GetBackupPrefix(),
            restartDelay: Rules.GetTotalMapCount() > 1 ? ConVars.TvDelay.Value : -1
        );
        var localize = Runtime.Plugin.Localizer;
        PlayerHelper.PrintToChatAllRepeat(localize["match.live", Rules.GetChatPrefix()]);
        Server.PrintToChatAll(localize["match.live_disclaimer", Rules.GetChatPrefix()]);
        Rules.ClearAllSurrenderFlags();
        if (!ConVars.IsKnifeRoundEnabled.Value)
            Cstv.Record(Rules.GetDemoFilename());
        PlayerHelper.RemovePlayerClans();
        TryForfeitMatch();
    }

    public void OnTick()
    {
        CheckPauseEvents();
        if (ConVars.ServerGraphicUrl.Value != "")
            foreach (var player in Rules.GetAllPlayers())
            {
                var deathTime = player.Handle?.PlayerPawn.Value?.DeathTime;
                if (Server.CurrentTime - deathTime < ConVars.ServerGraphicDuration.Value)
                    player.Handle?.PrintToCenterHtml(
                        $"<img src='{ConVars.ServerGraphicUrl.Value}'>"
                    );
            }
    }

    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo _)
    {
        Round += 1;
        RoundStartedAt = TimeHelper.Now();
        _canSurrender = true;
        foreach (var cts in _utilityDetonateTimers)
            cts.Cancel();
        _utilityDetonateTimers.Clear();
        foreach (var entityId in _thrownUtilities.Keys.ToList())
            SendOnUtilityDetonatedEvent(entityId);
        _thrownUtilities.Clear();
        _lastThrownSmokegrenade = 0;
        _didSmokeExtinguishMolotov.Clear();
        Rules.SendEvent(OnRoundStartEvent.Create());
        return HookResult.Continue;
    }

    public HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo _)
    {
        var playerState = @event.Userid?.GetState();
        if (playerState != null)
            Rules.SendEvent(OnGrenadeThrownEvent.Create(playerState, weapon: @event.Weapon));
        return HookResult.Continue;
    }

    public HookResult OnDecoyStarted(EventDecoyStarted @event, GameEventInfo _)
    {
        var playerState = @event.Userid?.GetState();
        if (playerState != null)
            Rules.SendEvent(OnDecoyStartedEvent.Create(playerState, weapon: "weapon_decoy"));
        return HookResult.Continue;
    }

    public HookResult OnHegrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo _)
    {
        var playerState = @event.Userid?.GetState();
        if (playerState != null)
        {
            var entityId = (uint)@event.Entityid;
            _thrownUtilities[entityId] = new(
                Rules.GetRoundNumber(),
                Rules.GetRoundTime(),
                playerState,
                "weapon_hegrenade"
            );
            _utilityDetonateTimers.Add(
                TimeHelper.DelayTicks(4, () => SendOnUtilityDetonatedEvent(entityId))
            );
        }
        return HookResult.Continue;
    }

    public HookResult OnSmokegrenadeDetonate(EventSmokegrenadeDetonate @event, GameEventInfo _)
    {
        var playerState = @event.Userid?.GetState();
        if (playerState != null)
        {
            var entityId = (uint)@event.Entityid;
            _lastThrownSmokegrenade = entityId;
            _thrownUtilities[entityId] = new(
                Rules.GetRoundNumber(),
                Rules.GetRoundTime(),
                playerState,
                "weapon_smokegrenade"
            );
            _utilityDetonateTimers.Add(
                TimeHelper.DelayTicks(32, () => SendOnUtilityDetonatedEvent(entityId))
            );
        }
        return HookResult.Continue;
    }

    public HookResult OnInfernoStartburn(EventInfernoStartburn @event, GameEventInfo _)
    {
        var entity = Utilities.GetEntityFromIndex<CBaseEntity>(@event.Entityid);
        var pawn = entity?.OwnerEntity.Value?.As<CCSPlayerPawn>();
        var controller = pawn?.Controller.Value?.As<CCSPlayerController>();
        var playerState = controller?.GetState();
        if (entity != null && playerState != null)
            _thrownUtilities[entity.Index] = new(
                Rules.GetRoundNumber(),
                Rules.GetRoundTime(),
                playerState,
                "weapon_molotov"
            );
        return HookResult.Continue;
    }

    public HookResult OnInfernoExtinguish(EventInfernoExtinguish @event, GameEventInfo _)
    {
        _didSmokeExtinguishMolotov[_lastThrownSmokegrenade] = true;
        return HookResult.Continue;
    }

    public HookResult OnInfernoExpire(EventInfernoExpire @event, GameEventInfo _)
    {
        SendOnUtilityDetonatedEvent((uint)@event.Entityid);
        return HookResult.Continue;
    }

    public HookResult OnFlashbangDetonate(EventFlashbangDetonate @event, GameEventInfo _)
    {
        var playerState = @event.Userid?.GetState();
        if (playerState != null)
        {
            var entityId = (uint)@event.Entityid;
            _thrownUtilities[entityId] = new(
                Rules.GetRoundNumber(),
                Rules.GetRoundTime(),
                playerState,
                "weapon_flashbang"
            );
            _utilityDetonateTimers.Add(
                TimeHelper.DelayTicks(4, () => SendOnUtilityDetonatedEvent(entityId))
            );
        }
        return HookResult.Continue;
    }

    public void OnPlayerTakeDamagePost(
        CCSPlayerPawn victimPawn,
        CTakeDamageInfo info,
        CTakeDamageResult result
    )
    {
        if (victimPawn.DesignerName != "player")
            return;
        // This listener wraps CBaseEntity::TakeDamageOld rather than OnTakeDamage_Alive, so it also
        // fires for damage that was never applied.
        if (result.HealthLost <= 0)
            return;
        var attacker = info.Attacker;
        if (attacker.Value?.DesignerName != "player")
            return;
        var victimController = victimPawn.OriginalController.Value;
        var victimState = victimController?.GetState();
        var attackerPawn = attacker.Value?.As<CCSPlayerPawn>();
        var attackerController = attackerPawn?.OriginalController.Value;
        var attackerState = attackerController?.GetState();
        if (victimController == null || victimState == null || attackerState == null)
            return;
        var inflictor = info.Inflictor.Value;
        var weaponDesignerName = info.GetInflictorDesignerName();
        if (inflictor == null || weaponDesignerName == null || weaponDesignerName == "worldent")
            return;
        var damage = result.HealthLost;
        var isFriendlyFire = victimState.Team == attackerState.Team;
        if (
            ItemHelper.IsUtilityDesignerName(weaponDesignerName)
            && _thrownUtilities.TryGetValue(inflictor.Index, out var utility)
        )
        {
            var victim = utility.GetValueOrDefault(victimState.Key, new(victimState));
            if (victimController.GetHealth() <= 0)
                victim.Killed = true;
            victim.Damage += damage;
            victim.FriendlyFire = isFriendlyFire;
            utility[victimState.Key] = victim;
        }
        if (isFriendlyFire)
            return;
        if (victimState.DamageReport.TryGetValue(attackerState.Key, out var attackerDamageReport))
        {
            attackerDamageReport.From.Value += damage;
            attackerDamageReport.From.Hits += 1;
        }
        if (attackerState.DamageReport.TryGetValue(victimState.Key, out var victimDamageReport))
        {
            victimDamageReport.To.Value += damage;
            victimDamageReport.To.Hits += 1;
        }
        var hitGroup = info.GetHitGroup();
        if (hitGroup == HitGroup_t.HITGROUP_INVALID)
            hitGroup = HitGroup_t.HITGROUP_GENERIC;
        _lastDamageWeapon[victimState.Key] = weaponDesignerName;
        Stats_OnPlayerTakeDamagePost(
            attackerState,
            weaponDesignerName,
            damage,
            hitGroup,
            hitToken: ItemHelper.IsUtilityDesignerName(weaponDesignerName)
                ? (int)inflictor.Index
                : Server.TickCount
        );
    }

    public HookResult OnBombExploded(EventBombExploded _, GameEventInfo __)
    {
        Rules.SendEvent(OnBombExplodedEvent.Create(_lastPlantedBombZone));
        return HookResult.Continue;
    }

    public HookResult OnRoundEndPre(EventRoundEnd @event, GameEventInfo _)
    {
        if (_isRestoring)
            return HookResult.Continue;
        _canSurrender = false;
        var localize = Runtime.Plugin.Localizer;
        var home = Rules.Teams.First();
        var away = Rules.Teams.Last();
        foreach (var player in PlayerHelper.GetAllPlayers())
            player.PrintToChat(
                localize[
                    "match.round_end_score",
                    Rules.GetChatPrefix(),
                    home.FormattedName,
                    home.Score,
                    away.Score,
                    away.FormattedName
                ]
            );
        foreach (var playerState in Rules.Teams.SelectMany(t => t.Players))
        {
            foreach (var report in playerState.DamageReport.Values)
            {
                playerState.Handle?.PrintToChat(
                    localize[
                        "match.round_end_damage",
                        Rules.GetChatPrefix(),
                        report.To.Value,
                        report.To.Hits,
                        report.From.Value,
                        report.From.Hits,
                        report.Player.Name,
                        report.Player.Handle?.GetHealth() ?? 0
                    ]
                );
                report.Reset();
            }
        }
        return HookResult.Continue;
    }

    public void SendOnUtilityDetonatedEvent(uint entityId)
    {
        if (!_thrownUtilities.TryGetValue(entityId, out var thrown))
            return;
        switch (thrown.Weapon)
        {
            case "weapon_hegrenade":
                Rules.SendEvent(OnHEGrenadeDetonatedEvent.Create(thrown));
                break;
            case "weapon_flashbang":
                Rules.SendEvent(OnFlashbangDetonatedEvent.Create(thrown));
                break;
            case "weapon_molotov":
                Rules.SendEvent(OnMolotovDetonatedEvent.Create(thrown));
                break;
            case "weapon_smokegrenade":
                Rules.SendEvent(
                    OnSmokeGrenadeDetonatedEvent.Create(
                        thrown.RoundNumber,
                        thrown.RoundTime,
                        thrown.Player,
                        weapon: thrown.Weapon,
                        didExtinguishMolotovs: _didSmokeExtinguishMolotov.ContainsKey(entityId)
                    )
                );
                break;
        }
        _thrownUtilities.Remove(entityId);
    }
}
