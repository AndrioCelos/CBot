using System;
using System.Collections.Generic;
using System.Timers;

using CBot;
using AnIRC;

namespace BombDefense {
    [ApiVersion(3, 7)]
    public class BombDefensePlugin : Plugin {
        private Timer DefuseTimer = new Timer() { AutoReset = false };
        private Timer RejoinTimer = new Timer(30e+3) { AutoReset = false };
		private string[] colours;
		private readonly string[] defaultColours = new[] { "red", "green", "blue", "yellow", "black" };
        private IrcClient defuseClient;
		private int mode;
        private Random rng = new Random();

        public override string Name => "Bomb Defense";

        public BombDefensePlugin(string key) {
            DefuseTimer.Elapsed += DefuseTimer_Elapsed;
            RejoinTimer.Elapsed += RejoinTimer_Elapsed;
        }

        [Trigger(@"^Bomb has been planted on (\S+)\.")]
		public void OnBomb(object sender, TriggerEventArgs e) {
            if (e.Client.CaseMappingComparer.Equals(e.Match.Groups[1], e.Client.Me.Nickname) &&
					e.Channel.Users[e.Sender.Nickname].Status >= ChannelStatus.Halfop) {
                defuseClient = e.Client;
				mode = 0;
                DefuseTimer.Interval = rng.NextDouble() * 5e+3 + 5e+3;
                DefuseTimer.Start();
            }
        }

		[Trigger(@"^stuffs the bomb into (\S+)'s pants. +The display reads \[\x02(\d+)\x02\] seconds.")]
		public void OnBomb2(object sender, TriggerEventArgs e) {
			if (e.Client.CaseMappingComparer.Equals(e.Match.Groups[1], e.Client.Me.Nickname) && 
					e.Channel.Users[e.Sender.Nickname].Status >= ChannelStatus.Halfop) {
				defuseClient = e.Client;
			}
		}

		[Trigger(@"^D\w+ the bomb by cutting the correct wire with ""cutwire <color>""\. There are \w+ wires\. They are (.*)\.$")]
		public void OnDescription(object sender, TriggerEventArgs e) {
			if (e.Client == defuseClient && e.Channel.Users[e.Sender.Nickname].Status >= ChannelStatus.Halfop) {
				mode = 1;
				colours = e.Match.Groups[1].Value.Split(new[] { ", ", " and " }, 0);
				DefuseTimer.Interval = rng.NextDouble() * 5e+3 + 5e+3;
				DefuseTimer.Start();
			}
		}

		[Trigger(@"^D\w+ the bomb by cutting the correct wire with ""cutwire <color>""\. There is one wire\. It is (.*)\.$")]
		public void OnDescription2(object sender, TriggerEventArgs e) {
			if (e.Client == defuseClient && e.Channel.Users[e.Sender.Nickname].Status >= ChannelStatus.Halfop) {
				mode = 1;
				colours = new[] { e.Match.Groups[1].Value };
				DefuseTimer.Interval = rng.NextDouble() * 5e+3 + 5e+3;
				DefuseTimer.Start();
			}
		}

		public void DefuseTimer_Elapsed(object sender, ElapsedEventArgs e) {
			colours = colours ?? defaultColours;
            string colour = colours[rng.Next(colours.Length)];

			switch (mode) {
				case 0: defuseClient.Send("PRIVMSG #game :+defuse " + colour); break;
				case 1: defuseClient.Send("PRIVMSG #game :cutwire " + colour); break;
			}

			colours = null;
		}

        public void RejoinTimer_Elapsed(object sender, ElapsedEventArgs e) {
            defuseClient.Send("JOIN #game");
        }

        public override bool OnChannelKick(object sender, ChannelKickEventArgs e) {
            if (e.Target.IsMe && e.Channel.Name == "#game" && (e.Reason == "Badaboom!" || e.Reason.EndsWith("*BOOM!*\x02")))
                RejoinTimer.Start();
            return base.OnChannelKick(sender, e);
        }
    }
}
