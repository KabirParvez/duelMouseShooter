using System.Drawing.Drawing2D;

namespace LoopGame;

internal sealed class GameForm : Form
{
	private readonly Font titleFont = new("Segoe UI", 30, FontStyle.Bold);
	private readonly Font instructionFont = new("Segoe UI", 15, FontStyle.Regular);
	private readonly Font detailFont = new("Consolas", 11, FontStyle.Regular);
	private readonly System.Windows.Forms.Timer fireIndicatorTimer;
	private readonly System.Windows.Forms.Timer combatTimer;
	private readonly List<ProjectileTracer> tracers = new();
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
	private static readonly PointF LeftWeaponMount = new(145, 570);
	private static readonly PointF RightWeaponMount = new(815, 570);
	private const ushort LeftButtonUp = 0x0002;
	private const ushort RightButtonUp = 0x0008;

	public GameForm()
	{
		Text = "Loop Game";
		ClientSize = new Size(960, 650);
		MinimumSize = new Size(700, 600);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(9, 15, 24);
		ForeColor = Color.FromArgb(225, 239, 247);
		DoubleBuffered = true;
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
		combatTimer.Start();
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

		if (movement.DeviceHandle == leftAssignment?.DeviceHandle)
		{
			leftCrosshair = MoveCrosshair(leftCrosshair, movement.DeltaX, movement.DeltaY);
			if ((movement.ButtonFlags & RawMouseInput.RightButtonDown) != 0)
			{
				leftFireHeld = true;
				TryFireLeftWeapon();
				ShowFireIndicator("LEFT FIRE");
			}
			if ((movement.ButtonFlags & RightButtonUp) != 0)
			{
				leftFireHeld = false;
			}
		}
		else if (movement.DeviceHandle == rightAssignment?.DeviceHandle)
		{
			rightCrosshair = MoveCrosshair(rightCrosshair, movement.DeltaX, movement.DeltaY);
			if ((movement.ButtonFlags & RawMouseInput.LeftButtonDown) != 0)
			{
				rightFireHeld = true;
			TryFireRightWeapon();
				ShowFireIndicator("RIGHT FIRE");
			}
			if ((movement.ButtonFlags & LeftButtonUp) != 0)
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
		if (assignmentState != AssignmentState.BothHandsReady)
		{
			return;
		}

		const float elapsedSeconds = 0.016f;
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

		for (int index = tracers.Count - 1; index >= 0; index--)
		{
			if (!tracers[index].Update(elapsedSeconds))
			{
				tracers.RemoveAt(index);
			}
		}

		Invalidate();
	}

	private void TryFireLeftWeapon()
	{
		if (leftFireCooldown > 0f)
		{
			return;
		}

		FireWeapon(LeftWeaponMount, leftCrosshair, Color.FromArgb(82, 226, 255));
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

		FireWeapon(RightWeaponMount, rightCrosshair, Color.FromArgb(255, 184, 92));
		rightFireCooldown = 0.18f;
		rightRecoil = 12f;
		rightMuzzleFlash = 0.08f;
		ShowFireIndicator("RIGHT FIRE");
	}

	private void FireWeapon(PointF mount, PointF aim, Color color)
	{
		PointF direction = GetDirection(mount, aim);
		PointF muzzle = Add(mount, Multiply(direction, 118f));
		PointF endpoint = Add(mount, Multiply(direction, 900f));
		tracers.Add(new ProjectileTracer(muzzle, endpoint, color));
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
			DrawFirstPersonBattlefield(graphics);
			DrawCenteredText(graphics, "BOTH HANDS READY", instructionFont, 28, Color.FromArgb(96, 239, 228));
			DrawCenteredText(graphics, $"LEFT ARM: {leftAssignment?.DeviceName}    RIGHT ARM: {rightAssignment?.DeviceName}", detailFont, 78, Color.FromArgb(190, 210, 218));
			DrawWeapon(graphics, LeftWeaponMount, leftCrosshair, Color.FromArgb(82, 226, 255), "LEFT WEAPON", leftRecoil, leftMuzzleFlash);
			DrawWeapon(graphics, RightWeaponMount, rightCrosshair, Color.FromArgb(255, 184, 92), "RIGHT WEAPON", rightRecoil, rightMuzzleFlash);
			DrawTracers(graphics);
			DrawCrosshair(graphics, leftCrosshair, Color.FromArgb(96, 239, 228), "LEFT");
			DrawCrosshair(graphics, rightCrosshair, Color.FromArgb(255, 184, 92), "RIGHT");
			using var readyBrush = new SolidBrush(Color.FromArgb(142, 174, 188));
			graphics.DrawString("LEFT ARM: READY", detailFont, readyBrush, 90, 106);
			graphics.DrawString("RIGHT ARM: READY", detailFont, readyBrush, ClientSize.Width - 235, 106);
			if (fireIndicator is not null)
			{
				DrawCenteredText(graphics, fireIndicator, instructionFont, ClientSize.Height - 72, Color.FromArgb(255, 220, 120));
			}
		}

		if (assignmentState != AssignmentState.BothHandsReady)
		{
			DrawRawInputDiagnostic(graphics);
		}
	}

	private void DrawFirstPersonBattlefield(Graphics graphics)
	{
		float horizon = ClientSize.Height * 0.43f;
		using var horizonPen = new Pen(Color.FromArgb(59, 151, 157), 2);
		graphics.DrawLine(horizonPen, 0, horizon, ClientSize.Width, horizon);
		using var floorBrush = new SolidBrush(Color.FromArgb(8, 22, 29));
		graphics.FillPolygon(floorBrush, new[]
		{
			new PointF(0, horizon), new PointF(ClientSize.Width, horizon),
			new PointF(ClientSize.Width, ClientSize.Height), new PointF(0, ClientSize.Height)
		});

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
			graphics.DrawLine(gridPen, Project(index * 1.5f, 0f, 2.5f), Project(index * 1.5f, 0f, 38f));
		}

		foreach (float depth in new[] { 3f, 4f, 5.5f, 7.5f, 10f, 14f, 19f, 26f, 35f })
		{
			graphics.DrawLine(gridPen, Project(-18f, 0f, depth), Project(18f, 0f, depth));
		}

		using var horizonPen = new Pen(Color.FromArgb(59, 151, 157), 2);
		graphics.DrawLine(horizonPen, 0, horizon, ClientSize.Width, horizon);
	}

	private void DrawStructure(Graphics graphics, float x, float halfWidth, float height, float depth, Color color)
	{
		PointF frontTopLeft = Project(x - halfWidth, height, depth - 0.6f);
		PointF frontTopRight = Project(x + halfWidth, height, depth - 0.6f);
		PointF frontBottomLeft = Project(x - halfWidth, 0f, depth - 0.6f);
		PointF frontBottomRight = Project(x + halfWidth, 0f, depth - 0.6f);
		PointF backTopRight = Project(x + halfWidth, height, depth + 0.6f);
		PointF backBottomRight = Project(x + halfWidth, 0f, depth + 0.6f);
		using var frontBrush = new SolidBrush(color);
		using var sideBrush = new SolidBrush(Color.FromArgb(Math.Max(10, color.R - 10), Math.Max(15, color.G - 12), Math.Max(20, color.B - 12)));
		using var edgePen = new Pen(Color.FromArgb(86, 177, 181), 1);
		graphics.FillPolygon(frontBrush, new[] { frontTopLeft, frontTopRight, frontBottomRight, frontBottomLeft });
		graphics.FillPolygon(sideBrush, new[] { frontTopRight, backTopRight, backBottomRight, frontBottomRight });
		graphics.DrawPolygon(edgePen, new[] { frontTopLeft, frontTopRight, frontBottomRight, frontBottomLeft });
		graphics.DrawLine(edgePen, frontTopRight, backTopRight);
	}

	private void DrawPerspectiveRail(Graphics graphics, float x, float y, float depth, Color color)
	{
		using var railPen = new Pen(color, 3);
		graphics.DrawLine(railPen, Project(x - 0.35f, y, 2.5f), Project(x - 0.35f, y, depth));
		graphics.DrawLine(railPen, Project(x + 0.35f, y, 2.5f), Project(x + 0.35f, y, depth));
		for (float z = 4f; z < depth; z += 4f)
		{
			graphics.DrawLine(railPen, Project(x - 0.35f, y, z), Project(x + 0.35f, y, z));
		}
	}

	private PointF Project(float x, float y, float z)
	{
		const float cameraHeight = 1.65f;
		const float focalLength = 470f;
		float safeDepth = MathF.Max(0.25f, z);
		return new PointF(
			(ClientSize.Width * 0.5f) + (x * focalLength / safeDepth),
			(ClientSize.Height * 0.43f) - ((y - cameraHeight) * focalLength / safeDepth));
	}

	private void DrawWeapon(Graphics graphics, PointF mount, PointF aim, Color accent, string label, float recoil, float muzzleFlash)
	{
		PointF direction = GetDirection(mount, aim);
		PointF normal = new(-direction.Y, direction.X);
		PointF basePoint = Add(mount, Multiply(direction, -recoil));
		PointF shoulder = Add(basePoint, Multiply(direction, -58f));
		PointF bodyCenter = Add(basePoint, Multiply(direction, 48f));
		PointF bodyFront = Add(bodyCenter, Multiply(direction, 34f));
		PointF bodyBack = Add(bodyCenter, Multiply(direction, -34f));

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

		PointF barrelStart = Add(basePoint, Multiply(direction, 62f));
		PointF muzzle = Add(basePoint, Multiply(direction, 118f));
		using var barrelPen = new Pen(Color.FromArgb(18, 24, 31), 14);
		using var barrelAccentPen = new Pen(accent, 4);
		graphics.DrawLine(barrelPen, barrelStart, muzzle);
		graphics.DrawLine(barrelAccentPen, barrelStart, muzzle);
		graphics.DrawLine(bodyPen, Add(bodyCenter, Multiply(normal, 11f)), Add(bodyCenter, Multiply(normal, -11f)));

		using var labelBrush = new SolidBrush(Color.FromArgb(170, accent.R, accent.G, accent.B));
		using var labelFormat = new StringFormat { Alignment = StringAlignment.Center };
		graphics.DrawString(label, detailFont, labelBrush, bodyCenter.X, bodyCenter.Y + 28, labelFormat);

		if (muzzleFlash > 0f)
		{
			float flashSize = 20f + (muzzleFlash * 90f);
			PointF flashTip = Add(muzzle, Multiply(direction, flashSize));
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
		foreach (ProjectileTracer tracer in tracers)
		{
			using var glowPen = new Pen(Color.FromArgb(70, tracer.Color.R, tracer.Color.G, tracer.Color.B), 9);
			using var tracerPen = new Pen(tracer.Color, 3);
			PointF tail = tracer.GetTailPosition();
			PointF position = tracer.GetPosition();
			graphics.DrawLine(glowPen, tail, position);
			graphics.DrawLine(tracerPen, tail, position);
		}
	}

	private static PointF GetDirection(PointF from, PointF to)
	{
		float x = to.X - from.X;
		float y = to.Y - from.Y;
		float length = MathF.Sqrt((x * x) + (y * y));
		return length < 0.001f ? new PointF(0, -1) : new PointF(x / length, y / length);
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
