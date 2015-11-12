using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

using CBot;
using IRC;

namespace BombDefense {
    [APIVersion(3, 2)]
    public class BombDefensePlugin : Plugin {
        private Timer DefuseTimer = new Timer() { AutoReset = false };
        private Timer RejoinTimer = new Timer(30e+3) { AutoReset = false };
        private IRCClient defuseConnection;
        private Random RNG = new Random();

        public override string Name {
            get {
                return "Bomb Defense";
            }
        }

        public BombDefensePlugin(string key) {
            DefuseTimer.Elapsed += DefuseTimer_Elapsed;
            RejoinTimer.Elapsed += RejoinTimer_Elapsed;
        }

        [Regex(@"^Bomb has been planted on (\S+)\.")]
        public void OnBomb(object sender, RegexEventArgs e) {
            if (e.Client.CaseMappingComparer.Equals(e.Match.Groups[1].Value, e.Client.Me.Nickname) && e.Sender.Ident.Contains("Prae")) {
                defuseConnection = e.Client;
                DefuseTimer.Interval = RNG.NextDouble() * 5e+3 + 5e+3;
                DefuseTimer.Start();
            }
        }

        public void DefuseTimer_Elapsed(object sender, ElapsedEventArgs e) {
            string colour;
            switch (RNG.Next(5)) {
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
            if (e.Sender.Nickname == ((IRCClient) sender).Me.Nickname && e.Channel == "#game" && e.Reason == "Badaboom!")
                RejoinTimer.Start();
            return base.OnChannelKick(sender, e);
        }
    }
}
