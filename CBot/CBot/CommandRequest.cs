using IRC;

namespace CBot {
    internal class CommandRequest {
        public bool IsTrigger;
        public Plugin Plugin;
        public IrcUser Sender;
        public IrcMessageTarget Channel;
        public string Label;
        public string Parameters;
        public bool GlobalCommand;
        public string FailureMessage;
    }
}