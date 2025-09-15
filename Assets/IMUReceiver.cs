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

    // --------- NEW: Launch settings ----------
    [Header("Launch Settings")]
    [Tooltip("Assign the brush tip (Handle.001).")]
    public Transform brushTip;
    [Tooltip("Projectile prefab with a Rigidbody (a sphere).")]
    public GameObject projectilePrefab;
    [Tooltip("Units per second for the launch velocity.")]
    public float launchSpeed = 6f;
    [Tooltip("Fire only when |accel| exceeds this (m/s^2-ish).")]
    public float fireAccelThreshold = 2.0f;
    [Tooltip("Cooldown between shots (sec).")]
    public float fireCooldown = 0.2f;
    [Tooltip("Auto-destroy projectile after this many seconds.")]
    public float projectileLifetime = 8f;
    [Tooltip("Also allow manual fire with space key (ignores threshold).")]
    public bool allowManualFire = true;

    float lastFireTime = -999f;

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
            // same correction you already use
            Quaternion q = control.q;
            q = new Quaternion(-q.x, q.y, q.z, q.w);
            Quaternion phoneToUnity = Quaternion.Euler(90, 0, 0) * q;
            referenceObject.rotation = phoneToUnity;

            // --------- NEW: Launch in accel direction ----------
            // Accel is in phone space; rotate it into world space with the same correction
            Vector3 accelPhone = control.accel;

            // Optional: remove gravity-ish bias. Uncomment if needed.
            // accelPhone -= new Vector3(0, 0, 9.81f); // depends on your phone's axis/gravity; tweak if wrong

            Vector3 accelWorld = phoneToUnity * accelPhone;
            float aMag = accelWorld.magnitude;

            bool manual = allowManualFire && Input.GetKeyDown(KeyCode.Space);
            if ((manual || aMag >= fireAccelThreshold) && (now - lastFireTime) >= fireCooldown)
            {
                FireProjectile(
                    origin: brushTip ? brushTip.position : referenceObject.position,
                    direction: accelWorld
                );
                lastFireTime = now;
            }
        }
    }

    void FireProjectile(Vector3 origin, Vector3 direction)
    {
        if (projectilePrefab == null)
        {
            // Fall back: create a sphere at runtime if no prefab provided.
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = Vector3.one * 0.05f;
            var rb0 = sphere.AddComponent<Rigidbody>();
            rb0.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            sphere.transform.position = origin;
            rb0.velocity = direction.normalized * launchSpeed;
            Destroy(sphere, projectileLifetime);
            return;
        }

        var go = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.velocity = direction.normalized * launchSpeed;

        if (projectileLifetime > 0f) Destroy(go, projectileLifetime);
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