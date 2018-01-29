using System;
using System.Collections.Generic;
using System.Reflection;
using System.Timers;

using AnIRC;
using Newtonsoft.Json;

namespace CBot {
    /// <summary>
    /// Stores data about an IRC connection and handles automatic reconnection.
    /// </summary>
    // TODO: Rename this to IrcNetwork?
    public class ClientEntry {
        /// <summary>The name of the IRC network.</summary>
        public string Name { get; set; }

		private bool _ReconnectEnabled;
        /// <summary>Returns or sets a value specifying whether CBot will automatically reconnect to this network.</summary>
        [JsonIgnore]
        public bool ReconnectEnabled {
            get { return this._ReconnectEnabled; }
            set {
                this._ReconnectEnabled = value;
                if (!value && this.ReconnectTimer.Enabled)
                    this.ReconnectTimer.Stop();
            }
        }
        /// <summary>Returns or sets the delay, in milliseconds, with which CBot will automatically reconnect.</summary>
		[JsonIgnore]
        public double ReconnectDelay { get { return this.ReconnectTimer.Interval; } set { this.ReconnectTimer.Interval = value; } }
		[JsonIgnore]
		private Timer ReconnectTimer;
		/// <summary>Returns the <see cref="IrcClient"/> object for this connection.</summary>
		[JsonIgnore]
		public IrcClient Client { get; internal set; }

        public string[] Nicknames { get; set; }
        public string Ident { get; set; }
        public string FullName { get; set; }

        public string Address { get; set; }
        public int Port { get; set; } = 6667;
        public bool TLS { get; set; }
        public bool AcceptInvalidTlsCertificate { get; set; }
        public string Password { get; set; }

        public string SaslUsername { get; set; }
        public string SaslPassword { get; set; }

        /// <summary>Indicates whether this network is defined in CBot's config file.</summary>
        [JsonIgnore]
        public bool SaveToConfig { get; set; }

        /// <summary>The list of channels to automatically join upon connecting.</summary>
        public List<AutoJoinChannel> AutoJoin = new List<AutoJoinChannel>();
        /// <summary>Contains the data used to deal with nickname services.</summary>
        public NickServSettings NickServ;

		// Diagnostic information.
		[JsonIgnore]
		public Plugin CurrentPlugin { get; internal set; }
		[JsonIgnore]
		public MethodInfo CurrentProcedure { get; internal set; }

		[JsonConstructor]
        public ClientEntry(string name) {
            this.Name = name;
            if (name.Contains(".")) this.Address = name;
            this.ReconnectTimer = new Timer(30000) { AutoReset = false };
            this.ReconnectTimer.Elapsed += ReconnectTimer_Elapsed;
        }

        /// <summary>
        /// Creates a new ClientEntry object with the specified network name, IRCClient object and reconnect delay.
        /// </summary>
        /// <param name="name">The name of the IRC network.</param>
        /// <param name="client">The IRCClient object for this connection.</param>
        /// <param name="reconnectDelay">Returns or sets the delay, in milliseconds, with which CBot should automatically reconnect.</param>
        public ClientEntry(string name, string address, int port, IrcClient client, int reconnectDelay = 30000) {
            if (address == null) throw new ArgumentNullException("address");

            this.Name = name;
            this.Address = address;
            this.Port = port;
            this.Client = client;
            this.ReconnectEnabled = true;
            this.ReconnectTimer = new Timer(reconnectDelay) { AutoReset = false };
            this.ReconnectTimer.Elapsed += ReconnectTimer_Elapsed;
        }

        internal void UpdateSettings() {
            this.Client.Address = this.Address;
            this.Client.Password = this.Password;
            this.Client.SSL = this.TLS;
            this.Client.AllowInvalidCertificate = this.AcceptInvalidTlsCertificate;
            this.Client.SaslUsername = this.SaslUsername;
            this.Client.SaslPassword = this.SaslPassword;
            this.Client.Me.Nickname = this.Nicknames[0];
            this.Client.Me.Ident = this.Ident;
            this.Client.Me.FullName = this.FullName;
        }

        public void Connect() {
            this.UpdateSettings();
            this.ReconnectTimer.Stop();
            this.Client.Connect(this.Address, this.Port);
        }

        internal void ReconnectTimer_Elapsed(object sender, ElapsedEventArgs e) {
            if (this.Client.State != IrcClientState.Disconnected) return;
            try {
                ConsoleUtils.WriteLine("Connecting to {0} ({1}) on port {2}.", this.Name, this.Address, this.Port);
                this.UpdateSettings();
                this.Client.Connect(this.Address, this.Port);
            } catch (Exception ex) {
                ConsoleUtils.WriteLine("%cREDConnection to {0} failed: {1}%r", this.Name, ex.Message);
                this.StartReconnect();
            }
        }

        internal void StartReconnect() {
            if (this.ReconnectEnabled && !this.ReconnectTimer.Enabled)
                this.ReconnectTimer.Start();
        }

    }
}
