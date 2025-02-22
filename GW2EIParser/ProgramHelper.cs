﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Discord;
using GW2EIBuilders;
using GW2EIDiscord;
using GW2EIDPSReport;
using GW2EIDPSReport.DPSReportJsons;
using GW2EIWingman;
using GW2EIEvtcParser;
using GW2EIGW2API;
using GW2EIParser.Exceptions;

namespace GW2EIParser
{
    internal static class ProgramHelper
    {
        internal static HTMLAssets htmlAssets { get; set; }

        internal static Version ParserVersion { get; } = new Version(Application.ProductVersion);

        private static readonly UTF8Encoding NoBOMEncodingUTF8 = new UTF8Encoding(false);

        internal static readonly string SkillAPICacheLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Content/SkillList.json";
        internal static readonly string SpecAPICacheLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Content/SpecList.json";
        internal static readonly string TraitAPICacheLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Content/TraitList.json";
        internal static readonly string EILogPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Logs/";

        internal static readonly GW2APIController APIController = new GW2APIController(SkillAPICacheLocation, SpecAPICacheLocation, TraitAPICacheLocation);

        internal static int GetMaxParallelRunning()
        {
            if (Properties.Settings.Default.SendEmbedToWebhook || Properties.Settings.Default.UploadToDPSReports || (false && Properties.Settings.Default.UploadToWingman))
            {
                return Math.Max(Environment.ProcessorCount / 2, 1);
            }
            else
            {
                return Environment.ProcessorCount;
            }
        }

        internal static EmbedBuilder GetEmbedBuilder()
        {
            var builder = new EmbedBuilder();
            builder.WithAuthor("Elite Insights " + ParserVersion.ToString(), "https://github.com/baaron4/GW2-Elite-Insights-Parser/blob/master/GW2EIParser/Content/LI.png?raw=true", "https://github.com/baaron4/GW2-Elite-Insights-Parser");
            return builder;
        }

        private static Embed BuildEmbed(ParsedEvtcLog log, string dpsReportPermalink)
        {
            EmbedBuilder builder = GetEmbedBuilder();
            builder.WithThumbnailUrl(log.FightData.Logic.Icon);
            //
            builder.AddField("Encounter Duration", log.FightData.DurationString);
            //
            if (log.FightData.Logic.GetInstanceBuffs(log).Any())
            {
                builder.AddField("Instance Buffs", string.Join("\n", log.FightData.Logic.GetInstanceBuffs(log).Select(x => (x.stack > 1 ? x.stack + " " : "") + x.buff.Name)));
            }
            //
            /*var playerByGroup = log.PlayerList.Where(x => !x.IsFakeActor).GroupBy(x => x.Group).ToDictionary(x => x.Key, x => x.ToList());
            var hasGroups = playerByGroup.Count > 1;
            foreach (KeyValuePair<int, List<Player>> pair in playerByGroup)
            {
                var groupField = new List<string>();
                foreach (Player p in pair.Value)
                {
                    groupField.Add(p.Character + " - " + p.Prof);
                }
                builder.AddField(hasGroups ? "Group " + pair.Key : "Party Composition", String.Join("\n", groupField));
            }*/
            //
            builder.AddField("Game Data", "ARC: " + log.LogData.ArcVersion + " | " + "GW2 Build: " + log.LogData.GW2Build);
            //
            builder.WithTitle(log.FightData.FightName);
            //builder.WithTimestamp(DateTime.Now);
            builder.WithFooter(log.LogData.LogStartStd + " / " + log.LogData.LogEndStd);
            builder.WithColor(log.FightData.Success ? Color.Green : Color.Red);
            if (dpsReportPermalink.Length > 0)
            {
                builder.WithUrl(dpsReportPermalink);
            }
            return builder.Build();
        }

        private static bool HasFormat()
        {
            return Properties.Settings.Default.SaveOutCSV || Properties.Settings.Default.SaveOutHTML || Properties.Settings.Default.SaveOutXML || Properties.Settings.Default.SaveOutJSON;
        }

        public static bool ParseMultipleLogs()
        {
            if (Properties.Settings.Default.ParseMultipleLogs)
            {
                if (!HasFormat() && (Properties.Settings.Default.UploadToDPSReports || (false && Properties.Settings.Default.UploadToWingman)))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        private static string[] UploadOperation(FileInfo fInfo, ParsedEvtcLog originalLog, OperationController originalController)
        {
            // Only upload supported 5 men, 10 men and golem logs, without anonymous players
            var isWingmanCompatible = !originalLog.ParserSettings.AnonymousPlayers && (
                            originalLog.FightData.Logic.Mode == GW2EIEvtcParser.EncounterLogic.FightLogic.ParseMode.Instanced10 ||
                            originalLog.FightData.Logic.Mode == GW2EIEvtcParser.EncounterLogic.FightLogic.ParseMode.Instanced5 ||
                            originalLog.FightData.Logic.Mode == GW2EIEvtcParser.EncounterLogic.FightLogic.ParseMode.Benchmark
                            );
            //Upload Process
            string[] uploadresult = new string[2] { "", "" };
            if (Properties.Settings.Default.UploadToDPSReports)
            {
                originalController.UpdateProgressWithCancellationCheck("Uploading to DPSReport using EI");
                DPSReportUploadObject response = DPSReportController.UploadUsingEI(fInfo, str => originalController.UpdateProgress(str), Properties.Settings.Default.DPSReportUserToken,
                originalLog.ParserSettings.AnonymousPlayers,
                originalLog.ParserSettings.DetailedWvWParse);
                uploadresult[0] = response != null ? response.Permalink : "Upload process failed";
                originalController.UpdateProgressWithCancellationCheck("DPSReports using EI: " + uploadresult[0]);
                /*
                if (Properties.Settings.Default.UploadToWingman)
                {
                    if (isWingmanCompatible)
                    {
                        traces.Add("Uploading to Wingman using DPSReport url");
                        WingmanController.UploadToWingmanUsingImportLogQueued(uploadresult[0], traces, ParserVersion);
                    }
                    else
                    {
                        traces.Add("Can not upload to Wingman using DPSReport url: unsupported log");
                    }
                }
                */
            }
            if (false && Properties.Settings.Default.UploadToWingman)
            {
#if !DEBUG
                if (!isWingmanCompatible)
                {
                    originalController.UpdateProgressWithCancellationCheck("Can not upload to Wingman: unsupported log");
                } 
                else
                {
                    string accName = originalLog.LogData.PoV != null ? originalLog.LogData.PoVAccount : null;

                    if (WingmanController.CheckUploadPossible(fInfo, accName, str => originalController.UpdateProgress(str), ParserVersion))
                    {
                        try
                        {
                            var expectedSettings = new EvtcParserSettings(Properties.Settings.Default.Anonymous,
                                                            Properties.Settings.Default.SkipFailedTries,
                                                            true,
                                                            true,
                                                            true,
                                                            Properties.Settings.Default.CustomTooShort,
                                                            Properties.Settings.Default.DetailledWvW);
                            ParsedEvtcLog logToUse = originalLog;
                            if (originalLog.ParserSettings.ComputeDamageModifiers != expectedSettings.ComputeDamageModifiers ||
                                originalLog.ParserSettings.ParsePhases != expectedSettings.ParsePhases ||
                                originalLog.ParserSettings.ParseCombatReplay != expectedSettings.ParseCombatReplay)
                            {
                                // We need to create a parser that matches Wingman's expected settings
                                var parser = new EvtcParser(expectedSettings, APIController);
                                originalController.UpdateProgressWithCancellationCheck("Wingman: Setting mismatch, creating a new ParsedEvtcLog");
                                logToUse = parser.ParseLog(originalController, fInfo, out GW2EIEvtcParser.ParserHelpers.ParsingFailureReason failureReason, Properties.Settings.Default.MultiThreaded);
                            }
                            byte[] jsonFile, htmlFile;
                            var uploadResult = new UploadResults();
                            {
                                var ms = new MemoryStream();
                                var sw = new StreamWriter(ms, NoBOMEncodingUTF8);
                                var builder = new RawFormatBuilder(logToUse, new RawFormatSettings(true), ParserVersion, uploadResult);

                                builder.CreateJSON(sw, false);
                                sw.Close();

                                jsonFile = ms.ToArray();
                            }
                            {
                                var ms = new MemoryStream();
                                var sw = new StreamWriter(ms, NoBOMEncodingUTF8);
                                var builder = new HTMLBuilder(logToUse, new HTMLSettings(false, false, null, null, true), htmlAssets, ParserVersion, uploadResult);

                                builder.CreateHTML(sw, null);
                                sw.Close();
                                htmlFile = ms.ToArray();
                            }
                            if (logToUse != originalLog)
                            {
                                originalController.UpdateProgressWithCancellationCheck("Wingman: new ParsedEvtcLog processing completed");
                            }
                            WingmanController.UploadProcessed(fInfo, accName, jsonFile, htmlFile, str => originalController.UpdateProgress(str), ParserVersion);
                        }
                        catch (Exception e)
                        {
                            originalController.UpdateProgressWithCancellationCheck("Can not upload to Wingman: " + e.Message);
                        }
                    } 
                    else
                    {
                        originalController.UpdateProgressWithCancellationCheck("Can not upload to Wingman: log already uploaded");
                    }
                }
                originalController.UpdateProgressWithCancellationCheck("Wingman: operation completed");
#endif

            }
            return uploadresult;
        }

        public static void DoWork(OperationController operation)
        {
            System.Globalization.CultureInfo before = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo("en-US");
            operation.Reset();
            try
            {
                operation.Start();
                var fInfo = new FileInfo(operation.InputFile);

                var parser = new EvtcParser(new EvtcParserSettings(Properties.Settings.Default.Anonymous,
                                                Properties.Settings.Default.SkipFailedTries,
                                                Properties.Settings.Default.ParsePhases,
                                                Properties.Settings.Default.ParseCombatReplay,
                                                Properties.Settings.Default.ComputeDamageModifiers,
                                                Properties.Settings.Default.CustomTooShort,
                                                Properties.Settings.Default.DetailledWvW),
                                            APIController);

                //Process evtc here
                ParsedEvtcLog log = parser.ParseLog(operation, fInfo, out GW2EIEvtcParser.ParserHelpers.ParsingFailureReason failureReason, Properties.Settings.Default.MultiThreaded && HasFormat());
                if (failureReason != null)
                {
                    failureReason.Throw();
                }
                operation.BasicMetaData = new OperationController.OperationBasicMetaData(log);
                string[] uploadStrings = UploadOperation(fInfo, log, operation);
                if (Properties.Settings.Default.SendEmbedToWebhook && Properties.Settings.Default.UploadToDPSReports)
                {
                    if (Properties.Settings.Default.SendSimpleMessageToWebhook)
                    {
                        WebhookController.SendMessage(Properties.Settings.Default.WebhookURL, uploadStrings[0], out string message);
                        operation.UpdateProgressWithCancellationCheck(message);
                    } 
                    else
                    {
                        WebhookController.SendMessage(Properties.Settings.Default.WebhookURL, BuildEmbed(log, uploadStrings[0]),out string message);
                        operation.UpdateProgressWithCancellationCheck(message);
                    }
                }
                if (uploadStrings[0].Contains("https"))
                {
                    operation.DPSReportLink = uploadStrings[0];
                }
                //Creating File
                GenerateFiles(log, operation, uploadStrings, fInfo);
            }
            catch (Exception ex)
            {
                throw new ProgramException(ex);
            }
            finally
            {
                operation.Stop();
                GC.Collect();
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        private static void CompressFile(string file, MemoryStream str, OperationController operation)
        {
            // Create the compressed file.
            byte[] data = str.ToArray();
            string outputFile = file + ".gz";
            using (FileStream outFile =
                        File.Create(outputFile))
            {
                using (var Compress =
                    new GZipStream(outFile,
                    CompressionMode.Compress))
                {
                    // Copy the source file into 
                    // the compression stream.
                    Compress.Write(data, 0, data.Length);
                }
            }
            operation.GeneratedFiles.Add(outputFile);
        }

        private static DirectoryInfo GetSaveDirectory(FileInfo fInfo)
        {
            //save location
            DirectoryInfo saveDirectory;
            if (Properties.Settings.Default.SaveAtOut || Properties.Settings.Default.OutLocation == null)
            {
                //Default save directory
                saveDirectory = fInfo.Directory;
                if (!saveDirectory.Exists)
                {
                    throw new InvalidOperationException("Save directory does not exist");
                }
            }
            else
            {
                if (!Directory.Exists(Properties.Settings.Default.OutLocation))
                {
                    throw new InvalidOperationException("Save directory does not exist");
                }
                saveDirectory = new DirectoryInfo(Properties.Settings.Default.OutLocation);
            }
            return saveDirectory;
        }

        public static void GenerateTraceFile(OperationController operation)
        {
            if (Properties.Settings.Default.SaveOutTrace)
            {
                var fInfo = new FileInfo(operation.InputFile);

                string fName = fInfo.Name.Split('.')[0];
                if (!fInfo.Exists)
                {
                    fInfo = new FileInfo(AppDomain.CurrentDomain.BaseDirectory);
                }

                DirectoryInfo saveDirectory = GetSaveDirectory(fInfo);

                string outputFile = Path.Combine(
                saveDirectory.FullName,
                $"{fName}.log"
                );
                operation.GeneratedFiles.Add(outputFile);
                using (var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                using (var sw = new StreamWriter(fs))
                {
                    operation.WriteLogMessages(sw);
                }
                operation.OutLocation = saveDirectory.FullName;
            }
        }

        private static void GenerateFiles(ParsedEvtcLog log, OperationController operation, string[] uploadStrings, FileInfo fInfo)
        {
            operation.UpdateProgressWithCancellationCheck("Creating File(s)");

            DirectoryInfo saveDirectory = GetSaveDirectory(fInfo);

            string result = log.FightData.Success ? "kill" : "fail";
            string encounterLengthTerm = Properties.Settings.Default.AddDuration ? "_" + (log.FightData.FightDuration / 1000).ToString() + "s" : "";
            string PoVClassTerm = Properties.Settings.Default.AddPoVProf ? "_" + log.LogData.PoV.Spec.ToString().ToLower() : "";
            string fName = fInfo.Name.Split('.')[0];
            fName = $"{fName}{PoVClassTerm}_{log.FightData.Logic.Extension}{encounterLengthTerm}_{result}";

            var uploadResults = new UploadResults(uploadStrings[0], uploadStrings[1]);
            operation.OutLocation = saveDirectory.FullName;
            if (Properties.Settings.Default.SaveOutHTML)
            {
                operation.UpdateProgressWithCancellationCheck("Creating HTML");
                string outputFile = Path.Combine(
                saveDirectory.FullName,
                $"{fName}.html"
                );
                operation.GeneratedFiles.Add(outputFile);
                operation.OpenableFiles.Add(outputFile);
                using (var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                using (var sw = new StreamWriter(fs))
                {
                    var builder = new HTMLBuilder(log, 
                        new HTMLSettings(
                            Properties.Settings.Default.LightTheme, 
                            Properties.Settings.Default.HtmlExternalScripts,
                            Properties.Settings.Default.HtmlExternalScriptsPath,
                            Properties.Settings.Default.HtmlExternalScriptsCdn,
                            Properties.Settings.Default.HtmlCompressJson
                        ), htmlAssets, ParserVersion, uploadResults);
                    builder.CreateHTML(sw, saveDirectory.FullName);
                }
                operation.UpdateProgressWithCancellationCheck("HTML created");
            }
            if (Properties.Settings.Default.SaveOutCSV)
            {
                operation.UpdateProgressWithCancellationCheck("Creating CSV");
                string outputFile = Path.Combine(
                    saveDirectory.FullName,
                    $"{fName}.csv"
                );
                operation.GeneratedFiles.Add(outputFile);
                operation.OpenableFiles.Add(outputFile);
                using (var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                using (var sw = new StreamWriter(fs, Encoding.GetEncoding(1252)))
                {
                    var builder = new CSVBuilder(log, new CSVSettings(","), ParserVersion, uploadResults);
                    builder.CreateCSV(sw);
                }
                operation.UpdateProgressWithCancellationCheck("CSV created");
            }
            if (Properties.Settings.Default.SaveOutJSON || Properties.Settings.Default.SaveOutXML)
            {
                var builder = new RawFormatBuilder(log, new RawFormatSettings(Properties.Settings.Default.RawTimelineArrays), ParserVersion, uploadResults);
                if (Properties.Settings.Default.SaveOutJSON)
                {
                    operation.UpdateProgressWithCancellationCheck("Creating JSON");
                    string outputFile = Path.Combine(
                        saveDirectory.FullName,
                        $"{fName}.json"
                    );
                    Stream str;
                    if (Properties.Settings.Default.CompressRaw)
                    {
                        str = new MemoryStream();
                    }
                    else
                    {
                        str = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
                    }
                    using (var sw = new StreamWriter(str, NoBOMEncodingUTF8))
                    {
                        builder.CreateJSON(sw, Properties.Settings.Default.IndentJSON);
                    }
                    if (str is MemoryStream msr)
                    {
                        CompressFile(outputFile, msr, operation);
                        operation.UpdateProgressWithCancellationCheck("JSON compressed");
                    }
                    else
                    {
                        operation.GeneratedFiles.Add(outputFile);
                    }
                    operation.UpdateProgressWithCancellationCheck("JSON created");
                }
                if (Properties.Settings.Default.SaveOutXML)
                {
                    operation.UpdateProgressWithCancellationCheck("Creating XML");
                    string outputFile = Path.Combine(
                        saveDirectory.FullName,
                        $"{fName}.xml"
                    );
                    Stream str;
                    if (Properties.Settings.Default.CompressRaw)
                    {
                        str = new MemoryStream();
                    }
                    else
                    {
                        str = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
                    }
                    using (var sw = new StreamWriter(str, NoBOMEncodingUTF8))
                    {
                        builder.CreateXML(sw, Properties.Settings.Default.IndentXML);
                    }
                    if (str is MemoryStream msr)
                    {
                        CompressFile(outputFile, msr, operation);
                        operation.UpdateProgressWithCancellationCheck("XML compressed");
                    }
                    else
                    {
                        operation.GeneratedFiles.Add(outputFile);
                    }
                    operation.UpdateProgressWithCancellationCheck("XML created");
                }
            }
            operation.UpdateProgressWithCancellationCheck($"Completed parsing for {result}ed {log.FightData.Logic.Extension}");
        }

    }
}
