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

    public IEnumerable<ZFLPlayerStats> HomeTeamStats => replay.HomeTeam.Players.Keys.OrderBy(id => id).Select(this.GetStatsFor);

    public IEnumerable<ZFLPlayerStats> VisitingTeamStats => replay.VisitingTeam.Players.Keys.OrderBy(id => id).Select(this.GetStatsFor);

    // Note that the in-game dedicated fans are irrelevant for ZFL, so we only care about the dice roll
    public int HomeFanAttendanceRoll => replay.ReplayRoot.SelectSingleNode("ReplayStep/EventFanFactor/HomeRoll/Dice/Die/Value")!.InnerText.ParseInt();

    public int VisitingFanAttendanceRoll => replay.ReplayRoot.SelectSingleNode("ReplayStep/EventFanFactor/AwayRoll/Dice/Die/Value")!.InnerText.ParseInt();

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

        foreach (var replayStep in replay.ReplayRoot.SelectNodes("ReplayStep")!.Cast<XmlElement>())
        {
            var turnover = false;

            foreach (var node in replayStep.ChildNodes.Cast<XmlElement>())
            {
                if (node.LocalName == "EventEndTurn")
                {
                    turnover = node["Reason"]!.InnerText == "2";
                    activeGamer = node["NextPlayingGamer"]?.InnerText.ParseInt() ?? 0;
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
                                this.GetStatsFor(playerId).FoulsInflicted += 1;
                                this.GetStatsFor(targetId).FoulsSustained += 1;
                                break;
                            case StepType.Referee:
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
                                case "ResultSkillUsage":
                                    {
                                        var skill = (Skill)result.SelectSingleNode("Skill")!.InnerText.ParseInt();
                                        var used = result.SelectSingleNode("Used")!.InnerText == "1";
                                        if (used && skill == Skill.StripBall)
                                        {
                                            this.GetStatsFor(playerIdR).Sacks += 1;
                                        }

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
                                        Debug.WriteLine($">> Picking block dice: {string.Join(", ", values)}");
                                        if (values.Length >= 2 && values.All(v => v == 0)) this.GetStatsFor(playerId).DubskullsRolled += 1;
                                    }
                                    break;
                                case "ResultBlockRoll":
                                    {
                                        var dieValue = result.SelectSingleNode("Die/Value")!.InnerText.ParseInt();
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

                                            if (ballCarrier >= 0 && ballCarrier == playerIdR)
                                            {
                                                this.GetStatsFor(activePlayer).Sacks += 1;
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
                                                    if (status == PlayerStatus.Dead) this.GetStatsFor(activePlayer).Kills += 1;
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
                                        
                                        if (ballCarrier >= 0 && defenderId == ballCarrier)
                                        {
                                            switch (outcome)
                                            {
                                                case BlockOutcome.AttackerDown:
                                                case BlockOutcome.BothStanding:
                                                case BlockOutcome.Pushed:
                                                    break;
                                                case BlockOutcome.BothDown:
                                                case BlockOutcome.BothWrestleDown:
                                                case BlockOutcome.DefenderDown:
                                                case BlockOutcome.DefenderPushedDown:
                                                    this.GetStatsFor(attackerId).Sacks += 1;
                                                    break;
                                                default:
                                                    throw new ArgumentOutOfRangeException();
                                            }
                                        }

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
                }
            }

            if (replayStep.SelectSingleNode("BoardState/Ball") is XmlElement ballNode)
            {
                if (ballNode["Carrier"] is { } carrierNode)
                {
                    var newCarrier = carrierNode.InnerText.ParseInt();
                    if (newCarrier != ballCarrier)
                    {
                        ballCarrier = newCarrier;
                        Debug.WriteLine($"* New ball carrier {newCarrier}!");
                    }
                }
                else if (ballCarrier != -1)
                {
                    ballCarrier = -1;
                    Debug.WriteLine("* Ball is loose!");
                }
            }
        }
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
}
