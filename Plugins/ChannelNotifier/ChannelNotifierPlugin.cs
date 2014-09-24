using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using CBot;
using IRC;

namespace ChannelNotifier
{
    [APIVersion(3, 0)]
    public class ChannelNotifierPlugin : Plugin
    {
        public List<string> Targets;

        public override string Name {
            get {
                return "Channel Notifier";
            }
        }

        public ChannelNotifierPlugin(string Key) {
            this.Targets = new List<string>();
            this.LoadConfig(Key);
        }

        public override void OnSave() {
            this.SaveConfig();
        }

        public void LoadConfig(string key) {
            string filename = Path.Combine("Config", key + ".ini");
            if (!File.Exists(filename)) return;
            StreamReader reader = new StreamReader(filename);
            string section = null;

            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                if (Regex.IsMatch(line, @"^(?>\s*);")) continue;  // Comment check

                Match match = Regex.Match(line, @"^\s*\[(.*?)\]?\s*$");
                if (match.Success) {
                    section = match.Groups[1].Value;
                } else {
                    match = Regex.Match(line, @"^\s*((?>[^=]*))=(.*)$");
                    if (match.Success) {
                        string field = match.Groups[1].Value;
                        string value = match.Groups[2].Value;

                        if (section == null) continue;
                        switch (section.ToUpper()) {
                            case "CONFIG":
                                switch (field.ToUpper()) {
                                    case "TARGETS":
                                        this.Targets = new List<string>(value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
                                        break;
                                }
                                break;
                        }
                    }
                }
            }
            reader.Close();
        }

        public void SaveConfig() {
            if (!Directory.Exists("Config"))
                Directory.CreateDirectory("Config");
            StreamWriter writer = new StreamWriter(Path.Combine("Config", MyKey + ".ini"), false);
            writer.WriteLine("[Config]");
            writer.WriteLine("Targets={0}", string.Join(",", this.Targets));
            writer.Close();
        }

        public void SendCheck(string message) {
            foreach (string channelName in this.Targets) {
                string[] fields = channelName.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };

                IEnumerable<IRCClient> connections;
                if (fields[0] == "*") connections = Bot.Connections;
                else {
                    IRCClient connection = Bot.Connections.FirstOrDefault(c => fields[0].Equals(c.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(c.Address, StringComparison.OrdinalIgnoreCase));
                    if (connection == default(IRCClient)) return;
                    connections = new IRCClient[] { connection };
                }

                foreach (IRCClient connection in connections) {
                    if (fields[1] == "*") {
                        foreach (Channel channel in connection.Channels)
                            channel.Say(message);
                    } else if (connection.IsChannel(fields[1])) {
                        connection.Send("PRIVMSG " + fields[1] + " :" + message);
                    } else {
                        if (Bot.UserHasPermission(connection, null, fields[1], MyKey + ".receive"))
                            connection.Send("PRIVMSG " + fields[1] + " :" + message);
                    }
                }
            }
        }

        public override void OnChannelJoin(IRCClient Connection, string Sender, string Channel) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F joined.", Channel, IRC.Colours.NicknameColour(Sender.Split(new char[] { '!' }, 2)[0]), Sender.Split(new char[] { '!' }, 2)[0]));
            base.OnChannelJoin(Connection, Sender, Channel);
        }

        public override void OnChannelJoinSelf(IRCClient Connection, string Sender, string Channel) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F joined.", Channel, IRC.Colours.NicknameColour(Sender.Split(new char[] { '!' }, 2)[0]), Sender.Split(new char[] { '!' }, 2)[0]));
            base.OnChannelJoinSelf(Connection, Sender, Channel);
        }

        public override void OnChannelExit(IRCClient Connection, string Sender, string Channel, string Reason) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F left: {3}.", Channel, IRC.Colours.NicknameColour(Sender.Split(new char[] { '!' }, 2)[0]), Sender.Split(new char[] { '!' }, 2)[0], Reason));
            base.OnChannelExit(Connection, Sender, Channel, Reason);
        }

        public override void OnChannelExitSelf(IRCClient Connection, string Sender, string Channel, string Reason) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F left: {3}.", Channel, IRC.Colours.NicknameColour(Sender.Split(new char[] { '!' }, 2)[0]), Sender.Split(new char[] { '!' }, 2)[0], Reason));
            base.OnChannelExit(Connection, Sender, Channel, Reason);
        }

        public override void OnChannelMessage(IRCClient Connection, string Sender, string Channel, string Message) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F: {3}.", Channel, IRC.Colours.NicknameColour(Sender.Split(new char[] { '!' }, 2)[0]), Sender.Split(new char[] { '!' }, 2)[0], Message));
            base.OnChannelMessage(Connection, Sender, Channel, Message);
        }

        public override void OnPrivateMessage(IRCClient Connection, string Sender, string Message) {
            if (!Message.StartsWith("!"))
                this.SendCheck(string.Format("\u000315[\u000312PM\u000315] {0}{1}\u000F: {2}.", IRC.Colours.NicknameColour(Sender.Split(new char[] { '!' }, 2)[0]), Sender.Split(new char[] { '!' }, 2)[0], Message));
            base.OnPrivateMessage(Connection, Sender, Message);
        }

        public override void OnPrivateAction(IRCClient Connection, string Sender, string Message) {
            this.SendCheck(string.Format("\u000315[\u000312PM\u000315] {0}{1}\u000F {2}.", IRC.Colours.NicknameColour(Sender.Split(new char[] { '!' }, 2)[0]), Sender.Split(new char[] { '!' }, 2)[0], Message));
            base.OnPrivateMessage(Connection, Sender, Message);
        }

        public override void OnPrivateNotice(IRCClient Connection, string Sender, string Message) {
            this.SendCheck(string.Format("\u000315[\u00038PM\u000315] {0}{1}\u00038:\u000F {2}.", IRC.Colours.NicknameColour(Sender.Split(new char[] { '!' }, 2)[0]), Sender.Split(new char[] { '!' }, 2)[0], Message));
            base.OnPrivateMessage(Connection, Sender, Message);
        }
    }
}
