namespace LoopGame;

using System.Numerics;

/// <summary>
/// Small world-space math helpers shared by collision and rendering.
/// Everything here works in XYZ world units, never in screen pixels.
/// </summary>
internal static class Geometry3D
{
	private const float Epsilon = 1e-6f;

	/// <summary>
	/// Closest approach between two 3D segments. Returns the SQUARED distance and
	/// the closest point on each segment. Used to test a projectile step
	/// (previous -> current) against an enemy capsule axis (feet -> crown).
	/// </summary>
	public static float ClosestPointsBetweenSegments(
		Vector3 firstStart,
		Vector3 firstEnd,
		Vector3 secondStart,
		Vector3 secondEnd,
		out Vector3 closestOnFirst,
		out Vector3 closestOnSecond)
	{
		Vector3 firstDelta = firstEnd - firstStart;
		Vector3 secondDelta = secondEnd - secondStart;
		Vector3 startDelta = firstStart - secondStart;
		float firstLengthSquared = Vector3.Dot(firstDelta, firstDelta);
		float secondLengthSquared = Vector3.Dot(secondDelta, secondDelta);
		float secondProjection = Vector3.Dot(secondDelta, startDelta);
		float firstFraction;
		float secondFraction;

		if (firstLengthSquared <= Epsilon && secondLengthSquared <= Epsilon)
		{
			closestOnFirst = firstStart;
			closestOnSecond = secondStart;
			return (firstStart - secondStart).LengthSquared();
		}

		if (firstLengthSquared <= Epsilon)
		{
			firstFraction = 0f;
			secondFraction = Math.Clamp(secondProjection / secondLengthSquared, 0f, 1f);
		}
		else
		{
			float firstProjection = Vector3.Dot(firstDelta, startDelta);
			if (secondLengthSquared <= Epsilon)
			{
				secondFraction = 0f;
				firstFraction = Math.Clamp(-firstProjection / firstLengthSquared, 0f, 1f);
			}
			else
			{
				float crossProjection = Vector3.Dot(firstDelta, secondDelta);
				float denominator = (firstLengthSquared * secondLengthSquared) - (crossProjection * crossProjection);
				firstFraction = denominator > Epsilon
					? Math.Clamp(((crossProjection * secondProjection) - (firstProjection * secondLengthSquared)) / denominator, 0f, 1f)
					: 0f;
				secondFraction = ((crossProjection * firstFraction) + secondProjection) / secondLengthSquared;

				if (secondFraction < 0f)
				{
					secondFraction = 0f;
					firstFraction = Math.Clamp(-firstProjection / firstLengthSquared, 0f, 1f);
				}
				else if (secondFraction > 1f)
				{
					secondFraction = 1f;
					firstFraction = Math.Clamp((crossProjection - firstProjection) / firstLengthSquared, 0f, 1f);
				}
			}
		}

		closestOnFirst = firstStart + (firstDelta * firstFraction);
		closestOnSecond = secondStart + (secondDelta * secondFraction);
		return (closestOnFirst - closestOnSecond).LengthSquared();
	}

	/// <summary>Horizontal (XZ) distance, ignoring height.</summary>
	public static float GroundDistance(Vector3 first, Vector3 second)
	{
		float deltaX = first.X - second.X;
		float deltaZ = first.Z - second.Z;
		return MathF.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
	}
}
