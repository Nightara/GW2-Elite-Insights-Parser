﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GW2EIEvtcParser;
using GW2EIEvtcParser.EIData;
using GW2EIEvtcParser.ParsedData;
using static GW2EIEvtcParser.ParserHelper;

namespace GW2EIBuilders
{
    public class CSVBuilder
    {
        private readonly ParsedEvtcLog _log;
        private readonly Version _parserVersion;
        private readonly IReadOnlyList<PhaseData> _phases;
        private readonly NPC _legacyTarget;
        private readonly StatisticsHelper _statistics;
        private StreamWriter _sw;
        private readonly string _delimiter;
        private readonly string[] _uploadResult;

        private readonly List<Player> _noFakePlayers;

        public CSVBuilder(ParsedEvtcLog log, CSVSettings settings, Version parserVersion, UploadResults uploadResults)
        {
            if (settings == null)
            {
                throw new InvalidDataException("Missing settings in CSVBuilder");
            }
            _log = log;
            _parserVersion = parserVersion;
            _delimiter = settings.Delimiter;
            _phases = log.FightData.GetPhases(log);
            _noFakePlayers = log.PlayerList.ToList();

            _statistics = log.StatisticsHelper;

            _uploadResult = uploadResults.ToArray();
            _legacyTarget = log.FightData.Logic.Targets.OfType<NPC>().FirstOrDefault();
            if (_legacyTarget == null)
            {
                throw new InvalidDataException("No Targets found for csv");
            }
        }
        private void WriteCell(string content)
        {
            _sw.Write(content + _delimiter, Encoding.GetEncoding(1252));
        }
        private void WriteCells(string[] content)
        {
            foreach (string cont in content)
            {
                _sw.Write(cont + _delimiter, Encoding.GetEncoding(1252));
            }
        }
        private void NewLine()
        {
            _sw.Write("\r\n", Encoding.GetEncoding(1252));
        }
        private void WriteLine(string[] content)
        {
            foreach (string cont in content)
            {
                _sw.Write(cont + _delimiter, Encoding.GetEncoding(1252));
            }
            NewLine();
        }
        //Creating CSV---------------------------------------------------------------------------------
        public void CreateCSV(StreamWriter sw)
        {
            _sw = sw;
            //header
            _log.UpdateProgressWithCancellationCheck("CSV: Building Meta Data");
            WriteLine(new[] { "Elite Insights", _parserVersion.ToString() });
            WriteLine(new[] { "ARC Version", _log.LogData.ArcVersion });
            WriteLine(new[] { "Fight ID", _log.FightData.TriggerID.ToString() });
            WriteLine(new[] { "Recorded By", _log.LogData.PoVName });
            WriteLine(new[] { "Time Start", _log.LogData.LogStartStd });
            WriteLine(new[] { "Time End", _log.LogData.LogEndStd });
            if (_uploadResult.Any(x => x != null && x.Length > 0))
            {
                WriteLine(new[] { "Links", _uploadResult[0], _uploadResult[1] });
            }
            else
            {
                NewLine();
            }
            NewLine();
            NewLine();
            NewLine();
            //Boss card
            WriteLine(new[] { "Boss", _log.FightData.FightName });
            WriteLine(new[] { "Success", _log.FightData.Success.ToString() });
            WriteLine(new[] { "Total Boss Health", _legacyTarget.GetHealth(_log.CombatData).ToString() });
            IReadOnlyList<HealthUpdateEvent> hpUpdates = _log.CombatData.GetHealthUpdateEvents(_legacyTarget.AgentItem);
            double hpLeft = hpUpdates.Count > 0
                ? hpUpdates.Last().HPPercent
                : 100.0;
            WriteLine(new[] { "Final Boss Health", (_legacyTarget.GetHealth(_log.CombatData) * hpLeft).ToString() });
            WriteLine(new[] { "Boss Health Burned %", (100.0 - hpLeft).ToString() });
            WriteLine(new[] { "Duration", _log.FightData.DurationString });

            //DPSStats
            _log.UpdateProgressWithCancellationCheck("CSV: Building DPS Data");
            CreateDPSTable(0);

            //DMGStatsBoss
            _log.UpdateProgressWithCancellationCheck("CSV: Building Boss Damage Data");
            CreateBossDMGStatsTable(0);

            //DMGStats All
            _log.UpdateProgressWithCancellationCheck("CSV: Building Damage Data");
            CreateDmgStatsTable(0);

            //Defensive Stats
            _log.UpdateProgressWithCancellationCheck("CSV: Building Defense Data");
            CreateDefTable(0);

            //Support Stats
            _log.UpdateProgressWithCancellationCheck("CSV: Building Support Data");
            CreateSupTable(0);

            // boons
            _log.UpdateProgressWithCancellationCheck("CSV: Building Boon Data");
            CreateUptimeTable(_statistics.PresentBoons, 0);

            //boonGenSelf
            CreateGenSelfTable(_statistics.PresentBoons, 0);

            // boonGenGroup
            CreateGenGroupTable(_statistics.PresentBoons, 0);

            // boonGenOGroup
            CreateGenOGroupTable(_statistics.PresentBoons, 0);

            //  boonGenSquad
            CreateGenSquadTable(_statistics.PresentBoons, 0);

            //Offensive Buffs stats
            _log.UpdateProgressWithCancellationCheck("CSV: Building Offensive Buff Data");
            // boons
            CreateUptimeTable(_statistics.PresentOffbuffs, 0);

            //boonGenSelf
            CreateGenSelfTable(_statistics.PresentOffbuffs, 0);

            // boonGenGroup
            CreateGenGroupTable(_statistics.PresentOffbuffs, 0);

            // boonGenOGroup
            CreateGenOGroupTable(_statistics.PresentOffbuffs, 0);

            //  boonGenSquad
            CreateGenSquadTable(_statistics.PresentOffbuffs, 0);

            //Defensive Buffs stats
            _log.UpdateProgressWithCancellationCheck("CSV: Building Defensive Buff Data");
            // boons
            CreateUptimeTable(_statistics.PresentDefbuffs, 0);

            //boonGenSelf
            CreateGenSelfTable(_statistics.PresentDefbuffs, 0);

            // boonGenGroup
            CreateGenGroupTable(_statistics.PresentDefbuffs, 0);

            // boonGenOGroup
            CreateGenOGroupTable(_statistics.PresentDefbuffs, 0);

            //  boonGenSquad
            CreateGenSquadTable(_statistics.PresentDefbuffs, 0);

            //Mechanics
            _log.UpdateProgressWithCancellationCheck("CSV: Building Mechanics Data");
            CreateMechanicTable(0);

            //Mech List
            CreateMechList();

            //Condi Uptime
            _log.UpdateProgressWithCancellationCheck("CSV: Building Boss Condition Data");
            CreateBossCondiUptime(0);
            //Condi Gen
            CreateCondiGen(0);
            //Boss boons
            _log.UpdateProgressWithCancellationCheck("CSV: Building Boss Boon Data");
            CreateBossBoonUptime(0);
        }
        private void CreateDPSTable(int phaseIndex)
        {
            PhaseData phase = _phases[phaseIndex];
            WriteLine(new[] { "Sub Group", "Profession","Role","Name","Account","WepSet1_1","WepSet1_2","WepSet2_1","WepSet2_2",
                "Boss DPS","Boss DMG","Boss Power DPS","Boss Power DMG","Boss Condi DPS","Boss Condi DMG",
                "All DPS","All DMG","All Power DPS","All Power DMG","All Condi DPS","All Condi DMG",
                "Times Downed", "Time Died","Percent Alive"});

            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                FinalDPS dps = player.GetDPSStats(_log, phase.Start, phase.End);
                FinalDefensesAll defense = player.GetDefenseStats(_log, phase.Start, phase.End);
                FinalDPS dpsBoss = player.GetDPSStats(_legacyTarget, _log, phase.Start, phase.End);
                string deathString = defense.DeadCount.ToString();
                string deadthTooltip = "";
                if (defense.DeadCount > 0)
                {
                    var deathDuration = TimeSpan.FromMilliseconds(defense.DeadDuration);
                    deadthTooltip = deathDuration.TotalSeconds + " seconds dead, " + (100.0 - Math.Round((deathDuration.TotalMilliseconds / phase.DurationInMS) * 100, 1)) + "% Alive";
                }
                IReadOnlyList<string> wep = player.GetWeaponSets(_log).ToArray();
                string build = "";
                if (player.Condition > 0)
                {
                    build += " Condi:" + player.Condition;
                }
                if (player.Concentration > 0)
                {
                    build += " Concentration:" + player.Concentration;
                }
                if (player.Healing > 0)
                {
                    build += " Healing:" + player.Healing;
                }
                if (player.Toughness > 0)
                {
                    build += " Toughness:" + player.Toughness;
                }
                WriteLine(new[] { player.Group.ToString(), player.Spec.ToString(),build,player.Character, player.Account ,wep[0],wep[1],wep[2],wep[3],
                dpsBoss.Dps.ToString(),dpsBoss.Damage.ToString(),dpsBoss.PowerDps.ToString(),dpsBoss.PowerDamage.ToString(),dpsBoss.CondiDps.ToString(),dpsBoss.CondiDamage.ToString(),
                dps.Dps.ToString(),dps.Damage.ToString(),dps.PowerDps.ToString(),dps.PowerDamage.ToString(),dps.CondiDps.ToString(),dps.CondiDamage.ToString(),
                defense.DownCount.ToString(), deathString, deadthTooltip});
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateBossDMGStatsTable(int phaseIndex)
        {
            //generate dmgstats table=
            WriteLine(new[] { "Sub Group", "Profession", "Name" ,
                "Critical%","Critical hits","Critical DMG",
                "Scholar%","Scholar hits","Scholar DMG","Scholar % increase",
                "Moving%","Moving Hits","Moving DMG","Moving % increase",
                "Flanking%","Flanking hits",
                "Glancing%","Glancing Hits",
                "Blind%","Blind Hits",
                "Total Hits",
                "Hits to Interupt","Hits Invulned","Time wasted","Time saved","Weapon Swaps"});
            int count = 0;
            PhaseData phase = _phases[phaseIndex];
            foreach (Player player in _noFakePlayers)
            {
                FinalGameplayStats stats = player.GetGameplayStats(_log, phase.Start, phase.End);
                FinalOffensiveStats statsBoss = player.GetOffensiveStats(_legacyTarget, _log, phase.Start, phase.End);
                IReadOnlyDictionary<string, DamageModifierStat> damageMods = player.GetDamageModifierStats(_legacyTarget, _log, phase.Start, phase.End);
                var scholar = new DamageModifierStat(0, 0, 0, 0);
                var moving = new DamageModifierStat(0, 0, 0, 0);
                if (damageMods.TryGetValue("Scholar Rune", out DamageModifierStat schoDict))
                {
                    scholar = schoDict;
                }
                if (damageMods.TryGetValue("Moving Bonus", out DamageModifierStat moveDict))
                {
                    moving = moveDict;
                }

                WriteLine(new[] { player.Group.ToString(), player.Spec.ToString(), player.Character,
                Math.Round((double)(statsBoss.CriticalCount) / statsBoss.CritableDirectDamageCount * 100,1).ToString(), statsBoss.CriticalCount.ToString(),statsBoss.CriticalDmg.ToString(),
                Math.Round((double)(scholar.HitCount) / scholar.TotalHitCount * 100,1).ToString(),scholar.HitCount.ToString(),scholar.DamageGain.ToString(),Math.Round(100.0 * (scholar.TotalDamage / (scholar.TotalDamage - scholar.DamageGain) - 1.0), 3).ToString(),
                Math.Round((double)(moving.HitCount) / moving.TotalHitCount * 100,1).ToString(),moving.HitCount.ToString(),moving.DamageGain.ToString(),Math.Round(100.0 * (moving.TotalDamage / (moving.TotalDamage - moving.DamageGain) - 1.0), 3).ToString(),
                Math.Round(statsBoss.FlankingCount / (double)statsBoss.ConnectedDirectDamageCount * 100,1).ToString(),statsBoss.FlankingCount.ToString(),
                Math.Round(statsBoss.GlanceCount / (double)statsBoss.ConnectedDirectDamageCount * 100,1).ToString(),statsBoss.GlanceCount.ToString(),
                Math.Round(statsBoss.Missed / (double)statsBoss.DirectDamageCount * 100,1).ToString(),statsBoss.Missed.ToString(),
                statsBoss.DirectDamageCount.ToString(),
                statsBoss.Interrupts.ToString(),statsBoss.Invulned.ToString(),stats.TimeWasted.ToString(),stats.TimeSaved.ToString(),stats.SwapCount.ToString() });
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateDmgStatsTable(int phaseIndex)
        {
            //generate dmgstats table
            WriteLine(new[] { "Sub Group", "Profession", "Name" ,
                "Critical%","Critical hits","Critical DMG",
                "Scholar%","Scholar hits","Scholar DMG","Scholar % increase",
                "Moving%","Moving Hits","Moving DMG","Moving % increase",
                "Flanking%","Flanking hits",
                "Glancing%","Glancing Hits",
                "Blind%","Blind Hits",
                "Total Hits",
                "Hits to Interupt","Hits Invulned","Time wasted","Time saved","Weapon Swaps"});
            int count = 0;
            PhaseData phase = _phases[phaseIndex];
            foreach (Player player in _noFakePlayers)
            {
                FinalGameplayStats gameplayStats = player.GetGameplayStats(_log, phase.Start, phase.End);
                FinalOffensiveStats offStats = player.GetOffensiveStats(null, _log, phase.Start, phase.End);
                IReadOnlyDictionary<string, DamageModifierStat> damageMods = player.GetDamageModifierStats(_legacyTarget, _log, phase.Start, phase.End);
                var scholar = new DamageModifierStat(0, 0, 0, 0);
                var moving = new DamageModifierStat(0, 0, 0, 0);
                if (damageMods.TryGetValue("Scholar Rune", out DamageModifierStat schoDict))
                {
                    scholar = schoDict;
                }
                if (damageMods.TryGetValue("Moving Bonus", out DamageModifierStat moveDict))
                {
                    moving = moveDict;
                }

                WriteLine(new[] { player.Group.ToString(), player.Spec.ToString(), player.Character,
                Math.Round((double)(offStats.CriticalCount) / offStats.CritableDirectDamageCount * 100,1).ToString(), offStats.CriticalCount.ToString(),offStats.CriticalDmg.ToString(),
                Math.Round((double)(scholar.HitCount) / scholar.TotalHitCount * 100,1).ToString(),scholar.HitCount.ToString(),scholar.DamageGain.ToString(),Math.Round(100.0 * (scholar.TotalDamage / (scholar.TotalDamage - scholar.DamageGain) - 1.0), 3).ToString(),
                Math.Round((double)(moving.HitCount) / moving.TotalHitCount * 100,1).ToString(),moving.HitCount.ToString(),moving.DamageGain.ToString(),Math.Round(100.0 * (moving.TotalDamage / (moving.TotalDamage - moving.DamageGain) - 1.0), 3).ToString(),
                Math.Round(offStats.FlankingCount / (double)offStats.ConnectedDirectDamageCount * 100,1).ToString(),offStats.FlankingCount.ToString(),
                Math.Round(offStats.GlanceCount / (double)offStats.ConnectedDirectDamageCount * 100,1).ToString(),offStats.GlanceCount.ToString(),
                Math.Round(offStats.Missed / (double)offStats.DirectDamageCount * 100,1).ToString(),offStats.Missed.ToString(),
                offStats.DirectDamageCount.ToString(),
                offStats.Interrupts.ToString(),offStats.Invulned.ToString(),gameplayStats.TimeWasted.ToString(),gameplayStats.TimeSaved.ToString(),gameplayStats.SwapCount.ToString() });
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateDefTable(int phaseIndex)
        {
            //generate defstats table
            WriteLine(new[] { "Sub Group", "Profession", "Name" ,
                "DMG Taken","DMG Barrier","Blocked","Invulned","Evaded","Dodges" });
            int count = 0;
            PhaseData phase = _phases[phaseIndex];
            foreach (Player player in _noFakePlayers)
            {
                FinalDefensesAll defenses = player.GetDefenseStats(_log, phase.Start, phase.End);

                WriteLine(new[] { player.Group.ToString(), player.Spec.ToString(), player.Character,
                defenses.DamageTaken.ToString(),defenses.DamageBarrier.ToString(),defenses.BlockedCount.ToString(),defenses.InvulnedCount.ToString(),defenses.EvadedCount.ToString(),defenses.DodgeCount.ToString() });
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateSupTable(int phaseIndex)
        {
            PhaseData phase = _phases[phaseIndex];
            //generate supstats table
            WriteLine(new[] { "Sub Group", "Profession", "Name" ,
                "Condi Cleanse","Condi Cleanse time", "Condi Cleanse Self","Condi Cleanse time self", "Boon Strips","Boon Strips time","Resurrects","Time Resurecting" });
            int count = 0;
            foreach (Player player in _noFakePlayers)
            {
                FinalToPlayersSupport support = player.GetToPlayerSupportStats(_log, phase.Start, phase.End);

                WriteLine(new[] { player.Group.ToString(), player.Spec.ToString(), player.Character,
                support.CondiCleanse.ToString(),support.CondiCleanseTime.ToString(), support.CondiCleanseSelf.ToString(), support.CondiCleanseTimeSelf.ToString(), support.BoonStrips.ToString(), support.BoonStripsTime.ToString(), support.Resurrects.ToString(),support.ResurrectTime.ToString() });
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateUptimeTable(IReadOnlyList<Buff> listToUse, int phaseIndex)
        {
            //generate Uptime Table table

            WriteCells(new[] { "Name", "Avg Boons" });
            foreach (Buff boon in listToUse)
            {
                WriteCell(boon.Name);

            }
            NewLine();

            PhaseData phase = _phases[phaseIndex];
            int count = 0;
            foreach (Player player in _noFakePlayers)
            {
                IReadOnlyDictionary<long, FinalActorBuffs> uptimes = player.GetBuffs(BuffEnum.Self, _log, phase.Start, phase.End);

                WriteCell(player.Character);
                WriteCell(player.GetGameplayStats(_log, phase.Start, phase.End).AvgBoons.ToString());
                foreach (Buff boon in listToUse)
                {
                    if (uptimes.TryGetValue(boon.ID, out FinalActorBuffs value))
                    {
                        if (boon.Type == Buff.BuffType.Duration)
                        {
                            WriteCell(value.Uptime + "%");
                        }
                        else if (boon.Type == Buff.BuffType.Intensity)
                        {
                            WriteCell(value.Uptime.ToString());
                        }

                    }
                    else
                    {
                        WriteCell("0");
                    }
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateGenSelfTable(IReadOnlyList<Buff> listToUse, int phaseIndex)
        {
            //generate Uptime Table table
            WriteCell("Name");
            foreach (Buff boon in listToUse)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();
            PhaseData phase = _phases[phaseIndex];
            int count = 0;
            foreach (Player player in _noFakePlayers)
            {
                IReadOnlyDictionary<long, FinalActorBuffs> uptimes = player.GetBuffs(BuffEnum.Self, _log, phase.Start, phase.End);

                WriteCell(player.Character);
                foreach (Buff boon in listToUse)
                {
                    string rate = "0";
                    string overstack = "0";
                    if (uptimes.TryGetValue(boon.ID, out FinalActorBuffs uptime))
                    {
                        if (uptime.Generation > 0 || uptime.Overstack > 0)
                        {
                            if (boon.Type == Buff.BuffType.Duration)
                            {
                                rate = uptime.Generation.ToString() + "%";
                                overstack = uptime.Overstack.ToString() + "%";
                            }
                            else if (boon.Type == Buff.BuffType.Intensity)
                            {
                                rate = uptime.Generation.ToString();
                                overstack = uptime.Overstack.ToString();
                            }
                        }
                    }
                    WriteCell(rate);
                    WriteCell(overstack);
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateGenGroupTable(IReadOnlyList<Buff> listToUse, int phaseIndex)
        {
            //generate Uptime Table table
            WriteCell("Name");
            foreach (Buff boon in listToUse)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();

            int count = 0;
            PhaseData phase = _phases[phaseIndex];
            foreach (Player player in _noFakePlayers)
            {
                IReadOnlyDictionary<long, FinalActorBuffs> boons = player.GetBuffs(BuffEnum.Group, _log, phase.Start, phase.End);

                WriteCell(player.Character);
                foreach (Buff boon in listToUse)
                {
                    string rate = "0";
                    string overstack = "0";
                    if (boons.TryGetValue(boon.ID, out FinalActorBuffs uptime))
                    {
                        if (uptime.Generation > 0 || uptime.Overstack > 0)
                        {
                            if (boon.Type == Buff.BuffType.Duration)
                            {
                                rate = uptime.Generation.ToString() + "%";
                                overstack = uptime.Overstack.ToString() + "%";
                            }
                            else if (boon.Type == Buff.BuffType.Intensity)
                            {
                                rate = uptime.Generation.ToString();
                                overstack = uptime.Overstack.ToString();
                            }

                        }
                    }
                    WriteCell(rate);
                    WriteCell(overstack);
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateGenOGroupTable(IReadOnlyList<Buff> listToUse, int phaseIndex)
        {
            //generate Uptime Table table
            WriteCell("Name");
            foreach (Buff boon in listToUse)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();

            int count = 0;
            PhaseData phase = _phases[phaseIndex];
            foreach (Player player in _noFakePlayers)
            {
                IReadOnlyDictionary<long, FinalActorBuffs> boons = player.GetBuffs(BuffEnum.OffGroup, _log, phase.Start, phase.End);

                WriteCell(player.Character);
                foreach (Buff boon in listToUse)
                {
                    string rate = "0";
                    string overstack = "0";
                    if (boons.TryGetValue(boon.ID, out FinalActorBuffs uptime))
                    {
                        if (uptime.Generation > 0 || uptime.Overstack > 0)
                        {
                            if (boon.Type == Buff.BuffType.Duration)
                            {
                                rate = uptime.Generation.ToString() + "%";
                                overstack = uptime.Overstack.ToString() + "%";
                            }
                            else if (boon.Type == Buff.BuffType.Intensity)
                            {
                                rate = uptime.Generation.ToString();
                                overstack = uptime.Overstack.ToString();
                            }

                        }
                    }
                    WriteCell(rate);
                    WriteCell(overstack);
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateGenSquadTable(IReadOnlyList<Buff> listToUse, int phaseIndex)
        {
            //generate Uptime Table table
            WriteCell("Name");
            foreach (Buff boon in listToUse)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();

            int count = 0;
            PhaseData phase = _phases[phaseIndex];
            foreach (Player player in _noFakePlayers)
            {
                IReadOnlyDictionary<long, FinalActorBuffs> boons = player.GetBuffs(BuffEnum.Squad, _log, phase.Start, phase.End);
                WriteCell(player.Character);
                foreach (Buff boon in listToUse)
                {
                    string rate = "0";
                    string overstack = "0";
                    if (boons.TryGetValue(boon.ID, out FinalActorBuffs uptime))
                    {
                        if (uptime.Generation > 0 || uptime.Overstack > 0)
                        {
                            if (boon.Type == Buff.BuffType.Duration)
                            {
                                rate = uptime.Generation.ToString() + "%";
                                overstack = uptime.Overstack.ToString() + "%";
                            }
                            else if (boon.Type == Buff.BuffType.Intensity)
                            {
                                rate = uptime.Generation.ToString();
                                overstack = uptime.Overstack.ToString();
                            }

                        }
                    }
                    WriteCell(rate);
                    WriteCell(overstack);
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateMechanicTable(int phaseIndex)
        {
            PhaseData phase = _phases[phaseIndex];
            IReadOnlyCollection<Mechanic> presMech = _log.MechanicData.GetPresentFriendlyMechs(_log, phase.Start, phase.End);
            //Dictionary<string, HashSet<Mechanic>> presEnemyMech = log.MechanicData.getPresentEnemyMechs(phaseIndex);
            //List<AbstractMasterPlayer> enemyList = log.MechanicData.getEnemyList(phaseIndex);
            int countLines = 0;
            if (presMech.Count > 0)
            {
                WriteCell("Name");
                foreach (Mechanic mech in presMech)
                {
                    WriteCell("\"" + mech.Description + "\"");
                }
                NewLine();

                foreach (Player p in _log.PlayerList)
                {
                    WriteCell(p.Character);
                    foreach (Mechanic mech in presMech)
                    {
                        int count = _log.MechanicData.GetMechanicLogs(_log, mech, p, phase.Start, phase.End).Count;
                        WriteCell(count.ToString());
                    }
                    NewLine();
                    countLines++;

                }

            }
            while (countLines < 15)//so each graph has equal spacing
            {
                NewLine();
                countLines++;
            }
        }
        private void CreateMechList()
        {
            MechanicData mData = _log.MechanicData;
            var mLogs = new List<MechanicEvent>();
            foreach (List<MechanicEvent> mLs in mData.GetAllMechanicEvents(_log))
            {
                mLogs.AddRange(mLs);
            }
            mLogs = mLogs.OrderBy(x => x.Time).ToList();
            int count = 0;
            WriteCell("Time");
            foreach (MechanicEvent m in mLogs)
            {
                WriteCell((m.Time / 1000.0).ToString());
            }
            NewLine();
            count++;
            WriteCell("Player");
            foreach (MechanicEvent m in mLogs)
            {
                WriteCell(m.Actor.Character);
            }
            NewLine();
            count++;
            WriteCell("Mechanic");
            foreach (MechanicEvent m in mLogs)
            {
                WriteCell("\"" + m.Description + "\"");
            }
            NewLine();
            count++;
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateBossCondiUptime(int phaseIndex)
        {
            NPC boss = _legacyTarget;
            PhaseData phase = _phases[phaseIndex];
            IReadOnlyDictionary<long, FinalActorBuffs> conditions = _legacyTarget.GetBuffs(BuffEnum.Self, _log, phase.Start, phase.End);

            WriteCell("Name");
            WriteCell("Avg");
            foreach (Buff boon in _statistics.PresentConditions)
            {
                WriteCell(boon.Name);
            }

            NewLine();
            int count = 0;
            WriteCell(boss.Character);
            WriteCell(Math.Round(_legacyTarget.GetGameplayStats(_log, phase.Start, phase.End).AvgConditions, 1).ToString());
            foreach (Buff boon in _statistics.PresentConditions)
            {
                if (conditions.TryGetValue(boon.ID, out FinalActorBuffs uptime))
                {
                    if (boon.Type == Buff.BuffType.Duration)
                    {
                        WriteCell(uptime.Uptime.ToString() + "%");
                    }
                    else
                    {
                        WriteCell(uptime.Uptime.ToString());
                    }
                }
                else
                {
                    WriteCell("0");
                }
            }
            count++;

            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateBossBoonUptime(int phaseIndex)
        {
            NPC boss = _legacyTarget;
            PhaseData phase = _phases[phaseIndex];
            IReadOnlyDictionary<long, FinalActorBuffs> conditions = _legacyTarget.GetBuffs(BuffEnum.Self, _log, phase.Start, phase.End);
            WriteCell("Name");
            WriteCell("Avg");
            foreach (Buff boon in _statistics.PresentBoons)
            {
                WriteCell(boon.Name);
            }

            NewLine();
            int count = 0;
            WriteCell(boss.Character);
            foreach (Buff boon in _statistics.PresentBoons)
            {
                if (conditions.TryGetValue(boon.ID, out FinalActorBuffs uptime))
                {
                    if (boon.Type == Buff.BuffType.Duration)
                    {
                        WriteCell(uptime.Uptime.ToString() + "%");
                    }
                    else
                    {
                        WriteCell(uptime.Uptime.ToString());
                    }
                }
                else
                {
                    WriteCell("0");
                }
            }
            count++;

            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateCondiGen(int phaseIndex)
        {
            PhaseData phase = _phases[phaseIndex];
            IReadOnlyDictionary<long, FinalBuffsDictionary> conditions = _legacyTarget.GetBuffsDictionary(_log, phase.Start, phase.End);
            //bool hasBoons = false;
            int count = 0;
            WriteCell("Name");
            foreach (Buff boon in _statistics.PresentConditions)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();
            foreach (Player player in _noFakePlayers)
            {
                WriteCell(player.Character);
                foreach (Buff boon in _statistics.PresentConditions)
                {
                    if (conditions.TryGetValue(boon.ID, out FinalBuffsDictionary uptime) && uptime.Generated.ContainsKey(player))
                    {
                        if (boon.Type == Buff.BuffType.Duration)
                        {
                            WriteCell(conditions[boon.ID].Generated[player].ToString() + "%");
                            WriteCell(conditions[boon.ID].Overstacked[player].ToString() + "%");
                        }
                        else
                        {
                            WriteCell(conditions[boon.ID].Generated[player].ToString());
                            WriteCell(conditions[boon.ID].Overstacked[player].ToString());
                        }
                    }
                    else
                    {
                        WriteCell("0");
                        WriteCell("0");
                    }
                }
                NewLine();
                count++;
            }


            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
    }
}
