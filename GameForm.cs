using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Numerics;

namespace LoopGame;

internal sealed class GameForm : Form
{
	private readonly Font titleFont = new("Segoe UI", 30, FontStyle.Bold);
	private readonly Font instructionFont = new("Segoe UI", 15, FontStyle.Regular);
	private readonly Font detailFont = new("Consolas", 11, FontStyle.Regular);
	private readonly System.Windows.Forms.Timer fireIndicatorTimer;
	private readonly System.Windows.Forms.Timer combatTimer;
	private readonly Stopwatch frameClock = Stopwatch.StartNew();
	private readonly List<ProjectileTracer> tracers = new();
	private readonly ParticleField particles = new();
	private CameraView? activeView;
	private Enemy? enemy;
	private float playerHealth = PlayerMaxHealth;
	private int score;
	private bool gameOver;
	private float gameOverInputDelay;
	private float damageFlash;
	private float targetDownBanner;
	private RawMouseInput? rawMouseInput;
	private int wmInputCount;
	private AssignmentState assignmentState = AssignmentState.WaitingForLeft;
	private MouseAssignment? leftAssignment;
	private MouseAssignment? rightAssignment;
	private PointF leftCrosshair = new(280, 330);
	private PointF rightCrosshair = new(680, 330);
	private string? fireIndicator;
	private RawMouseMovement? lastRawInput;
	private string lastRawInputType = "none";
	private bool leftFireHeld;
	private bool rightFireHeld;
	private float leftFireCooldown;
	private float rightFireCooldown;
	private float leftRecoil;
	private float rightRecoil;
	private float leftMuzzleFlash;
	private float rightMuzzleFlash;
	private Vector3 playerPosition = Vector3.Zero;
	private float cameraYaw;
	private float cameraPitch;
	private bool cameraControlHeld;
	private int movementDirection;
	private long lastFrameTimestamp;
	private static readonly Vector3 LeftWeaponPosition = new(-0.62f, -0.47f, 0.85f);
	private static readonly Vector3 RightWeaponPosition = new(0.62f, -0.47f, 0.85f);
	private const float FocalLength = 470f;
	private const float AimDistance = 36f;
	private const float CameraHeight = 1.65f;
	private const float LookSensitivity = 0.0028f;
	private const float MovementSpeed = 3.2f;
	private const float MinimumPitch = -1.35f;
	private const float MaximumPitch = 1.35f;
	private const float WheelStep = 120f;
	private const float PlayerMaxHealth = 100f;
	private const float ProjectileDamage = 14f;
	private const float HeadshotMultiplier = 2f;
	private const float DamageFlashDuration = 0.45f;
	private const float TargetDownBannerDuration = 3.2f;
	private const float GameOverInputDelay = 0.9f;

	public GameForm()
	{
		Text = "Loop Game";
		ClientSize = new Size(960, 650);
		MinimumSize = new Size(700, 600);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(9, 15, 24);
		ForeColor = Color.FromArgb(225, 239, 247);
		DoubleBuffered = true;
		KeyPreview = true;
		SetStyle(ControlStyles.ResizeRedraw, true);
		fireIndicatorTimer = new System.Windows.Forms.Timer { Interval = 450 };
		fireIndicatorTimer.Tick += ClearFireIndicator;
		combatTimer = new System.Windows.Forms.Timer { Interval = 16 };
		combatTimer.Tick += UpdateCombat;
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		rawMouseInput = new RawMouseInput(Handle);
		rawMouseInput.MouseMoved += OnRawMouseMoved;
		SpawnEnemy();
		combatTimer.Start();
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);

		if (e.KeyCode == Keys.R && assignmentState == AssignmentState.BothHandsReady)
		{
			RestartRound();
			e.Handled = true;
		}
	}

	/// <summary>Drops a single exosuit ahead of the player's current facing direction.</summary>
	private void SpawnEnemy()
	{
		Vector3 groundForward = GetCameraForwardOnGround();
		Vector3 spawnPosition = playerPosition + (groundForward * Enemy.SpawnDistance);
		// Face the player immediately so it does not spin on the first frame.
		float facing = MathF.Atan2(-groundForward.X, -groundForward.Z);
		enemy = new Enemy(new Vector3(spawnPosition.X, 0f, spawnPosition.Z), facing);
	}

	private void RestartRound()
	{
		playerPosition = Vector3.Zero;
		cameraYaw = 0f;
		cameraPitch = 0f;
		cameraControlHeld = false;
		movementDirection = 0;
		leftFireHeld = false;
		rightFireHeld = false;
		leftFireCooldown = 0f;
		rightFireCooldown = 0f;
		leftRecoil = 0f;
		rightRecoil = 0f;
		leftMuzzleFlash = 0f;
		rightMuzzleFlash = 0f;
		playerHealth = PlayerMaxHealth;
		score = 0;
		gameOver = false;
		gameOverInputDelay = 0f;
		damageFlash = 0f;
		targetDownBanner = 0f;
		tracers.Clear();
		particles.Clear();
		SpawnEnemy();
		Invalidate();
	}

	protected override void WndProc(ref Message message)
	{
		if (message.Msg == RawMouseInput.WmInput)
		{
			wmInputCount++;
		}

		if (rawMouseInput?.HandlesMessage(message.Msg) == true)
		{
			rawMouseInput.ProcessWindowMessage(message.LParam);
		}

		base.WndProc(ref message);
	}

	private void OnRawMouseMoved(object? sender, RawMouseMovement movement)
	{
		lastRawInput = movement;
		lastRawInputType = GetRawInputType(movement);
		Text = $"Loop Game | Raw Input {lastRawInputType} | Device {movement.DeviceHandle}";

		if (assignmentState == AssignmentState.WaitingForLeft)
		{
			if ((movement.ButtonFlags & RawMouseInput.RightButtonDown) == 0)
			{
				return;
			}

			leftAssignment = new MouseAssignment(movement.DeviceHandle, movement.DeviceName);
			assignmentState = AssignmentState.WaitingForRight;
			Invalidate();
			return;
		}

		if (assignmentState == AssignmentState.WaitingForRight && leftAssignment?.DeviceHandle != movement.DeviceHandle)
		{
			if ((movement.ButtonFlags & RawMouseInput.LeftButtonDown) == 0)
			{
				return;
			}

			rightAssignment = new MouseAssignment(movement.DeviceHandle, movement.DeviceName);
			assignmentState = AssignmentState.BothHandsReady;
			Invalidate();
			return;
		}

		if (assignmentState != AssignmentState.BothHandsReady)
		{
			return;
		}

		if (gameOver)
		{
			const int anyFireButton = RawMouseInput.LeftButtonDown | RawMouseInput.RightButtonDown;
			if (gameOverInputDelay <= 0f && (movement.ButtonFlags & anyFireButton) != 0)
			{
				RestartRound();
			}

			return;
		}

		if (movement.DeviceHandle == leftAssignment?.DeviceHandle)
		{
			leftCrosshair = MoveCrosshair(leftCrosshair, movement.DeltaX, movement.DeltaY);
			if ((movement.ButtonFlags & RawMouseInput.RightButtonDown) != 0)
			{
				leftFireHeld = true;
				TryFireLeftWeapon();
				ShowFireIndicator("LEFT FIRE");
			}
			if ((movement.ButtonFlags & RawMouseInput.RightButtonUp) != 0)
			{
				leftFireHeld = false;
			}
		}
		else if (movement.DeviceHandle == rightAssignment?.DeviceHandle)
		{
			if (cameraControlHeld)
			{
				cameraYaw += movement.DeltaX * LookSensitivity;
				cameraPitch = Math.Clamp(cameraPitch - (movement.DeltaY * LookSensitivity), MinimumPitch, MaximumPitch);
			}
			else
			{
				rightCrosshair = MoveCrosshair(rightCrosshair, movement.DeltaX, movement.DeltaY);
			}

			if ((movement.ButtonFlags & RawMouseInput.MiddleButtonDown) != 0)
			{
				cameraControlHeld = true;
			}
			if ((movement.ButtonFlags & RawMouseInput.MiddleButtonUp) != 0)
			{
				cameraControlHeld = false;
				movementDirection = 0;
			}
			if ((movement.ButtonFlags & RawMouseInput.MouseWheel) != 0 && movement.ButtonData != 0)
			{
				movementDirection = Math.Sign((short)movement.ButtonData);
			}

			if ((movement.ButtonFlags & RawMouseInput.LeftButtonDown) != 0)
			{
				rightFireHeld = true;
			TryFireRightWeapon();
				ShowFireIndicator("RIGHT FIRE");
			}
			if ((movement.ButtonFlags & RawMouseInput.LeftButtonUp) != 0)
			{
				rightFireHeld = false;
			}
		}

		Invalidate();
	}

	private static string GetRawInputType(RawMouseMovement movement)
	{
		if ((movement.ButtonFlags & RawMouseInput.LeftButtonDown) != 0)
		{
			return "left button down";
		}

		if ((movement.ButtonFlags & RawMouseInput.RightButtonDown) != 0)
		{
			return "right button down";
		}

		return movement.DeltaX != 0 || movement.DeltaY != 0 ? "movement" : "other";
	}

	private PointF MoveCrosshair(PointF current, int deltaX, int deltaY)
	{
		const float crosshairRadius = 22;
		float x = Math.Clamp(current.X + deltaX, crosshairRadius, ClientSize.Width - crosshairRadius);
		float y = Math.Clamp(current.Y + deltaY, 125 + crosshairRadius, ClientSize.Height - crosshairRadius);
		return new PointF(x, y);
	}

	private void ShowFireIndicator(string indicator)
	{
		fireIndicator = indicator;
		fireIndicatorTimer.Stop();
		fireIndicatorTimer.Start();
	}

	private void ClearFireIndicator(object? sender, EventArgs e)
	{
		fireIndicator = null;
		fireIndicatorTimer.Stop();
		Invalidate();
	}

	private void UpdateCombat(object? sender, EventArgs e)
	{
		long now = frameClock.ElapsedTicks;
		float elapsedSeconds = lastFrameTimestamp == 0
			? 0.016f
			: Math.Clamp((now - lastFrameTimestamp) / (float)Stopwatch.Frequency, 0.001f, 0.05f);
		lastFrameTimestamp = now;

		if (assignmentState != AssignmentState.BothHandsReady)
		{
			return;
		}

		damageFlash = MathF.Max(0f, damageFlash - elapsedSeconds);
		targetDownBanner = MathF.Max(0f, targetDownBanner - elapsedSeconds);
		particles.Update(elapsedSeconds);

		if (gameOver)
		{
			gameOverInputDelay = MathF.Max(0f, gameOverInputDelay - elapsedSeconds);
			UpdateTracers(elapsedSeconds);
			if (enemy is { State: EnemyState.Dying })
			{
				enemy.Update(elapsedSeconds, playerPosition);
			}

			Invalidate();
			return;
		}

		if (cameraControlHeld && movementDirection != 0)
		{
			Vector3 groundForward = GetCameraForwardOnGround();
			playerPosition += groundForward * (movementDirection * MovementSpeed * elapsedSeconds);
		}
		leftFireCooldown = MathF.Max(0f, leftFireCooldown - elapsedSeconds);
		rightFireCooldown = MathF.Max(0f, rightFireCooldown - elapsedSeconds);
		leftRecoil = MathF.Max(0f, leftRecoil - (elapsedSeconds * 70f));
		rightRecoil = MathF.Max(0f, rightRecoil - (elapsedSeconds * 70f));
		leftMuzzleFlash = MathF.Max(0f, leftMuzzleFlash - elapsedSeconds);
		rightMuzzleFlash = MathF.Max(0f, rightMuzzleFlash - elapsedSeconds);

		if (leftFireHeld)
		{
			TryFireLeftWeapon();
		}
		if (rightFireHeld)
		{
			TryFireRightWeapon();
		}

		UpdateEnemy(elapsedSeconds);
		UpdateTracers(elapsedSeconds);

		Invalidate();
	}

	private void UpdateEnemy(float elapsedSeconds)
	{
		if (enemy is null)
		{
			return;
		}

		float damage = enemy.Update(elapsedSeconds, playerPosition);
		if (damage > 0f)
		{
			ApplyPlayerDamage(damage);
		}

		if (enemy.State == EnemyState.Dead)
		{
			enemy = null;
		}
	}

	/// <summary>
	/// Advances every projectile and tests its swept world-space segment against the
	/// enemy capsule. Collision is pure XYZ - no screen rectangles are involved.
	/// </summary>
	private void UpdateTracers(float elapsedSeconds)
	{
		for (int index = tracers.Count - 1; index >= 0; index--)
		{
			ProjectileTracer tracer = tracers[index];
			if (!tracer.Update(elapsedSeconds))
			{
				tracers.RemoveAt(index);
				continue;
			}

			if (enemy is null || !enemy.IsAlive)
			{
				continue;
			}

			if (!enemy.TryHit(tracer.PreviousPosition, tracer.Position, out Vector3 hitPoint, out bool headshot))
			{
				continue;
			}

			particles.SpawnImpact(hitPoint, tracer.Color);
			tracers.RemoveAt(index);

			float damage = headshot ? ProjectileDamage * HeadshotMultiplier : ProjectileDamage;
			if (enemy.TakeDamage(damage))
			{
				particles.SpawnDestruction(enemy.ChestPosition);
				score += Enemy.ScoreValue;
				targetDownBanner = TargetDownBannerDuration;
			}
		}
	}

	private void ApplyPlayerDamage(float amount)
	{
		playerHealth = MathF.Max(0f, playerHealth - amount);
		damageFlash = DamageFlashDuration;

		if (playerHealth > 0f)
		{
			return;
		}

		gameOver = true;
		gameOverInputDelay = GameOverInputDelay;
		leftFireHeld = false;
		rightFireHeld = false;
		movementDirection = 0;
		cameraControlHeld = false;
	}

	private void TryFireLeftWeapon()
	{
		if (leftFireCooldown > 0f)
		{
			return;
		}

		FireWeapon(LeftWeaponPosition, leftCrosshair, Color.FromArgb(82, 226, 255), "LEFT ARM");
		leftFireCooldown = 0.18f;
		leftRecoil = 12f;
		leftMuzzleFlash = 0.08f;
		ShowFireIndicator("LEFT FIRE");
	}

	private void TryFireRightWeapon()
	{
		if (rightFireCooldown > 0f)
		{
			return;
		}

		FireWeapon(RightWeaponPosition, rightCrosshair, Color.FromArgb(255, 184, 92), "RIGHT ARM");
		rightFireCooldown = 0.18f;
		rightRecoil = 12f;
		rightMuzzleFlash = 0.08f;
		ShowFireIndicator("RIGHT FIRE");
	}

	private void FireWeapon(Vector3 weaponPosition, PointF crosshair, Color color, string owner)
	{
		Vector3 worldWeaponPosition = CameraLocalToWorld(weaponPosition);
		Vector3 direction = GetAimDirection(crosshair, weaponPosition);
		tracers.Add(new ProjectileTracer(worldWeaponPosition + (direction * 0.85f), direction * 42f, color, owner));
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using var backgroundBrush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(7, 13, 22), Color.FromArgb(18, 35, 47), 35f);
		graphics.FillRectangle(backgroundBrush, ClientRectangle);

		StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
		using var titleBrush = new SolidBrush(Color.FromArgb(96, 239, 228));
		graphics.DrawString("LOOP GAME", titleFont, titleBrush, new RectangleF(40, 38, ClientSize.Width - 80, 50), centered);

		if (assignmentState == AssignmentState.WaitingForLeft)
		{
			DrawCenteredText(graphics, "CHOOSE YOUR LEFT ARM", instructionFont, 190, Color.FromArgb(96, 239, 228));
			DrawCenteredText(graphics, "Right-click the mouse you want to use as your LEFT ARM", instructionFont, 255, Color.White);
		}
		else if (assignmentState == AssignmentState.WaitingForRight)
		{
			DrawCenteredText(graphics, "CHOOSE YOUR RIGHT ARM", instructionFont, 190, Color.FromArgb(96, 239, 228));
			DrawCenteredText(graphics, "Left-click the other mouse", instructionFont, 255, Color.White);
		}
		else
		{
			CameraView view = CreateCameraView();
			activeView = view;

			DrawFirstPersonBattlefield(graphics);

			// World objects, drawn before the first-person weapons so the arms stay in front.
			if (enemy is not null)
			{
				EnemyRenderer.Draw(graphics, enemy, view);
			}

			particles.Draw(graphics, view);
			DrawTracers(graphics);

			if (enemy is not null)
			{
				EnemyRenderer.DrawHealthBar(graphics, enemy, view, detailFont);
				DrawOffscreenEnemyMarker(graphics, view);
			}

			activeView = null;

			DrawCenteredText(graphics, "BOTH HANDS READY", instructionFont, 28, Color.FromArgb(96, 239, 228));
			DrawCenteredText(graphics, $"LEFT ARM: {leftAssignment?.DeviceName}    RIGHT ARM: {rightAssignment?.DeviceName}", detailFont, 78, Color.FromArgb(190, 210, 218));
			DrawWeapon(graphics, LeftWeaponPosition, leftCrosshair, Color.FromArgb(82, 226, 255), "LEFT WEAPON", leftRecoil, leftMuzzleFlash);
			DrawWeapon(graphics, RightWeaponPosition, rightCrosshair, Color.FromArgb(255, 184, 92), "RIGHT WEAPON", rightRecoil, rightMuzzleFlash);
			DrawCrosshair(graphics, leftCrosshair, Color.FromArgb(96, 239, 228), "LEFT");
			DrawCrosshair(graphics, rightCrosshair, Color.FromArgb(255, 184, 92), "RIGHT");
			using var readyBrush = new SolidBrush(Color.FromArgb(142, 174, 188));
			graphics.DrawString("LEFT ARM: READY", detailFont, readyBrush, 90, 106);
			graphics.DrawString("RIGHT ARM: READY", detailFont, readyBrush, ClientSize.Width - 235, 106);
			DrawPlayerStatus(graphics);

			if (fireIndicator is not null && !gameOver)
			{
				DrawCenteredText(graphics, fireIndicator, instructionFont, ClientSize.Height - 72, Color.FromArgb(255, 220, 120));
			}

			if (damageFlash > 0f)
			{
				int alpha = (int)Math.Clamp(damageFlash / DamageFlashDuration * 92f, 0f, 92f);
				using var hurtBrush = new SolidBrush(Color.FromArgb(alpha, 168, 26, 32));
				graphics.FillRectangle(hurtBrush, ClientRectangle);
			}

			if (targetDownBanner > 0f && !gameOver)
			{
				DrawCenteredText(graphics, "TARGET DESTROYED", instructionFont, 148, Color.FromArgb(255, 220, 120));
				DrawCenteredText(graphics, "PRESS R TO REDEPLOY", detailFont, 186, Color.FromArgb(170, 198, 208));
			}

			if (gameOver)
			{
				DrawGameOver(graphics);
			}
		}

		if (assignmentState != AssignmentState.BothHandsReady)
		{
			DrawRawInputDiagnostic(graphics);
		}
	}

	private void DrawFirstPersonBattlefield(Graphics graphics)
	{
		float horizon = GetHorizonY();
		using var floorBrush = new SolidBrush(Color.FromArgb(8, 22, 29));
		if (horizon < ClientSize.Height)
		{
			graphics.FillPolygon(floorBrush, new[]
			{
				new PointF(0, horizon), new PointF(ClientSize.Width, horizon),
				new PointF(ClientSize.Width, ClientSize.Height), new PointF(0, ClientSize.Height)
			});
		}

		DrawPerspectiveGrid(graphics, horizon);
		DrawStructure(graphics, -7.2f, 1.4f, 4.2f, 9f, Color.FromArgb(24, 61, 72));
		DrawStructure(graphics, 7.2f, 1.4f, 4.2f, 9f, Color.FromArgb(24, 61, 72));
		DrawStructure(graphics, -4.5f, 0.7f, 3.2f, 17f, Color.FromArgb(31, 55, 64));
		DrawStructure(graphics, 4.5f, 0.7f, 3.2f, 17f, Color.FromArgb(31, 55, 64));
		DrawStructure(graphics, -10f, 0.45f, 3.8f, 28f, Color.FromArgb(38, 68, 76));
		DrawStructure(graphics, 10f, 0.45f, 3.8f, 28f, Color.FromArgb(38, 68, 76));
		DrawPerspectiveRail(graphics, -3.8f, 0.25f, 32f, Color.FromArgb(47, 133, 143));
		DrawPerspectiveRail(graphics, 3.8f, 0.25f, 32f, Color.FromArgb(47, 133, 143));
	}

	private void DrawPerspectiveGrid(Graphics graphics, float horizon)
	{
		using var gridPen = new Pen(Color.FromArgb(22, 67, 76), 1);
		for (int index = -12; index <= 12; index++)
		{
			DrawWorldLine(graphics, gridPen, new Vector3(index * 1.5f, 0f, 2.5f), new Vector3(index * 1.5f, 0f, 38f));
		}

		foreach (float depth in new[] { 3f, 4f, 5.5f, 7.5f, 10f, 14f, 19f, 26f, 35f })
		{
			DrawWorldLine(graphics, gridPen, new Vector3(-18f, 0f, depth), new Vector3(18f, 0f, depth));
		}

		using var horizonPen = new Pen(Color.FromArgb(59, 151, 157), 2);
		if (horizon > -8f && horizon < ClientSize.Height + 8f)
		{
			graphics.DrawLine(horizonPen, 0, horizon, ClientSize.Width, horizon);
		}
	}

	private void DrawWorldLine(Graphics graphics, Pen pen, Vector3 start, Vector3 end)
	{
		if (TryProjectSegment(start, end, out PointF first, out PointF second))
		{
			graphics.DrawLine(pen, first, second);
		}
	}

	private void DrawStructure(Graphics graphics, float x, float halfWidth, float height, float depth, Color color)
	{
		CameraView view = activeView ?? CreateCameraView();
		Vector3 frontTopLeft = new(x - halfWidth, height, depth - 0.6f);
		Vector3 frontTopRight = new(x + halfWidth, height, depth - 0.6f);
		Vector3 frontBottomLeft = new(x - halfWidth, 0f, depth - 0.6f);
		Vector3 frontBottomRight = new(x + halfWidth, 0f, depth - 0.6f);
		Vector3 backTopRight = new(x + halfWidth, height, depth + 0.6f);
		Vector3 backBottomRight = new(x + halfWidth, 0f, depth + 0.6f);

		using var frontBrush = new SolidBrush(color);
		using var sideBrush = new SolidBrush(Color.FromArgb(Math.Max(10, color.R - 10), Math.Max(15, color.G - 12), Math.Max(20, color.B - 12)));
		using var edgePen = new Pen(Color.FromArgb(86, 177, 181), 1);

		if (view.TryProjectPolygon(new[] { frontTopRight, backTopRight, backBottomRight, frontBottomRight }, out PointF[] sideFace))
		{
			graphics.FillPolygon(sideBrush, sideFace);
		}

		if (view.TryProjectPolygon(new[] { frontTopLeft, frontTopRight, frontBottomRight, frontBottomLeft }, out PointF[] frontFace))
		{
			graphics.FillPolygon(frontBrush, frontFace);
			graphics.DrawPolygon(edgePen, frontFace);
		}

		DrawWorldLine(graphics, edgePen, frontTopRight, backTopRight);
	}

	private void DrawPerspectiveRail(Graphics graphics, float x, float y, float depth, Color color)
	{
		using var railPen = new Pen(color, 3);
		DrawWorldLine(graphics, railPen, new Vector3(x - 0.35f, y, 2.5f), new Vector3(x - 0.35f, y, depth));
		DrawWorldLine(graphics, railPen, new Vector3(x + 0.35f, y, 2.5f), new Vector3(x + 0.35f, y, depth));
		for (float z = 4f; z < depth; z += 4f)
		{
			DrawWorldLine(graphics, railPen, new Vector3(x - 0.35f, y, z), new Vector3(x + 0.35f, y, z));
		}
	}

	private void DrawWeapon(Graphics graphics, Vector3 weaponPosition, PointF crosshair, Color accent, string label, float recoil, float muzzleFlash)
	{
		Vector3 direction = GetAimDirection(crosshair, weaponPosition);
		Vector3 worldWeaponPosition = CameraLocalToWorld(weaponPosition);
		PointF mount = ProjectWorldToScreen(worldWeaponPosition - (direction * (recoil / 100f)));
		PointF muzzle = ProjectWorldToScreen(worldWeaponPosition + (direction * 0.85f) - (direction * (recoil / 100f)));
		PointF shoulder = ProjectWorldToScreen(worldWeaponPosition - (direction * 0.85f) - (direction * (recoil / 100f)));
		PointF bodyCenter = Midpoint(mount, muzzle);
		PointF screenDirection = NormalizeScreenDirection(mount, muzzle);
		PointF normal = new(-screenDirection.Y, screenDirection.X);
		PointF bodyFront = Add(bodyCenter, Multiply(screenDirection, 34f));
		PointF bodyBack = Add(bodyCenter, Multiply(screenDirection, -34f));

		using var armPen = new Pen(Color.FromArgb(38, 54, 66), 24);
		using var armAccentPen = new Pen(Color.FromArgb(76, 101, 115), 6);
		graphics.DrawLine(armPen, mount, shoulder);
		graphics.DrawLine(armAccentPen, mount, shoulder);

		PointF[] body =
		{
			Add(bodyBack, Multiply(normal, 22f)),
			Add(bodyFront, Multiply(normal, 15f)),
			Add(bodyFront, Multiply(normal, -15f)),
			Add(bodyBack, Multiply(normal, -22f))
		};
		using var bodyBrush = new SolidBrush(Color.FromArgb(28, 39, 49));
		using var bodyPen = new Pen(accent, 2);
		graphics.FillPolygon(bodyBrush, body);
		graphics.DrawPolygon(bodyPen, body);

		using var barrelPen = new Pen(Color.FromArgb(18, 24, 31), 14);
		using var barrelAccentPen = new Pen(accent, 4);
		graphics.DrawLine(barrelPen, bodyFront, muzzle);
		graphics.DrawLine(barrelAccentPen, bodyFront, muzzle);
		graphics.DrawLine(bodyPen, Add(bodyCenter, Multiply(normal, 11f)), Add(bodyCenter, Multiply(normal, -11f)));

		using var labelBrush = new SolidBrush(Color.FromArgb(170, accent.R, accent.G, accent.B));
		using var labelFormat = new StringFormat { Alignment = StringAlignment.Center };
		graphics.DrawString(label, detailFont, labelBrush, bodyCenter.X, bodyCenter.Y + 28, labelFormat);

		if (muzzleFlash > 0f)
		{
			float flashSize = 20f + (muzzleFlash * 90f);
			PointF flashTip = Add(muzzle, Multiply(screenDirection, flashSize));
			PointF[] flash =
			{
				muzzle,
				Add(muzzle, Multiply(normal, 9f)),
				flashTip,
				Add(muzzle, Multiply(normal, -9f))
			};
			using var flashBrush = new SolidBrush(Color.FromArgb(210, 255, 228, 142));
			graphics.FillPolygon(flashBrush, flash);
		}
	}

	private void DrawTracers(Graphics graphics)
	{
		Vector3 cameraPosition = playerPosition + new Vector3(0f, CameraHeight, 0f);

		foreach (ProjectileTracer tracer in tracers)
		{
			if (!TryProjectSegment(tracer.PreviousPosition, tracer.Position, out PointF tail, out PointF position))
			{
				continue;
			}

			// Thickness follows true distance from the camera, not raw world Z.
			float distance = Vector3.Distance(tracer.Position, cameraPosition);
			float depthScale = Math.Clamp(2.5f / MathF.Max(0.5f, distance), 0.35f, 2.5f);
			using var glowPen = new Pen(Color.FromArgb(70, tracer.Color.R, tracer.Color.G, tracer.Color.B), 9f * depthScale);
			using var tracerPen = new Pen(tracer.Color, 3f * depthScale);
			graphics.DrawLine(glowPen, tail, position);
			graphics.DrawLine(tracerPen, tail, position);
		}
	}

	private Vector3 GetAimDirection(PointF crosshair, Vector3 weaponPosition)
	{
		Vector3 cameraRay = Vector3.Normalize(new Vector3(
			(crosshair.X - (ClientSize.Width * 0.5f)) / FocalLength,
			((ClientSize.Height * 0.43f) - crosshair.Y) / FocalLength,
			1f));
		Vector3 aimPoint = CameraLocalToWorld(cameraRay * AimDistance);
		return Vector3.Normalize(aimPoint - CameraLocalToWorld(weaponPosition));
	}

	private PointF ProjectWorldToScreen(Vector3 worldPosition)
	{
		return ProjectCameraLocalToScreen(WorldToCameraLocal(worldPosition));
	}

	private PointF ProjectCameraLocalToScreen(Vector3 relative)
	{
		float depth = MathF.Max(0.25f, relative.Z);
		// Clamped so geometry grazing the near plane cannot produce absurd
		// coordinates that stall GDI+ with multi-million pixel line spans.
		return new PointF(
			Math.Clamp((ClientSize.Width * 0.5f) + (relative.X * FocalLength / depth), -25000f, 25000f),
			Math.Clamp((ClientSize.Height * 0.43f) - (relative.Y * FocalLength / depth), -25000f, 25000f));
	}

	/// <summary>Builds the per-frame camera snapshot shared by every world renderer.</summary>
	private CameraView CreateCameraView()
	{
		return new CameraView(
			playerPosition + new Vector3(0f, CameraHeight, 0f),
			RotateCameraLocal(Vector3.UnitX),
			RotateCameraLocal(Vector3.UnitY),
			RotateCameraLocal(Vector3.UnitZ),
			ClientSize,
			WorldToCameraLocal,
			ProjectCameraLocalToScreen);
	}

	/// <summary>Projects a world segment, clipping it against the near plane first.</summary>
	private bool TryProjectSegment(Vector3 start, Vector3 end, out PointF first, out PointF second)
	{
		first = PointF.Empty;
		second = PointF.Empty;
		Vector3 viewStart = WorldToCameraLocal(start);
		Vector3 viewEnd = WorldToCameraLocal(end);

		if (viewStart.Z < CameraView.NearPlane && viewEnd.Z < CameraView.NearPlane)
		{
			return false;
		}

		if (viewStart.Z < CameraView.NearPlane)
		{
			viewStart = Vector3.Lerp(viewStart, viewEnd, (CameraView.NearPlane - viewStart.Z) / (viewEnd.Z - viewStart.Z));
		}
		else if (viewEnd.Z < CameraView.NearPlane)
		{
			viewEnd = Vector3.Lerp(viewEnd, viewStart, (CameraView.NearPlane - viewEnd.Z) / (viewStart.Z - viewEnd.Z));
		}

		first = ProjectCameraLocalToScreen(viewStart);
		second = ProjectCameraLocalToScreen(viewEnd);
		return true;
	}

	/// <summary>
	/// Screen height of the true horizon for the current pitch. The ground plane
	/// meets infinity here, so the floor now falls away correctly when looking up.
	/// </summary>
	private float GetHorizonY()
	{
		float horizon = (ClientSize.Height * 0.43f) + (FocalLength * MathF.Tan(cameraPitch));
		return Math.Clamp(horizon, -4000f, ClientSize.Height + 4000f);
	}

	private Vector3 CameraLocalToWorld(Vector3 localPosition)
	{
		return playerPosition + new Vector3(0f, CameraHeight, 0f) + RotateCameraLocal(localPosition);
	}

	private Vector3 WorldToCameraLocal(Vector3 worldPosition)
	{
		Vector3 relative = worldPosition - (playerPosition + new Vector3(0f, CameraHeight, 0f));
		return InverseRotateCameraLocal(relative);
	}

	private Vector3 GetCameraForwardOnGround()
	{
		return Vector3.Normalize(new Vector3(MathF.Sin(cameraYaw), 0f, MathF.Cos(cameraYaw)));
	}

	private Vector3 RotateCameraLocal(Vector3 local)
	{
		float pitchCos = MathF.Cos(cameraPitch);
		float pitchSin = MathF.Sin(cameraPitch);
		Vector3 pitched = new(local.X, (local.Y * pitchCos) + (local.Z * pitchSin), (-local.Y * pitchSin) + (local.Z * pitchCos));
		float yawCos = MathF.Cos(cameraYaw);
		float yawSin = MathF.Sin(cameraYaw);
		return new Vector3(
			(pitched.X * yawCos) + (pitched.Z * yawSin),
			pitched.Y,
			(-pitched.X * yawSin) + (pitched.Z * yawCos));
	}

	private Vector3 InverseRotateCameraLocal(Vector3 worldRelative)
	{
		float yawCos = MathF.Cos(cameraYaw);
		float yawSin = MathF.Sin(cameraYaw);
		Vector3 yawRemoved = new(
			(worldRelative.X * yawCos) - (worldRelative.Z * yawSin),
			worldRelative.Y,
			(worldRelative.X * yawSin) + (worldRelative.Z * yawCos));
		float pitchCos = MathF.Cos(cameraPitch);
		float pitchSin = MathF.Sin(cameraPitch);
		return new Vector3(
			yawRemoved.X,
			(yawRemoved.Y * pitchCos) - (yawRemoved.Z * pitchSin),
			(yawRemoved.Y * pitchSin) + (yawRemoved.Z * pitchCos));
	}

	private static PointF NormalizeScreenDirection(PointF from, PointF to)
	{
		float x = to.X - from.X;
		float y = to.Y - from.Y;
		float length = MathF.Sqrt((x * x) + (y * y));
		return length < 0.001f ? new PointF(0, -1) : new PointF(x / length, y / length);
	}

	private static PointF Midpoint(PointF first, PointF second)
	{
		return new PointF((first.X + second.X) * 0.5f, (first.Y + second.Y) * 0.5f);
	}

	private static PointF Add(PointF point, PointF offset)
	{
		return new PointF(point.X + offset.X, point.Y + offset.Y);
	}

	private static PointF Multiply(PointF point, float scalar)
	{
		return new PointF(point.X * scalar, point.Y * scalar);
	}

	private void DrawRawInputDiagnostic(Graphics graphics)
	{
		string device = lastRawInput is null ? "NONE" : lastRawInput.DeviceHandle.ToString();
		string flags = lastRawInput is null ? "NONE" : $"0x{lastRawInput.ButtonFlags:X4}";
		string registration = rawMouseInput is null
			? "RAW INPUT REGISTRATION: NOT INITIALIZED"
			: $"RAW INPUT REGISTRATION: {(rawMouseInput.RegistrationSucceeded ? "SUCCESS" : "FAILED")}";
		string messageStatus = wmInputCount > 0
			? "RAW INPUT MESSAGE RECEIVED"
			: "RAW INPUT MESSAGE RECEIVED: NONE";
		string registrationError = rawMouseInput is null || rawMouseInput.RegistrationSucceeded
			? "NONE"
			: rawMouseInput.RegistrationErrorCode.ToString();
		DrawDiagnosticText(graphics, registration, 312, Color.FromArgb(255, 220, 120));
		DrawDiagnosticText(graphics, $"REGISTRATION ERROR: {registrationError}", 334, Color.White);
		DrawDiagnosticText(graphics, messageStatus, 356, Color.FromArgb(255, 220, 120));
		DrawDiagnosticText(graphics, $"WM_INPUT: {wmInputCount}", 378, Color.White);
		DrawDiagnosticText(graphics, $"MOUSE PACKETS: {rawMouseInput?.MousePacketCount ?? 0}", 404, Color.White);
		DrawDiagnosticText(graphics, $"LAST DEVICE: {device}", 426, Color.White);
		DrawDiagnosticText(graphics, $"LAST EVENT: {lastRawInputType.ToUpperInvariant()}", 448, Color.White);
		DrawDiagnosticText(graphics, $"LAST FLAGS: {flags}", 470, Color.White);
		DrawDiagnosticText(graphics, $"LEFT ARM DEVICE: {leftAssignment?.DeviceHandle.ToString() ?? "NONE"}", 492, Color.FromArgb(96, 239, 228));
		DrawDiagnosticText(graphics, $"RIGHT ARM DEVICE: {rightAssignment?.DeviceHandle.ToString() ?? "NONE"}", 514, Color.FromArgb(255, 184, 92));
		if (rawMouseInput is not null && wmInputCount == 0)
		{
			DrawDiagnosticText(graphics, "WAITING FOR RAW INPUT...", 536, Color.FromArgb(255, 220, 120));
		}
	}

	private void DrawDiagnosticText(Graphics graphics, string text, float y, Color color)
	{
		using var brush = new SolidBrush(color);
		graphics.DrawString(text, detailFont, brush, 70, y);
	}

	private void DrawCrosshair(Graphics graphics, PointF center, Color color, string label)
	{
		using var pen = new Pen(color, 2);
		const float radius = 22;
		graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
		graphics.DrawLine(pen, center.X - radius - 10, center.Y, center.X + radius + 10, center.Y);
		graphics.DrawLine(pen, center.X, center.Y - radius - 10, center.X, center.Y + radius + 10);
		using var brush = new SolidBrush(color);
		using var format = new StringFormat { Alignment = StringAlignment.Center };
		graphics.DrawString(label, detailFont, brush, center.X, center.Y + radius + 14, format);
	}

	private void DrawPlayerStatus(Graphics graphics)
	{
		const float barWidth = 224f;
		const float barHeight = 13f;
		float left = 90f;
		float top = ClientSize.Height - 56f;

		using var labelBrush = new SolidBrush(Color.FromArgb(142, 174, 188));
		graphics.DrawString("SUIT INTEGRITY", detailFont, labelBrush, left, top - 22f);

		using var backdropBrush = new SolidBrush(Color.FromArgb(180, 8, 16, 22));
		graphics.FillRectangle(backdropBrush, left, top, barWidth, barHeight);

		float fraction = Math.Clamp(playerHealth / PlayerMaxHealth, 0f, 1f);
		Color fill = fraction > 0.5f
			? Color.FromArgb(96, 239, 228)
			: fraction > 0.25f
				? Color.FromArgb(246, 198, 92)
				: Color.FromArgb(236, 92, 88);

		using var fillBrush = new SolidBrush(fill);
		graphics.FillRectangle(fillBrush, left, top, barWidth * fraction, barHeight);

		using var framePen = new Pen(Color.FromArgb(120, 168, 196, 208), 1f);
		graphics.DrawRectangle(framePen, left, top, barWidth, barHeight);

		using var valueBrush = new SolidBrush(Color.FromArgb(205, 225, 239, 247));
		graphics.DrawString($"{(int)MathF.Ceiling(playerHealth)} / {(int)PlayerMaxHealth}", detailFont, valueBrush, left + barWidth + 12f, top - 3f);
		graphics.DrawString($"SCORE: {score:D6}", detailFont, labelBrush, ClientSize.Width - 235f, 128f);
	}

	private void DrawGameOver(Graphics graphics)
	{
		using var scrimBrush = new SolidBrush(Color.FromArgb(198, 6, 10, 16));
		graphics.FillRectangle(scrimBrush, ClientRectangle);
		DrawCenteredText(graphics, "GAME OVER", titleFont, (ClientSize.Height * 0.5f) - 96f, Color.FromArgb(236, 92, 88));
		DrawCenteredText(graphics, $"FINAL SCORE: {score:D6}", instructionFont, (ClientSize.Height * 0.5f) - 20f, Color.FromArgb(225, 239, 247));
		DrawCenteredText(graphics, "PRESS R OR CLICK EITHER MOUSE TO REDEPLOY", detailFont, (ClientSize.Height * 0.5f) + 36f, Color.FromArgb(170, 198, 208));
	}

	/// <summary>
	/// A small chevron at the screen edge when the exosuit is out of view, so the
	/// player can find it again after turning. Derived from the camera-space
	/// direction, so it stays correct even when the target is directly behind.
	/// </summary>
	private void DrawOffscreenEnemyMarker(Graphics graphics, CameraView view)
	{
		if (enemy is null || !enemy.IsAlive)
		{
			return;
		}

		Vector3 viewPosition = view.ToView(enemy.ChestPosition);
		bool visible = viewPosition.Z >= CameraView.NearPlane;

		if (visible)
		{
			PointF onScreen = ProjectWorldToScreen(enemy.ChestPosition);
			if (onScreen.X > 20f && onScreen.X < ClientSize.Width - 20f && onScreen.Y > 120f && onScreen.Y < ClientSize.Height - 20f)
			{
				return;
			}
		}

		float directionX = viewPosition.X;
		float directionY = -viewPosition.Y;
		float length = MathF.Sqrt((directionX * directionX) + (directionY * directionY));
		if (length < 0.0001f)
		{
			directionX = 1f;
			directionY = 0f;
		}
		else
		{
			directionX /= length;
			directionY /= length;
		}

		float centerX = ClientSize.Width * 0.5f;
		float centerY = ClientSize.Height * 0.5f;
		float radius = MathF.Min(ClientSize.Width, ClientSize.Height) * 0.34f;
		float markerX = centerX + (directionX * radius);
		float markerY = centerY + (directionY * radius);
		float normalX = -directionY;
		float normalY = directionX;

		PointF[] chevron =
		{
			new(markerX + (directionX * 13f), markerY + (directionY * 13f)),
			new(markerX - (directionX * 6f) + (normalX * 9f), markerY - (directionY * 6f) + (normalY * 9f)),
			new(markerX - (directionX * 6f) - (normalX * 9f), markerY - (directionY * 6f) - (normalY * 9f))
		};

		using var markerBrush = new SolidBrush(Color.FromArgb(150, 236, 92, 88));
		graphics.FillPolygon(markerBrush, chevron);
	}

	private void DrawCenteredText(Graphics graphics, string text, Font font, float y, Color color)
	{
		using var brush = new SolidBrush(color);
		using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
		graphics.DrawString(text, font, brush, new RectangleF(32, y, ClientSize.Width - 64, 56), format);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			rawMouseInput?.Dispose();
			fireIndicatorTimer.Dispose();
			combatTimer.Stop();
			combatTimer.Dispose();
			titleFont.Dispose();
			instructionFont.Dispose();
			detailFont.Dispose();
		}

		base.Dispose(disposing);
	}
}
