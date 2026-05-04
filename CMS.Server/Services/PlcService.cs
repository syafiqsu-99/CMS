using CMS.Server.Models;
using CMS.Server.Services;
using PLC_Omron_Standard;
using PLC_Omron_Standard.Enums;
using System.Collections.Concurrent;
using System.Text;

namespace CMS.Server.Services
{
    /// <summary>
    /// Single consolidated service that owns both PLC communication (read/write)
    /// and the background polling loop that persists data to the database.
    /// 
    /// Replaces the old PlcService + PlcMonitorService pair.
    /// </summary>
    public sealed class PlcService : BackgroundService
    {
        // ── Connection cache ───────────────────────────────────────────────────
        private static readonly ConcurrentDictionary<string, PlcOmron> _plcConnections = new();

        // ── Guard: prevents a slow DB/PLC cycle from overlapping the next tick ─
        private readonly SemaphoreSlim _pollLock = new(1, 1);

        // ── DI ────────────────────────────────────────────────────────────────
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PlcService> _logger;

        // ── PLC addresses ─────────────────────────────────────────────────────
        private const string MasterIp = "172.17.86.80";
        private const string ReadKey = MasterIp + "_read";
        private const string WriteKey = MasterIp + "_write";
        private const int PollDelayMs = 200;
        private const int MachineCount = 26;

        private int _iterationCount;

        public PlcService(IServiceScopeFactory scopeFactory, ILogger<PlcService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // ── BackgroundService lifecycle ────────────────────────────────────────

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[PlcService] Starting at {Time}", DateTime.Now);
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("[PlcService] Stopping at {Time}", DateTime.Now);
            return base.StopAsync(cancellationToken);
        }

        // ── Main poll loop ─────────────────────────────────────────────────────

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Non-blocking wait: if the previous iteration is still running, skip this tick
                if (!await _pollLock.WaitAsync(0, stoppingToken))
                {
                    await Task.Delay(PollDelayMs, stoppingToken);
                    continue;
                }

                try
                {
                    _iterationCount++;

                    if (_iterationCount % 200 == 0)
                        _logger.LogInformation("[PlcService] Heartbeat – iteration {Count}", _iterationCount);

                    var plcResults = ReadAllPlcs();
                    var timestamp = DateTime.Now;

                    if (!plcResults.TryGetValue("Data", out var dataObj)
                        || dataObj is not Dictionary<string, object> combinedData)
                    {
                        _logger.LogWarning("[PlcService] No valid PLC data at {Time}", timestamp);
                        continue;
                    }

                    var dRaw = combinedData.GetValueOrDefault("D_RAW") as byte[] ?? Array.Empty<byte>();
                    var wRaw = combinedData.GetValueOrDefault("W_RAW") as bool[] ?? Array.Empty<bool>();

                    // Build typed snapshot for all 26 machines
                    var snapshots = BuildSnapshots(dRaw, wRaw, timestamp);

                    await PersistSnapshotsAsync(snapshots, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PlcService] Error at iteration {Count}", _iterationCount);
                }
                finally
                {
                    _pollLock.Release();
                }

                await Task.Delay(PollDelayMs, stoppingToken);
            }

            _logger.LogWarning("[PlcService] Poll loop ended. Total iterations: {Count}", _iterationCount);
        }

        // ── Snapshot builder ───────────────────────────────────────────────────

        private static List<PlcSnapshot> BuildSnapshots(byte[] dRaw, bool[] wRaw, DateTime timestamp)
        {
            var list = new List<PlcSnapshot>(MachineCount);

            for (int i = 0; i < MachineCount; i++)
            {
                int dOffset = i * 500;
                int wOffset = i * 3;

                list.Add(new PlcSnapshot
                {
                    id_machine = i + 1,
                    time = timestamp,

                    // D-memory (integers / floats / strings)
                    shot = Math.Min(ReadIntFromD(dRaw, 30 + dOffset), 10_000),
                    shot_accum = Math.Min(ReadIntFromD(dRaw, 32 + dOffset), 10_000),
                    act_ct = Math.Min(ReadFloatFromD(dRaw, 60 + dOffset), 1_000f),
                    mould_category_no = Math.Min(ReadIntFromD(dRaw, 90 + dOffset), 10),
                    stop_category = ReadStringFromD(dRaw, 300 + dOffset),
                    remark = ReadStringFromD(dRaw, 400 + dOffset),

                    reject_panelling = Math.Min(ReadFloatFromD(dRaw, 250 + dOffset), 10_000f),
                    reject_lumpy = Math.Min(ReadFloatFromD(dRaw, 255 + dOffset), 10_000f),
                    reject_black_dot = Math.Min(ReadFloatFromD(dRaw, 260 + dOffset), 10_000f),
                    reject_burst = Math.Min(ReadFloatFromD(dRaw, 265 + dOffset), 10_000f),
                    reject_startup = Math.Min(ReadFloatFromD(dRaw, 270 + dOffset), 10_000f),
                    reject_preform = Math.Min(ReadFloatFromD(dRaw, 275 + dOffset), 10_000f),
                    reject_purging = Math.Min(ReadFloatFromD(dRaw, 280 + dOffset), 10_000f),
                    reject_others = Math.Min(ReadFloatFromD(dRaw, 285 + dOffset), 10_000f),

                    // W-memory (bits)
                    status_start = ReadBitFromW(wRaw, (ushort)(0 + wOffset), 0),
                    status_off = ReadBitFromW(wRaw, (ushort)(0 + wOffset), 1),
                    production_running = ReadBitFromW(wRaw, (ushort)(0 + wOffset), 2),
                    visual_qc = ReadBitFromW(wRaw, (ushort)(0 + wOffset), 3),
                    done = ReadBitFromW(wRaw, (ushort)(0 + wOffset), 4),
                    remark_signal = ReadBitFromW(wRaw, (ushort)(0 + wOffset), 5),
                    reject_signal = ReadBitFromW(wRaw, (ushort)(0 + wOffset), 6),

                    util_barrel = ReadBitFromW(wRaw, (ushort)(1 + wOffset), 0),
                    util_hyd_motor = ReadBitFromW(wRaw, (ushort)(1 + wOffset), 1),
                    util_dehumidifier = ReadBitFromW(wRaw, (ushort)(1 + wOffset), 2),
                    util_chiller = ReadBitFromW(wRaw, (ushort)(1 + wOffset), 3),
                    util_material = ReadBitFromW(wRaw, (ushort)(1 + wOffset), 4),
                    util_dry_cycle = ReadBitFromW(wRaw, (ushort)(1 + wOffset), 5),
                });
            }

            return list;
        }

        // ── Database persistence ───────────────────────────────────────────────

        private async Task PersistSnapshotsAsync(List<PlcSnapshot> snapshots, CancellationToken ct)
        {
            // Create a short-lived scope per poll tick so DbContext/connections are disposed promptly
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
                    _logger.LogError(ex, "[PlcService] DB persist failed for machine {Id}", snapshot.id_machine);
                }
            }
        }

        // ── Public read interface ──────────────────────────────────────────────

        public Dictionary<string, object> ReadAllPlcs()
        {
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(ReadKey, out plc) || plc == null)
                {
                    plc = new PlcOmron(MasterIp, 9600, false, 80, 136);
                    _plcConnections[ReadKey] = plc;
                }

                plc.Connect();

                // Triple-read with majority vote for signal consistency
                if (!TryReadSnapshot(plc, out var d1, out var w1))
                {
                    _logger.LogWarning("[PlcService] Read attempt 1 failed");
                    return [];
                }

                if (!TryReadSnapshot(plc, out var d2, out var w2))
                {
                    _logger.LogWarning("[PlcService] Read attempt 2 failed");
                    return [];
                }

                if (AreSignalsConsistent(w1, w2, d1, d2))
                    return BuildResult(d2, w2);

                _logger.LogWarning("[PlcService] Inconsistency between reads 1 & 2 — tiebreaker read");

                if (!TryReadSnapshot(plc, out var d3, out var w3))
                {
                    _logger.LogWarning("[PlcService] Read attempt 3 failed — using read 2");
                    return BuildResult(d2, w2);
                }

                if (AreSignalsConsistent(w2, w3, d2, d3)) return BuildResult(d3, w3);
                if (AreSignalsConsistent(w1, w3, d1, d3)) return BuildResult(d3, w3);

                _logger.LogWarning("[PlcService] All 3 reads inconsistent — skipping iteration");
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlcService] Critical PLC error");
                if (plc != null)
                {
                    _plcConnections.TryRemove(ReadKey, out _);
                    try { plc.Disconnect(); } catch { /* ignore */ }
                }
                return [];
            }
        }

        public Dictionary<string, object?> ReadSubPlcSignals(int machineId)
        {
            var ip = $"172.17.86.{219 + machineId}";
            var cacheKey = $"sub_{ip}";
            var result = new Dictionary<string, object?>();
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(cacheKey, out plc) || plc == null)
                {
                    byte remoteNode = (byte)(219 + machineId);
                    plc = new PlcOmron(ip, 9600, false, remoteNode, 136);
                    _plcConnections[cacheKey] = plc;
                }
                plc.Connect();

                // D Memory: words 48–775
                const ushort dStart = 48;
                const ushort dEnd = 775;
                const int totalDWords = dEnd - dStart + 1;
                const int maxChunk = 500;

                byte[] dBuf = new byte[totalDWords * 2];
                bool dOk = true;

                for (int offset = 0; offset < totalDWords && dOk; offset += maxChunk)
                {
                    ushort chunkStart = (ushort)(dStart + offset);
                    ushort chunkSize = (ushort)Math.Min(maxChunk, totalDWords - offset);
                    try
                    {
                        byte[] chunk = plc.Read(chunkStart, chunkSize, 0, MemoryAreaBits.DataMemory);
                        Buffer.BlockCopy(chunk, 0, dBuf, offset * 2, chunk.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[SubPlc M{Id}] D read failed at offset {Offset}: {Msg}", machineId, offset, ex.Message);
                        dOk = false;
                    }
                }

                if (dOk)
                {
                    int Didx(int wordAddr) => (wordAddr - dStart) * 2;

                    // Production
                    result["D48"] = ReadIntAt(dBuf, Didx(48));        // Shot
                    result["D50"] = ReadIntAt(dBuf, Didx(50));        // Shot Accum
                    result["D90"] = ReadFloatAt(dBuf, Didx(90));      // Cycle Time (float)

                    // Strings
                    result["D200"] = ReadStringAt(dBuf, Didx(200));    // Type
                    result["D300"] = ReadStringAt(dBuf, Didx(300));    // Packer
                    result["D400"] = ReadStringAt(dBuf, Didx(400));    // Stop Category
                    result["D500"] = ReadStringAt(dBuf, Didx(500));    // Remark

                    // Reject (pcs)
                    result["D700"] = ReadFloatAt(dBuf, Didx(700));
                    result["D705"] = ReadFloatAt(dBuf, Didx(705));
                    result["D710"] = ReadFloatAt(dBuf, Didx(710));
                    result["D715"] = ReadFloatAt(dBuf, Didx(715));
                    result["D720"] = ReadFloatAt(dBuf, Didx(720));
                    result["D725"] = ReadFloatAt(dBuf, Didx(725));
                    result["D730"] = ReadFloatAt(dBuf, Didx(730));
                    result["D735"] = ReadFloatAt(dBuf, Didx(735));

                    // Reject (kg)
                    result["D740"] = ReadFloatAt(dBuf, Didx(740));
                    result["D745"] = ReadFloatAt(dBuf, Didx(745));
                    result["D750"] = ReadFloatAt(dBuf, Didx(750));
                    result["D755"] = ReadFloatAt(dBuf, Didx(755));
                    result["D760"] = ReadFloatAt(dBuf, Didx(760));
                    result["D765"] = ReadFloatAt(dBuf, Didx(765));
                    result["D770"] = ReadFloatAt(dBuf, Didx(770));
                    result["D775"] = ReadFloatAt(dBuf, Didx(775));
                }

                // W Memory bits: words 20–65
                try
                {
                    const ushort wStart = 20;
                    const ushort wEnd = 65;
                    ushort wBitCount = (ushort)((wEnd - wStart + 1) * 16);
                    byte[] wChunk = plc.Read(wStart, wBitCount, 0, MemoryAreaBits.Work);

                    bool Wbit(int word, int bit)
                    {
                        int idx = (word - wStart) * 16 + bit;
                        return idx >= 0 && idx < wChunk.Length && wChunk[idx] != 0;
                    }

                    // Status bits (W20)
                    result["W20.00"] = Wbit(20, 0);   // Status Start
                    result["W20.01"] = Wbit(20, 1);   // Status Off
                    result["W20.02"] = Wbit(20, 2);   // Prod Running
                    result["W20.03"] = Wbit(20, 3);   // Visual QC
                    result["W20.05"] = Wbit(20, 5);   // Remark Signal
                    result["W20.06"] = Wbit(20, 6);   // Reject Signal

                    // Utility bits
                    result["W60.00"] = Wbit(60, 0);   // Barrel
                    result["W61.00"] = Wbit(61, 0);   // Hyd. Motor
                    result["W62.00"] = Wbit(62, 0);   // Dehumidifier
                    result["W63.00"] = Wbit(63, 0);   // Dehumidifier Switch
                    result["W63.01"] = Wbit(63, 1);   // Chiller
                    result["W64.00"] = Wbit(64, 0);   // Material
                    result["W65.00"] = Wbit(65, 0);   // Dry Cycle
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[SubPlc M{Id}] W read failed: {Msg}", machineId, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SubPlc M{Id}] Critical error", machineId);
                if (plc != null)
                {
                    _plcConnections.TryRemove(cacheKey, out _);
                    try { plc.Disconnect(); } catch { /* ignore */ }
                }
                throw;
            }

            return result;
        }

        // ── Public write interface ─────────────────────────────────────────────

        public void UpdatePLCS(dynamic master)
        {
            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                int baseOffset = (master.id_machine - 1) * 500;
                int ipNode = 220 + (master.id_machine - 1);
                WriteIntOmron(plc, 10, ipNode);

                ushort addrPartWeight = (ushort)(525 + baseOffset);
                WriteFloatOmron(plc, 25, master.part_weight);
                WriteFloatOmron(plc, addrPartWeight, master.part_weight);

                ushort addrType = (ushort)(600 + baseOffset);
                WriteStringOmron(plc, 100, master.type);
                WriteStringOmron(plc, addrType, master.type);

                ushort addrPacker = (ushort)(700 + baseOffset);
                WriteStringOmron(plc, 200, master.packer);
                WriteStringOmron(plc, addrPacker, master.packer);

                WriteBoolOmron(plc, 4, 0, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlcService] UpdatePLCS failed");
            }
        }

        public void UpdatePacker(dynamic staffList)
        {
            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                foreach (var staff in staffList)
                {
                    int baseOffset = (staff.id_machine - 1) * 500;
                    int ipNode = 220 + (staff.id_machine - 1);
                    WriteIntOmron(plc, 10, ipNode);
                    ushort addrPacker = (ushort)(700 + baseOffset);
                    WriteStringOmron(plc, 200, staff.packer);
                    WriteStringOmron(plc, addrPacker, staff.packer);
                    WriteBoolOmron(plc, 4, 8, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlcService] UpdatePacker failed");
            }
        }

        public void UpdateMeasureQC(machine_master master)
        {
            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                int baseOffset = (master.id_machine - 1) * 3;
                int ipNode = 220 + (master.id_machine - 1);
                WriteIntOmron(plc, 20, ipNode);
                ushort addrMeasureQC = (ushort)(7 + baseOffset);
                WriteBoolOmron(plc, 3, 0, master.measure_qc);
                WriteBoolOmron(plc, addrMeasureQC, 0, master.measure_qc);
                WriteBoolOmron(plc, 3, 1, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlcService] UpdateMeasureQC failed");
            }
        }

        public void ChangePassword(Dictionary<string, int> passwords)
        {
            var addressMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                ["maintenance"] = 10,
                ["technician"] = 12,
                ["production"] = 14,
            };

            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                foreach (var (department, password) in passwords)
                {
                    if (addressMap.TryGetValue(department, out ushort address))
                        WriteHolding(plc, address, password);
                }

                WriteBoolOmron(plc, wordAddress: 1, bit: 1, value: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlcService] ChangePassword failed");
            }
        }

        // ── Private helpers: PLC read ──────────────────────────────────────────

        private PlcOmron GetWritePlc()
        {
            if (!_plcConnections.TryGetValue(WriteKey, out var plc) || plc == null)
            {
                plc = new PlcOmron(MasterIp, 9600, false, 80, 1);
                _plcConnections[WriteKey] = plc;
            }
            return plc;
        }

        private bool TryReadSnapshot(PlcOmron plc, out byte[] dBuffer, out bool[] wBits)
        {
            dBuffer = null!;
            wBits = null!;

            // D area: words 500–13500
            const ushort dStart = 500;
            const ushort dEnd = 13500;
            const int totalDWords = dEnd - dStart + 1;
            const int maxDWords = 500;

            byte[] buffer = new byte[totalDWords * 2];

            for (int offset = 0; offset < totalDWords; offset += maxDWords)
            {
                ushort chunkStart = (ushort)(dStart + offset);
                ushort chunkSize = (ushort)Math.Min(maxDWords, totalDWords - offset);
                int expectedBytes = chunkSize * 2;

                try
                {
                    byte[] chunk = plc.Read(chunkStart, chunkSize, 0, MemoryAreaBits.DataMemory);
                    if (chunk == null || chunk.Length < expectedBytes)
                    {
                        _logger.LogWarning("[PlcService] D short read at offset {Offset}: expected {Exp}, got {Got}",
                            offset, expectedBytes, chunk?.Length ?? 0);
                        return false;
                    }
                    Buffer.BlockCopy(chunk, 0, buffer, offset * 2, chunk.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[PlcService] D read failed at offset {Offset}: {Msg}", offset, ex.Message);
                    return false;
                }
            }

            // W area: words 5–81
            const ushort wStart = 5;
            const ushort wEnd = 81;
            int totalWWords = wEnd - wStart + 1;
            int totalBits = totalWWords * 16;
            bool[] bits = new bool[totalBits];

            try
            {
                byte[] chunk = plc.Read(wStart, (ushort)totalBits, 0, MemoryAreaBits.Work);
                if (chunk == null || chunk.Length < totalBits)
                {
                    _logger.LogWarning("[PlcService] W short read: expected {Exp}, got {Got}",
                        totalBits, chunk?.Length ?? 0);
                    return false;
                }
                for (int i = 0; i < totalBits; i++)
                    bits[i] = chunk[i] != 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[PlcService] W read failed: {Msg}", ex.Message);
                return false;
            }

            dBuffer = buffer;
            wBits = bits;
            return true;
        }

        private bool AreSignalsConsistent(bool[] w1, bool[] w2, byte[] d1, byte[] d2)
        {
            for (int i = 0; i < MachineCount; i++)
            {
                int wOffset = i * 3;
                int dOffset = i * 500;

                // Stable W bits
                if (ReadBit(w1, wOffset, 0) != ReadBit(w2, wOffset, 0)) return false; // status_start
                if (ReadBit(w1, wOffset, 1) != ReadBit(w2, wOffset, 1)) return false; // status_off
                if (ReadBit(w1, wOffset, 5) != ReadBit(w2, wOffset, 5)) return false; // remark_signal
                if (ReadBit(w1, wOffset, 6) != ReadBit(w2, wOffset, 6)) return false; // reject_signal
                if (ReadBit(w1, wOffset + 1, 0) != ReadBit(w2, wOffset + 1, 0)) return false; // util_barrel
                if (ReadBit(w1, wOffset + 1, 1) != ReadBit(w2, wOffset + 1, 1)) return false; // util_hyd_motor
                if (ReadBit(w1, wOffset + 1, 2) != ReadBit(w2, wOffset + 1, 2)) return false; // util_dehumidifier
                if (ReadBit(w1, wOffset + 1, 3) != ReadBit(w2, wOffset + 1, 3)) return false; // util_chiller
                if (ReadBit(w1, wOffset + 1, 4) != ReadBit(w2, wOffset + 1, 4)) return false; // util_material
                if (ReadBit(w1, wOffset + 1, 5) != ReadBit(w2, wOffset + 1, 5)) return false; // util_dry_cycle

                // Stop-category string
                if (ReadString(d1, 300 + dOffset) != ReadString(d2, 300 + dOffset)) return false;
            }
            return true;
        }

        // ── Private helpers: memory parsing ────────────────────────────────────

        private static int ReadIntFromD(byte[] buf, int wordIndex)
        {
            int byteIndex = wordIndex * 2;
            if (byteIndex + 4 > buf.Length) return 0;
            byte[] r = { buf[byteIndex + 1], buf[byteIndex], buf[byteIndex + 3], buf[byteIndex + 2] };
            return BitConverter.ToInt32(r, 0);
        }

        private static float ReadFloatFromD(byte[] buf, int wordIndex)
        {
            int byteIndex = wordIndex * 2;
            if (byteIndex + 4 > buf.Length) return 0f;
            byte[] r = { buf[byteIndex + 1], buf[byteIndex], buf[byteIndex + 3], buf[byteIndex + 2] };
            return BitConverter.ToSingle(r, 0);
        }

        private static string ReadStringFromD(byte[] buf, int wordIndex)
        {
            int byteIndex = wordIndex * 2;
            if (byteIndex >= buf.Length || buf[byteIndex] == 0) return string.Empty;
            return Encoding.ASCII.GetString(buf, byteIndex, 100).Trim('\0', ' ');
        }

        private static bool ReadBitFromW(bool[] buf, ushort wordIndex, int bit)
        {
            int index = wordIndex * 16 + bit;
            return index >= 0 && index < buf.Length && buf[index];
        }

        private static bool ReadBit(bool[] buf, int wordIndex, int bit)
        {
            int index = wordIndex * 16 + bit;
            return index >= 0 && index < buf.Length && buf[index];
        }

        private static string ReadString(byte[] buf, int wordIndex)
        {
            int byteIndex = wordIndex * 2;
            if (byteIndex >= buf.Length || buf[byteIndex] == 0) return string.Empty;
            return Encoding.ASCII.GetString(buf, byteIndex, 100).Trim('\0', ' ');
        }

        private static float ReadFloatAt(byte[] buf, int byteIndex)
        {
            if (byteIndex + 4 > buf.Length) return 0f;
            byte[] r = { buf[byteIndex + 1], buf[byteIndex], buf[byteIndex + 3], buf[byteIndex + 2] };
            return BitConverter.ToSingle(r, 0);
        }

        private static int ReadIntAt(byte[] buf, int byteIndex)
        {
            if (byteIndex + 4 > buf.Length) return 0;
            byte[] r = { buf[byteIndex + 1], buf[byteIndex], buf[byteIndex + 3], buf[byteIndex + 2] };
            return BitConverter.ToInt32(r, 0);
        }

        private static string ReadStringAt(byte[] buf, int byteIndex, int maxBytes = 200)
        {
            if (byteIndex >= buf.Length || buf[byteIndex] == 0) return string.Empty;
            int available = Math.Min(maxBytes, buf.Length - byteIndex);
            return Encoding.ASCII.GetString(buf, byteIndex, available).Trim('\0', ' ');
        }

        private static Dictionary<string, object> BuildResult(byte[] dBuffer, bool[] wBits)
            => new() { ["Data"] = new Dictionary<string, object> { ["D_RAW"] = dBuffer, ["W_RAW"] = wBits } };

        // ── Private helpers: PLC write ─────────────────────────────────────────

        private static bool WriteIntOmron(PlcOmron plc, ushort address, int value)
        {
            var b = BitConverter.GetBytes(value);
            return plc.Write(address, new byte[] { b[1], b[0], b[3], b[2] }, 0, 2, MemoryAreaBits.DataMemory);
        }

        private static bool WriteFloatOmron(PlcOmron plc, ushort address, float value)
        {
            var b = BitConverter.GetBytes(value);
            return plc.Write(address, new byte[] { b[1], b[0], b[3], b[2] }, 0, 2, MemoryAreaBits.DataMemory);
        }

        private static bool WriteStringOmron(PlcOmron plc, ushort address, string value, int maxLength = 100)
        {
            var stringBytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            var bytes = new byte[maxLength];
            Array.Copy(stringBytes, bytes, Math.Min(stringBytes.Length, maxLength));
            if (bytes.Length % 2 != 0) Array.Resize(ref bytes, bytes.Length + 1);
            return plc.Write(address, bytes, 0, (ushort)(bytes.Length / 2), MemoryAreaBits.DataMemory);
        }

        private static bool WriteBoolOmron(PlcOmron plc, ushort wordAddress, byte bit, bool value)
            => plc.Write(wordAddress, new byte[] { (byte)(value ? 1 : 0) }, bit, 1, MemoryAreaBits.Work);

        private static bool WriteHolding(PlcOmron plc, ushort address, int value)
        {
            var b = BitConverter.GetBytes(value);
            return plc.Write(address, new byte[] { b[1], b[0], b[3], b[2] }, 0, 2, (MemoryAreaBits)0xB2);
        }
    }

    // ── Typed snapshot record (replaces anonymous object) ─────────────────────

    /// <summary>
    /// Strongly typed PLC data snapshot for a single machine.
    /// Passed directly to BaseService.insertMachineMaster() so dynamic dispatch
    /// still works via the existing service signature.
    /// </summary>
    public sealed record PlcSnapshot
    {
        public int id_machine { get; init; }
        public DateTime time { get; init; }
        public int shot { get; init; }
        public int shot_accum { get; init; }
        public float act_ct { get; init; }
        public int mould_category_no { get; init; }
        public string stop_category { get; init; } = string.Empty;
        public string remark { get; init; } = string.Empty;
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
        public bool visual_qc { get; init; }
        public bool done { get; init; }
        public bool remark_signal { get; init; }
        public bool reject_signal { get; init; }
        public bool util_barrel { get; init; }
        public bool util_hyd_motor { get; init; }
        public bool util_dehumidifier { get; init; }
        public bool util_chiller { get; init; }
        public bool util_material { get; init; }
        public bool util_dry_cycle { get; init; }
    }
}