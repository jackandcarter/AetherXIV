using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Aether.Umbra.Framework;

internal static class UmbraPluginLocationPicker
{
    public static Task<string?> PickAsync(bool folder)
    {
        TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            int initialized = OleInitialize(0);
            try
            {
                if (initialized < 0) Marshal.ThrowExceptionForHR(initialized);
                completion.SetResult(folder ? PickFolder() : PickFile());
            }
            catch (Exception ex) { completion.SetException(ex); }
            finally { if (initialized >= 0) OleUninitialize(); }
        }) { IsBackground = true, Name = "Umbra plugin picker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static string? PickFile()
    {
        OpenFileName dialog = new()
        {
            Size = Marshal.SizeOf<OpenFileName>(),
            Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle,
            Filter = "Umbra plugins (*.zip;*.dll;*.json)\0*.zip;*.dll;*.json\0All files\0*.*\0\0",
            MaxFile = 32768,
            Title = "Add local plugins to Discover",
            Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x02000000
        };
        dialog.File = Marshal.AllocHGlobal(dialog.MaxFile * sizeof(char));
        try
        {
            Marshal.WriteInt16(dialog.File, 0);
            if (GetOpenFileNameW(dialog)) return Marshal.PtrToStringUni(dialog.File);
            uint error = CommDlgExtendedError();
            if (error != 0) throw new Win32Exception((int)error, "The plugin file picker could not open.");
            return null;
        }
        finally { Marshal.FreeHGlobal(dialog.File); }
    }

    private static string? PickFolder()
    {
        BrowseInfo info = new()
        {
            Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle,
            Title = "Select a plugin build folder or a folder containing plugin packages",
            Flags = 0x40 | 0x01
        };
        info.DisplayName = Marshal.AllocCoTaskMem(520);
        nint item;
        try { item = SHBrowseForFolderW(ref info); }
        finally { Marshal.FreeCoTaskMem(info.DisplayName); }
        if (item == 0) return null;
        try
        {
            StringBuilder path = new(32768);
            if (!SHGetPathFromIDListEx(item, path, (uint)path.Capacity, 0))
                throw new IOException("The selected folder is not a filesystem location.");
            return path.ToString();
        }
        finally { Marshal.FreeCoTaskMem(item); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal sealed class OpenFileName
    {
        public int Size;
        public nint Owner, Instance;
        public string? Filter, CustomFilter;
        public int MaxCustomFilter, FilterIndex = 1;
        public nint File;
        public int MaxFile;
        public nint FileTitle;
        public int MaxFileTitle;
        public string? InitialDirectory, Title;
        public uint Flags;
        public ushort FileOffset, FileExtension;
        public string? DefaultExtension;
        public nint Data, Hook, Template, Reserved;
        public uint ReservedValue, FlagsEx;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BrowseInfo
    {
        public nint Owner, Root, DisplayName;
        [MarshalAs(UnmanagedType.LPWStr)] public string Title;
        public uint Flags;
        public nint Callback, Data;
        public int Image;
    }
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileNameW([In, Out] OpenFileName dialog);
    [DllImport("comdlg32.dll")] private static extern uint CommDlgExtendedError();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint SHBrowseForFolderW(ref BrowseInfo info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SHGetPathFromIDListEx(nint item, StringBuilder path, uint length, uint flags);
}
