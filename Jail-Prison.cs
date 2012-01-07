﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Threading;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;

namespace JailPrison
{
    [APIVersion(1, 10)]
    public class Jail : TerrariaPlugin
    {
        public static List<Player> Players = new List<Player>();
        public static JPConfigFile JPConfig { get; set; }
        internal static string JPConfigPath { get { return Path.Combine(TShock.SavePath, "jpconfig.json"); } }

        public override string Name
        {
            get { return "Jail & Prison"; }
        }
        public override string Author
        {
            get { return "Created by DarkunderdoG"; }
        }
        public override string Description
        {
            get { return "Jail & Prison Plugin"; }
        }
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override void Initialize()
        {
            GameHooks.Initialize += OnInitialize;
            GameHooks.Update += OnUpdate;
            NetHooks.GreetPlayer += OnGreetPlayer;
            ServerHooks.Leave += OnLeave;
            ServerHooks.Chat += OnChat;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Initialize -= OnInitialize;
                GameHooks.Update -= OnUpdate;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                ServerHooks.Leave -= OnLeave;
                ServerHooks.Chat -= OnChat;
            }

            base.Dispose(disposing);
        }

        public Jail(Main game)
            : base(game)
        {
            Order = -3;
            JPConfig = new JPConfigFile();
        }

        public void OnInitialize()
        {
            SetupConfig();
            if (JPConfig.jailmode)
                Commands.ChatCommands.Add(new Command("jailcomm", warpjail, JPConfig.jailcomm));
            if (JPConfig.prisonmode)
            {
                Commands.ChatCommands.Add(new Command("prison", imprison, "imprison"));
                Commands.ChatCommands.Add(new Command("prison", setfree, "setfree"));
            }
            Commands.ChatCommands.Add(new Command("cfg", jailreload, "jailreload"));
        }

        public void OnGreetPlayer(int ply, HandledEventArgs e)
        {
            lock (Players)
                Players.Add(new Player(ply));
        }

        public class Player
        {
            public int Index { get; set; }
            public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
            public bool prisonMode { get; set; }
            public bool rulesMode { get; set; }
            public bool prisonModeSpam { get; set; }
            public bool rulesModeSpam { get; set; }
            public Player(int index)
            {
                Index = index;
                prisonMode = false;
                rulesMode = true;
                prisonModeSpam = true;
                rulesModeSpam = true;
            }
        }

        public static void SetupConfig()
        {
            try
            {
                if (File.Exists(JPConfigPath))
                {
                    JPConfig = JPConfigFile.Read(JPConfigPath);
                    // Add all the missing config properties in the json file
                }
                JPConfig.Write(JPConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in jail config file");
                Console.ForegroundColor = ConsoleColor.Gray;
                Log.Error("Jail Config Exception");
                Log.Error(ex.ToString());
            }
        }

        private DateTime LastCheck = DateTime.UtcNow;

        private void OnUpdate()
        {
            if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 1)
            {
                LastCheck = DateTime.UtcNow;
                lock (Players)
                    foreach (Player player in Players)
                    {
                        if (player.prisonMode && JPConfig.prisonmode)
                        {
                            if (TShock.Regions.InAreaRegionName(player.TSPlayer.TileX, player.TSPlayer.TileY) != "prison")
                            {
                                string warpName = "prison";
                                var warp = TShock.Warps.FindWarp(warpName);
                                if (warp.WarpPos != Vector2.Zero)
                                {
                                    if (player.TSPlayer.Teleport((int)warp.WarpPos.X, (int)warp.WarpPos.Y + 3))
                                    {
                                        if (player.prisonModeSpam)
                                        {
                                            player.TSPlayer.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...");
                                            player.prisonModeSpam = false;
                                        }
                                    }
                                }
                            }
                        }
                        if (player.rulesMode && !player.TSPlayer.Group.HasPermission("jail") && JPConfig.jailmode && !player.prisonMode)
                        {
                            if (TShock.Regions.InAreaRegionName(player.TSPlayer.TileX, player.TSPlayer.TileY) != "jail")
                            {
                                if (player.TSPlayer.Teleport(Main.spawnTileX, Main.spawnTileY))
                                {
                                    if (player.rulesModeSpam)
                                    {
                                        player.TSPlayer.SendMessage("You Cannot Get Out Of Jail Without Reading The Rules");
                                        player.rulesModeSpam = false;
                                    }
                                }
                            }
                        }
                    }
            }
        }

        private void OnLeave(int ply)
        {
            lock (Players)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].Index == ply)
                    {
                        Players.RemoveAt(i);
                        break; //Found the player, break.
                    }
                }
            }
        }

        private static int GetPlayerIndex(int ply)
        {
            lock (Players)
            {
                int index = -1;
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].Index == ply)
                        index = i;
                }
                return index;
            }
        }

        public void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
            string cmd = text.Split(' ')[0];
            var tsplr = TShock.Players[msg.whoAmI];
            if (cmd == "/warp")
            {
                if (text.Split(' ').Length > 1)
                {
                    if (TShock.Warps.FindWarp(text.Split(' ')[1]).WarpPos != Vector2.Zero)
                    {
                        if (Players[GetPlayerIndex(ply)].rulesMode && !tsplr.Group.HasPermission("jail") && TShock.Regions.InAreaRegionName(tsplr.TileX, tsplr.TileY) != "jail" && JPConfig.jailmode)
                        {
                            tsplr.SendMessage("You Can't Teleport Without Reading / Following The Rules!", Color.Red);
                            e.Handled = true;
                            return;
                        }
                        if (TShock.Regions.InAreaRegionName(tsplr.TileX, tsplr.TileY) == "jail" && !tsplr.Group.HasPermission("jail") && Players[GetPlayerIndex(ply)].rulesMode && JPConfig.jailmode)
                        {
                            tsplr.SendMessage("You Can't Exit Jail Without Reading / Following The Rules!", Color.Red);
                            e.Handled = true;
                            return;
                        }
                        if (Players[GetPlayerIndex(ply)].prisonMode && JPConfig.prisonmode)
                        {
                            tsplr.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...");
                            e.Handled = true;
                            return;
                        }
                    }
                    else return;
                }
                return;
            }

            else if (cmd == "/tp")
            {
                if (text.Split(' ').Length > 1)
                {
                    if (Players[GetPlayerIndex(ply)].rulesMode && !tsplr.Group.HasPermission("jail") && TShock.Regions.InAreaRegionName(tsplr.TileX, tsplr.TileY) != "jail" && JPConfig.jailmode)
                    {
                        tsplr.SendMessage("You Can't Teleport Without Reading / Following The Rules!", Color.Red);
                        e.Handled = true;
                        return;
                    }
                    if (TShock.Regions.InAreaRegionName(tsplr.TileX, tsplr.TileY) == "jail" && !tsplr.Group.HasPermission("jail") && Players[GetPlayerIndex(ply)].rulesMode && JPConfig.jailmode)
                    {
                        tsplr.SendMessage("You Can't Exit Jail Without Reading / Following The Rules!", Color.Red);
                        e.Handled = true;
                        return;
                    }
                    if (Players[GetPlayerIndex(ply)].prisonMode && JPConfig.prisonmode)
                    {
                        tsplr.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...");
                        e.Handled = true;
                        return;
                    }
                }
                else return;
            }
            else if (cmd == "/home")
            {
                if (Players[GetPlayerIndex(ply)].rulesMode && !tsplr.Group.HasPermission("jail") && TShock.Regions.InAreaRegionName(tsplr.TileX, tsplr.TileY) != "jail" && JPConfig.jailmode)
                {
                    tsplr.SendMessage("You Can't Teleport Without Reading / Following The Rules!", Color.Red);
                    e.Handled = true;
                    return;
                }
                if (TShock.Regions.InAreaRegionName(tsplr.TileX, tsplr.TileY) == "jail" && !tsplr.Group.HasPermission("jail") && Players[GetPlayerIndex(ply)].rulesMode && JPConfig.jailmode)
                {
                    tsplr.SendMessage("You Can't Exit Jail Without Reading / Following The Rules!", Color.Red);
                    e.Handled = true;
                    return;
                }
                if (Players[GetPlayerIndex(ply)].prisonMode && JPConfig.prisonmode)
                {
                    tsplr.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...");
                    e.Handled = true;
                    return;
                }
                tsplr.Spawn();
                tsplr.SendMessage("Teleported to your spawnpoint.");
                e.Handled = true;
                return;
            }

            else if (cmd == "/spawn")
            {
                if (Players[GetPlayerIndex(ply)].rulesMode && !tsplr.Group.HasPermission("jail") && TShock.Regions.InAreaRegionName(tsplr.TileX, tsplr.TileY) != "jail" && JPConfig.jailmode)
                {
                    tsplr.SendMessage("You Can't Teleport Without Reading / Following The Rules!", Color.Red);
                    e.Handled = true;
                    return;
                }
                if (TShock.Regions.InAreaRegionName(tsplr.TileX, tsplr.TileY) == "jail" && !tsplr.Group.HasPermission("jail") && Players[GetPlayerIndex(ply)].rulesMode && JPConfig.jailmode)
                {
                    tsplr.SendMessage("You Can't Exit Jail Without Reading / Following The Rules!", Color.Red);
                    e.Handled = true;
                    return;
                }
                if (Players[GetPlayerIndex(ply)].prisonMode && JPConfig.prisonmode)
                {
                    tsplr.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...");
                    e.Handled = true;
                    return;
                }
                else
                {
                    var warp = TShock.Warps.FindWarp("spawn");
                    if (tsplr.Teleport((int)warp.WarpPos.X, (int)warp.WarpPos.Y + 3))
                        tsplr.SendMessage("Teleported to the map's spawnpoint.");
                    e.Handled = true;
                    return;
                }
            }
        }

        private static void warpjail(CommandArgs args)
        {
            if (!args.Player.RealPlayer)
            {
                args.Player.SendMessage("You cannot use teleport commands!");
                return;
            }
            if (Players[GetPlayerIndex(args.Player.Index)].prisonMode)
            {
                args.Player.SendMessage("You are stuck in prison - An Admin or Mod Will Need To Let You Out...");
                return;
            }
            if (TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY) == "jail")
                args.Player.SendMessage("You May Now Exit The Jail!", Color.Pink);
            Players[GetPlayerIndex(args.Player.Index)].rulesMode = false;
            args.Player.SendMessage("Thanks For Reading The Rules!", Color.Pink);
            var foundplr = TShock.Utils.FindPlayer(args.Player.Name);
            if (JPConfig.groupname != "" && args.Player.Group.HasPermission("rulerank") && foundplr[0].IsLoggedIn)
            {
                var foundgrp = FindGroup(JPConfig.groupname);
                if (foundgrp.Count == 1)
                {
                    var loggeduser = TShock.Users.GetUserByName(args.Player.UserAccountName);
                    TShock.Users.SetUserGroup(loggeduser, foundgrp[0].Name);
                    args.Player.Group = foundgrp[0];
                    args.Player.SendMessage("Your Group Has Been Changed To " + foundgrp[0].Name, Color.Pink);
                    return;
                }
            }
            if (JPConfig.guestgroupname != "" && !foundplr[0].IsLoggedIn)
            {
                var foundguestgrp = FindGroup(JPConfig.guestgroupname);
                if (foundguestgrp.Count == 1)
                {
                    args.Player.Group = foundguestgrp[0];
                    args.Player.SendMessage("Your Group Has Temporarily Changed To " + foundguestgrp[0].Name, Color.HotPink);
                    args.Player.SendMessage("Use /register & /login to create a permanent account - Once Complete Type /"+ JPConfig.jailcomm +" Again.", Color.HotPink);
                }
            }
        }

        private static void imprison(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /imprison [player]", Color.Red);
                return;
            }

            var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (foundplr.Count == 0)
            {
                args.Player.SendMessage("Invalid player!", Color.Red);
                return;
            }
            else if (foundplr.Count > 1)
            {
                args.Player.SendMessage(string.Format("More than one ({0}) player matched!", args.Parameters.Count), Color.Red);
                return;
            }
            else if (foundplr[0].Group.HasPermission("prison"))
            {
                args.Player.SendMessage(string.Format("You Cannot Use This Command On This Player!", args.Parameters.Count), Color.Red);
                return;
            }
            var plr = foundplr[0];
            if (Players[GetPlayerIndex(plr.Index)].prisonMode)
            {
                args.Player.SendMessage("Player Is Already In Prison");
                return;
            }
            string warpName = "prison";
            var warp = TShock.Warps.FindWarp(warpName);
            if (warp.WarpPos != Vector2.Zero)
            {
                if (plr.Teleport((int)warp.WarpPos.X, (int)warp.WarpPos.Y + 3))
                {
                    plr.SendMessage(string.Format("{0} Warped you to the Prison! You Cannot Get Out Until An Admin Releases You", args.Player.Name), Color.Yellow);
                    args.Player.SendMessage(string.Format("You warped {0} to Prison!", plr.Name), Color.Yellow);
                    Players[GetPlayerIndex(plr.Index)].prisonMode = !Players[GetPlayerIndex(plr.Index)].prisonMode;
                }
            }
            else
            {
                args.Player.SendMessage("Prison Warp Was Not Made! Make One!", Color.Red);
            }
        }
        private static void setfree(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Invalid syntax! Proper syntax: /setfree [player]", Color.Red);
                return;
            }

            var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (foundplr.Count == 0)
            {
                args.Player.SendMessage("Invalid player!", Color.Red);
                return;
            }
            else if (foundplr.Count > 1)
            {
                args.Player.SendMessage(string.Format("More than one ({0}) player matched!", args.Parameters.Count), Color.Red);
                return;
            }
            else if (foundplr[0].Group.HasPermission("prison"))
            {
                args.Player.SendMessage(string.Format("You Cannot Use This Command On This Player!", args.Parameters.Count), Color.Red);
                return;
            }
            string warpName = "spawn";
            var warp = TShock.Warps.FindWarp(warpName);
            var plr = foundplr[0];
            if (!Players[GetPlayerIndex(plr.Index)].prisonMode)
            {
                args.Player.SendMessage("Player Is Already Free");
                return;
            }
            if (warp.WarpPos != Vector2.Zero)
            {
                if (plr.Teleport((int)warp.WarpPos.X, (int)warp.WarpPos.Y + 3))
                {
                    plr.SendMessage(string.Format("{0} Warped You To Spawn From Prison! Now Behave!!!!!", args.Player.Name, warpName), Color.Green);
                    args.Player.SendMessage(string.Format("You warped {0} to Spawn from Prison!", plr.Name, warpName), Color.Yellow);
                    Players[GetPlayerIndex(plr.Index)].prisonMode = !Players[GetPlayerIndex(plr.Index)].prisonMode;
                    Players[GetPlayerIndex(plr.Index)].prisonModeSpam = true;
                }
            }
            else
            {
                args.Player.SendMessage("Spawn Warp Was Not Made! Make One!", Color.Red);
            }
        }
        private static void jailreload(CommandArgs args)
        {
            SetupConfig();
            Log.Info("Jail Reload Initiated");
            args.Player.SendMessage("Jail Reload Initiated");
        }

        public static List<Group> FindGroup(string grp)
        {
            var found = new List<Group>();
            grp = grp.ToLower();
            foreach (Group group in TShock.Groups.groups)
            {
                if (group == null)
                    continue;

                string name = group.Name.ToLower();
                if (name.Equals(grp))
                    return new List<Group> { group };
                if (name.Contains(grp))
                    found.Add(group);
            }
            return found;
        }
    }
}