namespace ZFLStats;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using BloodBowl3;

internal class ZFLStatsAnalyzer(Replay replay)
{
    private readonly Dictionary<int, ZFLPlayerStats> stats = new ();

    public ZFLTeamStats HomeTeamStats = new(0)
    {
        Name = replay.HomeTeam.Name,
        // Note that the in-game dedicated fans are irrelevant for ZFL, so we only care about the dice roll
        Fans = replay.ReplayRoot.SelectSingleNode("ReplayStep/EventFanFactor/HomeRoll/Dice/Die/Value")!.InnerText.ParseInt()
    };

    public ZFLTeamStats VisitingTeamStats = new(1)
    {
        Name = replay.VisitingTeam.Name,
        Fans = replay.ReplayRoot.SelectSingleNode("ReplayStep/EventFanFactor/AwayRoll/Dice/Die/Value")!.InnerText.ParseInt()
    };

    public Task AnalyzeAsync()
    {
        return Task.Run(this.Analyze);
    }

    public void Analyze()
    {
        foreach (var p in replay.HomeTeam.Players.Values.Concat(replay.VisitingTeam.Players.Values))
        {
            var stats = this.GetStatsFor(p.Id);
            stats.SppEarned = p.SppGained;
            stats.Mvp = p.Mvp;
        }

        int activePlayer = -1;
        int blockingPlayer = -1;

        int passingPlayer = -1;
        int catchingPlayer = -1;

        int ballCarrier = -1;

        int activeGamer = -1;

        var lastTeamWithPossession = -1;
        int[] touchdownTurnCounter = null;

        foreach (var replayStep in replay.ReplayRoot.SelectNodes("ReplayStep")!.Cast<XmlElement>())
        {
            var turnover = false;

            foreach (var node in replayStep.ChildNodes.Cast<XmlElement>())
            {
                if (node.LocalName == "EventEndTurn")
                {
                    if (ballCarrier != -1 && lastTeamWithPossession == activeGamer)
                    {
                        Debug.Assert(replay.GetPlayer(ballCarrier).Team == lastTeamWithPossession);
                        touchdownTurnCounter[lastTeamWithPossession]++;
                        Debug.WriteLine($"Increasing touchdown turn counter for team {lastTeamWithPossession} to {touchdownTurnCounter[lastTeamWithPossession]}");
                    }

                    turnover = node["Reason"]!.InnerText == "2";
                    activeGamer = node["NextPlayingGamer"]?.InnerText.ParseInt() ?? 0;
                    activePlayer = -1;
                    blockingPlayer = -1;
                    passingPlayer = -1;
                    catchingPlayer = -1;
                    Debug.WriteLine($"End Turn{(turnover ? " (turnover!)" : string.Empty)}");
                }
                else if (node.LocalName == "EventUseSpecialCard")
                {
                    var cardId = (SpecialCard)node["CardId"]!.InnerText.ParseInt();
                    if (cardId is SpecialCard.Fireball or SpecialCard.Zap)
                    {
                        // Not sure if any other cards are relevant?
                        activePlayer = -1;
                        blockingPlayer = -1;
                        passingPlayer = -1;
                        catchingPlayer = -1;
                        Debug.WriteLine("Wizard used");
                    }
                }
                else if (node.LocalName == "EventExecuteSequence")
                {
                    foreach (var stepResult in node.SelectNodes("Sequence/StepResult")!.Cast<XmlElement>())
                    {
                        var stepName = stepResult["Step"]!["Name"]!.InnerText;
                        var step = stepResult["Step"]!["MessageData"][stepName];
                        var stepType = (StepType)step["StepType"]!.InnerText.ParseInt();
                        var playerId = step["PlayerId"]?.InnerText.ParseInt() ?? -1;
                        var targetId = step["TargetId"]?.InnerText.ParseInt() ?? -1;
                        Debug.WriteLine($"{stepName}: {stepType}, player {playerId}, target {targetId}");
                        switch (stepType)
                        {
                            case StepType.Kickoff:
                                activePlayer = -1;
                                blockingPlayer = -1;
                                passingPlayer = -1;
                                catchingPlayer = -1;
                                ballCarrier = -1;
                                Debug.WriteLine("Kickoff, resetting touchdown turn counters");
                                lastTeamWithPossession = -1;
                                touchdownTurnCounter = [1, 1];
                                break;
                            case StepType.Activation:
                                activePlayer = playerId;
                                blockingPlayer = -1;
                                passingPlayer = -1;
                                catchingPlayer = -1;
                                break;
                            case StepType.Move:
                                break;
                            case StepType.Damage:
                                break;
                            case StepType.Block:
                                blockingPlayer = playerId;
                                break;
                            case StepType.Pass:
                                passingPlayer = playerId;
                                catchingPlayer = targetId;
                                break;
                            case StepType.Catch:
                                break;
                            case StepType.Foul:
                            case StepType.ChainsawFoul:
                                this.GetStatsFor(playerId).FoulsInflicted += 1;
                                this.GetStatsFor(targetId).FoulsSustained += 1;
                                break;
                            case StepType.Referee:
                                break;
                            case StepType.ThrowTeamMate:
                                // Apparently it's ThrowerId instead of PlayerId for this *one* thing
                                Debug.Assert(playerId == -1);
                                playerId = step["ThrowerId"]?.InnerText.ParseInt() ?? -1;
                                break;
                        }

                        int lastDeadPlayerId = -1;

                        bool catchSuccess = false;

                        foreach (var results in stepResult.SelectNodes("Results/StringMessage")!.Cast<XmlElement>())
                        {
                            var resultsName = results["Name"]!.InnerText;
                            var result = results["MessageData"]![resultsName];
                            var playerIdR = result!["PlayerId"]?.InnerText.ParseInt() ?? -1;
                            var reason = result["Reason"]?.InnerXml.ParseInt() ?? -1;

                            switch (resultsName)
                            {
                                case "QuestionTouchBack":
                                    {
                                        activePlayer = -1;
                                        blockingPlayer = -1;
                                        passingPlayer = -1;
                                        catchingPlayer = -1;
                                    }
                                    break;
                                case "ResultSkillUsage":
                                    {
                                        var skill = (Skill)result.SelectSingleNode("Skill")!.InnerText.ParseInt();
                                        var used = result.SelectSingleNode("Used")!.InnerText == "1";
                                        Debug.WriteLine($"ResultSkillUsage {skill} used? {used}");
                                    }
                                    break;
                                case "ResultMoveOutcome":
                                    {
                                        if (result.SelectSingleNode("Rolls/RollSummary") is XmlElement roll)
                                        {
                                            var rollType = (RollType)roll["RollType"]!.InnerText.ParseInt();
                                            var outcome = roll["Outcome"]!.InnerText;
                                            if (rollType == RollType.Dodge && outcome == "0" && replay.GetPlayer(playerId).Team == activeGamer)
                                            {
                                                this.GetStatsFor(playerId).DodgeTurnovers += 1;
                                            }
                                        }

                                        Debug.WriteLine("ResultMoveOutcome");
                                    }
                                    break;
                                case "ResultRoll":
                                    {
                                        var dice = result.SelectNodes("Dice/Die")!.Cast<XmlElement>().ToArray();
                                        var dieType = (DieType)dice[0]["DieType"]!.InnerText.ParseInt();
                                        var values = dice.Select(d => d["Value"]!.InnerText.ParseInt()).ToArray();
                                        var failed = result["Outcome"]!.InnerText == "0";
                                        var rollType = (RollType)result["RollType"]!.InnerText.ParseInt();
                                        var outcome = result["Outcome"]?.InnerText.ParseInt() ?? 0;

                                        // Pass and catch reroll seem to be handled differently??
                                        if (failed && rollType == RollType.Pass)
                                        {
                                            passingPlayer = -1;
                                            catchingPlayer = -1;
                                        }

                                        if (!failed && rollType == RollType.Catch)
                                        {
                                            catchSuccess = true;
                                        }

                                        if (rollType == RollType.Armor)
                                        {
                                            this.GetStatsFor(playerId).ArmorRollsSustained += 1;
                                            if (outcome != 0)
                                            {
                                                this.GetStatsFor(playerId).ArmorBreaksSustained += 1;
                                            }
                                        }
                                        else if (rollType == RollType.Bribe)
                                        {
                                            Debug.Assert(values.Length == 1);
                                            this.GetTeamStatsFor(activeGamer).BribeRolls.Add(values[0]);
                                        }
                                        else if (rollType == RollType.ArgueTheCall)
                                        {
                                            Debug.Assert(values.Length == 1);
                                            this.GetTeamStatsFor(activeGamer).ArgueTheCallRolls.Add(values[0]);
                                        }

                                        if (TryGetRollStatType(rollType, out var statType))
                                        {
                                            var dict = this.GetStatsFor(playerId).Rolls.AddOrGet(statType, () => new Dictionary<int[], int>(RollComparer.Default));
                                            dict.AddOrUpdate(values, 1, r => r + 1);
                                        }

                                        Debug.WriteLine($">> {rollType} {dieType} rolls: {string.Join(", ", values)}");
                                    }
                                    break;
                                case "QuestionBlockDice":
                                    {
                                        var dice = result.SelectNodes("Dice/Die")!.Cast<XmlElement>().ToArray();
                                        var dieType = (DieType)dice[0]["DieType"]!.InnerText.ParseInt();
                                        Debug.Assert(dieType == DieType.Block);
                                        var values = dice.Select(d => d["Value"]!.InnerText.ParseInt()).ToArray();
                                        values.ForEach(dieValue =>
                                        {
                                            this.GetStatsFor(playerId).AllBlockDice.AddOrUpdate(BlockDieName(dieValue), 1, k => k + 1);
                                        });
                                        Debug.WriteLine($">> Picking block dice: {string.Join(", ", values)}");
                                        if (values.Length >= 2 && values.All(v => v == 0)) this.GetStatsFor(playerId).DubskullsRolled += 1;
                                    }
                                    break;
                                case "ResultBlockRoll":
                                    {
                                        var dieValue = result.SelectSingleNode("Die/Value")!.InnerText.ParseInt();
                                        this.GetStatsFor(playerId).ChosenBlockDice.AddOrUpdate(BlockDieName(dieValue), 1, k => k + 1);
                                        Debug.WriteLine($">> Block die {dieValue}");
                                    }
                                    break;
                                case "ResultPlayerRemoval":
                                    {
                                        var situation = (PlayerSituation)result["Situation"]!.InnerText.ParseInt();
                                        var status = (PlayerStatus)result["Status"]!.InnerText.ParseInt();
                                        Debug.WriteLine($">> Removing {playerIdR}, situation {situation}, reason {reason}");

                                        // For some reason we seem to get two player removal events for surfs, one with reason 1 (doesn't have injury info)
                                        // and then another removal event with reason 0 with injury info (just like for blocks etc)
                                        if (reason == 1)
                                        {
                                            if (blockingPlayer >= 0)
                                            {
                                                Debug.WriteLine($">> Surf by {activePlayer} on {playerIdR}");
                                                this.GetStatsFor(activePlayer).SurfsInflicted += 1;
                                                this.GetStatsFor(playerIdR).SurfsSustained += 1;
                                            }
                                            else
                                            {
                                                Debug.WriteLine($">> Non-surf removal by {activePlayer} on {playerIdR}");
                                            }
                                        }
                                        else
                                        {
                                            if (situation == PlayerSituation.Injured)
                                            {
                                                this.GetStatsFor(playerIdR).CasSustained += 1;
                                                if (activePlayer >= 0)
                                                {
                                                    this.GetStatsFor(activePlayer).CasInflicted += 1;
                                                    if (status == PlayerStatus.Dead)
                                                    {
                                                        this.GetStatsFor(activePlayer).Kills += 1;
                                                        this.GetStatsFor(playerIdR).Deaths += 1;
                                                    }
                                                }
                                            }

                                            if (status == PlayerStatus.Dead)
                                            {
                                                lastDeadPlayerId = playerIdR;
                                            }
                                        }
                                    }
                                    break;
                                case "ResultBlockOutcome":
                                    {
                                        var attackerId = result["AttackerId"]!.InnerText.ParseInt();
                                        var defenderId = result["DefenderId"]!.InnerText.ParseInt();
                                        var outcome = (BlockOutcome)result["Outcome"]!.InnerText.ParseInt();
                                        this.GetStatsFor(attackerId).BlocksInflicted++;
                                        this.GetStatsFor(defenderId).BlocksSustained++;
                                        Debug.WriteLine($">> Block by {attackerId} on {defenderId}, outcome {outcome}");
                                    }
                                    break;
                                case "ResultInjuryRoll":
                                    {
                                        var injury = (InjuryOutcome)result["Outcome"]!.InnerText.ParseInt();
                                        Debug.WriteLine($">> Injury outcome {injury}");
                                    }
                                    break;
                                case "ResultCasualtyRoll":
                                    var casualty = (CasualtyOutcome)result["Outcome"]!.InnerText.ParseInt();
                                    Debug.WriteLine($">> Casualty outcome {casualty}");
                                    break;
                                case "ResultRaisedDead":
                                    var zombieId = result["RaisedPlayerId"]!.InnerText.ParseInt();
                                    Debug.WriteLine($">> Raising {lastDeadPlayerId} as {zombieId}");
                                    break;
                                case "ResultPlayerSentOff":
                                    {
                                        var sentOffId = result["PlayerId"]!.InnerText.ParseInt();
                                        this.GetStatsFor(sentOffId).Expulsions += 1;
                                        Debug.WriteLine($">> Sending {sentOffId} off the pitch");
                                    }
                                    break;
                                case "ResultUseAction":
                                    {
                                        var action = (SequenceType)result["Action"]!.InnerText.ParseInt();
                                        switch (action)
                                        {
                                            case SequenceType.Blitz:
                                                this.GetStatsFor(playerId).Blitzes++;
                                                break;
                                        }
                                        Debug.WriteLine($">> Use action {action}");
                                    }
                                    break;
                                default:
                                    Debug.WriteLine(resultsName);
                                    break;
                            }
                        }

                        if (stepType == StepType.Catch && passingPlayer >= 0 && catchingPlayer >= 0 && catchSuccess)
                        {
                            this.GetStatsFor(passingPlayer).PassCompletions += 1;
                            passingPlayer = -1;
                            catchingPlayer = -1;
                        }
                    }
                }
                else if (node.LocalName == "EventTouchdown")
                {
                    var playerId = node["PlayerId"]!.InnerText.ParseInt();
                    this.GetStatsFor(playerId).TouchdownsScored += 1;

                    Debug.Assert(replay.GetPlayer(playerId).Team == lastTeamWithPossession);
                    Debug.Assert(activeGamer == lastTeamWithPossession);
                    this.GetTeamStatsFor(activeGamer).TurnsPerTouchdown.Add(touchdownTurnCounter[activeGamer]);
                    Debug.WriteLine($"Team {activeGamer} scored within {touchdownTurnCounter[activeGamer]} turns");
                    touchdownTurnCounter = [1, 1];
                    lastTeamWithPossession = -1;
                }
            }

            if (replayStep.SelectSingleNode("BoardState/Ball") is XmlElement ballNode)
            {
                if (ballNode["IsHeld"]?.InnerText != "1" || ballNode["Carrier"] == null)
                {
                    if (ballCarrier != -1)
                    {
                        if (activePlayer != -1 && activePlayer != ballCarrier)
                        {
                            GetStatsFor(activePlayer).Sacks += 1;
                        }

                        ballCarrier = -1;
                        Debug.WriteLine("* Ball is loose!");
                    }
                }
                else if (ballNode["Carrier"] is { } carrierNode)
                {
                    var newCarrier = carrierNode.InnerText.ParseInt();
                    if (newCarrier != ballCarrier)
                    {
                        if (activePlayer != -1 && ballCarrier != -1 && activePlayer != ballCarrier)
                        {
                            GetStatsFor(activePlayer).Sacks += 1;
                        }

                        ballCarrier = newCarrier;
                        Debug.WriteLine($"* New ball carrier {newCarrier}!");

                        var newTeamWithPossession = replay.GetPlayer(newCarrier).Team;
                        if (lastTeamWithPossession != -1 && lastTeamWithPossession != newTeamWithPossession)
                        {
                            Debug.WriteLine("New team in possession, resetting touchdown turn counters");
                            touchdownTurnCounter = [1, 1];
                        }

                        lastTeamWithPossession = newTeamWithPossession;
                    }
                }
            }
        }

        this.stats.Values.ForEach(p =>
        {
            if (p.Rolls.TryGetValue(RollStatType.Other, out var rolls))
            {
                rolls.ForEach(kvp =>
                {
                    Debug.Assert(kvp.Key.Length == 1);
                    Debug.Assert(kvp.Key[0] >= 1 && kvp.Key[0] <= 6);
                    p.OtherDice.Add(kvp.Key[0], kvp.Value);
                });
            }

            if (p.Rolls.TryGetValue(RollStatType.ArmorOrInjury, out rolls))
            {
                rolls.ForEach(kvp =>
                {
                    var r = kvp.Key.Sum();
                    p.ArmorAndInjuryDice.AddOrUpdate(r, kvp.Value, k => k + kvp.Value);
                });
            }
        });

        static void UpdateTeamStats(ZFLTeamStats team)
        {
            team.Players.ForEach(p =>
            {
                foreach (var kvp in p.AllBlockDice)
                {
                    team.AllBlockDice.AddOrUpdate(kvp.Key, kvp.Value, v => v + kvp.Value);
                }

                foreach (var kvp in p.ChosenBlockDice)
                {
                    team.ChosenBlockDice.AddOrUpdate(kvp.Key, kvp.Value, v => v + kvp.Value);
                }

                foreach (var kvp in p.ArmorAndInjuryDice)
                {
                    team.ArmorAndInjuryDice.AddOrUpdate(kvp.Key, kvp.Value, v => v + kvp.Value);
                }

                foreach (var kvp in p.OtherDice)
                {
                    team.OtherDice.AddOrUpdate(kvp.Key, kvp.Value, v => v + kvp.Value);
                }
            });
        }

        this.HomeTeamStats.Players = replay.HomeTeam.Players.Keys.OrderBy(id => id).Select(this.GetStatsFor).ToList();
        this.VisitingTeamStats.Players = replay.VisitingTeam.Players.Keys.OrderBy(id => id).Select(this.GetStatsFor).ToList();

        UpdateTeamStats(this.HomeTeamStats);
        UpdateTeamStats(this.VisitingTeamStats);
    }

    private ZFLPlayerStats GetStatsFor(int playerId)
    {
        Debug.Assert(playerId >= 0);
        if (!this.stats.TryGetValue(playerId, out var playerStats))
        {
            var p = replay.GetPlayer(playerId);
            playerStats = new ZFLPlayerStats(playerId, p.Name, p.LobbyId);
            this.stats.Add(playerId, playerStats);
        }

        return playerStats;
    }

    private ZFLTeamStats GetTeamStatsFor(int activeGamer)
    {
        if (activeGamer == 0)
            return this.HomeTeamStats;

        if (activeGamer == 1)
            return this.VisitingTeamStats;

        throw new ArgumentException();
    }

    private static bool TryGetRollStatType(RollType type, out RollStatType statType)
    {
        switch (type)
        {
            case RollType.Block:
                statType = RollStatType.Block;
                return true;
            case RollType.Armor:
            case RollType.Injury:
                statType = RollStatType.ArmorOrInjury;
                return true;
            case RollType.Casualty:
                statType = RollStatType.Casualty;
                return true;
            case RollType.GFI:
            case RollType.Dodge:
            case RollType.PickUp:
            case RollType.Pass:
            case RollType.Interception:
            case RollType.Catch:
            case RollType.WakeUp:
            case RollType.Pro:
            case RollType.StandUp:
            case RollType.JumpOver:
            case RollType.Dauntless:
            case RollType.JumpUp:
            case RollType.BoneHead:
            case RollType.ReallyStupid:
            case RollType.UnchannelledFury:
            case RollType.AnimalSavagery:
            case RollType.FoulAppearance:
            case RollType.ThrowTeamMate:
            case RollType.Land:
            case RollType.AlwaysHungry:
            case RollType.VomitAccuracy:
            case RollType.Regeneration:
            case RollType.Chainsaw:
            case RollType.TakeRoot:
            case RollType.Loner:
            case RollType.Animosity:
                statType = RollStatType.Other;
                return true;
            default:
                statType = (RollStatType)42;
                return false;
        }
    }

    private static string BlockDieName(int die)
    {
        switch (die)
        {
            case 0:
                return "AttackerDown";
            case 1:
                return "BothDown";
            case 2:
                return "Push";
            case 3:
                return "DefenderStumbles";
            case 4:
                return "DefenderDown";
            default:
                throw new ArgumentException();
        }
    }

    public class RollComparer : IEqualityComparer<int[]>
    {
        public static RollComparer Default = new RollComparer();

        public bool Equals(int[] x, int[] y)
        {
            if (x.Length != y.Length)
            {
                return false;
            }

            return x.OrderBy(a => a).SequenceEqual(y.OrderBy(b => b));
        }

        public int GetHashCode(int[] obj)
        {
            return obj.OrderBy(a => a).Select(a => a.GetHashCode()).Sum();
        }
    }
}