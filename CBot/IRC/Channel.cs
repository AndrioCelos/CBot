using System;
using System.Text;

namespace IRC {
    public class Channel {
        public IRCClient Client { get; private set; }

        internal short WaitingForNamesList;
        // TODO: actually use this.
        //internal short WaitingForWhoList;

        public string Name { get; private set; }
        public virtual string Modes { get; internal set; }
        public virtual IRC.ChannelAccess OwnStatus { get; internal set; }

        public DateTime Timestamp { get; internal set; }
        public string Topic { get; internal set; }
        public string TopicSetter { get; internal set; }
        public DateTime TopicStamp { get; internal set; }
        public IRC.ChannelUserCollection Users { get; internal set; }
        public string Key { get; internal set; }

        public Channel(string Name, IRCClient client) {
            this.Users = new IRC.ChannelUserCollection();
            this.Name = Name;
            this.Client = client;
        }
        public void NicknameMode(char Direction, char ModeCharacter, params string[] Members) {
            StringBuilder builder1 = new StringBuilder();
            StringBuilder builder2 = new StringBuilder();
            int i = 0; int count;

            while (i < Members.Length) {
                count = 0;
                builder1.Append(Direction.ToString());
                do {
                    builder1.Append(ModeCharacter);
                    if (count > 0) builder2.Append(" ");
                    builder2.Append(Members[i]);
                    ++i; ++count;
                } while (count < this.Client.Modes && i < Members.Length);
                this.Client.Send("MODE {0} {1} {2}", this.Name, builder1, builder2);
                builder1.Clear();
                builder2.Clear();
            }
        }
        public void DeHalfVoice(params string[] Members) {
            this.NicknameMode('-', 'V', Members);
        }
        public void DeVoice(params string[] Members) {
            this.NicknameMode('-', 'v', Members);
        }
        public void DeHalfOp(params string[] Members) {
            this.NicknameMode('-', 'h', Members);
        }
        public void DeOp(params string[] Members) {
            this.NicknameMode('-', 'o', Members);
        }
        public void DeAdmin(params string[] Members) {
            this.NicknameMode('-', 'a', Members);
        }
        public void HalfVoice(params string[] Members) {
            this.NicknameMode('+', 'V', Members);
        }
        public void Voice(params string[] Members) {
            this.NicknameMode('+', 'v', Members);
        }
        public void HalfOp(params string[] Members) {
            this.NicknameMode('+', 'h', Members);
        }
        public void Op(params string[] Members) {
            this.NicknameMode('+', 'o', Members);
        }
        public void Admin(params string[] Members) {
            this.NicknameMode('+', 'a', Members);
        }
        public void Ban(string Target) {
            this.NicknameMode('+', 'b', new string[] { Target });
        }
        public void Ban(string[] Targets) {
            this.NicknameMode('+', 'b', Targets);
        }
        public void Join() {
            this.Client.Send("JOIN {0}", this.Name);
        }
        public void Join(string Key) {
            this.Client.Send("JOIN {0} {1}", this.Name, Key);
        }
        public void Kick(string Target) {
            this.Client.Send("KICK {0} {1}", this.Name, Target);
        }
        public void Kick(string Target, string Message) {
            this.Client.Send("KICK {0} {1} :{2}", this.Name, Target, Message);
        }
        public void Kick(string[] Targets) {
            this.Client.Send("KICK {0} {1}", this.Name, string.Join(",", Targets));
        }
        public void Kick(string[] Targets, string Message) {
            this.Client.Send("KICK {0} {1} :{2}", this.Name, string.Join(",", Targets), Message);
        }
        public void Part() {
            this.Client.Send("PART {0}", this.Name);
        }
        public void Part(string Message) {
            this.Client.Send("PART {0} :{1}", this.Name, Message);
        }
        public void Say(string Message) {
            this.Client.Send("PRIVMSG {0} :{1}", this.Name, Message);
        }
        public void BanExcept(string Target) {
            this.BanExcept(new string[] { Target });
        }
        public void BanExcept(string[] Targets) {
            this.NicknameMode('+', 'e', Targets);
        }
        public void InviteExcept(string Target) {
            this.InviteExcept(new string[] { Target });
        }
        public void InviteExcept(string[] Targets) {
            this.NicknameMode('+', 'I', Targets);
        }
        public void Unban(string Target) {
            this.Unban(new string[] { Target });
        }
        public void Unban(string[] Targets) {
            this.NicknameMode('-', 'b', Targets);
        }
        public void BanUnExcept(string Target) {
            this.BanUnExcept(new string[] { Target });
        }
        public void BanUnExcept(string[] Targets) {
            this.NicknameMode('-', 'e', Targets);
        }
        public void InviteUnExcept(string Target) {
            this.InviteUnExcept(new string[] { Target });
        }
        public void InviteUnExcept(string[] Targets) {
            this.NicknameMode('-', 'I', Targets);
        }
        public void Quiet(string Target) {
            this.Quiet(new string[] { Target });
        }
        public void Quiet(string[] Targets) {
            this.NicknameMode('+', 'q', Targets);
        }
        public void UnQuiet(string Target) {
            this.Quiet(new string[] { Target });
        }
        public void UnQuiet(string[] Targets) {
            this.NicknameMode('-', 'q', Targets);
        }
    }
}