using System;

namespace IRC {
    /// <summary>Used to report the state of the IRC client.</summary>
    public enum IRCClientState {
        /// <summary>The client is not connected.</summary>
        Disconnected,
        /// <summary>The client is establishing a TCP connection.</summary>
        Connecting,
        /// <summary>The client is making an SSL handshake before logging in.</summary>
        SSLHandshaking,
        /// <summary>The client is registering to IRC.</summary>
        Registering,
        /// <summary>The client is negotiating capabilities with the server.</summary>
        CapabilityNegotiating,
        /// <summary>The client is authenticating using SASL.</summary>
        SASLAuthenticating,
        /// <summary>The client is online on IRC.</summary>
        Online
    }

    /// <summary>Represents the status a user can have on a channel. A user can have zero, one or more of these.</summary>
    /// <remarks>The flags are given values such that the higher bits represent higher status. Therefore, any user that can set modes on a channel will have access >= HalfOp, discounting oper powers.</remarks>
    [Obsolete("This enumeration is deprecated in favour of the ChannelStatus class, which can deal with status prefixes other than the six represented here.")]
    [Flags]
    public enum ChannelAccess {
        /// <summary>The user has no known status.</summary>
        Normal = 0,
        /// <summary>The user has half-voice (mode +V).</summary>
        /// <remarks>I've never heard of an IRC server supporting this, and support in CIRC may be removed in future.</remarks>
        HalfVoice = 1,
        /// <summary>The user has voice (mode +v).</summary>
        Voice = 2,
        /// <summary>The user has half-operator status (mode +h).</summary>
        /// <remarks>Many IRC servers don't support this.</remarks>
        HalfOp = 4,
        /// <summary>The user has operator status (mode +o).</summary>
        Op = 8,
        /// <summary>The user has administrator (or super-op) status (mode +a).</summary>
        /// <remarks>Many IRC servers don't support this.</remarks>
        Admin = 16,
        /// <summary>The user has owner status (mode +q).</summary>
        /// <remarks>Many IRC servers don't support this, and channel mode q is often used for other purposes.</remarks>
        Owner = 32
    }

    /// <summary>
    /// Specifies the reason a join command failed.
    /// </summary>
    public enum ChannelJoinDeniedReason {
        /// <summary>The join failed because the channel has reached its population limit.</summary>
        Limit = 1,
        /// <summary>The join failed because the channel is invite only.</summary>
        InviteOnly,
        /// <summary>The join failed because we are banned.</summary>
        Banned,
        /// <summary>The join failed because we did not give the correct key.</summary>
        KeyFailure,
        /// <summary>The join failed for some other reason.</summary>
        Other = -1
    }

    public enum DisconnectReason {
        /// <summary>The IRCClient.Disconnect method was called.</summary>
        ClientDisconnected,
        /// <summary>The server is closing the connection as a result of a QUIT command from the client.</summary>
        Quit,
        /// <summary>The server did not respond to a ping.</summary>
        PingTimeout,
        /// <summary>The server is closing the connection unexpectedly.</summary>
        ServerDisconnected,
        /// <summary>An exception occurred while receiving data.</summary>
        Exception,
        /// <summary>The TLS authentication failed.</summary>
        TlsAuthenticationFailed,
        /// <summary>The SASL authentication failed.</summary>
        SaslAuthenticationFailed
    }
}