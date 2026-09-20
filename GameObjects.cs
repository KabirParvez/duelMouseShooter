namespace LoopGame;

internal enum AssignmentState
{
	WaitingForLeft,
	WaitingForRight,
	BothHandsReady
}

internal sealed record MouseAssignment(IntPtr DeviceHandle, string DeviceName);

internal sealed record RawMouseMovement(
	IntPtr DeviceHandle,
	string DeviceName,
	int DeltaX,
	int DeltaY,
	ushort ButtonFlags);
