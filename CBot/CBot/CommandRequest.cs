using IRC;

namespace CBot {
    internal class CommandRequest {
        public bool Regex;
        public Plugin Plugin;
        public IRCUser Sender;
        public string Channel;
        public string InputLine;
        public bool GlobalCommand;
        public string FailureMessage;
    }
}