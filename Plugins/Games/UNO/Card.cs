namespace UNO;
/// <summary>Represents an UNO card as a <see cref="byte"/>.</summary>
public struct Card : IEquatable<Card> {
	private byte value;

	public Card(byte value) {
		this.value = value;
	}
	public Card(Colour colour, Rank rank) {
		this.value = (rank.HasFlag(Rank.Wild) ? (byte) rank : (byte) ((int) colour + (int) rank));
	}

	/// <summary>Returns a <see cref="Card"/> value representing a Wild card.</summary>
	public static Card Wild => new Card((byte) Rank.Wild);
	/// <summary>Returns a <see cref="Card"/> value representing a Wild Draw Four card.</summary>
	public static Card WildDrawFour => new Card((byte) Rank.WildDrawFour);
	/// <summary>Returns a <see cref="Card"/> value representing no card.</summary>
	public static Card None => new Card(255);

	/// <summary>Returns the colour of this card.</summary>
	public Colour Colour => (Colour) (this.value & 48);
	/// <summary>Returns the rank of this card.</summary>
	public Rank Rank => (Rank) ((this.value & (byte) Rank.Wild) != 0 ? this.value : this.value & 15);
	/// <summary>Returns a value indicating whether this card is any kind of wild card.</summary>
	public bool IsWild => (this.value & (byte) Rank.Wild) != 0;

	public bool Equals(Card other) => this == other;
	public override bool Equals(object other) => other is Card && this == (Card) other;

	public static bool operator ==(Card v1, Card v2) => v1.value == v2.value;
	public static bool operator !=(Card v1, Card v2) => v1.value != v2.value;

	public override int GetHashCode() => this.value.GetHashCode();

	public override string ToString() {
		var s = new char[2];

		if (this.IsWild) {
			// Wild card
			s[0] = 'W';
			if (this == Card.Wild)
				s[1] = ' ';
			else if (this == Card.WildDrawFour)
				s[1] = 'D';
			else
				s[1] = '?';
		} else {
			switch (this.Colour) {
				case Colour.Red   : s[0] = 'R'; break;
				case Colour.Yellow: s[0] = 'Y'; break;
				case Colour.Green : s[0] = 'G'; break;
				case Colour.Blue  : s[0] = 'B'; break;
				default           : s[0] = '?'; break;
			}
			switch (this.Rank) {
				case (Rank)     0: s[1] = '0'; break;
				case (Rank)     1: s[1] = '1'; break;
				case (Rank)     2: s[1] = '2'; break;
				case (Rank)     3: s[1] = '3'; break;
				case (Rank)     4: s[1] = '4'; break;
				case (Rank)     5: s[1] = '5'; break;
				case (Rank)     6: s[1] = '6'; break;
				case (Rank)     7: s[1] = '7'; break;
				case (Rank)     8: s[1] = '8'; break;
				case (Rank)     9: s[1] = '9'; break;
				case Rank.Reverse: s[1] = 'R'; break;
				case Rank.Skip   : s[1] = 'S'; break;
				case Rank.DrawTwo: s[1] = 'D'; break;
				default          : s[1] = '?'; break;
			}
		}
		return new string(s);
	}


	public static explicit operator Card(byte value) => new Card(value);
	public static explicit operator byte(Card card) => card.value;
}

/// <summary>
/// Represents the colur of an UNO card.
/// </summary>
public enum Colour : byte {
	Red = 0,
	Yellow = 16,
	Green = 32,
	Blue = 48,
	/// <summary>The player to move must choose a colour.</summary>
	Pending = 64,
	/// <summary>No colour has been chosen. For a Wild card, anything may be played on it.</summary>
	None = 128
}

/// <summary>
/// Represents the rank (number or action) of an UNO card.
/// </summary>
public enum Rank : byte {
	Zero,
	One,
	Two,
	Three,
	Four,
	Five,
	Six,
	Seven,
	Eight,
	Nine,
	Reverse,
	Skip,
	DrawTwo,
	Wild = 64,
	WildDrawFour
}
