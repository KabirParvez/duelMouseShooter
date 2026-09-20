using System.Runtime.InteropServices;
using System.Text;

namespace LoopGame;

internal sealed class RawMouseInput : IDisposable
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
	public int WindowMessageCount { get; private set; }
	public int MousePacketCount { get; private set; }
	public string RegistrationStatus { get; }

	public RawMouseInput(IntPtr windowHandle)
	{
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
			RegistrationStatus = $"FAILED ({Marshal.GetLastWin32Error()})";
			return;
		}

		RegistrationStatus = $"REGISTERED HWND {windowHandle}";
	}

	public void ProcessWindowMessage(IntPtr inputHandle)
	{
		WindowMessageCount++;
		ProcessInput(inputHandle);
	}

	public bool HandlesMessage(int message)
	{
		return message == WmInput;
	}

	private void ProcessInput(IntPtr inputHandle)
	{
		uint dataSize = 0;
		uint queryResult = GetRawInputData(inputHandle, RidInput, IntPtr.Zero, ref dataSize, (uint)Marshal.SizeOf<RawInputHeader>());
		if (queryResult == uint.MaxValue || dataSize < (uint)Marshal.SizeOf<RawInputHeader>())
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

			int headerSize = Marshal.SizeOf<RawInputHeader>();
			if (dataSize < headerSize + Marshal.SizeOf<RawMouse>() || Marshal.ReadInt32(buffer) != RimTypeMouse)
			{
				return;
			}

			RawInputHeader header = Marshal.PtrToStructure<RawInputHeader>(buffer);
			RawMouse mouse = Marshal.PtrToStructure<RawMouse>(IntPtr.Add(buffer, headerSize));
			IntPtr deviceHandle = header.Device;
			ushort buttonFlags = mouse.ButtonFlags;
			int deltaX = mouse.LastX;
			int deltaY = mouse.LastY;

			MousePacketCount++;
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

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct RawInputDevice
	{
		public ushort UsagePage;
		public ushort Usage;
		public uint Flags;
		public IntPtr Target;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct RawInputHeader
	{
		public uint Type;
		public uint Size;
		public IntPtr Device;
		public IntPtr Param;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct RawMouse
	{
		public ushort Flags;
		public ushort ButtonFlags;
		public ushort ButtonData;
		public uint RawButtons;
		public int LastX;
		public int LastY;
		public uint ExtraInformation;
	}
}
