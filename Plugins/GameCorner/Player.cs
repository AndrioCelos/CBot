namespace GameCorner;
public abstract class Player {
	public IGame Game { get; }
	public abstract bool IsPlaying { get; }
	public virtual string Name { get; set; }

	public Player(IGame game, string name) {
		this.Game = game;
		this.Name = name;
	}

	public int GetIndex() => this.Game.Players.IndexOf(this);
}
