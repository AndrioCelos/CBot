using System;

namespace CBot {
	/// <summary>
	/// Contains the information needed to join a channel.
	/// </summary>
	public struct AutoJoinChannel {
		/// <summary>The channel name.</summary>
		public string Channel;
		/// <summary>A key needed to join the channel, if any.</summary>
		public string Key;

		/// <summary>Creates an AutoJoinChannel value with the specified channel name and no key (null).</summary>
		/// <param name="channel">The name of the channel.</param>
		/// <exception cref="ArgumentException">channel contains a delimiter (space, null, newline or comma).</exception>
		public AutoJoinChannel(string channel) : this(channel, null) { }
		/// <summary>Creates an AutoJoinChannel value with the specified channel name and key.</summary>
		/// <param name="channel">The name of the channel.</param>
		/// <exception cref="ArgumentException">channel contains a delimiter (space, null, newline or comma).</exception>
		public AutoJoinChannel(string channel, string key) {
			if (channel != null && channel.IndexOfAny(new char[] { ' ', '\0', '\n', ',' }) != -1)
				throw new ArgumentException("Invalid characters in the channel name.", nameof(channel));

			if (key != null && key.IndexOfAny(new char[] { ' ', '\0', '\n', ',' }) != -1)
				throw new ArgumentException("Invalid characters in the channel key.", nameof(key));

			this.Channel = channel;
			this.Key = key;
		}
	}
}
