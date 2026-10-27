// Acquisizione da schede USB (Magewell USB Capture, AJA U-TAP, webcam...) tramite
// Media Foundation di Windows: i dispositivi UVC funzionano senza driver aggiuntivi.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace LiveCount
{
    static class MF
    {
        public static readonly Guid DevSourceType = new Guid("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3");
        public static readonly Guid DevSourceTypeVidcap = new Guid("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");
        public static readonly Guid DevFriendlyName = new Guid("60d0e559-52f8-4fa2-bbce-acdb34a8ec01");
        public static readonly Guid MT_MajorType = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        public static readonly Guid MT_Subtype = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        public static readonly Guid MT_FrameSize = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
        public static readonly Guid MT_FrameRate = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
        public static readonly Guid MT_DefaultStride = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
        public static readonly Guid MediaType_Video = new Guid("73646976-0000-0010-8000-00aa00389b71");
        public static readonly Guid VideoFormat_RGB32 = new Guid("00000016-0000-0010-8000-00aa00389b71");
        public static readonly Guid VideoFormat_MJPG = new Guid("47504a4d-0000-0010-8000-00aa00389b71");
        public static readonly Guid VideoFormat_H264 = new Guid("34363248-0000-0010-8000-00aa00389b71");
        public static readonly Guid ReaderAdvancedVideoProcessing = new Guid("0f81da2c-b537-4672-a8b2-a681b17307a3");
        public static readonly Guid IID_IMFMediaSource = new Guid("279a808d-aec7-40c8-9c6b-a6b492c78a66");

        public const int FirstVideoStream = unchecked((int)0xFFFFFFFC);
        public const int AllStreams = unchecked((int)0xFFFFFFFE);
        public const int ReaderFlagError = 0x1, ReaderFlagEndOfStream = 0x2;

        [DllImport("mfplat.dll")] public static extern int MFStartup(int version, int flags);
        [DllImport("mfplat.dll")] public static extern int MFCreateAttributes(out IMFAttributes attributes, int initialSize);
        [DllImport("mfplat.dll")] public static extern int MFCreateMediaType([MarshalAs(UnmanagedType.IUnknown)] out object mediaType);
        [DllImport("mf.dll")] public static extern int MFEnumDeviceSources(IMFAttributes attributes, out IntPtr activates, out int count);
        [DllImport("mfreadwrite.dll")]
        public static extern int MFCreateSourceReaderFromMediaSource(IMFMediaSource source, IMFAttributes attributes, out IMFSourceReader reader);

        static bool _started;
        static readonly object _startLock = new object();

        public static void Startup()
        {
            lock (_startLock)
            {
                if (_started) return;
                Marshal.ThrowExceptionForHR(MFStartup(0x00020070, 0));
                _started = true;
            }
        }
    }

    // Solo i metodi usati hanno la firma reale; gli altri servono a mantenere l'ordine della vtable.
    [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFAttributes
    {
        void GetItem(); void GetItemType(); void CompareItem(); void Compare();
        [PreserveSig] int GetUINT32([In] ref Guid key, out int value);
        [PreserveSig] int GetUINT64([In] ref Guid key, out long value);
        void GetDouble();
        [PreserveSig] int GetGUID([In] ref Guid key, out Guid value);
        void GetStringLength(); void GetString();
        [PreserveSig] int GetAllocatedString([In] ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] out string value, out int length);
        void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
        [PreserveSig] int SetUINT32([In] ref Guid key, int value);
        [PreserveSig] int SetUINT64([In] ref Guid key, long value);
        void SetDouble();
        [PreserveSig] int SetGUID([In] ref Guid key, [In] ref Guid value);
        void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
    }

    [ComImport, Guid("7fee9e9a-4a89-47a6-899c-b6a53a70fb67"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFActivate
    {
        void GetItem(); void GetItemType(); void CompareItem(); void Compare(); void GetUINT32(); void GetUINT64(); void GetDouble(); void GetGUID();
        void GetStringLength(); void GetString();
        [PreserveSig] int GetAllocatedString([In] ref Guid key, [MarshalAs(UnmanagedType.LPWStr)] out string value, out int length);
        void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
        void SetUINT32(); void SetUINT64(); void SetDouble(); void SetGUID(); void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore();
        void GetCount(); void GetItemByIndex(); void CopyAllItems();
        [PreserveSig] int ActivateObject([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        [PreserveSig] int ShutdownObject();
        [PreserveSig] int DetachObject();
    }

    // Serve solo come tipo per il marshalling (QueryInterface su IID_IMFMediaType).
    [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFMediaType { }

    [ComImport, Guid("279a808d-aec7-40c8-9c6b-a6b492c78a66"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFMediaSource
    {
        void GetEvent(); void BeginGetEvent(); void EndGetEvent(); void QueueEvent();
        void GetCharacteristics(); void CreatePresentationDescriptor(); void Start(); void Stop(); void Pause();
        [PreserveSig] int Shutdown();
    }

    [ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFSourceReader
    {
        void GetStreamSelection();
        [PreserveSig] int SetStreamSelection(int streamIndex, [MarshalAs(UnmanagedType.Bool)] bool selected);
        [PreserveSig] int GetNativeMediaType(int streamIndex, int typeIndex, [MarshalAs(UnmanagedType.IUnknown)] out object mediaType);
        [PreserveSig] int GetCurrentMediaType(int streamIndex, [MarshalAs(UnmanagedType.IUnknown)] out object mediaType);
        [PreserveSig] int SetCurrentMediaType(int streamIndex, IntPtr reserved, IMFMediaType mediaType);
        void SetCurrentPosition();
        [PreserveSig] int ReadSample(int streamIndex, int controlFlags, out int actualStreamIndex, out int streamFlags, out long timestamp, [MarshalAs(UnmanagedType.IUnknown)] out object sample);
        void Flush(); void GetServiceForStream(); void GetPresentationAttribute();
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFSample
    {
        void GetItem(); void GetItemType(); void CompareItem(); void Compare(); void GetUINT32(); void GetUINT64(); void GetDouble(); void GetGUID();
        void GetStringLength(); void GetString(); void GetAllocatedString(); void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown();
        void SetItem(); void DeleteItem(); void DeleteAllItems(); void SetUINT32(); void SetUINT64(); void SetDouble(); void SetGUID(); void SetString();
        void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
        void GetSampleFlags(); void SetSampleFlags(); void GetSampleTime(); void SetSampleTime(); void GetSampleDuration(); void SetSampleDuration();
        void GetBufferCount(); void GetBufferByIndex();
        [PreserveSig] int ConvertToContiguousBuffer([MarshalAs(UnmanagedType.IUnknown)] out object buffer);
    }

    [ComImport, Guid("045fa593-8799-42b8-bc8d-8968c6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr buffer, out int maxLength, out int currentLength);
        [PreserveSig] int Unlock();
    }

    [ComImport, Guid("7dc9d5f9-9ed9-44ec-9bbf-0600bb589fbb"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMF2DBuffer
    {
        [PreserveSig] int Lock2D(out IntPtr scanline0, out int pitch);
        [PreserveSig] int Unlock2D();
    }

    public class UsbCapture : VideoSource
    {
        volatile IMFMediaSource _source;

        public override string Kind { get { return "USB"; } }

        // Elenco dei dispositivi di acquisizione video. Nomi duplicati ricevono " #2", " #3"...
        public static List<string> ListDevices()
        {
            var names = new List<string>();
            foreach (var d in Enumerate(false)) names.Add(d.Key);
            return names;
        }

        // Esegue l'enumerazione su un thread MTA; se 'activateName' e' valorizzato
        // restituisce anche l'oggetto IMFActivate di quel dispositivo.
        static List<KeyValuePair<string, IMFActivate>> Enumerate(bool keep, string activateName = null)
        {
            var result = new List<KeyValuePair<string, IMFActivate>>();
            Exception error = null;
            var t = new Thread(delegate()
            {
                try { EnumerateCore(result, keep, activateName); }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
            t.Join(5000);
            return result;
        }

        static void EnumerateCore(List<KeyValuePair<string, IMFActivate>> result, bool keep, string activateName)
        {
            MF.Startup();
            IMFAttributes attr;
            Marshal.ThrowExceptionForHR(MF.MFCreateAttributes(out attr, 1));
            Guid k = MF.DevSourceType, v = MF.DevSourceTypeVidcap;
            attr.SetGUID(ref k, ref v);
            IntPtr arr; int count;
            if (MF.MFEnumDeviceSources(attr, out arr, out count) < 0 || arr == IntPtr.Zero) { Marshal.ReleaseComObject(attr); return; }
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < count; i++)
            {
                IntPtr p = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
                var act = (IMFActivate)Marshal.GetObjectForIUnknown(p);
                Marshal.Release(p);
                string name; int len;
                Guid fn = MF.DevFriendlyName;
                if (act.GetAllocatedString(ref fn, out name, out len) < 0 || string.IsNullOrEmpty(name)) name = "Dispositivo video";
                int n;
                seen[name] = n = (seen.ContainsKey(name) ? seen[name] : 0) + 1;
                if (n > 1) name += " #" + n;
                bool wanted = keep && name == activateName;
                result.Add(new KeyValuePair<string, IMFActivate>(name, wanted ? act : null));
                if (!wanted) Marshal.ReleaseComObject(act);
            }
            Marshal.FreeCoTaskMem(arr);
            Marshal.ReleaseComObject(attr);
        }

        protected override void Abort()
        {
            var s = _source;
            if (s != null) try { s.Shutdown(); } catch { }
        }

        protected override void Run(string name)
        {
            while (!Stopping)
            {
                try { Capture(name); }
                catch (Exception ex) { LastError = ex.Message; }
                // Dispositivo assente, occupato da un'altra app o scollegato: si riprova.
                for (int i = 0; i < 20 && !Stopping; i++) Thread.Sleep(100);
            }
        }

        void Capture(string name)
        {
            MF.Startup();
            IMFActivate act = null;
            foreach (var d in Enumerate(true, name)) if (d.Value != null) act = d.Value;
            if (act == null) { LastError = "Dispositivo non trovato"; return; }

            object srcObj = null;
            IMFAttributes attr = null;
            IMFSourceReader reader = null;
            try
            {
                Guid iid = MF.IID_IMFMediaSource;
                int hr = act.ActivateObject(ref iid, out srcObj);
                if (hr < 0) { LastError = "Dispositivo occupato o non disponibile (0x" + hr.ToString("X8") + ")"; return; }
                _source = (IMFMediaSource)srcObj;

                Marshal.ThrowExceptionForHR(MF.MFCreateAttributes(out attr, 1));
                Guid avp = MF.ReaderAdvancedVideoProcessing;
                attr.SetUINT32(ref avp, 1);
                Marshal.ThrowExceptionForHR(MF.MFCreateSourceReaderFromMediaSource(_source, attr, out reader));
                reader.SetStreamSelection(MF.AllStreams, false);
                reader.SetStreamSelection(MF.FirstVideoStream, true);

                int width, height;
                double fps;
                ChooseFormat(reader, out width, out height, out fps);

                // Uscita in RGB32 (BGRX in memoria): la conversione la fa Media Foundation.
                object outObj;
                Marshal.ThrowExceptionForHR(MF.MFCreateMediaType(out outObj));
                var outAttr = (IMFAttributes)outObj;
                Guid kMajor = MF.MT_MajorType, vVideo = MF.MediaType_Video, kSub = MF.MT_Subtype, vRgb = MF.VideoFormat_RGB32, kSize = MF.MT_FrameSize;
                outAttr.SetGUID(ref kMajor, ref vVideo);
                outAttr.SetGUID(ref kSub, ref vRgb);
                if (width > 0) outAttr.SetUINT64(ref kSize, ((long)width << 32) | (uint)height);
                Marshal.ThrowExceptionForHR(reader.SetCurrentMediaType(MF.FirstVideoStream, IntPtr.Zero, (IMFMediaType)outObj));
                Marshal.ReleaseComObject(outObj);

                int stride;
                ReadCurrent(reader, out width, out height, out stride);
                LastError = null;

                while (!Stopping)
                {
                    int actual, flags; long ts; object sampleObj;
                    hr = reader.ReadSample(MF.FirstVideoStream, 0, out actual, out flags, out ts, out sampleObj);
                    if (hr < 0 || (flags & (MF.ReaderFlagError | MF.ReaderFlagEndOfStream)) != 0)
                    {
                        if (sampleObj != null) Marshal.ReleaseComObject(sampleObj);
                        if (!Stopping) LastError = "Lettura interrotta (dispositivo scollegato?)";
                        return;
                    }
                    if ((flags & 0x20) != 0) ReadCurrent(reader, out width, out height, out stride); // formato cambiato
                    if (sampleObj == null) continue;
                    try { CopySample((IMFSample)sampleObj, width, height, stride, fps); }
                    finally { Marshal.ReleaseComObject(sampleObj); }
                }
            }
            finally
            {
                var s = _source;
                _source = null;
                if (reader != null) Marshal.ReleaseComObject(reader);
                if (attr != null) Marshal.ReleaseComObject(attr);
                if (s != null) { try { s.Shutdown(); } catch { } }
                if (srcObj != null) Marshal.ReleaseComObject(srcObj);
                try { act.ShutdownObject(); } catch { }
                Marshal.ReleaseComObject(act);
            }
        }

        // Sceglie il formato nativo migliore: piu' pixel, poi piu' fps (max 60), preferendo il non compresso.
        static void ChooseFormat(IMFSourceReader reader, out int width, out int height, out double fps)
        {
            width = height = 0; fps = 0;
            object best = null;
            long bestScore = -1;
            for (int i = 0; ; i++)
            {
                object mt;
                if (reader.GetNativeMediaType(MF.FirstVideoStream, i, out mt) < 0 || mt == null) break;
                var a = (IMFAttributes)mt;
                long size, rate; Guid sub;
                Guid kSize = MF.MT_FrameSize, kRate = MF.MT_FrameRate, kSub = MF.MT_Subtype;
                if (a.GetUINT64(ref kSize, out size) < 0) size = 0;
                if (a.GetUINT64(ref kRate, out rate) < 0) rate = 0;
                if (a.GetGUID(ref kSub, out sub) < 0) sub = Guid.Empty;
                int w = (int)(size >> 32), h = (int)(size & 0xFFFFFFFF);
                uint num = (uint)(rate >> 32), den = (uint)(rate & 0xFFFFFFFF);
                double f = den > 0 ? (double)num / den : 0;
                bool compressed = sub == MF.VideoFormat_MJPG || sub == MF.VideoFormat_H264;
                long score = (long)w * h * 1000 + (long)(Math.Min(f, 60) * 10) * 2 + (compressed ? 0 : 1);
                if (score > bestScore)
                {
                    if (best != null) Marshal.ReleaseComObject(best);
                    best = mt; bestScore = score; width = w; height = h; fps = f;
                }
                else Marshal.ReleaseComObject(mt);
            }
            if (best != null)
            {
                reader.SetCurrentMediaType(MF.FirstVideoStream, IntPtr.Zero, (IMFMediaType)best);
                Marshal.ReleaseComObject(best);
            }
        }

        static void ReadCurrent(IMFSourceReader reader, out int width, out int height, out int stride)
        {
            width = height = 0; stride = 0;
            object mt;
            if (reader.GetCurrentMediaType(MF.FirstVideoStream, out mt) < 0 || mt == null) return;
            var a = (IMFAttributes)mt;
            long size; int s;
            Guid kSize = MF.MT_FrameSize, kStride = MF.MT_DefaultStride;
            if (a.GetUINT64(ref kSize, out size) >= 0) { width = (int)(size >> 32); height = (int)(size & 0xFFFFFFFF); }
            stride = a.GetUINT32(ref kStride, out s) >= 0 ? s : width * 4;
            Marshal.ReleaseComObject(mt);
        }

        void CopySample(IMFSample sample, int width, int height, int stride, double fps)
        {
            object bufObj;
            if (sample.ConvertToContiguousBuffer(out bufObj) < 0 || bufObj == null) return;
            try
            {
                // IMF2DBuffer fornisce direttamente la prima riga e il passo (negativo se dal basso).
                var b2 = bufObj as IMF2DBuffer;
                IntPtr scan0; int pitch;
                if (b2 != null && b2.Lock2D(out scan0, out pitch) >= 0)
                {
                    try { PublishFrame(scan0, width, height, pitch, 0, fps); }
                    finally { b2.Unlock2D(); }
                    return;
                }
                var mb = (IMFMediaBuffer)bufObj;
                IntPtr data; int max, cur;
                if (mb.Lock(out data, out max, out cur) < 0) return;
                try
                {
                    if (stride < 0) data = new IntPtr(data.ToInt64() + (long)(height - 1) * -stride);
                    if ((long)Math.Abs(stride) * height <= cur) PublishFrame(data, width, height, stride, 0, fps);
                }
                finally { mb.Unlock(); }
            }
            finally { Marshal.ReleaseComObject(bufObj); }
        }
    }
}
