using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace CBot;
/// <summary>
/// Represents a user account.
/// </summary>
public class Account {
	/// <summary>The user's password, or a hashed representation of it.</summary>
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public string? Password;
	/// <summary>A HashType value specifying how the password is hashed.</summary>
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)] [DefaultValue(HashType.None)] [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
	public HashType HashType;
	/// <summary>A list of permissions that the user is to be granted.</summary>
	public string[] Permissions;

	private const int SHA256_SALT_BYTES = 32;

	public Account(string[] permissions)
		=> this.Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));

	[JsonConstructor]
	public Account(HashType hashType, string? password, string[] permissions) {
		this.HashType = hashType;
		this.Password = password;
		this.Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
	}

	public void SetPassword(string password) {
		if (this.HashType == HashType.None) this.HashType = HashType.SHA256Salted;
		switch (this.HashType) {
			case HashType.PlainText:
				this.Password = password;
				break;
			case HashType.SHA256Salted:
				var bytes = HashPassword(password);
				this.Password = string.Join(null, bytes.Select(b => b.ToString("x2")));
				break;
			default:
				throw new InvalidOperationException("The account has an unknown hash type.");
		}
	}
	public void SetPassword(string password, HashType hashType) {
		this.HashType = hashType;
		this.SetPassword(password);
	}

	/// <summary>Returns a SHA-256 hash and salt for a password.</summary>
	public static byte[] HashPassword(string password) => HashPassword(password, ReadOnlyMemory<byte>.Empty);
	/// <summary>Returns a SHA-256 hash and salt for a password.</summary>
	public static byte[] HashPassword(string password, ReadOnlyMemory<byte> salt) {
		var hashInput = new byte[SHA256_SALT_BYTES + Encoding.UTF8.GetByteCount(password)];

		// Generate random salt using a cryptographically-secure psuedo-random number generator.
		if (salt.IsEmpty) salt = RandomNumberGenerator.GetBytes(SHA256_SALT_BYTES);
		salt.CopyTo(hashInput);

		// Use SHA-256 to generate the hash.
		Encoding.UTF8.GetBytes(password).CopyTo(hashInput, SHA256_SALT_BYTES);
		var hash = SHA256.HashData(hashInput);

		var result = new byte[SHA256_SALT_BYTES + 32];
		salt.CopyTo(result);
		hash.CopyTo(result, SHA256_SALT_BYTES);
		return result;
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
				var bytes = new byte[this.Password.Length / 2];
				for (int i = 0; i < bytes.Length; ++i)
					bytes[i] = Convert.ToByte(this.Password.Substring(i * 2, 2), 16);
				return VerifySha256Salted(password, bytes);
			default:
				throw new InvalidOperationException("The account has an unknown hash type.");
		}
	}

	public static bool VerifySha256Salted(string password, ReadOnlySpan<byte> hash) {
		if (password == null || hash == null) return false;

		var salt = new byte[32];
		hash[..SHA256_SALT_BYTES].CopyTo(salt);

		// Hash the input password and check it against the correct hash.
		// TODO: upgrade to bcrypt at some point... maybe.
		var checkHash = HashPassword(password, salt).AsSpan(SHA256_SALT_BYTES);
		return SlowEquals(hash, checkHash);
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
	public static bool SlowEquals(ReadOnlySpan<byte> v1, ReadOnlySpan<byte> v2) {
		var diff = v1.Length ^ v2.Length;  // The xor operation returns 0 if, and only if, the operands are identical.
		for (int i = 0; i < v1.Length && i < v2.Length; ++i)
			diff |= v1[i] ^ v2[i];
		return diff == 0;
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
