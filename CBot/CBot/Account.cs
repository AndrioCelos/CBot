using System;

namespace CBot {
    /// <summary>
    /// Represents a user account.
    /// </summary>
	public class Account {
        /// <summary>
        /// A hexadecimal representation of a 32-byte salt followed by a SHA-256 hash of the same salt and user's password.
        /// </summary>
		public string Password;
        /// <summary>
        /// A list of permissions that the user is to be granted.
        /// </summary>
		public string[] Permissions;
	}
}
