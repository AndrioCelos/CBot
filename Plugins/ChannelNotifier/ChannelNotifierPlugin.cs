﻿using System;
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
    [APIVersion(3, 2)]
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
            StreamWriter writer = new StreamWriter(Path.Combine("Config", this.Key + ".ini"), false);
            writer.WriteLine("[Config]");
            writer.WriteLine("Targets={0}", string.Join(",", this.Targets));
            writer.Close();
        }

        public void SendCheck(string message, IRCClient originConnection, string originChannel) {
            foreach (string channelName in this.Targets) {
                string[] fields = channelName.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };

                IEnumerable<ClientEntry> clients;
                if (fields[0] == "*") clients = Bot.Clients;
                else {
                    ClientEntry client = Bot.Clients.FirstOrDefault(c => fields[0].Equals(c.Client.Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(c.Client.Address, StringComparison.OrdinalIgnoreCase));
                    if (client == default(ClientEntry)) return;
                    clients = new ClientEntry[] { client };
                }

                foreach (ClientEntry clientEntry in clients) {
                    IRCClient client = clientEntry.Client;
                    if (fields[1] == "*") {
                        if (client == originConnection) {
                            foreach (IRCChannel channel in client.Channels.Where(_channel => _channel.Name != originChannel))
                                channel.Say(message);
                        } else {
                            foreach (IRCChannel channel in client.Channels)
                                channel.Say(message);
                        }
                    } else if (client.IsChannel(fields[1]) && (client != originConnection || !client.CaseMappingComparer.Equals(fields[1], originChannel))) {
                        client.Send("PRIVMSG " + fields[1] + " :" + message);
                    } else {
                        IRCUser user;
                        if ((client != originConnection || !client.CaseMappingComparer.Equals(fields[1], originChannel)) && client.Users.TryGetValue(fields[1], out user) && Bot.UserHasPermission(client, null, user, this.Key + ".receive"))
                            client.Send("PRIVMSG " + fields[1] + " :" + message);
                    }
                }
            }
        }

        public override bool OnChannelJoin(object sender, ChannelJoinEventArgs e) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F joined.", e.Channel, IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname), (IRCClient) sender, e.Channel);
            return base.OnChannelJoin(sender, e);
        }

        public override bool OnChannelLeave(object sender, ChannelPartEventArgs e) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F left: {3}.", e.Channel, IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname, e.Message), (IRCClient) sender, e.Channel);
            return base.OnChannelLeave(sender, e);
        }

        public override bool OnChannelLeaveSelf(object sender, ChannelPartEventArgs e) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F left: {3}.", e.Channel, IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname, e.Message), (IRCClient) sender, e.Channel);
            return base.OnChannelLeaveSelf(sender, e);
        }

        public override bool OnChannelMessage(object sender, ChannelMessageEventArgs e) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F: {3}", e.Channel, IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname, e.Message), (IRCClient) sender, e.Channel);
            return base.OnChannelMessage(sender, e);
        }

        public override bool OnChannelAction(object sender, ChannelMessageEventArgs e) {
            this.SendCheck(string.Format("\u000315[\u000F{0}\u000315] {1}{2}\u000F {3}", e.Channel, IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname, e.Message), (IRCClient) sender, e.Channel);
            return base.OnChannelAction(sender, e);
        }

        public override bool OnChannelNotice(object sender, ChannelMessageEventArgs e) {
            this.SendCheck(string.Format("\u000315[\u00038{0}\u000315] {1}{2}\u00038:\u000F {3}", e.Channel, IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname, e.Message), (IRCClient) sender, e.Channel);
            return base.OnChannelNotice(sender, e);
        }

        public override bool OnPrivateMessage(object sender, PrivateMessageEventArgs e) {
            if (base.OnPrivateMessage(sender, e)) return true;

            string message;
            // Don't relay the message if it's a ZNC 'not connected' notification.
            if (Regex.IsMatch(e.Message, @"^Your message to \S+ got lost")) return false;

            // Don't relay the message if it's a command.
            Match match = Regex.Match(e.Message, @"^" + Regex.Escape(((IRCClient) sender).Me.Nickname) + @"\.*[:,-]? (.*)", RegexOptions.IgnoreCase);
            if (match.Success)
                message = match.Groups[1].Value;
            else
                message = e.Message;
            if (message.Length == 0 || !Bot.GetCommandPrefixes((IRCClient) sender, e.Sender.Nickname).Contains(message[0].ToString()))
                this.SendCheck(string.Format("\u000315[\u000312PM\u000315] {0}{1}\u000F: {2}", IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname, e.Message), (IRCClient) sender, e.Sender.Nickname);

            return false;
        }

        public override bool OnPrivateAction(object sender, PrivateMessageEventArgs e) {
            this.SendCheck(string.Format("\u000315[\u000312PM\u000315] {0}{1}\u000F {2}", IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname, e.Message), (IRCClient) sender, e.Sender.Nickname);
            return base.OnPrivateAction(sender, e);
        }

        public override bool OnPrivateNotice(object sender, PrivateMessageEventArgs e) {
            this.SendCheck(string.Format("\u000315[\u00037PM\u000315] {0}{1}\u00038:\u000F {2}", IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname, e.Message), (IRCClient) sender, e.Sender.Nickname);
            return base.OnPrivateNotice(sender, e);
        }

        public override bool OnInvite(object sender, ChannelInviteEventArgs e) {
            this.SendCheck(string.Format("Invited to \u0002{0}\u0002 by {1}{2}\u000F.", e.Channel, IRC.Colours.NicknameColour(e.Sender.Nickname), e.Sender.Nickname), (IRCClient) sender, e.Sender.Nickname);
            return base.OnInvite(sender, e);
        }
    }
}
