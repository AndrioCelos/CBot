using System;
using System.Text;

namespace IRC {
    /// <summary>
    /// Represents an IRC channel.
    /// </summary>
    public class Channel {
        /// <summary>The IRC connection that this channel belongs to.</summary>
        public IRCClient Client { get; }

        internal short WaitingForNamesList;
        // TODO: actually use this.
        //internal short WaitingForWhoList;

        /// <summary>The name of the channel.</summary>
        public string Name { get; }
        /// <summary>The modes on the channel.</summary>
        public virtual string Modes { get; internal set; }
        /// <summary>The local user's access level on the channel.</summary>
        public virtual IRC.ChannelAccess OwnStatus { get; internal set; }

        /// <summary>The time the channel was created.</summary>
        public DateTime Timestamp { get; internal set; }
        /// <summary>The channel topic, or null if none is set.</summary>
        public string Topic { get; internal set; }
        /// <summary>The name or hostmask of the user who set the topic (whichever the server decided to send).</summary>
        public string TopicSetter { get; internal set; }
        /// <summary>The time the topic was last changed.</summary>
        public DateTime TopicStamp { get; internal set; }
        /// <summary>The users on the channel.</summary>
        public IRC.ChannelUserCollection Users { get; internal set; }
        /// <summary>The key to the channel, or null if none is set.</summary>
        public string Key { get; internal set; }

        /// <summary>
        /// Creates a new Channel object with the specified name and IRCClient object.
        /// </summary>
        /// <param name="name">The name of the channel.</param>
        /// <param name="client">The IRC connection that this channel belongs to.</param>
        public Channel(string name, IRCClient client) {
            this.Users = new IRC.ChannelUserCollection();
            this.Name = name;
            this.Client = client;
        }

        /// <summary>
        /// Applies a nickname mode such as v or o to one or more users on the channel, as efficiently as possible.
        /// </summary>
        /// <param name="direction">The direction of the mode change; '+' or '-'.</param>
        /// <param name="mode">The channel mode to change.</param>
        /// <param name="members">The users to affect.</param>
        public void NicknameMode(char direction, char mode, params string[] members) {
            StringBuilder builder1 = new StringBuilder();
            StringBuilder builder2 = new StringBuilder();
            int i = 0; int count;

            while (i < members.Length) {
                count = 0;
                builder1.Append(direction.ToString());
                do {
                    builder1.Append(mode);
                    if (count > 0) builder2.Append(" ");
                    builder2.Append(members[i]);
                    ++i; ++count;
                } while (count < this.Client.Modes && i < members.Length);
                this.Client.Send("MODE {0} {1} {2}", this.Name, builder1, builder2);
                builder1.Clear();
                builder2.Clear();
            }
        }
        /// <summary>Removes half-voice from one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void DeHalfVoice(params string[] Members) {
            this.NicknameMode('-', 'V', Members);
        }
        /// <summary>Removes voice from one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void DeVoice(params string[] Members) {
            this.NicknameMode('-', 'v', Members);
        }
        /// <summary>Removes half-operator status from one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void DeHalfOp(params string[] Members) {
            this.NicknameMode('-', 'h', Members);
        }
        /// <summary>Removes operator status from one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void DeOp(params string[] Members) {
            this.NicknameMode('-', 'o', Members);
        }
        /// <summary>Removes admin status from one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void DeAdmin(params string[] Members) {
            this.NicknameMode('-', 'a', Members);
        }
        /// <summary>Gives half-voice to one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void HalfVoice(params string[] Members) {
            this.NicknameMode('+', 'V', Members);
        }
        /// <summary>Gives voice to one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void Voice(params string[] Members) {
            this.NicknameMode('+', 'v', Members);
        }
        /// <summary>Gives half-operator status to one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void HalfOp(params string[] Members) {
            this.NicknameMode('+', 'h', Members);
        }
        /// <summary>Gives operator status to one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void Op(params string[] Members) {
            this.NicknameMode('+', 'o', Members);
        }
        /// <summary>Gives admin status to one or more users on this channel.</summary>
        /// <param name="Members">The users to affect.</param>
        public void Admin(params string[] Members) {
            this.NicknameMode('+', 'a', Members);
        }
        /// <summary>Bans a hostmasks from the channel.</summary>
        /// <param name="Target">The hostmask to ban.</param>
        public void Ban(string Target) {
            this.NicknameMode('+', 'b', new string[] { Target });
        }
        /// <summary>Bans one or more hostmasks from the channel.</summary>
        /// <param name="Targets">The hostmasks to ban.</param>
        public void Ban(string[] Targets) {
            this.NicknameMode('+', 'b', Targets);
        }
        /// <summary>Joins this channel.</summary>
        public void Join() {
            this.Client.Send("JOIN {0}", this.Name);
        }
        /// <summary>Joins this channel using the specified key.</summary>
        /// <param name="Key">The channel key.</param>
        public void Join(string Key) {
            this.Client.Send("JOIN {0} {1}", this.Name, Key);
        }
        /// <summary>Kicks a user out of this channel.</summary>
        /// <param name="Target">The user to kick.</param>
        public void Kick(string Target) {
            this.Client.Send("KICK {0} {1}", this.Name, Target);
        }
        /// <summary>Kicks a user out of this channel with the specified message.</summary>
        /// <param name="Target">The user to kick.</param>
        /// <param name="Message">The kick message.</param>
        public void Kick(string Target, string Message) {
            this.Client.Send("KICK {0} {1} :{2}", this.Name, Target, Message);
        }
        /// <summary>Kicks one or more users out of this channel.</summary>
        /// <param name="Targets">The users to kick.</param>
        public void Kick(string[] Targets) {
            this.Client.Send("KICK {0} {1}", this.Name, string.Join(",", Targets));
        }
        /// <summary>Kicks one or more users out of this channel with the specified message.</summary>
        /// <param name="Targets">The users to kick.</param>
        /// <param name="Message">The kick message.</param>
        public void Kick(string[] Targets, string Message) {
            this.Client.Send("KICK {0} {1} :{2}", this.Name, string.Join(",", Targets), Message);
        }
        /// <summary>Leaves this channel.</summary>
        public void Part() {
            this.Client.Send("PART {0}", this.Name);
        }
        /// <summary>Leaves this channel with the specified message.</summary>
        /// <param name="Message">The part message.</param>
        public void Part(string Message) {
            this.Client.Send("PART {0} :{1}", this.Name, Message);
        }
        /// <summary>Sends a message to this channel.</summary>
        /// <param name="Message">What to say.</param>
        public void Say(string Message) {
            this.Client.Send("PRIVMSG {0} :{1}", this.Name, Message);
        }
        /// <summary>Sets a ban exception on a hostmask. Not all IRC networks support ban exceptions.</summary>
        /// <param name="Targets">The hostmask to exempt.</param>
        public void BanExcept(string Target) {
            this.BanExcept(new string[] { Target });
        }
        /// <summary>Sets one or more ban exceptions. Not all IRC networks support ban exceptions.</summary>
        /// <param name="Targets">The hostmasks to exempt.</param>
        public void BanExcept(string[] Targets) {
            this.NicknameMode('+', 'e', Targets);
        }
        /// <summary>Sets an invite exception on a hostmask. Not all IRC networks support invite exceptions.</summary>
        /// <param name="Targets">The hostmask to exempt.</param>
        public void InviteExcept(string Target) {
            this.InviteExcept(new string[] { Target });
        }
        /// <summary>Sets one or more invite exceptions. Not all IRC networks support invite exceptions.</summary>
        /// <param name="Targets">The hostmasks to exempt.</param>
        public void InviteExcept(string[] Targets) {
            this.NicknameMode('+', 'I', Targets);
        }
        /// <summary>Unbans a hostmask from the channel.</summary>
        /// <param name="Targets">The hostmask to unban.</param>
        public void Unban(string Target) {
            this.Unban(new string[] { Target });
        }
        /// <summary>Unbans one or more hostmasks from the channel.</summary>
        /// <param name="Targets">The hostmasks to unban.</param>
        public void Unban(string[] Targets) {
            this.NicknameMode('-', 'b', Targets);
        }
        /// <summary>Removes a ban exception. Not all IRC networks support ban exceptions.</summary>
        /// <param name="Targets">The hostmask to remove an exempt for.</param>
        public void BanUnExcept(string Target) {
            this.BanUnExcept(new string[] { Target });
        }
        /// <summary>Removes one or more ban exceptions. Not all IRC networks support ban exceptions.</summary>
        /// <param name="Targets">The hostmasks to remove an exempt for.</param>
        public void BanUnExcept(string[] Targets) {
            this.NicknameMode('-', 'e', Targets);
        }
        /// <summary>Removes an invite exception. Not all IRC networks support invite exceptions.</summary>
        /// <param name="Targets">The hostmask to remove an exempt for.</param>
        public void InviteUnExcept(string Target) {
            this.InviteUnExcept(new string[] { Target });
        }
        /// <summary>Removes one or more invite exceptions. Not all IRC networks support invite exceptions.</summary>
        /// <param name="Targets">The hostmasks to remove an exempt for.</param>
        public void InviteUnExcept(string[] Targets) {
            this.NicknameMode('-', 'I', Targets);
        }
        /// <summary>Quiets a hostmask. Not all IRC networks support quiet bans.</summary>
        /// <param name="Targets">The hostmask to quiet.</param>
        public void Quiet(string Target) {
            this.Quiet(new string[] { Target });
        }
        /// <summary>Quiets one or more hostmasks. Not all IRC networks support quiet bans.</summary>
        /// <param name="Targets">The hostmasks to quiet.</param>
        public void Quiet(string[] Targets) {
            this.NicknameMode('+', 'q', Targets);
        }
        /// <summary>Unquiets a hostmask. Not all IRC networks support quiet bans.</summary>
        /// <param name="Targets">The hostmask to unquiet.</param>
        public void UnQuiet(string Target) {
            this.Quiet(new string[] { Target });
        }
        /// <summary>Unquiets one or more hostmasks. Not all IRC networks support quiet bans.</summary>
        /// <param name="Targets">The hostmasks to unquiet.</param>
        public void UnQuiet(string[] Targets) {
            this.NicknameMode('-', 'q', Targets);
        }
    }
}