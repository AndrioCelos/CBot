using System.Collections;
using System.Collections.ObjectModel;
using AnIRC;
using CBot;

namespace GameCorner;
public interface IGame {
	string Name { get; }
	IList Players { get; }
}

public abstract class Game<TPlayer> : IGame where TPlayer : Player {
	/// <summary>When overridden, returns the name of this game.</summary>
	public abstract string Name { get; }

	public IrcMessageTarget Venue { get; set; }

	public ObservableCollection<TPlayer> Players { get; } = new ObservableCollection<TPlayer>();
	IList IGame.Players => this.Players;

	/// <summary>Returns or sets the number of players to advance. Can be used to reverse or alter the turn order.</summary>
	public int TurnOffset { get; set; } = 1;
	/// <summary>Returns the index of the player with the turn.</summary>
	public int TurnIndex { get; set; }
	/// <summary>Returns the index of the last player who is allowed to take a turn.</summary>
	public int TurnSkipIndex { get; set; }
	/// <summary>Returns the player with the turn.</summary>
	public TPlayer TurnPlayer => this.Players[this.TurnIndex];

	public Game(IrcMessageTarget venue) {
		this.Venue = venue;
	}

	public int IndexOf(Func<TPlayer, bool> predicate) {
		for (int i = 0; i < this.Players.Count; ++i) {
			if (predicate.Invoke(this.Players[i])) return i;
		}
		return -1;
	}

	/// <summary>Returns the list of remaining player indices in turn order, ending with the current player.</summary>
	public IEnumerable<int> NextPlayerIndices {
		get {
			int i = this.TurnIndex;
			do {
				i = (i + this.TurnOffset) % this.Players.Count;
				if (i < 0) i += this.Players.Count;
				if (this.Players[i].IsPlaying) yield return i;
			} while (i != this.TurnIndex);
		}
	}
	/// <summary>Returns the list of remaining players in turn order, starting with the current player.</summary>
	public IEnumerable<TPlayer> NextPlayers => this.NextPlayerIndices.Select(i => this.Players[i]);

	/// <summary>Advances the turn to the next player who is playing.</summary>
	public void AdvanceTurn() {
		this.TurnIndex = this.NextPlayerIndices.First();
		this.TurnSkipIndex = this.TurnIndex;
	}

	/// <summary>Checks whether the player at the specified index is allowed to move in a turn-based game.</summary>
	public bool CanMove(int playerIndex) {
		int i = this.TurnIndex;
		while (true) {
			if (i == playerIndex) return true;
			if (i == this.TurnSkipIndex) return false;
			i += this.TurnSkipIndex;
			if (i == this.TurnIndex) return false;
		}
	}
	/// <summary>Checks whether the specified player is allowed to move in a turn-based game.</summary>
	public bool CanMove(Player player) {
		int i = this.TurnIndex;
		while (true) {
			if (this.Players[i] == player) return true;
			if (i == this.TurnSkipIndex) return false;
			i += this.TurnSkipIndex;
			if (i == this.TurnIndex) return false;
		}
	}

	/// <summary>Skips to the player at the specified index, executing a callback for each player skipped.</summary>
	public void SkipTo(int playerIndex, Action<int> skipCallback) {
		//if (!this.CanMove(playerIndex)) throw new ArgumentException("Cannot skip to a player not allowed to move yet.", nameof(playerIndex));
		while (this.TurnIndex != playerIndex) {
			skipCallback?.Invoke(this.TurnIndex);
			if (this.TurnSkipIndex == this.TurnIndex) this.TurnSkipIndex += this.TurnOffset;
			this.TurnIndex += this.TurnOffset;
		}
	}
}

public abstract class GamePlugin : Plugin {
	/// <summary>The set of prizes that are available to this game.</summary>
	public List<Tuple<Prize, int>> Prizes { get; } = new List<Tuple<Prize, int>>();
}

public class GameTimerEventArgs<TGame> : EventArgs {
	public TGame Game { get; }
	public DateTime SignalTime { get; }

	public GameTimerEventArgs(TGame game, DateTime signalTime) {
		this.Game = game;
		this.SignalTime = signalTime;
	}
}
