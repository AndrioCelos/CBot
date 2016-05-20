using System;
using System.Timers;

using CBot;
using IRC;

namespace BombDefense {
    [ApiVersion(3, 3)]
    public class BombDefensePlugin : Plugin {
        private Timer DefuseTimer = new Timer() { AutoReset = false };
        private Timer RejoinTimer = new Timer(30e+3) { AutoReset = false };
        private IrcClient defuseConnection;
        private Random rng = new Random();

        public override string Name => "Bomb Defense";

        public BombDefensePlugin(string key) {
            DefuseTimer.Elapsed += DefuseTimer_Elapsed;
            RejoinTimer.Elapsed += RejoinTimer_Elapsed;
        }

        [Trigger(@"^Bomb has been planted on (\S+)\.")]
        public void OnBomb(object sender, TriggerEventArgs e) {
            if (e.Client.CaseMappingComparer.Equals(e.Match.Groups[1].Value, e.Client.Me.Nickname) && e.Sender.Ident.Contains("Prae")) {
                defuseConnection = e.Client;
                DefuseTimer.Interval = rng.NextDouble() * 5e+3 + 5e+3;
                DefuseTimer.Start();
            }
        }

        public void DefuseTimer_Elapsed(object sender, ElapsedEventArgs e) {
            string colour;
            switch (rng.Next(5)) {
                case  0: colour = "red"; break;
                case  1: colour = "green"; break;
                case  2: colour = "blue"; break;
                case  3: colour = "yellow"; break;
                default: colour = "black"; break;
            }
            defuseConnection.Send("PRIVMSG #game :+defuse " + colour);
        }

        public void RejoinTimer_Elapsed(object sender, ElapsedEventArgs e) {
            defuseConnection.Send("JOIN #game");
        }

        public override bool OnChannelKick(object sender, ChannelKickEventArgs e) {
            if (e.Target == e.Channel.Me && e.Channel.Name == "#game" && e.Reason == "Badaboom!")
                RejoinTimer.Start();
            return base.OnChannelKick(sender, e);
        }
    }
}
