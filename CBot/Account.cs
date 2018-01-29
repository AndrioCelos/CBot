using System;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace CBot {
    /// <summary>
    /// Represents a user account.
    /// </summary>
	public class Account {
        /// <summary>The user's password, or a hashed representation of it.</summary>
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string Password;
        /// <summary>A HashType value specifying how the password is hashed.</summary>
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)] [DefaultValue(HashType.None)] [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public HashType HashType;
        /// <summary>A list of permissions that the user is to be granted.</summary>
		public string[] Permissions;

        public void SetPassword(string password) {
            if (this.HashType == HashType.None) this.HashType = HashType.SHA256Salted;
            switch (this.HashType) {
                case HashType.PlainText:
                    this.Password = password;
                    break;
                case HashType.SHA256Salted:
                    byte[] salt = new byte[32];
                    new RNGCryptoServiceProvider().GetBytes(salt);
                    byte[] hash = new SHA256Managed().ComputeHash(salt.Concat(Encoding.UTF8.GetBytes(password)).ToArray());
                    this.Password = string.Join(null, salt.Select(b => b.ToString("x2"))) + string.Join(null, hash.Select(b => b.ToString("x2")));
                    break;
                default:
                    throw new InvalidOperationException("The account has an unknown hash type.");
            }
        }
        public void SetPassword(string password, HashType hashType) {
            this.HashType = hashType;
            this.SetPassword(password);
        }

        /// <summary>
        /// Determines whether the user's password matches the given string.
        /// </summary>
        /// <returns>True if the given password matches; false otherwise, or if the given string is null.</returns>
        public bool VerifyPassword(string password) {
            switch (this.HashType) {
                case HashType.None:
                    return false;
                case HashType.PlainText:
                    if (password == null || this.Password == null) return false;
                    return SlowEquals(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(this.Password));
                case HashType.SHA256Salted:
                    if (password == null || this.Password == null) return false;

                    byte[] salt = new byte[32];
                    byte[] hash = new byte[32];

                    // First, decode the salt and correct hash.
                    for (int i = 0; i < 32; ++i)
                        salt[i] = Convert.ToByte(this.Password.Substring(i * 2, 2), 16);
                    for (int i = 0; i < 32; ++i)
                        hash[i] = Convert.ToByte(this.Password.Substring(i * 2 + 64, 2), 16);

                    // Hash the input password and check it against the correct hash.
                    // TODO: upgrade to bcrypt at some point... maybe.
                    var checkHash = new SHA256Managed().ComputeHash(salt.Concat(Encoding.UTF8.GetBytes(password)).ToArray());
                    return SlowEquals(hash, checkHash);
                default:
                    throw new InvalidOperationException("The account has an unknown hash type.");
            }
        }

        /// <summary>Compares two byte arrays in constant time.</summary>
        /// <param name="v1">The first array to compare.</param>
        /// <param name="v2">The second array to compare.</param>
        /// <returns>True if the two arrays have the same length and content; false otherwise.</returns>
        /// <remarks>
        ///   A time-constant comparison is used for security purposes.
        ///   The term refers to the property that the time taken by this function is unrelated to the content of the arrays,
        ///     but is related to their lengths (specifically, the smaller of their lengths).
        ///   In theory, variations in the time taken to compare the arrays could be used to gain secret information
        ///   about a user's credentials. In practice, such an attack would be difficult to perform over IRC, but
        ///   we use time-constant comparisons anyway.
        ///   For more information, see https://crackstation.net/hashing-security.htm
        ///     'Why does the hashing code on this page compare the hashes in "length-constant" time?'
        /// </remarks>
        public static bool SlowEquals(byte[] v1, byte[] v2) {
            if (v1 == null) return (v2 == null);
            if (v2 == null) return false;

            int diff = v1.Length ^ v2.Length;  // The xor operation returns 0 if, and only if, the operands are identical.
            for (int i = 0; i < v1.Length && i < v2.Length; ++i)
                diff |= (v1[i] ^ v2[i]);
            return (diff == 0);
        }
    }

    /// <summary>
    /// Specifies how a CBot account password is hashed.
    /// </summary>
    public enum HashType {
        /// <summary>There is no password.</summary>
        None,
        /// <summary>The field is the plaintext password.</summary>
        PlainText,
        /// <summary>The field is a 256-bit salt followed by a salted SHA-256 hash.</summary>
        SHA256Salted
    }

}
