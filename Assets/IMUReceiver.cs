using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class ImuUdpLogger : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 26761;

    [Header("Logging")]
    public float logIntervalSeconds = 2f;
    public bool showHud = true;

    [Header("Reference Object")]
    [Tooltip("Assign a cube (or any Transform) here to follow phone rotation.")]
    public Transform referenceObject;

    [Tooltip("Device ID to control the referenceObject. Leave -1 to use first phone seen.")]
    public int controlDeviceId = -1;

    UdpClient udp;
    Thread recvThread;
    volatile bool running;

    class DeviceState
    {
        public byte deviceId;
        public Quaternion q;
        public Vector3 gyro;
        public Vector3 accel;
        public ushort lastSeq;
        public ulong lastTimestampUs;
        public int totalPackets;
        public int packetsSinceLastLog;
        public float lastLogTime;
        public float hzSmoothed;
        public volatile bool seen;
    }

    readonly object _lock = new object();
    readonly Dictionary<byte, DeviceState> devices = new Dictionary<byte, DeviceState>();

    void Start()
    {
        try
        {
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.ReceiveBufferSize = 1 << 20;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            running = true;
            recvThread = new Thread(RecvLoop) { IsBackground = true };
            recvThread.Start();

            Debug.Log($"[ImuUdpLogger] Listening on UDP {listenPort}.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ImuUdpLogger] Failed to bind UDP port {listenPort}: {e}");
        }
    }

    void RecvLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, 0);
        const int MinPacketSize = 1 + 1 + 2 + 8 + 13 * 4;

        while (running)
        {
            try
            {
                var data = udp.Receive(ref ep);
                if (data == null || data.Length < MinPacketSize) continue;

                int o = 0;
                byte version = data[o++]; if (version != 1) continue;
                byte deviceId = data[o++];

                ushort seq = (ushort)(data[o++] | (data[o++] << 8));

                ulong tsUs = 0;
                for (int i = 0; i < 8; i++) tsUs |= ((ulong)data[o++]) << (8 * i);

                float ReadF() { float v = BitConverter.ToSingle(data, o); o += 4; return v; }

                float qx = ReadF(), qy = ReadF(), qz = ReadF(), qw = ReadF();
                float gx = ReadF(), gy = ReadF(), gz = ReadF();
                float ax = ReadF(), ay = ReadF(), az = ReadF();
                o += 3 * 4; // reserved

                lock (_lock)
                {
                    if (!devices.TryGetValue(deviceId, out var st))
                    {
                        st = new DeviceState { deviceId = deviceId };
                        devices[deviceId] = st;
                    }

                    st.lastSeq = seq;
                    st.lastTimestampUs = tsUs;
                    st.q = new Quaternion(qx, qy, qz, qw);
                    st.gyro = new Vector3(gx, gy, gz);
                    st.accel = new Vector3(ax, ay, az);
                    st.totalPackets++;
                    st.packetsSinceLastLog++;
                    st.seen = true;
                }
            }
            catch (SocketException) { if (!running) break; }
            catch (Exception e) { Console.WriteLine($"[ImuUdpLogger] Worker exception: {e.Message}"); }
        }
    }

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        DeviceState control = null;

        lock (_lock)
        {
            foreach (var kv in devices)
            {
                var st = kv.Value;

                // pick reference device
                if (control == null)
                {
                    if (controlDeviceId < 0 || kv.Key == (byte)controlDeviceId)
                        control = st;
                }

                // logging
                if (st.lastLogTime <= 0f) st.lastLogTime = now;
                float dt = now - st.lastLogTime;
                if (dt >= logIntervalSeconds)
                {
                    float hz = st.packetsSinceLastLog / Mathf.Max(dt, 1e-3f);
                    st.hzSmoothed = (st.hzSmoothed <= 0f) ? hz : Mathf.Lerp(st.hzSmoothed, hz, 0.4f);
                    st.lastLogTime = now;
                    st.packetsSinceLastLog = 0;

                    Debug.Log(
                        $"[IMU d{st.deviceId}] seq={st.lastSeq} rate={st.hzSmoothed:F1} Hz " +
                        $"q=({st.q.x:F2},{st.q.y:F2},{st.q.z:F2},{st.q.w:F2})"
                    );
                }
            }
        }

        // Apply orientation to reference object
        if (referenceObject && control != null)
        {
            // Adjust axes: phone frame vs Unity frame might differ
            // Try as-is first; if it looks wrong, experiment with rotation multipliers.
            Quaternion q = control.q;
            // q = new Quaternion(-q.x, -q.y, q.z, q.w); // Original
            q = new Quaternion(q.x, q.y, q.z, q.w); // Works on Sony Xperia 10 VI

            // Then apply your 90° rotation correction
            referenceObject.rotation = Quaternion.Euler(90, 0, 180) * q;
        }
    }

    void OnGUI()
    {
        if (!showHud) return;
        int line = 0;
        GUI.Label(new Rect(10, 10 + 20 * line++, 600, 20), $"IMU UDP Logger @ {listenPort}");

        lock (_lock)
        {
            foreach (var kv in devices)
            {
                var st = kv.Value;
                if (!st.seen) continue;
                GUI.Label(new Rect(10, 10 + 20 * line++, 1400, 20),
                    $"d{st.deviceId}  {st.hzSmoothed:F0} Hz  q=[{st.q.x:F2},{st.q.y:F2},{st.q.z:F2},{st.q.w:F2}]");
            }
        }
    }

    void OnDestroy()
    {
        running = false;
        try { udp?.Close(); } catch { }
        try { recvThread?.Join(100); } catch { }
    }
}
