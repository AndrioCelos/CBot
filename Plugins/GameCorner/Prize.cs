using AnIRC;

namespace GameCorner;
public abstract class Prize {
	public abstract Task<bool> AwardAsync(IrcUser user, IGame game);
	public abstract int Remaining { get; }
}

public class TestPrize : Prize {
	public override Task<bool> AwardAsync(IrcUser user, IGame game) {
		user.Say("Congratulations. At some point we'll add actual prizes.");
		return Task.FromResult(true);
	}
	public override int Remaining => int.MaxValue;
}
