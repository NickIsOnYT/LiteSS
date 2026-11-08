using System.Runtime.InteropServices;
using System.Text;

namespace LiteSS;

/// <summary>Encrypts small secrets (API keys) with Windows DPAPI for the current user.</summary>
internal static class SecretProtector
{
    private const int CryptProtectUiForbidden = 0x1;

    public static string Protect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        try
        {
            return Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(value), protect: true));
        }
        catch (Exception)
        {
            // Never let a protection failure stop the app from saving settings.
            return string.Empty;
        }
    }

    public static string Unprotect(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return string.Empty;
        }

        try
        {
            var bytes = Convert.FromBase64String(protectedValue);
            return Encoding.UTF8.GetString(Transform(bytes, protect: false));
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static byte[] Transform(byte[] data, bool protect)
    {
        var input = new DataBlob();
        var output = new DataBlob();

        try
        {
            input.Data = Marshal.AllocHGlobal(data.Length);
            input.Length = data.Length;
            Marshal.Copy(data, 0, input.Data, data.Length);

            var ok = protect
                ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output);

            if (!ok || output.Data == IntPtr.Zero || output.Length <= 0)
            {
                throw new InvalidOperationException("DPAPI failed.");
            }

            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (input.Data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(input.Data);
            }

            if (output.Data != IntPtr.Zero)
            {
                LocalFree(output.Data);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Length;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
