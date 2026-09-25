/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Match.Get5.Events;

namespace Match;

public partial class LiveState
{
    private readonly Dictionary<int, List<(PlayerState, PlayerStats)>> _statsBackup = [];
    private readonly Dictionary<int, List<(PlayerTeam, TeamStats)>> _teamStatsBackup = [];
    private readonly Dictionary<CsTeam, bool> _isTeamClutching = [];
    private readonly Dictionary<string, int> _roundClutchingCount = [];
    private readonly Dictionary<string, int> _roundKills = [];
    private readonly Dictionary<string, (CsTeam, string, CsTeam, long)> _playerKilledBy = [];

    // Last weapon that damaged each victim, resolved from the inflictor. Needed because
    // `player_death.weapon` reports fire kills as the raw `inferno` entity name, never
    // distinguishing molotov from incendiary.
    private readonly Dictionary<string, string> _lastDamageWeapon = [];

    // Deduplicates damage callbacks into hits. One shot may damage multiple players (pellets,
    // penetration, or an explosion), but it is still one hit in the weapon statistics.
    private readonly Dictionary<(string, string), int> _lastHitToken = [];
    private bool _hadOpeningDuel = false;
    private bool _isRestoring = false;
    private int _restoreRound = 0;

    // KAST
    private readonly Dictionary<string, bool> _playerDied = [];
    private readonly Dictionary<string, bool> _playerKilledOrAssistedOrTradedKill = [];
    private readonly Dictionary<string, bool> _playerPlayedRound = [];

    public HookResult Stats_OnRoundStart(EventRoundStart @event, GameEventInfo _)
    {
        if (_isRestoring)
        {
            if (_restoreRound == 0)
                Rules.ResetAllPlayerAndTeamStats();
            else
                RestoreStats(_restoreRound);
            _isRestoring = false;
        }
        _isTeamClutching.Clear();
        _roundClutchingCount.Clear();
        _playerKilledBy.Clear();
        _lastDamageWeapon.Clear();
        _lastHitToken.Clear();
        _hadOpeningDuel = false;
        _playerDied.Clear();
        _playerKilledOrAssistedOrTradedKill.Clear();
        _playerPlayedRound.Clear();
        foreach (var player in Rules.GetAllPlayers())
        {
            _roundKills[player.Key] = 0;
            if (player.Handle != null)
            {
                player.Stats.RoundsPlayed += 1;
                _playerPlayedRound[player.Key] = true;
            }
        }
        return HookResult.Continue;
    }

    public HookResult Stats_OnWeaponFire(EventWeaponFire @event, GameEventInfo _)
    {
        var playerState = @event.Userid?.GetState();
        if (
            playerState != null
            && @event.Weapon != "world"
            && !ItemHelper.IsMeleeDesignerName(@event.Weapon)
        )
            playerState?.Stats
                .GetWeaponStats(ItemHelper.NormalizeDesignerName(@event.Weapon, playerState.Handle))
                .Shots += 1;
        return HookResult.Continue;
    }

    public HookResult Stats_OnPlayerBlind(EventPlayerBlind @event, GameEventInfo _)
    {
        var attackerState = @event.Attacker?.GetState();
        var victimState = @event.Userid?.GetState();
        if (attackerState != null && victimState != null)
        {
            var friendlyFire = attackerState.Team == victimState.Team;
            if (@event.BlindDuration > 2.5f && attackerState != victimState)
                if (friendlyFire)
                    attackerState.Stats.FriendliesFlashed += 1;
                else
                    attackerState.Stats.EnemiesFlashed += 1;

            var entityId = (uint)@event.Entityid;
            if (_thrownUtilities.TryGetValue(entityId, out var utility))
            {
                var theVictim = utility.GetValueOrDefault(victimState.Key, new(victimState));
                theVictim.FriendlyFire = friendlyFire;
                theVictim.BlindDuration = @event.BlindDuration;
                utility[victimState.Key] = theVictim;
            }
        }
        return HookResult.Continue;
    }

    public HookResult Stats_OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo _)
    {
        var playerState = @event.Userid?.GetState();
        if (playerState != null)
            _playerDied[playerState.Key] = true;
        return HookResult.Continue;
    }

    public HookResult Stats_OnPlayerDeath(EventPlayerDeath @event, GameEventInfo _)
    {
        if (_isRestoring)
            return HookResult.Continue;
        var attacker = @event.Attacker;
        var attackerState = attacker?.GetState();
        var victimState = @event.Userid?.GetState();
        if (victimState == null)
            return HookResult.Continue;
        var victimTeam = victimState.Team.CurrentTeam;
        if (!_isTeamClutching.ContainsKey(victimTeam))
        {
            var aliveTeammates = EntityHelper.GetAlivePawnsInTeam(victimTeam).ToList();
            if (aliveTeammates.Count == 1)
            {
                _isTeamClutching[victimTeam] = true;
                var clutcherState = aliveTeammates[0].OriginalController.Value?.GetState();
                if (clutcherState != null)
                    _roundClutchingCount[clutcherState.Key] = EntityHelper
                        .GetAlivePawnsInTeam(victimTeam.Toggle())
                        .Count();
            }
        }
        var killedByBomb = @event.Weapon == "planted_c4";
        var killedWithKnife = ItemHelper.IsMeleeDesignerName(@event.Weapon);
        var isSuicide =
            (attackerState == null || attackerState.Key == victimState.Key) && !killedByBomb;
        var headshot = @event.Headshot;
        var eventWeapon = @event.Weapon;
        if (
            eventWeapon == "inferno"
            && _lastDamageWeapon.TryGetValue(victimState.Key, out var lastDamageWeapon)
        )
            eventWeapon = lastDamageWeapon;
        var normalizedWeapon = ItemHelper.NormalizeDesignerName(eventWeapon);
        var assisterState = @event.Assister?.GetState();
        if (assisterState != null && assisterState.Team != victimState.Team)
            if (@event.Assistedflash)
                assisterState.Stats.FlashbangAssists += 1;
            else
            {
                assisterState.Stats.Assists += 1;
                _playerKilledOrAssistedOrTradedKill[assisterState.Key] = true;
            }
        victimState.Stats.Deaths += 1;
        _playerDied[victimState.Key] = true;
        if (isSuicide)
            victimState.Stats.Suicides += 1;
        else if (!killedByBomb)
        {
            if (attackerState?.Team == victimState.Team)
                attackerState.Stats.Teamkills += 1;
            else if (attackerState != null)
            {
                var weaponStats = attackerState.Stats.GetWeaponStats(normalizedWeapon);
                weaponStats.Kills += 1;
                if (headshot)
                    weaponStats.Headshots += 1;
                var attackerTeam = attackerState.Team.CurrentTeam;
                if (!_hadOpeningDuel)
                {
                    _hadOpeningDuel = true;
                    if (attackerTeam == CsTeam.Terrorist)
                        attackerState.Stats.FirstKillsT += 1;
                    else
                        attackerState.Stats.FirstKillsCT += 1;
                    if (victimTeam == CsTeam.Terrorist)
                        victimState.Stats.FirstDeathsT += 1;
                    else
                        victimState.Stats.FirstDeathsCT += 1;
                }
                _roundKills[attackerState.Key] += 1;
                _playerKilledBy[victimState.Key] = (
                    victimTeam,
                    attackerState.Key,
                    attackerTeam,
                    TimeHelper.Now()
                );
                var isTradeKill = false;
                foreach (
                    var (
                        aVictim,
                        (theVictimTeam, theVictimAttacker, theVictimAttackerTeam, theVictimKilledAt)
                    ) in _playerKilledBy
                )
                    if (
                        attackerTeam == theVictimTeam
                        && victimState.Key == theVictimAttacker
                        && victimTeam == theVictimAttackerTeam
                        && (TimeHelper.Now() - theVictimKilledAt) <= 2_000
                    )
                    {
                        isTradeKill = true;
                        _playerKilledOrAssistedOrTradedKill[aVictim] = true;
                    }
                if (isTradeKill)
                    attackerState.Stats.TradeKills += 1;
                attackerState.Stats.Kills += 1;
                _playerKilledOrAssistedOrTradedKill[attackerState.Key] = true;
                if (headshot)
                    attackerState.Stats.HeadshotKills += 1;
                if (killedWithKnife)
                    attackerState.Stats.KnifeKills += 1;
            }
        }
        Rules.SendEvent(
            OnPlayerDeathEvent.Create(
                player: victimState,
                attackerState,
                assisterState,
                weapon: normalizedWeapon,
                isKilledByBomb: killedByBomb,
                isHeadshot: headshot,
                isThruSmoke: @event.Thrusmoke,
                isPenetrated: @event.Penetrated,
                isAttackerBlind: @event.Attackerblind,
                isNoScope: @event.Noscope,
                isSuicide: isSuicide,
                isFriendlyFire: victimState.Team == attackerState?.Team,
                isFlashAssist: @event.Assistedflash
            )
        );
        return HookResult.Continue;
    }

    public HookResult Stats_OnBombPlanted(EventBombPlanted @event, GameEventInfo _)
    {
        _bombPlantedAt = TimeHelper.Now();
        _lastPlantedBombZone = @event.Userid?.PlayerPawn.Value?.WhichBombZone;
        var playerState = @event.Userid?.GetState();
        if (playerState != null)
        {
            playerState.Stats.BombPlants += 1;
            Rules.SendEvent(OnBombPlantedEvent.Create(playerState, site: _lastPlantedBombZone));
        }
        return HookResult.Continue;
    }

    public HookResult Stats_OnBombDefused(EventBombDefused @event, GameEventInfo _)
    {
        var playerState = @event.Userid?.GetState();
        if (playerState != null)
        {
            playerState.Stats.BombDefuses += 1;
            var plantedC4 = Utilities
                .FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4")
                .FirstOrDefault();
            long bombTimeRemaining;
            if (plantedC4 != null)
                bombTimeRemaining = (long)((plantedC4.C4Blow - Server.CurrentTime) * 1000);
            else
            {
                Runtime.Log("No planted_c4 entity found, falling back to wall clock.");
                var timeToDefuse = TimeHelper.Now() - _bombPlantedAt;
                var c4Timer = (ConVar.Find("mp_c4timer")?.GetPrimitiveValue<int>() ?? 0) * 1000;
                bombTimeRemaining = c4Timer - timeToDefuse;
            }
            if (bombTimeRemaining < 0)
            {
                Runtime.Log($"bombTimeRemaining={bombTimeRemaining} is negative!");
                bombTimeRemaining = 0;
            }
            Rules.SendEvent(
                OnBombDefusedEvent.Create(
                    playerState,
                    site: _lastPlantedBombZone,
                    bombTimeRemaining
                )
            );
        }
        return HookResult.Continue;
    }

    public HookResult Stats_OnRoundMvp(EventRoundMvp @event, GameEventInfo _)
    {
        var playerState = @event.Userid?.GetState();
        if (playerState != null)
        {
            playerState.Stats.MVPs += 1;
            Rules.SendEvent(OnPlayerBecameMVPEvent.Create(playerState, reason: @event.Reason));
        }
        return HookResult.Continue;
    }

    public void Stats_OnPlayerTakeDamagePost(
        PlayerState attackerState,
        string weaponDesignerName,
        int damage,
        HitGroup_t hitGroup,
        int hitToken
    )
    {
        if (ItemHelper.IsUtilityDesignerName(weaponDesignerName))
            attackerState.Stats.UtilDamage += damage;
        attackerState.Stats.Damage += damage;
        var weaponStats = attackerState.Stats.GetWeaponStats(
            ItemHelper.NormalizeDesignerName(weaponDesignerName, null)
        );
        weaponStats.Damage += damage;
        var hitKey = (attackerState.Key, weaponDesignerName);
        if (_lastHitToken.TryGetValue(hitKey, out var lastToken) && lastToken == hitToken)
            return;
        _lastHitToken[hitKey] = hitToken;
        weaponStats.Hits += 1;
        switch (hitGroup)
        {
            case HitGroup_t.HITGROUP_HEAD:
                weaponStats.HeadHits += 1;
                break;
            case HitGroup_t.HITGROUP_NECK:
                weaponStats.NeckHits += 1;
                break;
            case HitGroup_t.HITGROUP_CHEST:
                weaponStats.ChestHits += 1;
                break;
            case HitGroup_t.HITGROUP_STOMACH:
                weaponStats.StomachHits += 1;
                break;
            case HitGroup_t.HITGROUP_LEFTARM:
                weaponStats.LeftArmHits += 1;
                break;
            case HitGroup_t.HITGROUP_RIGHTARM:
                weaponStats.RightArmHits += 1;
                break;
            case HitGroup_t.HITGROUP_LEFTLEG:
                weaponStats.LeftLegHits += 1;
                break;
            case HitGroup_t.HITGROUP_RIGHTLEG:
                weaponStats.RightLegHits += 1;
                break;
            case HitGroup_t.HITGROUP_GEAR:
                weaponStats.GearHits += 1;
                break;
        }
    }

    public HookResult Stats_OnRoundEnd(EventRoundEnd @event, GameEventInfo _)
    {
        if (_isRestoring)
            return HookResult.Continue;
        // `Game_Commencing` is a full match reset, not a played round; and any `round_end` arriving
        // after the map result was recorded must not mutate it retroactively.
        if ((RoundEndReason)@event.Reason == RoundEndReason.GameCommencing)
            return HookResult.Continue;
        if (Rules.MapEndResult != null)
            return HookResult.Continue;
        var gameRules = EntityHelper.GetGameRules();
        if (gameRules == null)
            return HookResult.Continue;
        var winner = (CsTeam)@event.Winner;
        var winnerTeam = Rules.Teams.FirstOrDefault(t => t.CurrentTeam == winner);
        switch (winnerTeam?.CurrentTeam)
        {
            case CsTeam.Terrorist:
                winnerTeam.Stats.ScoreT += 1;
                break;
            case CsTeam.CounterTerrorist:
                winnerTeam.Stats.ScoreCT += 1;
                break;
        }
        var completedRounds = gameRules.TotalRoundsPlayed + 1;
        _statsBackup[completedRounds] = [];
        _teamStatsBackup[completedRounds] = [];
        foreach (var team in Rules.Teams)
        {
            _teamStatsBackup[completedRounds].Add((team, team.Stats.Clone()));
            foreach (var player in team.Players)
            {
                if (player.Handle != null)
                    player.Stats.Score = player.Handle.Score;
                if (_roundKills.TryGetValue(player.Key, out var kills))
                    switch (kills)
                    {
                        case 1:
                            player.Stats.K1 += 1;
                            break;
                        case 2:
                            player.Stats.K2 += 1;
                            break;
                        case 3:
                            player.Stats.K3 += 1;
                            break;
                        case 4:
                            player.Stats.K4 += 1;
                            break;
                        case 5:
                            player.Stats.K5 += 1;
                            break;
                    }
                if (
                    player.Team.CurrentTeam == winner
                    && _roundClutchingCount.TryGetValue(player.Key, out var opponents)
                )
                    switch (opponents)
                    {
                        case 1:
                            player.Stats.V1 += 1;
                            break;
                        case 2:
                            player.Stats.V2 += 1;
                            break;
                        case 3:
                            player.Stats.V3 += 1;
                            break;
                        case 4:
                            player.Stats.V4 += 1;
                            break;
                        case 5:
                            player.Stats.V5 += 1;
                            break;
                    }

                if (_playerPlayedRound.ContainsKey(player.Key))
                    if (
                        _playerKilledOrAssistedOrTradedKill.ContainsKey(player.Key)
                        || !_playerDied.ContainsKey(player.Key)
                    )
                        player.Stats.KAST += 1;

                _statsBackup[completedRounds].Add((player, player.Stats.Clone()));
            }
        }
        WriteStatsBackupToDisk(completedRounds);
        Rules.SendEvent(OnRoundEndEvent.Create(winner: winnerTeam, reason: @event.Reason));
        Rules.SendEvent(OnRoundStatsUpdatedEvent.Create());
        return HookResult.Continue;
    }
}
