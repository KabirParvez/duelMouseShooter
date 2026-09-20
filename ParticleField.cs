namespace LoopGame;

using System.Numerics;

internal struct Particle
{
	public Vector3 Position;
	public Vector3 Velocity;
	public float Gravity;
	public float Drag;
	public float Life;
	public float MaxLife;
	public float Radius;
	public Color Color;
}

/// <summary>
/// A very small world-space particle system. Particles live at real XYZ positions
/// and are projected through the shared camera pipeline, so they shrink with
/// distance exactly like every other world object.
/// </summary>
internal sealed class ParticleField
{
	private const int MaxParticles = 420;

	private readonly List<Particle> particles = new(MaxParticles);
	private readonly Random random = new();

	public int Count => particles.Count;

	public void Clear()
	{
		particles.Clear();
	}

	public void Update(float deltaTime)
	{
		for (int index = particles.Count - 1; index >= 0; index--)
		{
			Particle particle = particles[index];
			particle.Life -= deltaTime;
			if (particle.Life <= 0f)
			{
				particles.RemoveAt(index);
				continue;
			}

			particle.Velocity += new Vector3(0f, -particle.Gravity * deltaTime, 0f);
			particle.Velocity *= MathF.Max(0f, 1f - (particle.Drag * deltaTime));
			particle.Position += particle.Velocity * deltaTime;

			// Cheap ground bounce so sparks do not sink through the battlefield.
			if (particle.Position.Y < 0.02f)
			{
				particle.Position.Y = 0.02f;
				particle.Velocity = new Vector3(particle.Velocity.X * 0.4f, MathF.Abs(particle.Velocity.Y) * 0.25f, particle.Velocity.Z * 0.4f);
			}

			particles[index] = particle;
		}
	}

	/// <summary>Small bright spark burst where a projectile struck armour.</summary>
	public void SpawnImpact(Vector3 origin, Color color)
	{
		for (int index = 0; index < 12; index++)
		{
			Spawn(new Particle
			{
				Position = origin,
				Velocity = NextDirection() * NextFloat(1.6f, 5.4f),
				Gravity = 7.5f,
				Drag = 2.2f,
				Life = NextFloat(0.18f, 0.42f),
				MaxLife = 0.42f,
				Radius = NextFloat(0.018f, 0.05f),
				Color = index % 3 == 0 ? Color.FromArgb(255, 255, 238, 176) : color
			});
		}
	}

	/// <summary>Large debris and smoke burst when the exosuit is destroyed.</summary>
	public void SpawnDestruction(Vector3 origin)
	{
		for (int index = 0; index < 46; index++)
		{
			Spawn(new Particle
			{
				Position = origin + (NextDirection() * NextFloat(0f, 0.45f)),
				Velocity = NextDirection() * NextFloat(2.2f, 8.5f),
				Gravity = 9.0f,
				Drag = 1.1f,
				Life = NextFloat(0.5f, 1.35f),
				MaxLife = 1.35f,
				Radius = NextFloat(0.03f, 0.1f),
				Color = index % 4 == 0
					? Color.FromArgb(255, 255, 226, 150)
					: Color.FromArgb(255, 236, 118, 74)
			});
		}

		for (int index = 0; index < 18; index++)
		{
			Spawn(new Particle
			{
				Position = origin + (NextDirection() * NextFloat(0f, 0.5f)),
				Velocity = (NextDirection() * NextFloat(0.4f, 1.5f)) + new Vector3(0f, 1.3f, 0f),
				Gravity = -1.1f,
				Drag = 1.6f,
				Life = NextFloat(0.9f, 1.8f),
				MaxLife = 1.8f,
				Radius = NextFloat(0.12f, 0.3f),
				Color = Color.FromArgb(150, 86, 96, 104)
			});
		}
	}

	public void Draw(Graphics graphics, CameraView view)
	{
		foreach (Particle particle in particles)
		{
			if (!view.TryProjectRadius(particle.Position, particle.Radius, out PointF center, out float screenRadius))
			{
				continue;
			}

			if (center.X < -200f || center.Y < -200f || center.X > view.Viewport.Width + 200f || center.Y > view.Viewport.Height + 200f)
			{
				continue;
			}

			screenRadius = Math.Clamp(screenRadius, 1.1f, 110f);
			float fade = Math.Clamp(particle.Life / MathF.Max(0.0001f, particle.MaxLife), 0f, 1f);
			int alpha = (int)Math.Clamp(particle.Color.A * fade, 0f, 255f);
			if (alpha <= 2)
			{
				continue;
			}

			using var brush = new SolidBrush(Color.FromArgb(alpha, particle.Color));
			graphics.FillEllipse(brush, center.X - screenRadius, center.Y - screenRadius, screenRadius * 2f, screenRadius * 2f);
		}
	}

	private void Spawn(Particle particle)
	{
		if (particles.Count >= MaxParticles)
		{
			particles.RemoveAt(0);
		}

		particles.Add(particle);
	}

	private Vector3 NextDirection()
	{
		float azimuth = NextFloat(0f, MathF.Tau);
		float height = NextFloat(-1f, 1f);
		float ring = MathF.Sqrt(MathF.Max(0f, 1f - (height * height)));
		return new Vector3(MathF.Cos(azimuth) * ring, height, MathF.Sin(azimuth) * ring);
	}

	private float NextFloat(float minimum, float maximum)
	{
		return minimum + ((float)random.NextDouble() * (maximum - minimum));
	}
}
