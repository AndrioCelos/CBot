using System;

namespace CBot {
    /// <summary>Specifies how a message should be sent by Bot.Say.</summary>
    [Flags]
    public enum SayOptions : short {
        /// <summary>Sends the message to channel operators only. This will always use the NOTICE command.</summary>
        OpsOnly = 9,
        /// <summary>The first character of the message will be capitalised.</summary>
        Capitalise = 2,
        /// <summary>The NOTICE command will be used, even to channels.</summary>
        NoticeAlways = 8,
        /// <summary>The PRIVMSG command will be used, even to users.</summary>
        NoticeNever = 4,
    }
}
