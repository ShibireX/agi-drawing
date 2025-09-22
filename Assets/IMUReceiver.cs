using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    // --------- Player rig / spawning ---------
    [Header("Player Rig Spawning")]
    [Tooltip("Prefab that contains a root with a visible reference (e.g., a cube) and a child named 'Tip' used as brush tip. If null, a simple cube + sphere tip will be created at runtime.")]
    public GameObject playerRigPrefab;
    [Tooltip("Seconds of silence before despawning a player's rig. Set <= 0 to never despawn.")]
    public float despawnAfterSeconds = 15f;

    [Tooltip("Maximum simultaneous players (devices) to spawn")] 
    [Range(1, 8)]
    public int maxPlayers = 4;

    [Header("Spawn Layout (Left → Right)")]
    [Tooltip("World-space anchor for the LEFT-most spawn. If null, a fallback position is used.")]
    public Transform spawnLeft;
    [Tooltip("World-space anchor for the RIGHT-most spawn. If null, a fallback position is used.")]
    public Transform spawnRight;
    [Tooltip("Fallback left position used if spawnLeft is null.")]
    public Vector3 fallbackLeft = new Vector3(-2f, 1f, 0f);
    [Tooltip("Fallback right position used if spawnRight is null.")]
    public Vector3 fallbackRight = new Vector3( 2f, 1f, 0f);

    [Header("Launch Settings (shared for all players)")]
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
    [Tooltip("Also allow manual fire with space key (fires all rigs; ignores threshold).")]
    public bool allowManualFire = true;

    // Networking
    UdpClient udp;
    Thread recvThread;
    volatile bool running;

    // Monotonic time for cross-thread timestamps
    Stopwatch sw;

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
        public double lastSeenSeconds; // stopwatch time (seconds)
    }

    // Runtime player rig bound to a deviceId
    class PlayerRig
    {
        public byte deviceId;
        public GameObject root;
        public Transform reference; // rotates with phone
        public Transform tip;       // brush tip position
        public float lastFireTime = -999f;
    }

    readonly object _lock = new object();
    readonly Dictionary<byte, DeviceState> devices = new Dictionary<byte, DeviceState>();
    readonly Dictionary<byte, PlayerRig> rigs = new Dictionary<byte, PlayerRig>();

    // Tracks first-seen order of devices currently occupying slots
    readonly List<byte> spawnOrder = new List<byte>();

    Vector3 GetLeft()  => spawnLeft  ? spawnLeft.position  : fallbackLeft;
    Vector3 GetRight() => spawnRight ? spawnRight.position : fallbackRight;

    Vector3 GetSlotPosition(int slot)
    {
        if (maxPlayers <= 1) return GetLeft();
        float t = Mathf.Clamp01(slot / Mathf.Max(1f, (maxPlayers - 1))); // 0..1
        return Vector3.Lerp(GetLeft(), GetRight(), t);
    }

    void RelayoutAll()
    {
        for (int i = 0; i < spawnOrder.Count; i++)
        {
            byte id = spawnOrder[i];
            if (rigs.TryGetValue(id, out var rig) && rig.root)
            {
                rig.root.transform.position = GetSlotPosition(i);
            }
        }
    }

    void Start()
    {
        try
        {
            sw = Stopwatch.StartNew();

            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.ReceiveBufferSize = 1 << 20;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            running = true;
            recvThread = new Thread(RecvLoop) { IsBackground = true };
            recvThread.Start();

        }
        catch (Exception)
        {

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
                    st.lastSeenSeconds = sw.Elapsed.TotalSeconds;
                }
            }
            catch (SocketException) { if (!running) break; }
            catch (Exception e) { UnityEngine.Debug.Log($"[ImuUdpLogger] Worker exception: {e.Message}"); }
        }
    }

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        List<byte> toDespawn = null;

        lock (_lock)
        {
            foreach (var kv in devices)
            {
                var st = kv.Value;

                // Spawn rig on first sight, with left→right slots and maxPlayers cap
                if (!rigs.ContainsKey(kv.Key))
                {
                    // If device is not already queued, add to order
                    if (!spawnOrder.Contains(kv.Key))
                    {
                        if (spawnOrder.Count >= maxPlayers)
                        {
                            // Over capacity: do not spawn (we'll try again when a slot frees)
                            continue;
                        }
                        spawnOrder.Add(kv.Key);
                    }

                    int slot = spawnOrder.IndexOf(kv.Key);
                    Vector3 spawnPos = GetSlotPosition(slot);
                    rigs[kv.Key] = CreateRig(kv.Key, spawnPos);
                }

                // Logging per device
                if (st.lastLogTime <= 0f) st.lastLogTime = now;
                float dt = now - st.lastLogTime;
                if (dt >= logIntervalSeconds)
                {
                    float hz = st.packetsSinceLastLog / Mathf.Max(dt, 1e-3f);
                    st.hzSmoothed = (st.hzSmoothed <= 0f) ? hz : Mathf.Lerp(st.hzSmoothed, hz, 0.4f);
                    st.lastLogTime = now;
                    st.packetsSinceLastLog = 0;

                    UnityEngine.Debug.Log(
                        $"[IMU d{st.deviceId}] seq={st.lastSeq} rate={st.hzSmoothed:F1} Hz " +
                        $"q=({st.q.x:F2},{st.q.y:F2},{st.q.z:F2},{st.q.w:F2})");
                }

                // Update rig transform + fire logic
                if (rigs.TryGetValue(kv.Key, out var rig))
                {
                    // Orientation correction (same as before)
                    Quaternion q = st.q;
                    q = new Quaternion(-q.x, q.y, q.z, q.w);
                    Quaternion phoneToUnity = Quaternion.Euler(90, 0, 0) * q;

                    // Ensure position matches assigned left→right slot every frame
                    int slotIndex = spawnOrder.IndexOf(kv.Key);
                    if (slotIndex >= 0)
                    {
                        Vector3 desiredPos = GetSlotPosition(slotIndex);
                        if (rig.root) rig.root.transform.position = desiredPos;
                    }

                    if (rig.reference) rig.reference.rotation = phoneToUnity;

                    // Accel in world space
                    Vector3 accelWorld = phoneToUnity * st.accel;
                    float aMag = accelWorld.magnitude;

                    bool manual = allowManualFire && Input.GetKeyDown(KeyCode.Space);
                    if ((manual || aMag >= fireAccelThreshold) && (now - rig.lastFireTime) >= fireCooldown)
                    {
                        FireProjectile(
                            origin: rig.tip ? rig.tip.position : (rig.reference ? rig.reference.position : Vector3.zero),
                            direction: accelWorld
                        );
                        rig.lastFireTime = now;
                    }
                }

                // Despawn tracking
                if (despawnAfterSeconds > 0f)
                {
                    double silentFor = sw.Elapsed.TotalSeconds - st.lastSeenSeconds;
                    if (silentFor >= despawnAfterSeconds)
                    {
                        if (toDespawn == null) toDespawn = new List<byte>();
                        toDespawn.Add(kv.Key);
                    }
                }
            }
        }

        // Despawn outside the lock
        if (toDespawn != null)
        {
            bool layoutChanged = false;
            foreach (var id in toDespawn)
            {
                if (rigs.TryGetValue(id, out var rig))
                {
                    Destroy(rig.root);
                    rigs.Remove(id);
                }
                lock (_lock) { devices.Remove(id); }
                if (spawnOrder.Remove(id)) layoutChanged = true;
                UnityEngine.Debug.Log($"[ImuUdpLogger] Despawned rig for device {id} (timeout).");
            }
            if (layoutChanged) RelayoutAll();
        }
    }

    PlayerRig CreateRig(byte deviceId, Vector3 spawnPosition)
    {
        var rig = new PlayerRig { deviceId = deviceId };

        if (playerRigPrefab)
        {
            rig.root = Instantiate(playerRigPrefab);
            rig.root.transform.position = spawnPosition;
            rig.reference = rig.root.transform;
            var tip = rig.root.transform.Find("Handle.001");
            rig.tip = tip ? tip : rig.root.transform;
        }
        else
        {
            // Minimal default: cube as reference + small sphere tip in front
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = $"PlayerRig_{deviceId}";
            root.transform.localScale = Vector3.one * 0.1f;

            var tipObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tipObj.name = "Tip";
            tipObj.transform.SetParent(root.transform, false);
            tipObj.transform.localScale = Vector3.one * 0.05f;
            tipObj.transform.localPosition = new Vector3(0, 0, 0.1f);

            rig.root = root;
            rig.reference = root.transform;
            rig.tip = tipObj.transform;
            root.transform.position = spawnPosition;
        }

        UnityEngine.Debug.Log($"[ImuUdpLogger] Spawned rig for device {deviceId}.");
        return rig;
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
                double silentFor = sw.Elapsed.TotalSeconds - st.lastSeenSeconds;
                GUI.Label(new Rect(10, 10 + 20 * line++, 1400, 20),
                    $"d{st.deviceId}  {st.hzSmoothed:F0} Hz  q=[{st.q.x:F2},{st.q.y:F2},{st.q.z:F2},{st.q.w:F2}]  idle={silentFor:F1}s");
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