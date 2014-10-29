using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CBot {
    public class NickServSettings {
        public string[] RegisteredNicknames;
        public bool AnyNickname;
        public bool UseGhostCommand;
        public string GhostCommand;
        public string Password;
        public string IdentifyCommand;
        public string Hostmask;
        public string RequestMask;
        public DateTime IdentifyTime;

        public NickServSettings() {
            this.RegisteredNicknames = new string[0];
            this.AnyNickname = false;
            this.UseGhostCommand = true;
            this.GhostCommand = "PRIVMSG $target :GHOST $nickname $password";
            this.IdentifyCommand = "PRIVMSG $target :IDENTIFY $password";
            this.Hostmask = "NickServ!*@*";
            this.RequestMask = "*IDENTIFY*";
            this.IdentifyTime = default(DateTime);
        }
    }
}
