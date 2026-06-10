using CMS.Server.Models;
using PLC_Omron_Standard;
using PLC_Omron_Standard.Enums;
using System.Collections.Concurrent;
using System.Text;

namespace CMS.Server.Services
{
    public sealed class MainPlcService : BackgroundService
    {
        private static readonly ConcurrentDictionary<string, PlcOmron> _plcConnections = new();

        private readonly SemaphoreSlim _pollLock = new(1, 1);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MainPlcService> _logger;

        private const string MasterIp = "172.17.86.80";
        private const string ReadKey = MasterIp + "_read";
        private const string WriteKey = MasterIp + "_write";
        private const int PollDelayMs = 100;

        // D memory: machines start at D500, 500 words per machine
        private const ushort DBase = 500;
        private const int DPerMachine = 500;
        private const int DMaxChunk = 400; // FINS protocol word limit per read

        // W memory: machines start at W5, 3 words (48 bits) per machine
        private const ushort WBase = 5;
        private const int WPerMachine = 3;
        private const int WMaxChunk = 480; // FINS protocol bit limit per read

        private IReadOnlyList<(int id, string name, string ip)> _cachedMachines = [];
        private DateTime _machinesCachedAt = DateTime.MinValue;
        private readonly SemaphoreSlim _machineCacheLock = new(1, 1);
        private const int MachineCacheMinutes = 5;

        private int _iterationCount;

        public MainPlcService(IServiceScopeFactory scopeFactory, ILogger<MainPlcService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // ── BackgroundService lifecycle ────────────────────────────────────────

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[MainPlcService] Starting at {Time}", DateTime.Now);
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("[MainPlcService] Stopping at {Time}", DateTime.Now);
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[MainPlcService] Poll loop started.");
            await RunMasterPlcLoopAsync(stoppingToken);
            _logger.LogWarning("[MainPlcService] Poll loop exited. Total iterations: {Count}", _iterationCount);
        }

        // ── Master PLC poll loop ───────────────────────────────────────────────

        private async Task RunMasterPlcLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await _pollLock.WaitAsync(0, stoppingToken))
                {
                    await Task.Delay(PollDelayMs, stoppingToken);
                    continue;
                }

                try
                {
                    _iterationCount++;

                    var machines = await GetCachedMachinesAsync();
                    if (machines.Count == 0) continue;

                    var timestamp = DateTime.Now;

                    if (!TryReadMasterPlc(machines.Count, out var dBuffer, out var wBits))
                    {
                        if (_iterationCount % 50 == 0)
                            _logger.LogWarning("[MainPlcService] Master PLC read failed at {Time}", timestamp);
                        continue;
                    }

                    var snapshots = BuildSnapshots(dBuffer, wBits, timestamp, machines.Count);

                    //DebugMasterPlc(snapshots.FirstOrDefault(s => s.id_machine == 0));

                    await PersistSnapshotsAsync(snapshots, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MainPlcService] Error at iteration {Count}", _iterationCount);
                }
                finally
                {
                    _pollLock.Release();
                }

                await Task.Delay(PollDelayMs, stoppingToken);
            }
        }

        // ── Master PLC read ────────────────────────────────────────────────────

        private bool TryReadMasterPlc(int machineCount, out byte[] dBuffer, out bool[] wBits)
        {
            dBuffer = null!;
            wBits = null!;

            PlcOmron? plc = null;
            try
            {
                if (!_plcConnections.TryGetValue(ReadKey, out plc) || plc == null)
                {
                    plc = new PlcOmron(MasterIp, 9600, false, 80, 136);
                    _plcConnections[ReadKey] = plc;
                }

                plc.Connect();

                // ── D memory read ──────────────────────────────────────────────
                int totalDWords = machineCount * DPerMachine;
                byte[] dBuf = new byte[totalDWords * 2];

                for (int offset = 0; offset < totalDWords; offset += DMaxChunk)
                {
                    ushort chunkStart = (ushort)(DBase + offset);
                    ushort chunkSize = (ushort)Math.Min(DMaxChunk, totalDWords - offset);

                    byte[] chunk = plc.Read(chunkStart, chunkSize, 0, MemoryAreaBits.DataMemory);
                    if (chunk == null || chunk.Length < chunkSize * 2)
                    {
                        _logger.LogWarning("[MainPlcService] D short read at D{Addr}", chunkStart);
                        return false;
                    }

                    Buffer.BlockCopy(chunk, 0, dBuf, offset * 2, chunk.Length);
                }

                // ── W memory read ──────────────────────────────────────────────
                int totalWBits = machineCount * WPerMachine * 16;
                bool[] wBuf = new bool[totalWBits];

                for (int offset = 0; offset < totalWBits; offset += WMaxChunk)
                {
                    ushort chunkSize = (ushort)Math.Min(WMaxChunk, totalWBits - offset);
                    ushort chunkStartWord = (ushort)(WBase + offset / 16);

                    byte[] chunk = plc.Read(chunkStartWord, chunkSize, 0, MemoryAreaBits.Work);
                    if (chunk == null || chunk.Length < chunkSize)
                    {
                        _logger.LogWarning("[MainPlcService] W short read at W{Word}.0", chunkStartWord);
                        return false;
                    }

                    for (int i = 0; i < chunkSize; i++)
                        wBuf[offset + i] = chunk[i] != 0;
                }

                dBuffer = dBuf;
                wBits = wBuf;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainPlcService] PLC read error");
                if (plc != null)
                {
                    _plcConnections.TryRemove(ReadKey, out _);
                    try { plc.Disconnect(); } catch { }
                }
                return false;
            }
        }

        // ── Snapshot builder ───────────────────────────────────────────────────

        private static List<PlcSnapshot> BuildSnapshots(byte[] dRaw, bool[] wRaw, DateTime timestamp, int machineCount)
        {
            // D: machine m starts at byte offset (m * DPerMachine + (absoluteWord - DBase)) * 2
            int Didx(int m, int absWord) => (m * DPerMachine + (absWord - DBase)) * 2;

            // W: machine m, word wordOffset, bit within that word
            int Widx(int m, int wordOffset, int bit) => (m * WPerMachine + wordOffset) * 16 + bit;

            bool Wbit(int idx) => idx >= 0 && idx < wRaw.Length && wRaw[idx];

            var list = new List<PlcSnapshot>(machineCount);

            for (int m = 0; m < machineCount; m++)
            {
                list.Add(new PlcSnapshot
                {
                    id_machine = m,
                    time = timestamp,

                    // D510 – Visual QC count
                    visual_qc = Math.Min(ReadInt(dRaw, Didx(m, 510)), 10_000),
                    // D512 – Measure QC count
                    measure_qc = Math.Min(ReadInt(dRaw, Didx(m, 512)), 10_000),
                    // D525 – Part weight (float, grams)
                    part_weight = Math.Min(ReadFloat(dRaw, Didx(m, 525)), 1_000f),
                    // D530 – Shot count this run
                    shot = Math.Min(ReadInt(dRaw, Didx(m, 530)), 10_000),
                    // D532 – Accumulated shot count
                    shot_accum = Math.Min(ReadInt(dRaw, Didx(m, 532)), 10_000),
                    // D560 – Actual cycle time (seconds, float)
                    act_ct = Math.Min(ReadFloat(dRaw, Didx(m, 560)), 1_000f),
                    // D590 – Mould change category number
                    mould_category_no = Math.Min(ReadInt(dRaw, Didx(m, 590)), 10),
                    // D600 – Product type string (100 bytes)
                    type = ReadString(dRaw, Didx(m, 600), 100),
                    // D700 – Packer name string (100 bytes)
                    packer = ReadString(dRaw, Didx(m, 700), 100),
                    // D800 – Stop category string (100 bytes)
                    stop_category = ReadString(dRaw, Didx(m, 800), 100),
                    // D900 – Remark / problem string (100 bytes)
                    remark = ReadString(dRaw, Didx(m, 900), 100),

                    // D750 – Reject: panelling (kg, float)
                    reject_panelling = Math.Min(ReadFloat(dRaw, Didx(m, 750)), 10_000f),
                    // D755 – Reject: lumpy (kg, float)
                    reject_lumpy = Math.Min(ReadFloat(dRaw, Didx(m, 755)), 10_000f),
                    // D760 – Reject: black dot (kg, float)
                    reject_black_dot = Math.Min(ReadFloat(dRaw, Didx(m, 760)), 10_000f),
                    // D765 – Reject: burst (kg, float)
                    reject_burst = Math.Min(ReadFloat(dRaw, Didx(m, 765)), 10_000f),
                    // D770 – Reject: startup (kg, float)
                    reject_startup = Math.Min(ReadFloat(dRaw, Didx(m, 770)), 10_000f),
                    // D775 – Reject: preform (kg, float)
                    reject_preform = Math.Min(ReadFloat(dRaw, Didx(m, 775)), 10_000f),
                    // D780 – Reject: purging (kg, float)
                    reject_purging = Math.Min(ReadFloat(dRaw, Didx(m, 780)), 10_000f),
                    // D785 – Reject: others (kg, float)
                    reject_others = Math.Min(ReadFloat(dRaw, Didx(m, 785)), 10_000f),

                    // W[m,0].0 – Machine status start (running)
                    status_start = Wbit(Widx(m, 0, 0)),
                    // W[m,0].1 – Machine status off
                    status_off = Wbit(Widx(m, 0, 1)),
                    // W[m,0].2 – Production running signal
                    production_running = Wbit(Widx(m, 0, 2)),
                    // W[m,0].3 – QC visual inspection signal
                    qc_signal = Wbit(Widx(m, 0, 3)),
                    // W[m,0].4 – Done / cycle complete signal
                    done = Wbit(Widx(m, 0, 4)),
                    // W[m,0].5 – Remark/problem note signal
                    remark_signal = Wbit(Widx(m, 0, 5)),
                    // W[m,0].6 – Reject entry signal
                    reject_signal = Wbit(Widx(m, 0, 6)),
                    // W[m,0].7 – QC reset signal
                    qc_reset_signal = Wbit(Widx(m, 0, 7)),

                    // W[m,1].0 – Utility: barrel heater on
                    util_barrel = Wbit(Widx(m, 1, 0)),
                    // W[m,1].1 – Utility: hydraulic motor on
                    util_hyd_motor = Wbit(Widx(m, 1, 1)),
                    // W[m,1].2 – Utility: dehumidifier on
                    util_dehumidifier = Wbit(Widx(m, 1, 2)),
                    // W[m,1].3 – Utility: chiller on
                    util_chiller = Wbit(Widx(m, 1, 3)),
                    // W[m,1].4 – Utility: material feeder on
                    util_material = Wbit(Widx(m, 1, 4)),
                    // W[m,1].5 – Utility: dry cycle active
                    util_dry_cycle = Wbit(Widx(m, 1, 5)),
                });
            }

            return list;
        }

        // ── Database persistence ───────────────────────────────────────────────

        private async Task PersistSnapshotsAsync(List<PlcSnapshot> snapshots, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var baseService = scope.ServiceProvider.GetRequiredService<BaseService>();

            foreach (var snapshot in snapshots)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await baseService.insertMachineMaster(snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MainPlcService] DB persist failed for machine {Id}", snapshot.id_machine);
                }
            }
        }

        // ── Machine list cache ─────────────────────────────────────────────────

        public async Task<IReadOnlyList<(int id, string name, string ip)>> GetCachedMachinesAsync()
        {
            if ((DateTime.UtcNow - _machinesCachedAt).TotalMinutes < MachineCacheMinutes && _cachedMachines.Count > 0)
                return _cachedMachines;

            await _machineCacheLock.WaitAsync();
            try
            {
                if ((DateTime.UtcNow - _machinesCachedAt).TotalMinutes < MachineCacheMinutes && _cachedMachines.Count > 0)
                    return _cachedMachines;

                using var scope = _scopeFactory.CreateScope();
                var baseService = scope.ServiceProvider.GetRequiredService<BaseService>();
                var fetched = await baseService.GetMachineIdsAsync();

                if (fetched != null && fetched.Count > 0)
                {
                    _cachedMachines = fetched;
                    _machinesCachedAt = DateTime.UtcNow;
                }

                return _cachedMachines;
            }
            finally
            {
                _machineCacheLock.Release();
            }
        }

        // ── Write interface ────────────────────────────────────────────────────

        public void UpdatePLCS(dynamic master)
        {
            Console.WriteLine($"[Machine {master.id_machine}] PLC Mould Change");
            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                int id_machine = (int)master.id_machine;
                ushort dBase = (ushort)(500 + id_machine * 500);

                // H10 – Sub-PLC node number for routing
                WriteInt(plc, 10, 220 + id_machine);

                // D25 / D(base+25) – Part weight
                WriteFloat(plc, 25, master.part_weight);
                WriteFloat(plc, (ushort)(dBase + 25), master.part_weight);

                // D100 / D(base+100) – Product type string
                WriteString(plc, 100, master.type);
                WriteString(plc, (ushort)(dBase + 100), master.type);

                // D200 / D(base+200) – Packer name string
                WriteString(plc, 200, master.packer);
                WriteString(plc, (ushort)(dBase + 200), master.packer);

                // W4.0 – Mould change done trigger
                WriteBool(plc, 4, 0, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainPlcService] UpdatePLCS failed");
            }
        }

        public void UpdatePacker(dynamic master)
        {
            Console.WriteLine($"[Machine] Packer Change");
            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                foreach (var staff in master)
                {
                    int baseOffset = staff.id_machine * 500;

                    // H10 – Sub-PLC node number for routing
                    WriteInt(plc, 10, 220 + (int)staff.id_machine);

                    // D200 / D(700+baseOffset) – Packer name string
                    WriteString(plc, 200, staff.packer);
                    WriteString(plc, (ushort)(700 + baseOffset), staff.packer);

                    // W4.8 – Packer update trigger
                    WriteBool(plc, 4, 8, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainPlcService] UpdatePacker failed");
            }
        }

        public void WriteQcMeasure(int id_machine, int value)
        {
            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                ushort mainAddr = (ushort)(DBase + id_machine * DPerMachine + 12); // D(500 + m*500 + 12) = D512 for M0

                // Main PLC: D(500 + id_machine*500 + 12) – QC_MEASURE
                WriteInt(plc, mainAddr, value);

                // Sub PLC: D12 – QC_MEASURE
                WriteInt(plc, 20, 220 + id_machine); // H20 – sub-PLC node for QC routing
                WriteInt(plc, 12, value);             // D12 – sub-PLC QC_MEASURE

                // W1.1 – Password update trigger
                WriteBool(plc, 3, 0, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainPlcService] WriteQcMeasure failed for machine {Id}", id_machine);
            }
        }

        public void ResetQcCounters(int id_machine)
        {
            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                ushort visualAddr = (ushort)(DBase + id_machine * DPerMachine + 10); // D510 for M0
                ushort measureAddr = (ushort)(DBase + id_machine * DPerMachine + 12); // D512 for M0

                // Main PLC: zero out both QC counters for this machine
                WriteInt(plc, visualAddr, 0);
                WriteInt(plc, measureAddr, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainPlcService] ResetQcCounters failed for machine {Id}", id_machine);
            }
        }

        public void ChangePassword(Dictionary<string, int> passwords)
        {
            Console.WriteLine("[Central PLC] Password Changed");

            // H30/H32/H34/H36 – Department passwords in holding memory
            var addressMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                ["production"] = 30,
                ["technician"] = 32,
                ["maintenance"] = 34,
                ["qc"] = 36,
            };

            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                foreach (var (dept, pass) in passwords)
                {
                    if (addressMap.TryGetValue(dept, out ushort address))
                        WriteHolding(plc, address, pass);
                }

                // W3.3 – Password update trigger
                WriteBool(plc, 3, 3, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainPlcService] ChangePassword failed");
            }
        }

        // ── Debug ──────────────────────────────────────────────────────────────

        private static void DebugMasterPlc(PlcSnapshot? r)
        {
            if (r == null || r.id_machine != 0) return;

            Console.WriteLine($"\n[Master] === Machine 0 ===");
            Console.WriteLine($"  QC_VISUAL={r.visual_qc,-6} QC_MEASURE={r.measure_qc,-6}");
            Console.WriteLine($"  SHOT={r.shot,-6} SHOT_ACCUM={r.shot_accum,-6} ACT_CT={r.act_ct:F3}");
            Console.WriteLine($"  STOP_CAT=\"{r.stop_category}\" REMARKS=\"{r.remark}\" MOULD_CAT_NO={r.mould_category_no}");
            Console.WriteLine($"  STATUS_START={r.status_start} STATUS_OFF={r.status_off} PROD_RUN={r.production_running} QC={r.qc_signal} DONE={r.done} REM={r.remark_signal} REJ={r.reject_signal} QC_RST={r.qc_reset_signal}");
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private PlcOmron GetWritePlc()
        {
            if (!_plcConnections.TryGetValue(WriteKey, out var plc) || plc == null)
            {
                plc = new PlcOmron(MasterIp, 9600, false, 80, 1);
                _plcConnections[WriteKey] = plc;
            }
            return plc;
        }

        internal static int ReadInt(byte[] buf, int idx)
        {
            if (idx < 0 || idx + 4 > buf.Length) return 0;
            return BitConverter.ToInt32(new byte[] { buf[idx + 1], buf[idx], buf[idx + 3], buf[idx + 2] }, 0);
        }

        internal static float ReadFloat(byte[] buf, int idx)
        {
            if (idx < 0 || idx + 4 > buf.Length) return 0f;
            float val = BitConverter.ToSingle(new byte[] { buf[idx + 1], buf[idx], buf[idx + 3], buf[idx + 2] }, 0);

            return float.IsNaN(val) || float.IsInfinity(val) ? 0f : val;
        }

        internal static string ReadString(byte[] buf, int idx, int maxBytes = 100)
        {
            if (idx < 0 || idx >= buf.Length) return string.Empty;
            int available = Math.Min(maxBytes, buf.Length - idx);
            int length = 0;
            while (length < available && buf[idx + length] != 0) length++;
            return length == 0 ? string.Empty : Encoding.ASCII.GetString(buf, idx, length).Trim();
        }

        private static bool WriteInt(PlcOmron plc, ushort addr, int value)
        {
            var b = BitConverter.GetBytes(value);
            return plc.Write(addr, [b[1], b[0], b[3], b[2]], 0, 2, MemoryAreaBits.DataMemory);
        }

        private static bool WriteFloat(PlcOmron plc, ushort addr, float value)
        {
            var b = BitConverter.GetBytes(value);
            return plc.Write(addr, [b[1], b[0], b[3], b[2]], 0, 2, MemoryAreaBits.DataMemory);
        }

        private static bool WriteString(PlcOmron plc, ushort addr, string value, int maxLength = 100)
        {
            var bytes = new byte[maxLength];
            var src = Encoding.ASCII.GetBytes(value ?? string.Empty);
            Array.Copy(src, bytes, Math.Min(src.Length, maxLength));
            if (bytes.Length % 2 != 0) Array.Resize(ref bytes, bytes.Length + 1);
            return plc.Write(addr, bytes, 0, (ushort)(bytes.Length / 2), MemoryAreaBits.DataMemory);
        }

        private static bool WriteBool(PlcOmron plc, ushort word, byte bit, bool value)
            => plc.Write(word, [(byte)(value ? 1 : 0)], bit, 1, MemoryAreaBits.Work);

        private static bool WriteHolding(PlcOmron plc, ushort addr, int value)
        {
            var b = BitConverter.GetBytes(value);
            return plc.Write(addr, [b[1], b[0], b[3], b[2]], 0, 2, (MemoryAreaBits)0xB2);
        }
    }

    // ── Snapshot record ────────────────────────────────────────────────────────

    public sealed record PlcSnapshot
    {
        public int id_machine { get; init; }
        public DateTime time { get; init; }
        public int shot { get; init; }
        public int shot_accum { get; init; }
        public float act_ct { get; init; }
        public int mould_category_no { get; init; }
        public string packer { get; init; } = string.Empty;
        public string type { get; init; } = string.Empty;
        public string stop_category { get; init; } = string.Empty;
        public string remark { get; init; } = string.Empty;
        public float part_weight { get; init; }
        public float reject_panelling { get; init; }
        public float reject_lumpy { get; init; }
        public float reject_black_dot { get; init; }
        public float reject_burst { get; init; }
        public float reject_startup { get; init; }
        public float reject_preform { get; init; }
        public float reject_purging { get; init; }
        public float reject_others { get; init; }
        public bool status_start { get; init; }
        public bool status_off { get; init; }
        public bool production_running { get; init; }
        public int visual_qc { get; init; }
        public int measure_qc { get; init; }
        public bool done { get; init; }
        public bool qc_signal { get; init; }
        public bool remark_signal { get; init; }
        public bool reject_signal { get; init; }
        public bool qc_reset_signal { get; init; }
        public bool util_barrel { get; init; }
        public bool util_hyd_motor { get; init; }
        public bool util_dehumidifier { get; init; }
        public bool util_chiller { get; init; }
        public bool util_material { get; init; }
        public bool util_dry_cycle { get; init; }
    }
}