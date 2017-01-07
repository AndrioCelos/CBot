using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IRC.Replies;

namespace IRC {
    /// <summary>
    /// Provides a <see cref="TaskCompletionSource{TResult}"/> object to allow users to await a response to an IRC command.
    /// </summary>
    /// <remarks>
    ///     <para>Async requests are intended to provide a clean, simple way to allow code to asynchronously wait for a reply from the IRC server.</para>
    ///     <para>At the core of the <see cref="AsyncRequest"/> class is the <see cref="Task"/> property; it returns the task that can be awaited.
    ///         Since there is no base type for <see cref="TaskCompletionSource{TResult}"/>, derived classes must provide the <see cref="Task"/> object.</para>
    ///     <para>Async requests can listen for specific replies from the server, specified by the <see cref="Replies"/> property.
    ///         When a matching reply is received, the <see cref="OnReply(IrcLine, ref bool)"/> method is called,
    ///         allowing a derived class to respond.</para>
    ///     <para>If the reply is a final reply (specified by the value true in the <see cref="Replies"/> collection
    ///         or by the <see cref="OnReply(IrcLine, ref bool)"/> method through a ref parameter), 
    ///         the <see cref="IrcClient"/> class will automatically drop the request.</para>
    ///     <para>If the connection is lost, or a timeout occurs, the <see cref="OnFailure(Exception)"/> method is called
    ///         and the request is dropped.</para>
    ///     <para>The IRC client read thread must not be blocked waiting on an async request; this would cause a deadlock.
    ///         Await the request instead.</para>
    /// </remarks>
    /// <example>
    ///     <para>The <see cref="ChannelJoinEventArgs"/> object provided in the <see cref="IrcClient.ChannelJoin"/> event
    ///         now contains a <see cref="AsyncRequest"/> that will complete when the NAMES list is received.
    ///         This example will print the number of users in the channel and the number of ops.</para>
    ///     <code>
    ///         public async void IrcClient_ChannelJoin(object sender, ChannelJoinEventArgs e) {
    ///             if (e.Sender.IsMe) {
    ///                 try {
    ///                     await e.NamesTask;
    ///                     Console.WriteLine($"{e.Channel.Name} has {e.Channel.Users.Count} users and {e.Channel.Users.StatusCount(ChannelStatus.Op)} ops.");
    ///                 } catch (Exception ex) { }
    ///             }
    ///         }
    ///     </code>
    /// </example>
    public abstract class AsyncRequest {
        /// <summary>Returns the set of replies that this <see cref="AsyncRequest"/> is listening for.</summary>
        public ReadOnlyDictionary<string, bool> Replies { get; }
        /// <summary>Provides read-write access to the <see cref="Replies"/> collection.</summary>
        protected IDictionary<string, bool> RepliesSource { get; }

		/// <summary>Returns the list of parameters that must be present for this <see cref="AsyncRequest"/> to receive the reply.</summary>
		/// <remarks>
		/// Each element in the list must either match the parameter in the corresponding position (case insensitive) or be null.
		/// If this is null or empty, no checks on parameters will be done.
		/// </remarks>
		public ReadOnlyCollection<string> Parameters { get; }
		/// <summary>Provides read-write access to the <see cref="Parameters"/> collection.</summary>
		protected IList<string> ParametersSource { get; }

        /// <summary>Returns a <see cref="Task"/> object representing the status of this <see cref="AsyncRequest"/>.</summary>
        /// <remarks>
        ///     The details of what happens to the task are up to the implementation, but in general, the task might complete when a final response is received,
        ///     or fail if the connection is lost.
        ///     Derived classes may introduce other failure conditions.
        /// </remarks>
        public abstract Task Task { get; }
		/// <summary>Returns a value indicating whether this <see cref="AsyncRequest"/> can time out.</summary>
		public virtual bool CanTimeout => true;

        /// <summary>Initializes a new <see cref="AsyncRequest"/> waiting for the specified list of replies.</summary>
        /// <param name="replies">A dictionary with the replies waited on as keys. For each, if the corresponding value is true, the reply is considered a final reply.</param>
        protected AsyncRequest(IDictionary<string, bool> replies) : this(replies, null) { }
		/// <summary>Initializes a new <see cref="AsyncRequest"/> waiting for the specified list of replies and parameters.</summary>
		/// <param name="replies">A dictionary with the replies waited on as keys. For each, if the corresponding value is true, the reply is considered a final reply.</param>
		/// <param name="parameters">A list of parameters that the reply must have. See <see cref="Parameters"/> for more details.</param>
		protected AsyncRequest(IDictionary<string, bool> replies, IList<string> parameters) {
			this.RepliesSource = replies;
			this.Replies = new ReadOnlyDictionary<string, bool>(replies);
			this.ParametersSource = parameters;
			this.Parameters = (parameters != null ? new ReadOnlyCollection<string>(parameters) : null);
		}

		/// <summary>Called when one of the replies listed in the <see cref="Replies"/> table is received.</summary>
		/// <param name="line">The IRC line that was matched.</param>
		/// <param name="final">Indicates whether this is considered a final response, and the <see cref="AsyncRequest"/> will be dropped.</param>
		/// <returns>True if processing of async requests of this type should be stopped; false otherwise.</returns>
		protected internal abstract bool OnReply(IrcLine line, ref bool final);
        /// <summary>Called when the request times out, or the connection is lost.</summary>
        protected internal abstract void OnFailure(Exception exception);


        /// <summary>
        /// Represents an <see cref="AsyncRequest"/> whose task does not return a value, and completes when a final response is received.
        /// </summary>
        public class VoidAsyncRequest : AsyncRequest {
            protected TaskCompletionSource<object> TaskSource { get; } = new TaskCompletionSource<object>();
            /// <summary>Returns a <see cref="Task"/> object representing the status of this <see cref="AsyncRequest"/>.</summary>
            /// <remarks>This task will complete when a final response is received.</remarks>
            public override Task Task => this.TaskSource.Task;

			public override bool CanTimeout { get; }

			private IrcClient client;
            private string nickname;
            private HashSet<string> errors;

            public VoidAsyncRequest(IDictionary<string, bool> replies, IList<string> parameters) : base(replies, parameters) {
				this.CanTimeout = true;
			}
			public VoidAsyncRequest(IrcClient client, string nickname, string successReply, IList<string> parameters, params string[] errors) : this(client, nickname, successReply, parameters, true, errors) { }
			public VoidAsyncRequest(IrcClient client, string nickname, string successReply, IList<string> parameters, bool canTimeout, params string[] errors) : base(getReplies(successReply, errors), parameters) {
                this.client = client;
                this.nickname = nickname;
                this.errors = new HashSet<string>(errors);
				this.CanTimeout = canTimeout;
            }

            private static Dictionary<string, bool> getReplies(string successReply, IEnumerable<string> errors) {
                var replies = new Dictionary<string, bool>();
                replies.Add(successReply, true);
                foreach (var reply in errors)
                    replies.Add(reply, true);
                return replies;
            }

            protected internal override bool OnReply(IrcLine line, ref bool final) {
                if (final) {
                    if (!char.IsDigit(line.Message[0]) && this.nickname != null && line.Prefix != null &&
                        !this.client.CaseMappingComparer.Equals(this.nickname, Hostmask.GetNickname(line.Prefix))) {
                        // Wrong user.
                        final = false;
                        return false;
                    }
                    if (line.Message[0] == '4' || (this.errors != null && this.errors.Contains(line.Message))) {
                        this.TaskSource.SetException(new AsyncRequestErrorException(line));
                        return true;
                    }
                    this.TaskSource.SetResult(null);
                }
                return false;
            }

			protected internal override void OnFailure(Exception exception) => this.TaskSource.SetException(exception);
		}

		/// <summary>
		/// An <see cref="AsyncRequest"/> that listenes for a user's account name.
		/// </summary>
		/// <remarks>
		/// This class does not send a WHOIS or any other command to the server; that must be done by the caller.
		/// </remarks>
		public class AccountAsyncRequest : AsyncRequest {
            private static Dictionary<string, bool> replies = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
                { RPL_ENDOFWHOIS, false }, { RPL_WHOISACCOUNT, false }
            };

            private TaskCompletionSource<string> taskSource { get; } = new TaskCompletionSource<string>();
            /// <summary>Returns the <see cref="IrcUser"/> this <see cref="AccountAsyncRequest"/> is tracking.</summary>
            public IrcUser User { get; }
            /// <summary>Returns a <c>Task&lt;string&gt;</c> that follows the status of this <see cref="AsyncRequest"/> and returns the account name.</summary>
            public override Task Task => this.taskSource.Task;

            public AccountAsyncRequest(IrcUser user) : base(replies) {
                this.User = user;
            }

            protected internal override bool OnReply(IrcLine line, ref bool final) {
                if (this.User.Client.CaseMappingComparer.Equals(line.Parameters[1], this.User.Nickname)) {
                    this.taskSource.SetResult(this.User.Account);
                    final = true;
                }
                return false;
            }

            protected internal override void OnFailure(Exception exception) {
                this.taskSource.SetException(exception);
            }
        }

        /// <summary>
        /// An <see cref="AsyncRequest"/> that listens for a WHOIS response.
        /// </summary>
        public class WhoAsyncRequest : AsyncRequest {
            private static Dictionary<string, bool> replies = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
                // Successful replies
                { RPL_WHOREPLY, false },
                { RPL_ENDOFWHO, true },

                // Error replies
                { ERR_NOSUCHSERVER, true },
                { ERR_NOSUCHCHANNEL, true },
            };

            private IrcClient client;
            private List<WhoResponse> responses;

            public ReadOnlyCollection<WhoResponse> Responses { get; }

            private TaskCompletionSource<ReadOnlyCollection<WhoResponse>> taskSource { get; } = new TaskCompletionSource<ReadOnlyCollection<WhoResponse>>();
            private string target;
            public override Task Task => this.taskSource.Task;

            public WhoAsyncRequest(IrcClient client, string target) : base(replies, new[] { null, target }) {
                this.client = client;
                this.target = target;
                this.responses = new List<WhoResponse>();
                this.Responses = this.responses.AsReadOnly();
            }

            protected internal override bool OnReply(IrcLine line, ref bool final) {
                switch (line.Message) {
                    case RPL_WHOREPLY:
                        string[] fields = line.Parameters[7].Split(new char[] { ' ' }, 2);

                        var reply = new WhoResponse() {
                            Ident = line.Parameters[2],
                            Host = line.Parameters[3],
                            Server = line.Parameters[4],
                            Nickname = line.Parameters[5],
                            HopCount = int.Parse(fields[0]),
                            FullName = fields[1]
                        };

                        if (line.Parameters[1] != "*") {
                            reply.Channel = line.Parameters[1];
                            reply.ChannelStatus = new ChannelStatus(this.client);
                        }

                        foreach (char flag in line.Parameters[6]) {
							switch (flag) {
                                case 'G':
                                    reply.Away = true;
                                    break;
                                case '*':
                                    reply.Oper = true;
                                    break;
                                default:
                                    if (client.Extensions.StatusPrefix.TryGetValue(flag, out char mode)) {
                                        if (reply.ChannelStatus == null) reply.ChannelStatus = new ChannelStatus(this.client);
                                        reply.ChannelStatus.Add(mode);
                                    }
                                    break;
                            }
                        }

						this.responses.Add(reply);
                        break;

                    case RPL_ENDOFWHO:
                        this.taskSource.SetResult(this.Responses);
                        final = true;
                        break;

                    case ERR_NOSUCHSERVER:
                    case ERR_NOSUCHCHANNEL:
                        this.taskSource.SetException(new AsyncRequestErrorException(line));
                        final = true;
                        break;
                }

                return true;
            }

			protected internal override void OnFailure(Exception exception) => this.taskSource.SetException(exception);
		}

		/// <summary>
		/// An <see cref="AsyncRequest"/> that listens for a WHOIS response.
		/// </summary>
		public class WhoisAsyncRequest : AsyncRequest {
            private static Dictionary<string, bool> replies = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
                // Successful replies
                { "275", false },
                { RPL_AWAY, false },
                { RPL_WHOISREGNICK, false },
                { "308", false },
                { "309", false },
                { "310", false },
                { RPL_WHOISUSER, false },
                { RPL_WHOISSERVER, false },
                { RPL_WHOISOPERATOR, false },
                { "316", false },
                { RPL_WHOISIDLE, false },
                { RPL_WHOISCHANNELS, false },
                { "320", false },
                { RPL_WHOISACCOUNT, false },
                { "703", false },

                // End of WHOIS list
                { RPL_ENDOFWHOIS, true },

                // Error replies
                { ERR_NOSUCHSERVER, true },
                { ERR_NONICKNAMEGIVEN, true },
                { ERR_NOSUCHNICK, true }
            };

            private IrcClient client;
            private WhoisResponse response;
            private IrcLine error;

            private TaskCompletionSource<WhoisResponse> taskSource { get; } = new TaskCompletionSource<WhoisResponse>();
            public string Target { get; }
            public override Task Task => this.taskSource.Task;

            public WhoisAsyncRequest(IrcClient client, string target) : base(replies, new[] { null, target }) {
                this.client = client;
                this.Target = target;
                this.response = new WhoisResponse(client);
            }

            protected internal override bool OnReply(IrcLine line, ref bool final) {
                response.lines.Add(line);

                switch (line.Message) {
                    case RPL_AWAY:
                        response.AwayMessage = line.Parameters[2];
                        break;
                    case RPL_WHOISREGNICK:
                        if (response.Account == null) response.Account = line.Parameters[1];
                        break;
                    case RPL_WHOISUSER:
                        response.Nickname = line.Parameters[1];
                        response.Ident = line.Parameters[2];
                        response.Host = line.Parameters[3];
                        response.FullName = line.Parameters[5];
                        break;
                    case RPL_WHOISSERVER:
                        response.ServerName = line.Parameters[2];
                        response.ServerInfo = line.Parameters[3];
                        break;
                    case RPL_WHOISOPERATOR:
                        response.Oper = true;
                        break;
                    case RPL_WHOISIDLE:
                        response.IdleTime = TimeSpan.FromSeconds(long.Parse(line.Parameters[2]));
                        if (line.Parameters.Length > 4)
                            response.SignonTime = IrcClient.DecodeUnixTime(long.Parse(line.Parameters[3]));
                        break;
                    case RPL_WHOISCHANNELS:
                        foreach (var token in line.Parameters[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                            for (int i = 0; i < token.Length; ++i) {
                                if (this.client.Extensions.ChannelTypes.Contains(token[i])) {
                                    response.channels.Add(token.Substring(i), ChannelStatus.FromPrefix(this.client, token.Take(i)));
                                    break;
                                }
                            }
                        }
                        break;
                    case RPL_WHOISACCOUNT:
                        response.Account = line.Parameters[2];
                        break; 
                    case RPL_ENDOFWHOIS:
                        if (response.Nickname != null) {
                            this.taskSource.SetResult(response);
                        } else if (error != null) {
                            this.taskSource.SetException(new AsyncRequestErrorException(line));
                        } else {
                            this.taskSource.SetException(new IOException("The server did not send any response."));
                        }
                        final = true;
                        break;

                    case ERR_NOSUCHSERVER:
                    case ERR_NOSUCHNICK:
                    case ERR_NONICKNAMEGIVEN:
                        if (error == null) error = line;
                        break;
                }
                return true;
            }

			protected internal override void OnFailure(Exception exception) => this.taskSource.SetException(exception);
		}

		public class CtcpAsyncRequest : AsyncRequest {
			private static Dictionary<string, bool> replies = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
                // Successful replies
                { "NOTICE", false },

                // Error replies
				{ ERR_CANNOTSENDTOCHAN, false },
				{ ERR_NOTOPLEVEL, false },
				{ ERR_WILDTOPLEVEL, false },
				{ ERR_TOOMANYTARGETS, false },
				{ ERR_NOSUCHNICK, false },
			};

			private IrcClient client;

			private TaskCompletionSource<string> taskSource { get; } = new TaskCompletionSource<string>();
			private string target;
			private string request;
			public override Task Task => this.taskSource.Task;

			public CtcpAsyncRequest(IrcClient client, string target, string request) : base(replies) {
				this.client = client;
				this.target = target;
				this.request = request;
			}

			protected internal override bool OnReply(IrcLine line, ref bool final) {
				if (client.CaseMappingComparer.Equals(line.Message, "NOTICE")) {
					if (line.Parameters[1].Length >= 2 && line.Parameters[1].StartsWith("\u0001") && line.Parameters[1].EndsWith("\u0001") &&
						client.CaseMappingComparer.Equals(Hostmask.GetNickname(line.Prefix), this.target) &&
						client.CaseMappingComparer.Equals(line.Parameters[0], client.Me.Nickname)) {
						var fields = line.Parameters[1].Substring(1, line.Parameters[1].Length - 2).Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
						if (this.request.Equals(fields[0], StringComparison.InvariantCultureIgnoreCase)) {
							this.taskSource.SetResult(fields.Length >= 2 ? fields[1] : null);
							final = true;
							return true;
						}
					}
				} else if (line.Message[0] == '4') {
					if (client.CaseMappingComparer.Equals(line.Parameters[1], this.target)) {
						this.taskSource.SetException(new AsyncRequestErrorException(line));
						final = true;
						return true;
					}
				}
				return false;
			}

			protected internal override void OnFailure(Exception exception) => this.taskSource.SetException(exception);
		}

		/// <summary>
		/// Represents an <see cref="AsyncRequest"/> that waits for a message from a specific user to a specific target.
		/// </summary>
		public class MessageAsyncRequest : AsyncRequest {
			private static Dictionary<string, bool> repliesPrivmsg = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
                { "PRIVMSG", false },
			};
			private static Dictionary<string, bool> repliesNotice = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
				{ "NOTICE", false },
			};

			protected TaskCompletionSource<string> TaskSource { get; } = new TaskCompletionSource<string>();
			/// <summary>Returns a <see cref="Task"/> object representing the status of this <see cref="AsyncRequest"/>.</summary>
			/// <remarks>This task will complete when a final response is received.</remarks>
			public override Task Task => this.TaskSource.Task;

			public override bool CanTimeout => false;

			private IrcUser user;
			private IrcMessageTarget target;

			public MessageAsyncRequest(IrcUser user, IrcMessageTarget target, bool notice) : base(notice ? repliesNotice : repliesPrivmsg) {
				this.user = user;
				this.target = target;
			}

			protected internal override bool OnReply(IrcLine line, ref bool final) {
				if (this.user.Client.CaseMappingComparer.Equals(Hostmask.GetNickname(line.Prefix ?? this.user.Client.ServerName), this.user.Nickname) &&
					this.user.Client.CaseMappingComparer.Equals(line.Parameters[0], this.target.Target)) {
					this.TaskSource.SetResult(line.Parameters[1]);
					final = true;
				}
				return false;
			}

			protected internal override void OnFailure(Exception exception) => this.TaskSource.SetException(exception);
		}
	}

	[Serializable]
    public class AsyncRequestErrorException : Exception {
        public IrcLine Line { get; }

        public AsyncRequestErrorException(IrcLine line) : base(line.Parameters[line.Parameters.Length - 1]) {
            this.Line = line;
        }
    }

    /// <summary>
    /// The exception that is thrown when an async request fails because the connection is lost.
    /// </summary>
    [Serializable]
    public class AsyncRequestDisconnectedException : Exception {
        /// <summary>Returns a <see cref="IRC.DisconnectReason"/> value indicating the cause of the disconnection.</summary>
        public DisconnectReason DisconnectReason { get; }
        private const string defaultMessage = "The request failed because the connection to the server was lost.";

        /// <summary>Initializes a new <see cref="AsyncRequestDisconnectedException"/> object with the specified <see cref="IRC.DisconnectReason"/> value.</summary>
        /// <param name="reason">A <see cref="IRC.DisconnectReason"/> value indicating the cause of the disconnection.</param>
        public AsyncRequestDisconnectedException(DisconnectReason reason) : base(defaultMessage) {
            this.DisconnectReason = reason;
        }
        /// <summary>Initializes a new <see cref="AsyncRequestDisconnectedException"/> object with the specified <see cref="IRC.DisconnectReason"/> value and inner exception.</summary>
        /// <param name="reason">A <see cref="IRC.DisconnectReason"/> value indicating the cause of the disconnection.</param>
        /// <param name="inner">The exception that caused or resulted from the disconnection.</param>
        public AsyncRequestDisconnectedException(DisconnectReason reason, Exception inner) : base(defaultMessage, inner) {
            this.DisconnectReason = reason;
        }
    }

}
