// Ricezione NDI tramite la libreria ufficiale (Processing.NDI.Lib.x64.dll).
// La DLL puo' essere incorporata nell'exe come risorsa: all'avvio viene estratta in
// %LOCALAPPDATA%\LiveCount e caricata, senza installare nulla.
// NDI(R) is a registered trademark of Vizrt NDI AB - https://ndi.video
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace LiveCount
{
    static class NdiNative
    {
        const string Lib = NdiLoader.DllName;

        [StructLayout(LayoutKind.Sequential)]
        public struct Source { public IntPtr p_ndi_name; public IntPtr p_url_address; }

        [StructLayout(LayoutKind.Sequential)]
        public struct FindCreate
        {
            [MarshalAs(UnmanagedType.U1)] public bool show_local_sources;
            public IntPtr p_groups;
            public IntPtr p_extra_ips;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RecvCreateV3
        {
            public Source source_to_connect_to;
            public int color_format;
            public int bandwidth;
            [MarshalAs(UnmanagedType.U1)] public bool allow_video_fields;
            public IntPtr p_ndi_recv_name;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VideoFrameV2
        {
            public int xres, yres, FourCC, frame_rate_N, frame_rate_D;
            public float picture_aspect_ratio;
            public int frame_format_type;
            public long timecode;
            public IntPtr p_data;
            public int line_stride_in_bytes;
            public IntPtr p_metadata;
            public long timestamp;
        }

        public const int ColorFormat_BGRX_BGRA = 0;
        public const int Bandwidth_Highest = 100;
        public const int FrameType_Video = 1;
        public const int FrameType_Error = 4;

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool NDIlib_initialize();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_find_create_v2(ref FindCreate settings);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_find_destroy(IntPtr find);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_find_get_current_sources(IntPtr find, out uint count);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NDIlib_recv_create_v3(ref RecvCreateV3 settings);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_destroy(IntPtr recv);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int NDIlib_recv_capture_v2(IntPtr recv, ref VideoFrameV2 video, IntPtr audio, IntPtr metadata, uint timeoutMs);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void NDIlib_recv_free_video_v2(IntPtr recv, ref VideoFrameV2 video);
    }

    static class NdiLoader
    {
        public const string DllName = "Processing.NDI.Lib.x64.dll";
        const string ResourceName = "ndi.x64.dll";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibrary(string path);

        public static bool Loaded;
        public static string Origin = "";
        public static string Error = "";

        public static bool TryLoad()
        {
            if (Loaded) return true;
            if (IntPtr.Size != 8) { Error = "NDI richiede l'esecuzione a 64 bit."; return false; }

            var candidates = new List<KeyValuePair<string, string>>();
            string embedded = ExtractEmbedded();
            if (embedded != null) candidates.Add(new KeyValuePair<string, string>("integrata nell'exe", embedded));
            candidates.Add(new KeyValuePair<string, string>("cartella dell'exe", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllName)));
            foreach (var v in new[] { "NDI_RUNTIME_DIR_V6", "NDI_RUNTIME_DIR_V5" })
            {
                foreach (var dir in new[] { Environment.GetEnvironmentVariable(v), Environment.GetEnvironmentVariable(v, EnvironmentVariableTarget.Machine) })
                    if (!string.IsNullOrEmpty(dir))
                        candidates.Add(new KeyValuePair<string, string>("runtime NDI installato", Path.Combine(dir, DllName)));
            }

            foreach (var c in candidates)
            {
                if (!File.Exists(c.Value) || LoadLibrary(c.Value) == IntPtr.Zero) continue;
                try
                {
                    if (!NdiNative.NDIlib_initialize()) { Error = "NDI non supportato da questa CPU."; return false; }
                }
                catch (Exception ex) { Error = "Errore NDI: " + ex.Message; return false; }
                Loaded = true;
                Origin = c.Key;
                return true;
            }
            Error = "Libreria NDI non trovata.";
            return false;
        }

        static string ExtractEmbedded()
        {
            try
            {
                using (var rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                {
                    if (rs == null) return null;
                    var data = new byte[rs.Length];
                    int read = 0;
                    while (read < data.Length)
                    {
                        int n = rs.Read(data, read, data.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    string hash;
                    using (var sha = SHA1.Create())
                        hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").Substring(0, 12);
                    // Cartella per versione: exe diversi con DLL diverse non si pestano i piedi.
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiveCount", "ndi-" + hash);
                    string path = Path.Combine(dir, DllName);
                    if (!File.Exists(path) || new FileInfo(path).Length != data.Length)
                    {
                        Directory.CreateDirectory(dir);
                        string tmp = path + "." + System.Diagnostics.Process.GetCurrentProcess().Id + ".tmp";
                        File.WriteAllBytes(tmp, data);
                        try { if (File.Exists(path)) File.Delete(path); File.Move(tmp, path); }
                        catch { try { File.Delete(tmp); } catch { } }
                    }
                    return File.Exists(path) ? path : null;
                }
            }
            catch { return null; }
        }
    }

    // Riceve una sorgente NDI su un thread dedicato.
    public class NdiReceiver : VideoSource
    {
        readonly IntPtr _find;

        public NdiReceiver()
        {
            var fc = new NdiNative.FindCreate { show_local_sources = true };
            _find = NdiNative.NDIlib_find_create_v2(ref fc);
        }

        public override string Kind { get { return "NDI"; } }

        public List<string> GetSources()
        {
            var list = new List<string>();
            if (_find == IntPtr.Zero) return list;
            uint n;
            IntPtr p = NdiNative.NDIlib_find_get_current_sources(_find, out n);
            int size = Marshal.SizeOf(typeof(NdiNative.Source));
            for (int i = 0; i < n && p != IntPtr.Zero; i++)
            {
                var src = (NdiNative.Source)Marshal.PtrToStructure(new IntPtr(p.ToInt64() + i * size), typeof(NdiNative.Source));
                string name = FromUtf8(src.p_ndi_name);
                if (!string.IsNullOrEmpty(name)) list.Add(name);
            }
            return list;
        }

        protected override void Run(string name)
        {
            IntPtr pName = ToUtf8(name), pRecvName = ToUtf8("LiveCount");
            var rc = new NdiNative.RecvCreateV3
            {
                source_to_connect_to = new NdiNative.Source { p_ndi_name = pName },
                color_format = NdiNative.ColorFormat_BGRX_BGRA,
                bandwidth = NdiNative.Bandwidth_Highest,
                allow_video_fields = false,
                p_ndi_recv_name = pRecvName
            };
            IntPtr recv = IntPtr.Zero;
            try
            {
                recv = NdiNative.NDIlib_recv_create_v3(ref rc);
                if (recv == IntPtr.Zero) return;
                while (!Stopping)
                {
                    var vf = new NdiNative.VideoFrameV2();
                    int type = NdiNative.NDIlib_recv_capture_v2(recv, ref vf, IntPtr.Zero, IntPtr.Zero, 200);
                    if (type == NdiNative.FrameType_Video)
                    {
                        try
                        {
                            float aspect = vf.picture_aspect_ratio > 0 ? vf.picture_aspect_ratio : 0;
                            double fps = vf.frame_rate_D > 0 ? (double)vf.frame_rate_N / vf.frame_rate_D : 0;
                            int stride = vf.line_stride_in_bytes > 0 ? vf.line_stride_in_bytes : vf.xres * 4;
                            PublishFrame(vf.p_data, vf.xres, vf.yres, stride, aspect, fps);
                        }
                        finally { NdiNative.NDIlib_recv_free_video_v2(recv, ref vf); }
                    }
                    else if (type == NdiNative.FrameType_Error) Thread.Sleep(100);
                }
            }
            catch { }
            finally
            {
                if (recv != IntPtr.Zero) NdiNative.NDIlib_recv_destroy(recv);
                Marshal.FreeHGlobal(pName);
                Marshal.FreeHGlobal(pRecvName);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_find != IntPtr.Zero) NdiNative.NDIlib_find_destroy(_find);
        }

        static IntPtr ToUtf8(string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s ?? "");
            IntPtr p = Marshal.AllocHGlobal(b.Length + 1);
            Marshal.Copy(b, 0, p, b.Length);
            Marshal.WriteByte(p, b.Length, 0);
            return p;
        }

        static string FromUtf8(IntPtr p)
        {
            if (p == IntPtr.Zero) return null;
            int len = 0;
            while (Marshal.ReadByte(p, len) != 0) len++;
            var b = new byte[len];
            Marshal.Copy(p, b, 0, len);
            return Encoding.UTF8.GetString(b);
        }
    }
}
