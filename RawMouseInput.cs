using System.Runtime.InteropServices;
using System.Text;

namespace LoopGame;

internal sealed class RawMouseInput : NativeWindow, IDisposable
{
	private const uint RidInput = 0x10000003;
	private const uint RidevInputSink = 0x00000100;
	private const uint RidiDevicename = 0x20000007;
	private const uint RimTypeMouse = 0;
	private const int WmInput = 0x00ff;
	internal const ushort LeftButtonDown = 0x0001;
	internal const ushort RightButtonDown = 0x0004;

	private bool disposed;

	public event EventHandler<RawMouseMovement>? MouseMoved;

	public RawMouseInput(IntPtr windowHandle)
	{
		AssignHandle(windowHandle);

		var devices = new RawInputDevice[]
		{
			new()
			{
				UsagePage = 0x01,
				Usage = 0x02,
				Flags = RidevInputSink,
				Target = windowHandle
			}
		};

		if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
		{
			throw new InvalidOperationException($"Unable to register for raw mouse input. Win32 error: {Marshal.GetLastWin32Error()}.");
		}
	}

	protected override void WndProc(ref Message message)
	{
		if (message.Msg == WmInput)
		{
			ProcessInput(message.LParam);
		}

		base.WndProc(ref message);
	}

	private void ProcessInput(IntPtr inputHandle)
	{
		uint dataSize = 0;
		GetRawInputData(inputHandle, RidInput, IntPtr.Zero, ref dataSize, (uint)Marshal.SizeOf<RawInputHeader>());

		if (dataSize == 0)
		{
			return;
		}

		IntPtr buffer = Marshal.AllocHGlobal((int)dataSize);
		try
		{
			if (GetRawInputData(inputHandle, RidInput, buffer, ref dataSize, (uint)Marshal.SizeOf<RawInputHeader>()) == uint.MaxValue)
			{
				return;
			}

			int headerSize = IntPtr.Size == 8 ? 24 : 16;
			if (dataSize < headerSize + 20 || Marshal.ReadInt32(buffer) != RimTypeMouse)
			{
				return;
			}

			IntPtr deviceHandle = Marshal.ReadIntPtr(buffer, 8);
			int mouseOffset = headerSize;
			ushort buttonFlags = (ushort)Marshal.ReadInt16(buffer, mouseOffset + 4);
			int deltaX = Marshal.ReadInt32(buffer, mouseOffset + 12);
			int deltaY = Marshal.ReadInt32(buffer, mouseOffset + 16);

			if (deltaX == 0 && deltaY == 0 && buttonFlags == 0)
			{
				return;
			}

			MouseMoved?.Invoke(this, new RawMouseMovement(deviceHandle, GetDeviceName(deviceHandle), deltaX, deltaY, buttonFlags));
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}
	}

	private static string GetDeviceName(IntPtr deviceHandle)
	{
		uint characterCount = 0;
		GetRawInputDeviceInfo(deviceHandle, RidiDevicename, IntPtr.Zero, ref characterCount);

		if (characterCount == 0)
		{
			return $"Raw Input device {deviceHandle}";
		}

		var name = new StringBuilder((int)characterCount);
		uint result = GetRawInputDeviceInfo(deviceHandle, RidiDevicename, name, ref characterCount);
		return result == uint.MaxValue ? $"Raw Input device {deviceHandle}" : name.ToString();
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		ReleaseHandle();
		GC.SuppressFinalize(this);
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterRawInputDevices(
		[In] RawInputDevice[] devices,
		uint deviceCount,
		uint deviceSize);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint GetRawInputData(
		IntPtr rawInput,
		uint command,
		IntPtr data,
		ref uint size,
		uint headerSize);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern uint GetRawInputDeviceInfo(
		IntPtr device,
		uint command,
		IntPtr data,
		ref uint size);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern uint GetRawInputDeviceInfo(
		IntPtr device,
		uint command,
		StringBuilder data,
		ref uint size);

	[StructLayout(LayoutKind.Sequential)]
	private struct RawInputDevice
	{
		public ushort UsagePage;
		public ushort Usage;
		public uint Flags;
		public IntPtr Target;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RawInputHeader
	{
		public uint Type;
		public uint Size;
		public IntPtr Device;
		public IntPtr Param;
	}
}
