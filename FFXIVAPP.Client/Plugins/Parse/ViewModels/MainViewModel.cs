﻿// FFXIVAPP.Client
// MainViewModel.cs
// 
// © 2013 Ryan Wilson

#region Usings

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using FFXIVAPP.Client.Helpers;
using FFXIVAPP.Client.Plugins.Parse.Models;
using FFXIVAPP.Client.Plugins.Parse.Models.Events;
using FFXIVAPP.Client.Plugins.Parse.Models.Stats;
using FFXIVAPP.Client.Plugins.Parse.Models.Timelines;
using FFXIVAPP.Client.Plugins.Parse.Monitors;
using FFXIVAPP.Client.Plugins.Parse.Views;
using FFXIVAPP.Client.Properties;
using FFXIVAPP.Common.Models;
using FFXIVAPP.Common.RegularExpressions;
using FFXIVAPP.Common.ViewModelBase;
using Microsoft.Win32;
using Newtonsoft.Json;
using SmartAssembly.Attributes;

#endregion

namespace FFXIVAPP.Client.Plugins.Parse.ViewModels
{
    [DoNotObfuscate]
    internal sealed class MainViewModel : INotifyPropertyChanged
    {
        #region Property Bindings

        private static MainViewModel _instance;
        private bool _isAdvancedView;
        private bool _isBasicView;

        public static MainViewModel Instance
        {
            get { return _instance ?? (_instance = new MainViewModel()); }
        }

        public bool IsBasicView
        {
            get { return _isBasicView; }
            set
            {
                _isBasicView = value;
                RaisePropertyChanged();
            }
        }

        public bool IsAdvancedView
        {
            get { return _isAdvancedView; }
            set
            {
                _isAdvancedView = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Declarations

        public ICommand ProcessSampleCommand { get; private set; }
        public ICommand SwitchToLogViewCommand { get; private set; }
        public ICommand SwitchToAdvancedViewCommand { get; private set; }
        public ICommand SwitchToBasicViewCommand { get; private set; }
        public ICommand ResetStatsCommand { get; private set; }
        public ICommand Convert2JsonCommand { get; private set; }
        public ICommand SetCommand { get; private set; }
        public ICommand GetCommand { get; private set; }

        #endregion

        public MainViewModel()
        {
            IsBasicView = true;
            ProcessSampleCommand = new DelegateCommand(ProcessSample);
            SwitchToLogViewCommand = new DelegateCommand(SwitchToLogView);
            SwitchToAdvancedViewCommand = new DelegateCommand(SwitchToAdvancedView);
            SwitchToBasicViewCommand = new DelegateCommand(SwitchToBasicView);
            ResetStatsCommand = new DelegateCommand(ResetStats);
            Convert2JsonCommand = new DelegateCommand(Convert2Json);
            SetCommand = new DelegateCommand(Set);
            GetCommand = new DelegateCommand(Get);
        }

        public static List<Dictionary<string,List<StatGroup>>> ParseHistoryList = new List<Dictionary<string, List<StatGroup>>>();

        private static void Set()
        {
            var hasDamage = ParseControl.Instance.Timeline.Overall.Stats.GetStatValue("TotalOverallDamage") > 0;
            if (hasDamage)
            {
                var historyItem = new Dictionary<string, List<StatGroup>>
                {
                    {
                        "Overall", new List<StatGroup>
                        {
                            ParseControl.Instance.Timeline.Overall
                        }
                    },
                    {
                        "Party", ParseControl.Instance.Timeline.Party.ToList()
                    },
                    {
                        "Monster", ParseControl.Instance.Timeline.Monster.ToList()
                    }
                };
                ParseHistoryList.Add(historyItem);
            }
            ParseControl.Instance.StatMonitor.Clear();
        }

        private static void Get()
        {
            if (!ParseHistoryList.Any())
            {
                return;
            }
            var historyItem = ParseHistoryList.First();
            var overall = historyItem["Overall"];
            foreach (var o in overall)
            {
                ParseControl.Instance.Timeline.Overall.Add(o);
            }
            var monsters = historyItem["Monster"];
            foreach (var m in monsters)
            {
                ParseControl.Instance.Timeline.Monster.Add(m);
            }
            var party = historyItem["Party"];
            foreach (var p in party)
            {
                ParseControl.Instance.Timeline.Party.Add(p);
            }
        }

        #region Loading Functions

        #endregion

        #region Utility Functions

        #endregion

        #region Command Bindings

        private static void ProcessSample()
        {
            if (Constants.IsOpen)
            {
                var popupContent = new PopupContent();
                popupContent.Title = AppViewModel.Instance.Locale["app_WarningMessage"];
                popupContent.Message = "Game is open. Please close before choosing a file.";
                popupContent.IsOkayOnly = true;
                PopupHelper.Toggle(popupContent);
                EventHandler closedDelegate = null;
                closedDelegate = delegate { PopupHelper.MessagePopup.Closed -= closedDelegate; };
                PopupHelper.MessagePopup.Closed += closedDelegate;
                return;
            }
            var openFileDialog = new OpenFileDialog
            {
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory + "Logs",
                Multiselect = false,
                Filter = "XML Files (*.xml)|*.xml"
            };
            openFileDialog.FileOk += delegate
            {
                var count = 0;
                var sampleXml = XDocument.Load(openFileDialog.FileName);
                var items = new Dictionary<int, string[]>();
                foreach (var xElement in sampleXml.Descendants()
                                                  .Elements("Entry"))
                {
                    var xKey = (string) xElement.Attribute("Key");
                    var xLine = (string) xElement.Element("Line");
                    var xTimeStamp = (string) xElement.Element("TimeStamp");
                    if (String.IsNullOrWhiteSpace(xKey) || String.IsNullOrWhiteSpace(xLine))
                    {
                        continue;
                    }
                    items.Add(count, new[]
                    {
                        xKey, xLine, xTimeStamp
                    });
                    ++count;
                }
                Func<bool> dFunc = delegate
                {
                    foreach (var item in items)
                    {
                        var code = item.Value[0];
                        var line = item.Value[1];
                        line = line.Replace("", "⇒");
                        line = line.Replace("[01010101]", "");
                        line = line.Replace("[CF010101]", "");
                        var timeStampColor = Settings.Default.TimeStampColor.ToString();
                        var timeStamp = DateTime.Now.ToString("[HH:mm:ss] ");
                        timeStamp = String.IsNullOrWhiteSpace(item.Value[2]) ? timeStamp : item.Value[2].Trim() + " ";
                        var color = (Constants.Colors.ContainsKey(code)) ? Constants.Colors[code][0] : "FFFFFF";
                        if (Constants.Parse.Abilities.Contains(code) && Regex.IsMatch(line, @".+(((cast|use)s?|(lance|utilise)z?)\s|の「)", SharedRegEx.DefaultOptions))
                        {
                            Common.Constants.FD.AppendFlow(timeStamp, "", line, new[]
                            {
                                timeStampColor, "#" + color
                            }, MainView.View.AbilityChatFD._FDR);
                        }
                        EventParser.Instance.ParseAndPublish(Convert.ToUInt32(code, 16), line, false);
                    }
                    return true;
                };
                dFunc.BeginInvoke(null, null);
            };
            openFileDialog.ShowDialog();
        }

        private static void SwitchToLogView()
        {
            if (MainView.View.MainViewTC.SelectedIndex == 2)
            {
                return;
            }
            MainView.View.MainViewTC.SelectedIndex = 2;
            if (Instance.IsBasicView)
            {
                Instance.IsBasicView = false;
                Instance.IsAdvancedView = true;
                return;
            }
            if (!Instance.IsAdvancedView)
            {
                return;
            }
            Instance.IsBasicView = true;
            Instance.IsAdvancedView = false;
        }

        private static void SwitchToAdvancedView()
        {
            MainView.View.MainViewTC.SelectedIndex = 1;
            Instance.IsBasicView = false;
            Instance.IsAdvancedView = true;
        }

        private static void SwitchToBasicView()
        {
            MainView.View.MainViewTC.SelectedIndex = 0;
            Instance.IsBasicView = true;
            Instance.IsAdvancedView = false;
        }

        /// <summary>
        /// </summary>
        private static void ResetStats()
        {
            var popupContent = new PopupContent();
            popupContent.Title = AppViewModel.Instance.Locale["app_WarningMessage"];
            popupContent.Message = AppViewModel.Instance.Locale["parse_ResetStatsMessage"];
            popupContent.CanSayNo = true;
            PopupHelper.Toggle(popupContent);
            EventHandler closedDelegate = null;
            closedDelegate = delegate
            {
                switch (PopupHelper.Result)
                {
                    case MessageBoxResult.Yes:
                        MainView.View.AbilityChatFD._FD.Blocks.Clear();
                        ParseControl.Instance.StatMonitor.Clear();
                        break;
                    case MessageBoxResult.No:
                        break;
                }
                PopupHelper.MessagePopup.Closed -= closedDelegate;
            };
            PopupHelper.MessagePopup.Closed += closedDelegate;
        }

        private static void Convert2Json()
        {
            #region Generate Overall-Player-Monster

            dynamic overallStats = new Dictionary<string, object>();
            dynamic playerStats = new Dictionary<string, object>();
            dynamic monsterStats = new Dictionary<string, object>();
            overallStats.Add("Stats", ParseControl.Instance.Timeline.Overall.Stats.ToDictionary(s => s.Name, s => s.Value));
            var partyTimeline = ParseControl.Instance.Timeline.Party;
            var playerNames = partyTimeline.Select(p => p.Name)
                                           .ToList();
            foreach (var playerName in playerNames)
            {
                var player = partyTimeline.GetGroup(playerName);
                playerStats.Add(playerName, new Dictionary<string, object>
                {
                    {
                        "Stats", new Dictionary<string, object>()
                    },
                    {
                        "Abilities", new Dictionary<string, object>()
                    },
                    {
                        "Monsters", new Dictionary<string, object>()
                    },
                    {
                        "Healing", new Dictionary<string, object>()
                    },
                    {
                        "Players", new Dictionary<string, object>()
                    },
                    {
                        "Damage", new Dictionary<string, object>()
                    }
                });
                playerStats[playerName]["Stats"] = player.Stats.ToDictionary(s => s.Name, s => s.Value);
                var playerAbilities = player.GetGroup("Abilities");
                foreach (var playerAbility in playerAbilities)
                {
                    playerStats[playerName]["Abilities"].Add(playerAbility.Name, playerAbility.Stats.ToDictionary(s => s.Name, s => s.Value));
                }
                var playerMonsters = player.GetGroup("Monsters");
                foreach (var playerMonster in playerMonsters)
                {
                    playerStats[playerName]["Monsters"].Add(playerMonster.Name, new Dictionary<string, object>
                    {
                        {
                            "Stats", playerMonster.Stats.ToDictionary(s => s.Name, s => s.Value)
                        },
                        {
                            "Abilities", playerMonster.GetGroup("Abilities")
                                                      .ToDictionary(a => a.Name, a => a.Stats.ToDictionary(s => s.Name, s => s.Value))
                        }
                    });
                }
                var playerHealings = player.GetGroup("Healing");
                foreach (var playerHealing in playerHealings)
                {
                    playerStats[playerName]["Healing"].Add(playerHealing.Name, playerHealing.Stats.ToDictionary(s => s.Name, s => s.Value));
                }
                var playerPlayers = player.GetGroup("Players");
                foreach (var playerPlayer in playerPlayers)
                {
                    playerStats[playerName]["Players"].Add(playerPlayer.Name, new Dictionary<string, object>
                    {
                        {
                            "Stats", playerPlayer.Stats.ToDictionary(s => s.Name, s => s.Value)
                        },
                        {
                            "Abilities", playerPlayer.GetGroup("Abilities")
                                                     .ToDictionary(a => a.Name, a => a.Stats.ToDictionary(s => s.Name, s => s.Value))
                        }
                    });
                }
                var playerDamages = player.GetGroup("Damage");
                foreach (var playerDamage in playerDamages)
                {
                    playerStats[playerName]["Damage"].Add(playerDamage.Name, new Dictionary<string, object>
                    {
                        {
                            "Stats", playerDamage.Stats.ToDictionary(s => s.Name, s => s.Value)
                        },
                        {
                            "Abilities", playerDamage.GetGroup("Abilities")
                                                     .ToDictionary(a => a.Name, a => a.Stats.ToDictionary(s => s.Name, s => s.Value))
                        }
                    });
                }
            }
            var monsterTimeline = ParseControl.Instance.Timeline.Monster;
            var monsterNames = monsterTimeline.Select(p => p.Name)
                                              .ToList();
            foreach (var monsterName in monsterNames)
            {
                var monster = monsterTimeline.GetGroup(monsterName);
                monsterStats.Add(monsterName, new Dictionary<string, object>
                {
                    {
                        "Stats", new Dictionary<string, object>()
                    },
                    {
                        "Abilities", new Dictionary<string, object>()
                    },
                    {
                        "Drops", new Dictionary<string, object>()
                    }
                });
                monsterStats[monsterName]["Stats"] = monster.Stats.ToDictionary(s => s.Name, s => s.Value);
                var monsterAbilities = monster.GetGroup("Abilities");
                foreach (var monsterAbility in monsterAbilities)
                {
                    monsterStats[monsterName]["Abilities"].Add(monsterAbility.Name, monsterAbility.Stats.ToDictionary(s => s.Name, s => s.Value));
                }
                var monsterDrops = monster.GetGroup("Drops");
                foreach (var monsterDrop in monsterDrops)
                {
                    monsterStats[monsterName]["Drops"].Add(monsterDrop.Name, monsterDrop.Stats.ToDictionary(s => s.Name, s => s.Value));
                }
            }
            dynamic results = new Dictionary<string, object>
            {
                {
                    "Overall", overallStats
                },
                {
                    "Player", playerStats
                },
                {
                    "Monster", monsterStats
                }
            };

            #endregion

            Clipboard.SetText(JsonConvert.SerializeObject(results));
        }

        #endregion

        #region Implementation of INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private void RaisePropertyChanged([CallerMemberName] string caller = "")
        {
            PropertyChanged(this, new PropertyChangedEventArgs(caller));
        }

        #endregion
    }
}