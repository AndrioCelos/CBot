using System;
using System.Collections.Generic;
using System.Timers;

using IRC;

namespace CBot {
    /// <summary>
    /// Stores data about an IRC connection and handles automatic reconnection.
    /// </summary>
    public class ClientEntry {
        /// <summary>The name of the IRC network.</summary>
        public string Name;
        private bool _ReconnectEnabled;
        /// <summary>Returns or sets a value specifying whether CBot will automatically reconnect to this network.</summary>
        public bool ReconnectEnabled {
            get { return this._ReconnectEnabled; }
            set {
                this._ReconnectEnabled = value;
                if (!value && this.ReconnectTimer.Enabled)
                    this.ReconnectTimer.Stop();
            }
        }
        /// <summary>Returns or sets the delay, in milliseconds, with which CBot will automatically reconnect.</summary>
        public double ReconnectDelay { get { return this.ReconnectTimer.Interval; } set { this.ReconnectTimer.Interval = value; } }
        private Timer ReconnectTimer;
        /// <summary>Returns the IRCClient object for this connection.</summary>
        public IRCClient Client { get; internal set; }

        public string[] Nicknames { get; internal set; }
        public string[] Ident { get; internal set; }
        public string[] FullName { get; internal set; }

        public string NetworkName { get; internal set; }
        public string Address { get; set; }
        public int Port { get; set; }

        public bool SaveToConfig { get; set; } = true;

        /// <summary>The list of channels to automatically join upon connecting.</summary>
        public List<AutoJoinChannel> AutoJoin;
        /// <summary>Contains the data used to deal with nickname services.</summary>
        public NickServSettings NickServ;

        /// <summary>
        /// Creates a new ClientEntry object with the specified network name, IRCClient object and reconnect delay.
        /// </summary>
        /// <param name="name">The name of the IRC network.</param>
        /// <param name="client">The IRCClient object for this connection.</param>
        /// <param name="reconnectDelay">Returns or sets the delay, in milliseconds, with which CBot should automatically reconnect.</param>
        public ClientEntry(string name, string address, int port, IRCClient client, int reconnectDelay = 30000) {
            if (address == null) throw new ArgumentNullException("address");

            this.Name = name;
            this.Address = address;
            this.Port = port;
            this.Client = client;
            this.ReconnectEnabled = true;
            this.ReconnectTimer = new Timer(reconnectDelay) { AutoReset = false };
            this.ReconnectTimer.Elapsed += ReconnectTimer_Elapsed;
            this.AutoJoin = new List<AutoJoinChannel>();
        }

        internal void ReconnectTimer_Elapsed(object sender, ElapsedEventArgs e) {
            try {
                ConsoleUtils.WriteLine("Connecting to {0} on port {1}.", (object) this.Client.IP ?? (object) this.Client.Address, this.Client.Port);
                this.Client.Connect(this.Address, this.Port);
            } catch (Exception ex) {
                ConsoleUtils.WriteLine("%cREDConnection to {0} failed: {1}%r", this.Client.Address, ex.Message);
                this.StartReconnect();
            }
        }

        internal void StartReconnect() {
            if (this.ReconnectEnabled && !this.ReconnectTimer.Enabled)
                this.ReconnectTimer.Start();
        }

    }
}
