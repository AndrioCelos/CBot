namespace GameCorner;
/// <summary>
/// Specifies that a game can run alongside another game without the <see cref="InclusiveAttribute"/> attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class InclusiveAttribute : Attribute {
	// This attribute type has no parameters.
}
