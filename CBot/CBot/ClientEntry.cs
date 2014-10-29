using System;
using System.Collections.Generic;
using System.Timers;

using IRC;

namespace CBot {
    public class ClientEntry {
        public string Name;
        private bool _ReconnectEnabled;
        public bool ReconnectEnabled {
            get { return this._ReconnectEnabled; }
            set {
                this._ReconnectEnabled = value;
                if (!value && this.ReconnectTimer.Enabled)
                    this.ReconnectTimer.Stop();
            }
        }
        public double ReconnectDelay { get { return this.ReconnectTimer.Interval; } set { this.ReconnectTimer.Interval = value; } }
        private Timer ReconnectTimer;
        public IRCClient Client { get; internal set; }

        public List<string> AutoJoin;
        public NickServSettings NickServ;

        public ClientEntry(string name, IRCClient client, int reconnectDelay = 30000) {
            this.Name = name;
            this.Client = client;
            this.ReconnectEnabled = true;
            this.ReconnectTimer = new Timer(reconnectDelay) { AutoReset = false };
            this.ReconnectTimer.Elapsed += ReconnectTimer_Elapsed;
            this.AutoJoin = new List<string>();
        }

        void ReconnectTimer_Elapsed(object sender, ElapsedEventArgs e) {
            try {
                ConsoleUtils.WriteLine("Connecting to {0} on port {1}.", (object) this.Client.IP ?? (object) this.Client.Address, this.Client.Port);
                this.Client.Connect();
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
