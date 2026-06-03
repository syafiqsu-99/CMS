using CMS.Server.Models;
using PLC_Omron_Standard;
using PLC_Omron_Standard.Enums;
using System.Collections.Concurrent;
using System.Text;
using System.Xml.Linq;

namespace CMS.Server.Services
{
    public sealed class PlcService : BackgroundService
    {
        private static readonly ConcurrentDictionary<string, PlcOmron> _plcConnections = new();

        private readonly SemaphoreSlim _pollLock = new(1, 1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PlcService> _logger;
        private readonly IHostEnvironment _env;

        private const string MasterIp = "172.17.86.80";
        private const string ReadKey = MasterIp + "_read";
        private const string WriteKey = MasterIp + "_write";
        private const int PollDelayMs = 200;

        private IReadOnlyList<(int id, string name, string ip)> _cachedMachines = [];
        private DateTime _machinesCachedAt = DateTime.MinValue;
        private readonly SemaphoreSlim _machineCacheLock = new(1, 1);
        private const int MachineCacheMinutes = 5;

        private readonly ConcurrentDictionary<int, Dictionary<string, object?>?> _subPlcCache = new();
        private readonly ConcurrentDictionary<int, bool> _subPlcOnline = new();
        private const int SubPlcSweepDelayMs = 5000;

        private int _iterationCount;

        // ── Sub-PLC D memory layout ────────────────────────────────────────────
        private const ushort SubDStart = 10;
        private const ushort SubDEnd = 776;
        private const int SubDTotal = SubDEnd - SubDStart + 1; // 767 words

        // ── Sub-PLC W memory layout ────────────────────────────────────────────
        private const ushort SubWStart = 0;
        private const int SubWTotalWords = 78;   // W0–W77
        private const int SubWTotalBits = SubWTotalWords * 16; // 1248

        // ── Sub-PLC H memory layout ────────────────────────────────────────────
        private const ushort SubHStart = 0;
        private const int SubHTotal = 38; // H0–H37

        public PlcService(IServiceScopeFactory scopeFactory, ILogger<PlcService> logger, IHostEnvironment env)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _env = env;
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[PlcService] Running Master and Sub-PLC sweeps.");

            await Task.WhenAll(
                RunMasterPlcLoopAsync(stoppingToken),
                RunSubPlcSweepLoopAsync(stoppingToken)
            );

            _logger.LogWarning("[PlcService] All loops exited.");
        }

        // ── Master PLC loop ────────────────────────────────────────────────────

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
                    if (machines.Count == 0)
                    {
                        continue;
                    }

                    var plcResults = ReadAllPlcs(machines.Count);
                    var timestamp = DateTime.Now;

                    if (!plcResults.TryGetValue("Data", out var dataObj)
                        || dataObj is not Dictionary<string, object> combinedData)
                    {
                        if (_iterationCount % 25 == 0)
                            _logger.LogWarning("[PlcService] No valid Master PLC data at {Time}", timestamp);

                        continue;
                    }

                    var dRaw = combinedData.GetValueOrDefault("D_RAW") as byte[] ?? Array.Empty<byte>();
                    var wRaw = combinedData.GetValueOrDefault("W_RAW") as bool[] ?? Array.Empty<bool>();

                    var snapshots = BuildSnapshots(dRaw, wRaw, timestamp, machines.Count);

                    // Debug
                    var m0Snapshot = snapshots.FirstOrDefault(s => s.id_machine == 0);
                    if (m0Snapshot != null)
                    {
                        DebugMasterPlc(m0Snapshot);
                    }

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

            _logger.LogWarning("[PlcService] Master poll loop ended. Total iterations: {Count}", _iterationCount);
        }

        // ── Sub-PLC sweep loop ─────────────────────────────────────────────────

        private async Task RunSubPlcSweepLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var machines = await GetCachedMachinesAsync();

                    foreach (var machine in machines)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        try
                        {
                            var data = await Task.Run(() => ReadSubPlcSignals(machine.id), stoppingToken);
                            data["machine_name"] = machine.name;
                            _subPlcCache[machine.id] = data;
                            _subPlcOnline[machine.id] = true;
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("[SubPlc M{Id}] Sweep read failed: {Msg}", machine.id, ex.Message);
                            _subPlcOnline[machine.id] = false;
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PlcService] Sub-PLC sweep loop error");
                }

                await Task.Delay(SubPlcSweepDelayMs, stoppingToken).ConfigureAwait(false);
            }

            _logger.LogWarning("[PlcService] Sub-PLC sweep loop ended.");
        }

        // ── Sub-PLC cache read ─────────────────────────────────────────────────

        public Dictionary<string, object?> GetSubPlcSignalCache()
        {
            var result = new Dictionary<string, object?>();

            foreach (var (id, data) in _subPlcCache)
            {
                result[id.ToString()] = new Dictionary<string, object?>
                {
                    ["data"] = data,
                    ["online"] = _subPlcOnline.GetValueOrDefault(id, false),
                };
            }

            foreach (var machine in _cachedMachines)
            {
                if (!result.ContainsKey(machine.id.ToString()))
                    result[machine.id.ToString()] = null;
            }

            return result;
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

        // ── Snapshot builder (Master PLC → per-machine data) ──────────────────

        private static List<PlcSnapshot> BuildSnapshots(byte[] dRaw, bool[] wRaw, DateTime timestamp, int machineCount)
        {
            const int dPerMachine = 500;
            const int wPerMachine = 3;

            int Didx(int machineIdx, int absoluteWord)
                => (machineIdx * dPerMachine + (absoluteWord - 500)) * 2;

            int Widx(int machineIdx, int wordOffset, int bit)
                => (machineIdx * wPerMachine + wordOffset) * 16 + bit;

            bool Wbit(bool[] buf, int idx)
                => idx >= 0 && idx < buf.Length && buf[idx];

            var list = new List<PlcSnapshot>(machineCount);

            for (int m = 0; m < machineCount; m++)
            {
                list.Add(new PlcSnapshot
                {
                    id_machine = m,
                    time = timestamp,

                    visual_qc = Math.Min(ReadIntAt(dRaw, Didx(m, 510)), 10_000),
                    measure_qc = Math.Min(ReadIntAt(dRaw, Didx(m, 512)), 10_000),
                    part_weight = Math.Min(ReadFloatAt(dRaw, Didx(m, 525)), 1_000f),
                    shot = Math.Min(ReadIntAt(dRaw, Didx(m, 530)), 10_000),
                    shot_accum = Math.Min(ReadIntAt(dRaw, Didx(m, 532)), 10_000),
                    act_ct = Math.Min(ReadFloatAt(dRaw, Didx(m, 560)), 1_000f),
                    mould_category_no = Math.Min(ReadIntAt(dRaw, Didx(m, 590)), 10),
                    type = ReadStringAt(dRaw, Didx(m, 600), 100),
                    packer= ReadStringAt(dRaw, Didx(m, 700), 100),
                    stop_category = ReadStringAt(dRaw, Didx(m, 800), 100),
                    remark = ReadStringAt(dRaw, Didx(m, 900), 100),

                    reject_panelling = Math.Min(ReadFloatAt(dRaw, Didx(m, 750)), 10_000f),
                    reject_lumpy = Math.Min(ReadFloatAt(dRaw, Didx(m, 755)), 10_000f),
                    reject_black_dot = Math.Min(ReadFloatAt(dRaw, Didx(m, 760)), 10_000f),
                    reject_burst = Math.Min(ReadFloatAt(dRaw, Didx(m, 765)), 10_000f),
                    reject_startup = Math.Min(ReadFloatAt(dRaw, Didx(m, 770)), 10_000f),
                    reject_preform = Math.Min(ReadFloatAt(dRaw, Didx(m, 775)), 10_000f),
                    reject_purging = Math.Min(ReadFloatAt(dRaw, Didx(m, 780)), 10_000f),
                    reject_others = Math.Min(ReadFloatAt(dRaw, Didx(m, 785)), 10_000f),

                    status_start = Wbit(wRaw, Widx(m, 0, 0)),
                    status_off = Wbit(wRaw, Widx(m, 0, 1)),
                    production_running = Wbit(wRaw, Widx(m, 0, 2)),
                    qc_signal = Wbit(wRaw, Widx(m, 0, 3)),
                    done = Wbit(wRaw, Widx(m, 0, 4)),
                    remark_signal = Wbit(wRaw, Widx(m, 0, 5)),
                    reject_signal = Wbit(wRaw, Widx(m, 0, 6)),

                    util_barrel = Wbit(wRaw, Widx(m, 1, 0)),
                    util_hyd_motor = Wbit(wRaw, Widx(m, 1, 1)),
                    util_dehumidifier = Wbit(wRaw, Widx(m, 1, 2)),
                    util_chiller = Wbit(wRaw, Widx(m, 1, 3)),
                    util_material = Wbit(wRaw, Widx(m, 1, 4)),
                    util_dry_cycle = Wbit(wRaw, Widx(m, 1, 5)),

                    qc_mea_signal = Wbit(wRaw, Widx(m, 2, 0)),
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
                    _logger.LogError(ex, "[PlcService] DB persist failed for machine {Id}", snapshot.id_machine);
                }
            }
        }

        // ── Master PLC read ────────────────────────────────────────────────────

        public Dictionary<string, object> ReadAllPlcs(int machineCount)
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

                if (!TryReadSnapshot(plc, machineCount, out var d1, out var w1))
                {
                    _logger.LogWarning("[PlcService] Read attempt 1 failed");
                    return [];
                }

                if (!TryReadSnapshot(plc, machineCount, out var d2, out var w2))
                {
                    _logger.LogWarning("[PlcService] Read attempt 2 failed");
                    return [];
                }

                if (AreSignalsConsistent(w1, w2, d1, d2, machineCount))
                    return BuildResult(d2, w2);

                _logger.LogWarning("[PlcService] Inconsistency between reads 1 & 2 — tiebreaker read");

                if (!TryReadSnapshot(plc, machineCount, out var d3, out var w3))
                {
                    _logger.LogWarning("[PlcService] Read attempt 3 failed — using read 2");
                    return BuildResult(d2, w2);
                }

                if (AreSignalsConsistent(w2, w3, d2, d3, machineCount)) return BuildResult(d3, w3);
                if (AreSignalsConsistent(w1, w3, d1, d3, machineCount)) return BuildResult(d3, w3);

                _logger.LogWarning("[PlcService] All 3 reads inconsistent — skipping iteration");
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlcService] Critical PLC error");
                if (plc != null)
                {
                    _plcConnections.TryRemove(ReadKey, out _);
                    try { plc.Disconnect(); } catch { }
                }
                return [];
            }
        }

        // ── Sub-PLC read ───────────────────────────────────────────────────────

        public Dictionary<string, object?> ReadSubPlcSignals(int id_machine)
        {
            var ip = $"172.17.86.{220 + id_machine}";
            var cacheKey = $"sub_{ip}";
            var result = new Dictionary<string, object?>();
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(cacheKey, out plc) || plc == null)
                {
                    byte remoteNode = (byte)(220 + id_machine);
                    plc = new PlcOmron(ip, 9600, false, remoteNode, 136);
                    _plcConnections[cacheKey] = plc;
                }

                plc.Connect();

                // 1. PING Check (Avoid spamming short reads if offline)
                try
                {
                    var ping = plc.Read(SubDStart, 1, 0, MemoryAreaBits.DataMemory);
                    if (ping == null || ping.Length < 2 || (ping.Length == 2 && ping[0] == 0x00 && ping[1] == 0xF5))
                    {
                        // _logger.LogWarning("[SubPlc M{Id}] Unreachable/Offline. Skipping reading.", id_machine);
                        return result; // Empty result safely ignores the data read
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[SubPlc M{Id}] Unreachable ({Msg}). Skipping reading.", id_machine, ex.Message);
                    return result; // Empty result
                }

                // ── D Memory ──────────────────────────────────────────────────────
                byte[] dBuf = new byte[SubDTotal * 2];
                bool dOk = true;
                const int maxChunk = 400;

                for (int offset = 0; offset < SubDTotal && dOk; offset += maxChunk)
                {
                    ushort chunkStart = (ushort)(SubDStart + offset);
                    ushort chunkSize = (ushort)Math.Min(maxChunk, SubDTotal - offset);
                    int expected = chunkSize * 2;

                    try
                    {
                        byte[] chunk = plc.Read(chunkStart, chunkSize, 0, MemoryAreaBits.DataMemory);

                        if (chunk == null || chunk.Length < expected)
                        {
                            string extraInfo = chunk?.Length == 2 ? $" (FINS Error Code: {BitConverter.ToString(chunk)})" : "";
                            _logger.LogWarning(
                                "[SubPlc M{Id}] D short read at D{Addr}: expected {Exp} bytes, got {Got}{Extra}",
                                id_machine, chunkStart, expected, chunk?.Length ?? 0, extraInfo);
                            dOk = false;
                            break;
                        }

                        Buffer.BlockCopy(chunk, 0, dBuf, offset * 2, chunk.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[SubPlc M{Id}] D read failed at D{Addr}: {Msg}",
                            id_machine, chunkStart, ex.Message);
                        dOk = false;
                    }
                }

                if (dOk)
                {
                    int Bidx(int dWord) => (dWord - SubDStart) * 2;

                    // Production counters
                    result["D10"] = SubReadInt(dBuf, Bidx(10));
                    result["D12"] = SubReadInt(dBuf, Bidx(12));
                    result["D35"] = SubReadFloat(dBuf, Bidx(35));
                    result["D40"] = SubReadInt(dBuf, Bidx(40));
                    result["D42"] = SubReadInt(dBuf, Bidx(42));
                    result["D44"] = SubReadFloat(dBuf, Bidx(44));
                    result["D48"] = SubReadInt(dBuf, Bidx(48));
                    result["D50"] = SubReadInt(dBuf, Bidx(50));
                    result["D70"] = SubReadFloat(dBuf, Bidx(70));
                    result["D80"] = SubReadFloat(dBuf, Bidx(80));
                    result["D90"] = SubReadFloat(dBuf, Bidx(90));

                    // Stop / Mould category
                    result["D110"] = SubReadInt(dBuf, Bidx(110));
                    result["D115"] = SubReadInt(dBuf, Bidx(115));
                    result["D120"] = SubReadInt(dBuf, Bidx(120));
                    result["D180"] = SubReadInt(dBuf, Bidx(180));
                    result["D190"] = SubReadInt(dBuf, Bidx(190));

                    // Strings 
                    result["D200"] = SubReadString(dBuf, Bidx(200), 100);
                    result["D300"] = SubReadString(dBuf, Bidx(300), 100);
                    result["D400"] = SubReadString(dBuf, Bidx(400), 100);
                    result["D500"] = SubReadString(dBuf, Bidx(500), 100);

                    // Reject kg — current shot
                    result["D600"] = SubReadFloat(dBuf, Bidx(600));
                    result["D605"] = SubReadFloat(dBuf, Bidx(605));
                    result["D610"] = SubReadFloat(dBuf, Bidx(610));
                    result["D615"] = SubReadFloat(dBuf, Bidx(615));
                    result["D620"] = SubReadFloat(dBuf, Bidx(620));
                    result["D625"] = SubReadFloat(dBuf, Bidx(625));
                    result["D630"] = SubReadFloat(dBuf, Bidx(630));
                    result["D635"] = SubReadFloat(dBuf, Bidx(635));
                    result["D640"] = SubReadFloat(dBuf, Bidx(640));
                    result["D645"] = SubReadFloat(dBuf, Bidx(645));
                    result["D650"] = SubReadFloat(dBuf, Bidx(650));
                    result["D655"] = SubReadFloat(dBuf, Bidx(655));
                    result["D660"] = SubReadFloat(dBuf, Bidx(660));
                    result["D665"] = SubReadFloat(dBuf, Bidx(665));
                    result["D670"] = SubReadFloat(dBuf, Bidx(670));
                    result["D675"] = SubReadFloat(dBuf, Bidx(675));

                    // Reject kg — total accumulated
                    result["D700"] = SubReadFloat(dBuf, Bidx(700));
                    result["D705"] = SubReadFloat(dBuf, Bidx(705));
                    result["D710"] = SubReadFloat(dBuf, Bidx(710));
                    result["D715"] = SubReadFloat(dBuf, Bidx(715));
                    result["D720"] = SubReadFloat(dBuf, Bidx(720));
                    result["D725"] = SubReadFloat(dBuf, Bidx(725));
                    result["D730"] = SubReadFloat(dBuf, Bidx(730));
                    result["D735"] = SubReadFloat(dBuf, Bidx(735));

                    // Reject pcs — accumulated
                    result["D740"] = SubReadFloat(dBuf, Bidx(740));
                    result["D745"] = SubReadFloat(dBuf, Bidx(745));
                    result["D750"] = SubReadFloat(dBuf, Bidx(750));
                    result["D755"] = SubReadFloat(dBuf, Bidx(755));
                    result["D760"] = SubReadFloat(dBuf, Bidx(760));
                    result["D765"] = SubReadFloat(dBuf, Bidx(765));
                    result["D770"] = SubReadFloat(dBuf, Bidx(770));
                    result["D775"] = SubReadFloat(dBuf, Bidx(775));

                    //DebugSubPlcD(id_machine, result);
                }

                // ── W Memory ───────────────────────────────────────────────────────
                byte[] wBuf = new byte[SubWTotalBits];
                bool wOk = true;
                // W MaxChunk set to exact multiple of 16 (480 bits = 30 words) ensuring bit offset is ALWAYS 0.
                const int maxWChunk = 480;

                for (int offset = 0; offset < SubWTotalBits && wOk; offset += maxWChunk)
                {
                    ushort chunkSize = (ushort)Math.Min(maxWChunk, SubWTotalBits - offset);
                    ushort chunkStartWord = (ushort)(SubWStart + (offset / 16));

                    try
                    {
                        byte[] chunk = plc.Read(chunkStartWord, chunkSize, 0, MemoryAreaBits.Work);

                        if (chunk == null || chunk.Length < chunkSize)
                        {
                            string extraInfo = chunk?.Length == 2 ? $" (FINS Error Code: {BitConverter.ToString(chunk)})" : "";
                            _logger.LogWarning(
                                "[SubPlc M{Id}] W short read at W{Word}.0: expected {Exp} bits, got {Got}{Extra}",
                                id_machine, chunkStartWord, chunkSize, chunk?.Length ?? 0, extraInfo);
                            wOk = false;
                            break;
                        }

                        Buffer.BlockCopy(chunk, 0, wBuf, offset, chunk.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[SubPlc M{Id}] W chunk read failed: {Msg}", id_machine, ex.Message);
                        wOk = false;
                    }
                }

                if (wOk)
                {
                    bool Wbit(int word, int bit)
                    {
                        int idx = (word - SubWStart) * 16 + bit;
                        return idx >= 0 && idx < wBuf.Length && wBuf[idx] != 0;
                    }

                    result["W0.00"] = Wbit(0, 0);
                    result["W1.00"] = Wbit(1, 0);
                    result["W2.00"] = Wbit(2, 0);
                    result["W3.00"] = Wbit(3, 0);
                    result["W4.00"] = Wbit(4, 0);
                    result["W5.00"] = Wbit(5, 0);
                    result["W5.01"] = Wbit(5, 1);
                    result["W6.00"] = Wbit(6, 0);
                    result["W7.00"] = Wbit(7, 0);
                    result["W8.00"] = Wbit(8, 0);
                    result["W9.00"] = Wbit(9, 0);

                    result["W20.00"] = Wbit(20, 0);
                    result["W20.01"] = Wbit(20, 1);
                    result["W20.02"] = Wbit(20, 2);
                    result["W20.03"] = Wbit(20, 3);
                    result["W20.04"] = Wbit(20, 4);
                    result["W20.05"] = Wbit(20, 5);
                    result["W20.06"] = Wbit(20, 6);

                    result["W30.00"] = Wbit(30, 0);

                    result["W60.00"] = Wbit(60, 0);
                    result["W60.01"] = Wbit(60, 1);
                    result["W60.02"] = Wbit(60, 2);
                    result["W60.03"] = Wbit(60, 3);
                    result["W60.04"] = Wbit(60, 4);
                    result["W60.05"] = Wbit(60, 5);

                    result["W70.00"] = Wbit(70, 0);
                    result["W71.00"] = Wbit(71, 0);
                    result["W72.00"] = Wbit(72, 0);
                    result["W73.00"] = Wbit(73, 0);
                    result["W74.00"] = Wbit(74, 0);
                    result["W75.00"] = Wbit(75, 0);
                    result["W76.00"] = Wbit(76, 0);
                    result["W77.00"] = Wbit(77, 0);

                    //DebugSubPlcW(id_machine, result);
                }

                // ── H Memory ───────────────────────────────────────────────────────
                try
                {
                    byte[] hBuf = plc.Read(SubHStart, (ushort)SubHTotal, 0, (MemoryAreaBits)0xB2);
                    int expectedH = SubHTotal * 2;

                    if (hBuf == null || hBuf.Length < expectedH)
                    {
                        string extraInfo = hBuf?.Length == 2 ? $" (FINS Error Code: {BitConverter.ToString(hBuf)})" : "";
                        _logger.LogWarning(
                            "[SubPlc M{Id}] H short read: expected {Exp} bytes, got {Got}{Extra}",
                            id_machine, expectedH, hBuf?.Length ?? 0, extraInfo);
                    }
                    else
                    {
                        int Hidx(int hWord) => hWord * 2;

                        result["H0"]  = SubReadFloat(hBuf, Hidx(0));
                        result["H5"]  = SubReadInt(hBuf, Hidx(5));
                        result["H10"] = SubReadFloat(hBuf, Hidx(10));
                        result["H15"] = SubReadString(hBuf, Hidx(15), 10);
                        result["H20"] = SubReadInt(hBuf, Hidx(20));
                        result["H30"] = SubReadInt(hBuf, Hidx(30));
                        result["H32"] = SubReadInt(hBuf, Hidx(32));
                        result["H34"] = SubReadInt(hBuf, Hidx(34));
                        result["H36"] = SubReadInt(hBuf, Hidx(36));

                        //DebugSubPlcH(id_machine, result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[SubPlc M{Id}] H read failed: {Msg}", id_machine, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SubPlc M{Id}] Critical error", id_machine);
                if (plc != null)
                {
                    _plcConnections.TryRemove(cacheKey, out _);
                    try { plc.Disconnect(); } catch { }
                }
                throw;
            }

            return result;
        }

        // ── Public write interface ─────────────────────────────────────────────

        public void UpdatePLCS(dynamic master)
        {
            Console.WriteLine($"[Machine {master.id_machine}] Mould Change");
            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                int id_machine = (int)master.id_machine;
                int ipNode = 220 + id_machine;
                ushort dBase = (ushort)(500 + id_machine * 500);

                WriteIntOmron(plc, 10, ipNode);

                ushort addrPartWeight = (ushort)(dBase + 25);
                WriteFloatOmron(plc, 25, master.part_weight);
                WriteFloatOmron(plc, addrPartWeight, master.part_weight);

                ushort addrType = (ushort)(dBase + 100);
                WriteStringOmron(plc, 100, master.type);
                WriteStringOmron(plc, addrType, master.type);

                ushort addrPacker = (ushort)(dBase + 200);
                WriteStringOmron(plc, 200, master.packer);
                WriteStringOmron(plc, addrPacker, master.packer);

                WriteBoolOmron(plc, 4, 0, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlcService] UpdatePLCS failed");
            }
        }

        public void UpdatePacker(dynamic master)
        {
            Console.WriteLine($"[Machine {master.id_machine}] Packer Change");
            PlcOmron? plc = null;
            try
            {
                plc = GetWritePlc();
                plc.Connect();

                foreach (var staff in master)
                {
                    int baseOffset = staff.id_machine * 500;
                    int ipNode = 220 + staff.id_machine;
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

                int id_machine = master.id_machine;
                int ipNode = 220 + id_machine;
                ushort dBase = (ushort)(500 + id_machine * 500);

                WriteIntOmron(plc, 20, ipNode);
                WriteIntOmron(plc, 27, master.measure_qc);

                ushort addrMeasureQc = (ushort)(dBase + 12);
                WriteIntOmron(plc, addrMeasureQc, master.measure_qc);

                WriteBoolOmron(plc, 3, 0, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlcService] UpdateMeasureQC failed");
            }
        }

        public void ChangePassword(Dictionary<string, int> passwords)
        {
            Console.WriteLine($"[Central PLC] Password Changed");
            var addressMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                ["maintenance"] = 30,
                ["technician"] = 32,
                ["production"] = 34,
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

                WriteBoolOmron(plc, wordAddress: 1, bit: 1, value: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlcService] ChangePassword failed");
            }
        }

        // ── Master PLC TryReadSnapshot ─────────────────────────────────────────

        private bool TryReadSnapshot(PlcOmron plc, int machineCount, out byte[] dBuffer, out bool[] wBits)
        {
            dBuffer = null!;
            wBits = null!;

            if (machineCount == 0) return false;

            const ushort dBase = 500;
            const int dPerMachine = 500;
            const int maxChunk = 400;

            int totalDWords = machineCount * dPerMachine;
            byte[] buffer = new byte[totalDWords * 2];

            for (int offset = 0; offset < totalDWords; offset += maxChunk)
            {
                ushort chunkStart = (ushort)(dBase + offset);
                ushort chunkSize = (ushort)Math.Min(maxChunk, totalDWords - offset);
                int expected = chunkSize * 2;

                try
                {
                    byte[] chunk = plc.Read(chunkStart, chunkSize, 0, MemoryAreaBits.DataMemory);

                    if (chunk == null || chunk.Length < expected)
                    {
                        string extraInfo = chunk?.Length == 2 ? $" (FINS Error Code: {BitConverter.ToString(chunk)})" : "";
                        _logger.LogWarning(
                            "[MasterPlc] D short read at D{Addr}: expected {Exp} bytes, got {Got}{Extra}",
                            chunkStart, expected, chunk?.Length ?? 0, extraInfo);
                        return false;
                    }

                    Buffer.BlockCopy(chunk, 0, buffer, offset * 2, chunk.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[MasterPlc] D read failed at D{Addr}: {Msg}", chunkStart, ex.Message);
                    return false;
                }
            }

            // W Memory chunked setup
            const ushort wBase = 5;
            const int wPerMachine = 3;

            int totalWBits = machineCount * wPerMachine * 16;
            bool[] bits = new bool[totalWBits];

            // W MaxChunk set to exact multiple of 16 ensuring bit offset is ALWAYS 0.
            const int maxWChunk = 480;

            for (int offset = 0; offset < totalWBits; offset += maxWChunk)
            {
                ushort chunkSize = (ushort)Math.Min(maxWChunk, totalWBits - offset);
                ushort chunkStartWord = (ushort)(wBase + (offset / 16));

                try
                {
                    byte[] chunk = plc.Read(chunkStartWord, chunkSize, 0, MemoryAreaBits.Work);

                    if (chunk == null || chunk.Length < chunkSize)
                    {
                        string extraInfo = chunk?.Length == 2 ? $" (FINS Error Code: {BitConverter.ToString(chunk)})" : "";
                        _logger.LogWarning(
                            "[MasterPlc] W short read at W{Word}.0: expected {Exp} bits, got {Got}{Extra}",
                            chunkStartWord, chunkSize, chunk?.Length ?? 0, extraInfo);
                        return false;
                    }

                    for (int i = 0; i < chunkSize; i++)
                        bits[offset + i] = chunk[i] != 0;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[MasterPlc] W read failed at W{Word}.0: {Msg}", chunkStartWord, ex.Message);
                    return false;
                }
            }

            dBuffer = buffer;
            wBits = bits;
            return true;
        }

        private bool AreSignalsConsistent(bool[] w1, bool[] w2, byte[] d1, byte[] d2, int machineCount)
        {
            const int dPerMachine = 500;
            const int wPerMachine = 3;

            for (int m = 0; m < machineCount; m++)
            {
                int dByteBase = m * dPerMachine * 2;

                int Widx(int wordOffset, int bit) => (m * wPerMachine + wordOffset) * 16 + bit;
                bool Wb1(int wo, int b) { int idx = Widx(wo, b); return idx < w1.Length && w1[idx]; }
                bool Wb2(int wo, int b) { int idx = Widx(wo, b); return idx < w2.Length && w2[idx]; }

                if (Wb1(0, 0) != Wb2(0, 0)) return false;
                if (Wb1(0, 1) != Wb2(0, 1)) return false;
                if (Wb1(0, 2) != Wb2(0, 2)) return false;
                if (Wb1(0, 3) != Wb2(0, 3)) return false;
                if (Wb1(0, 4) != Wb2(0, 4)) return false;
                if (Wb1(0, 5) != Wb2(0, 5)) return false;
                if (Wb1(0, 6) != Wb2(0, 6)) return false;
                if (Wb1(1, 0) != Wb2(1, 0)) return false;
                if (Wb1(1, 1) != Wb2(1, 1)) return false;
                if (Wb1(1, 2) != Wb2(1, 2)) return false;
                if (Wb1(1, 3) != Wb2(1, 3)) return false;
                if (Wb1(1, 4) != Wb2(1, 4)) return false;
                if (Wb1(1, 5) != Wb2(1, 5)) return false;
                if (Wb1(2, 0) != Wb2(2, 0)) return false;

                // Stop category string consistency
                int strByteIdx = dByteBase + (800 - 500) * 2;
                if (ReadStringAt(d1, strByteIdx, 100) != ReadStringAt(d2, strByteIdx, 100)) return false;
                // Remark string consistency
                int remByteIdx = dByteBase + (900 - 500) * 2;
                if (ReadStringAt(d1, remByteIdx, 100) != ReadStringAt(d2, remByteIdx, 100)) return false;
            }

            return true;
        }

        // ── Debug helpers ──────────────────────────────────────────────────────

        private static void DebugSubPlcD(int id_machine, Dictionary<string, object?> r)
        {
            if (id_machine != 0) return;

            Console.WriteLine($"\n[SubPlc M{id_machine}] === Sub D Memory ===");
            Console.WriteLine($"  QC_VISUAL={r["D10"],-6} QC_MEASURE={r["D12"],-6} PART_WEIGHT={r["D35"]:F3}");
            Console.WriteLine($"  SHOT_RUN={r["D40"],-6} SHOT_OTHERS={r["D42"],-6} SHOT_TOTAL={r["D48"],-6} SHOT_ACCUM={r["D50"]}");
            Console.WriteLine($"  LAST_CT={r["D70"]:F3}  ACCUM_CT={r["D80"]:F3}  ACT_CT={r["D90"]:F3}");
            Console.WriteLine($"  STOP_CAT_NO={r["D110"],-4} MOULD_CAT_NO={r["D120"],-4} HMI_PAGE={r["D190"]}");
            Console.WriteLine($"  MODEL=\"{r["D200"]}\"  PACKER=\"{r["D300"]}\"");
            Console.WriteLine($"  STOP_CATEGORY=\"{r["D400"]}\"  REMARKS=\"{r["D500"]}\"");
            Console.WriteLine($"  Reject KG — Panel={r["D600"]:F2} Lumpy={r["D610"]:F2} BlackDot={r["D620"]:F2} Burst={r["D630"]:F2}");
            Console.WriteLine($"              Startup={r["D640"]:F2} Preform={r["D650"]:F2} Purging={r["D660"]:F2} Others={r["D670"]:F2}");
            Console.WriteLine($"  Tot KG    — Panel={r["D700"]:F2} Lumpy={r["D705"]:F2} BlackDot={r["D710"]:F2} Burst={r["D715"]:F2}");
            Console.WriteLine($"              Startup={r["D720"]:F2} Preform={r["D725"]:F2} Purging={r["D730"]:F2} Others={r["D735"]:F2}");
            Console.WriteLine($"  Tot PCS   — Panel={r["D740"]:F0} Lumpy={r["D745"]:F0} BlackDot={r["D750"]:F0} Burst={r["D755"]:F0}");
            Console.WriteLine($"              Startup={r["D760"]:F0} Preform={r["D765"]:F0} Purging={r["D770"]:F0} Others={r["D775"]:F0}");
        }

        private static void DebugSubPlcW(int id_machine, Dictionary<string, object?> r)
        {
            if (id_machine != 0) return;

            Console.WriteLine($"[SubPlc M{id_machine}] === Sub W Memory ===");
            Console.WriteLine($"  POWER={r["W0.00"]} START_AUTO={r["W1.00"]} PROD_RUN={r["W2.00"]} SHOT={r["W3.00"]}");
            Console.WriteLine($"  SHIFT={r["W5.00"]} RESET={r["W5.01"]} REMARK={r["W6.00"]} REJECT={r["W7.00"]} QC={r["W9.00"]}");
            Console.WriteLine($"  Central: START={r["W20.00"]} OFF={r["W20.01"]} RUN={r["W20.02"]} QC={r["W20.03"]} DONE={r["W20.04"]} REM={r["W20.05"]} REJ={r["W20.06"]}");
            Console.WriteLine($"  Util: Barrel={r["W60.00"]} Motor={r["W60.01"]} Dehum={r["W60.02"]} Chiller={r["W60.03"]} Mat={r["W60.04"]} DryCycle={r["W60.05"]}");
            Console.WriteLine($"  Alarm: Barrel={r["W70.00"]} Motor={r["W71.00"]} Dehum={r["W72.00"]} Chiller={r["W73.00"]} Mat={r["W74.00"]} Gen={r["W75.00"]}");
        }

        private static void DebugSubPlcH(int id_machine, Dictionary<string, object?> r)
        {
            if (id_machine != 0) return;

            Console.WriteLine($"[SubPlc M{id_machine}] === Sub H Memory ===");
            Console.WriteLine($"  CT_CONST={r["H0"]:F3} IP_NODE={r["H20"]}");
            Console.WriteLine($"  PROD_PASS={r["H30"]} TECH_PASS={r["H32"]} MAIN_PASS={r["H34"]} QC_PASS={r["H36"]}\n");
        }

        private static void DebugMasterPlc(PlcSnapshot r)
        {
            Console.WriteLine($"\n[Master] === Machine 0 ===");
            Console.WriteLine($"  QC_VISUAL={r.visual_qc,-6} QC_MEASURE={r.measure_qc,-6}");
            Console.WriteLine($"  SHOT={r.shot,-6} SHOT_ACCUM={r.shot_accum,-6}  ACT_CT={r.act_ct:F3}");
            Console.WriteLine($"  STOP_CAT=\"{r.stop_category}\" REMARKS=\"{r.remark}\" MOULD_CAT_NO={r.mould_category_no,-4}");
            //Console.WriteLine($"  Reject KG — Panel={r.reject_panelling:F2} Lumpy={r.reject_lumpy:F2} BlackDot={r.reject_black_dot:F2} Burst={r.reject_burst:F2}");
            //Console.WriteLine($"              Startup={r.reject_startup:F2} Preform={r.reject_preform:F2} Purging={r.reject_purging:F2} Others={r.reject_others:F2}");
            Console.WriteLine($"  STATUS_START={r.status_start} STATUS_OFF={r.status_off} PROD_RUN={r.production_running} QC_SIGNAL={r.qc_signal} DONE={r.done} REMARK_SIGNAL={r.remark_signal} REJECT_SIGNAL={r.reject_signal}");
            //Console.WriteLine($"  Util — Barrel={r.util_barrel} Motor={r.util_hyd_motor} Dehum={r.util_dehumidifier} Chiller={r.util_chiller} Mat={r.util_material} DryCycle={r.util_dry_cycle}\n");
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

        private static int ReadIntAt(byte[] buf, int byteIdx)
        {
            if (byteIdx < 0 || byteIdx + 4 > buf.Length) return 0;
            byte[] r = { buf[byteIdx + 1], buf[byteIdx], buf[byteIdx + 3], buf[byteIdx + 2] };
            return BitConverter.ToInt32(r, 0);
        }

        private static float ReadFloatAt(byte[] buf, int byteIdx)
        {
            if (byteIdx < 0 || byteIdx + 4 > buf.Length) return 0f;
            byte[] r = { buf[byteIdx + 1], buf[byteIdx], buf[byteIdx + 3], buf[byteIdx + 2] };
            return BitConverter.ToSingle(r, 0);
        }

        private static string ReadStringAt(byte[] buf, int byteIdx, int maxBytes = 100)
        {
            if (byteIdx < 0 || byteIdx >= buf.Length) return string.Empty;
            int available = Math.Min(maxBytes, buf.Length - byteIdx);
            int length = 0;
            while (length < available && buf[byteIdx + length] != 0) length++;
            return length == 0 ? string.Empty : Encoding.ASCII.GetString(buf, byteIdx, length).Trim();
        }

        // Sub-PLC read helpers

        private static int SubReadInt(byte[] buf, int byteIdx)
        {
            if (byteIdx < 0 || byteIdx + 4 > buf.Length) return 0;
            byte[] r = { buf[byteIdx + 1], buf[byteIdx], buf[byteIdx + 3], buf[byteIdx + 2] };
            return BitConverter.ToInt32(r, 0);
        }

        private static float SubReadFloat(byte[] buf, int byteIdx)
        {
            if (byteIdx < 0 || byteIdx + 4 > buf.Length) return 0f;
            byte[] r = { buf[byteIdx + 1], buf[byteIdx], buf[byteIdx + 3], buf[byteIdx + 2] };
            return BitConverter.ToSingle(r, 0);
        }

        private static string SubReadString(byte[] buf, int byteIdx, int maxBytes = 100)
        {
            if (byteIdx < 0 || byteIdx >= buf.Length) return string.Empty;
            int available = Math.Min(maxBytes, buf.Length - byteIdx);
            int length = 0;
            while (length < available && buf[byteIdx + length] != 0) length++;
            return length == 0 ? string.Empty : Encoding.ASCII.GetString(buf, byteIdx, length).Trim();
        }

        // ── PLC write helpers ──────────────────────────────────────

        private static Dictionary<string, object> BuildResult(byte[] dBuffer, bool[] wBits)
            => new() { ["Data"] = new Dictionary<string, object> { ["D_RAW"] = dBuffer, ["W_RAW"] = wBits } };

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

    // ── Typed snapshot record ──────────────────────────────────────────────────

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
        public bool util_barrel { get; init; }
        public bool util_hyd_motor { get; init; }
        public bool util_dehumidifier { get; init; }
        public bool util_chiller { get; init; }
        public bool util_material { get; init; }
        public bool util_dry_cycle { get; init; }
        public bool qc_mea_signal { get; init; }
    }
}