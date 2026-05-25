using CMS.Server.Models;
using CMS.Server.Services;
using PLC_Omron_Standard;
using PLC_Omron_Standard.Enums;
using System.Collections.Concurrent;
using System.Reflection.PortableExecutable;
using System.Text;

namespace CMS.Server.Services
{
    public sealed class PlcService : BackgroundService
    {
        // ── Connection cache ───────────────────────────────────────────────────
        private static readonly ConcurrentDictionary<string, PlcOmron> _plcConnections = new();

        // ── Guard ─
        private readonly SemaphoreSlim _pollLock = new(1, 1);

        // ── DI ────────────────────────────────────────────────────────────────
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PlcService> _logger;
        private readonly IHostEnvironment _env;

        // ── Master PLC addresses ───────────────────────────────────────────────
        private const string MasterIp = "172.17.86.80";
        private const string ReadKey = MasterIp + "_read";
        private const string WriteKey = MasterIp + "_write";
        private const int PollDelayMs = 200;

        // ── Machine list cache ──────────────────────────
        private IReadOnlyList<(int id, string name, string ip)> _cachedMachines = [];
        private DateTime _machinesCachedAt = DateTime.MinValue;
        private readonly SemaphoreSlim _machineCacheLock = new(1, 1);
        private const int MachineCacheMinutes = 5;

        // ── Sub-PLC signal cache ──
        private readonly ConcurrentDictionary<int, Dictionary<string, object?>?> _subPlcCache = new();
        private readonly ConcurrentDictionary<int, bool> _subPlcOnline = new();
        private const int SubPlcSweepDelayMs = 5000;

        private int _iterationCount;

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

        // ── Entry point ────────────────────────────────────────────────────────

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_env.IsDevelopment())
            {
                _logger.LogInformation("[PlcService] Development mode — sub-PLC sweep only.");
                await RunSubPlcSweepLoopAsync(stoppingToken);
            }
            else
            {
                await Task.WhenAll(
                    RunMasterPlcLoopAsync(stoppingToken),
                    RunSubPlcSweepLoopAsync(stoppingToken)
                );
            }

            _logger.LogWarning("[PlcService] All loops exited.");
        }

        // ── Master PLC loop ─────────────────────────────────

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

                    var machines = await GetCachedMachinesAsync();
                    var snapshots = BuildSnapshots(dRaw, wRaw, timestamp, machines.Count);

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

        // ── Sub-PLC sweep loop ─────────────────────────────
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
                            _logger.LogWarning("[SubPlc M{Id}] Sweep read failed: {Msg}",
                                machine.id, ex.Message);
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

        // ── Sub-PLC cache read — zero I/O, called by controller ───────────────

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
            if ((DateTime.UtcNow - _machinesCachedAt).TotalMinutes < MachineCacheMinutes)
                return _cachedMachines;

            await _machineCacheLock.WaitAsync();
            try
            {
                if ((DateTime.UtcNow - _machinesCachedAt).TotalMinutes < MachineCacheMinutes)
                    return _cachedMachines;

                using var scope = _scopeFactory.CreateScope();
                var baseService = scope.ServiceProvider.GetRequiredService<BaseService>();
                _cachedMachines = await baseService.GetMachineIdsAsync();
                _machinesCachedAt = DateTime.UtcNow;
                return _cachedMachines;
            }
            finally
            {
                _machineCacheLock.Release();
            }
        }

        // ── Snapshot builder ───────────────────────────────────────────────────

        private static List<PlcSnapshot> BuildSnapshots(byte[] dRaw, bool[] wRaw, DateTime timestamp, int machineCount)
        {
            const int dPerMachine = 500;
            const int wPerMachine = 3;

            var list = new List<PlcSnapshot>(machineCount);

            for (int id_machine = 0; id_machine < machineCount; id_machine++)
            {
                int dByteBase = id_machine * dPerMachine * 2;
                int Didx(int absoluteWord) => dByteBase + (absoluteWord - 500) * 2;
                int Widx(int wordOffset, int bit) => (id_machine * wPerMachine + wordOffset) * 16 + bit;


                list.Add(new PlcSnapshot
                {
                    id_machine = id_machine,
                    time = timestamp,

                    visual_qc         = Math.Min(ReadIntAt(dRaw,   Didx(510)), 10_000),
                    measure_qc        = Math.Min(ReadIntAt(dRaw,   Didx(512)), 10_000),
                    shot              = Math.Min(ReadIntAt(dRaw,   Didx(530)), 10_000),
                    shot_accum        = Math.Min(ReadIntAt(dRaw,   Didx(532)), 10_000),
                    act_ct            = Math.Min(ReadFloatAt(dRaw, Didx(560)), 1_000f),
                    mould_category_no = Math.Min(ReadIntAt(dRaw,   Didx(590)), 10),
                    stop_category     = ReadStringAt(dRaw, Didx(800), 100),
                    remark            = ReadStringAt(dRaw, Didx(900), 100),

                    reject_panelling  = Math.Min(ReadFloatAt(dRaw, Didx(750)), 10_000f),
                    reject_lumpy      = Math.Min(ReadFloatAt(dRaw, Didx(755)), 10_000f),
                    reject_black_dot  = Math.Min(ReadFloatAt(dRaw, Didx(760)), 10_000f),
                    reject_burst      = Math.Min(ReadFloatAt(dRaw, Didx(765)), 10_000f),
                    reject_startup    = Math.Min(ReadFloatAt(dRaw, Didx(770)), 10_000f),
                    reject_preform    = Math.Min(ReadFloatAt(dRaw, Didx(775)), 10_000f),
                    reject_purging    = Math.Min(ReadFloatAt(dRaw, Didx(780)), 10_000f),
                    reject_others     = Math.Min(ReadFloatAt(dRaw, Didx(785)), 10_000f),

                    // W5: status/signal bits
                    status_start       = ReadBitAt(wRaw, Widx(0, 0)),
                    status_off         = ReadBitAt(wRaw, Widx(0, 1)),
                    production_running = ReadBitAt(wRaw, Widx(0, 2)),
                    qc_signal          = ReadBitAt(wRaw, Widx(0, 3)),
                    done               = ReadBitAt(wRaw, Widx(0, 4)),
                    remark_signal      = ReadBitAt(wRaw, Widx(0, 5)),
                    reject_signal      = ReadBitAt(wRaw, Widx(0, 6)),

                    // W6: utility bits
                    util_barrel        = ReadBitAt(wRaw, Widx(1, 0)),
                    util_hyd_motor     = ReadBitAt(wRaw, Widx(1, 1)),
                    util_dehumidifier  = ReadBitAt(wRaw, Widx(1, 2)),
                    util_chiller       = ReadBitAt(wRaw, Widx(1, 3)),
                    util_material      = ReadBitAt(wRaw, Widx(1, 4)),
                    util_dry_cycle     = ReadBitAt(wRaw, Widx(1, 5)),
                });
            }

            return list;

            static bool ReadBitAt(bool[] buf, int idx) => idx >= 0 && idx < buf.Length && buf[idx];
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

        // ── Sub-PLC read (one machine) ─────────────────────────────────────────

        public Dictionary<string, object?> ReadSubPlcSignals(int id_machine)
        {
            var ip       = $"172.17.86.{220 + id_machine}";
            var cacheKey = $"sub_{ip}";
            var result   = new Dictionary<string, object?>();
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

                // ── D Memory: read words 0–799 in chunks of 500 ──────────────────────
                const ushort dStart = 0;
                const int totalDWords = 800;
                const int maxChunk = 500;

                byte[] dBuf = new byte[totalDWords * 2];
                bool    dOk = true;

                for (int offset = 0; offset < totalDWords && dOk; offset += maxChunk)
                {
                    ushort chunkStart = (ushort)(dStart + offset);
                    ushort chunkSize = (ushort)Math.Min(maxChunk, totalDWords - offset);
                    try
                    {
                        byte[] chunk = plc.Read(chunkStart, chunkSize, 0, MemoryAreaBits.DataMemory);
                        if (chunk == null || chunk.Length < chunkSize * 2)
                        {
                            _logger.LogWarning("[SubPlc M{Id}] D short read at offset {Offset}: expected {Exp}, got {Got}",
                                id_machine, offset, chunkSize * 2, chunk?.Length ?? 0);
                            dOk = false;
                            break;
                        }
                        Buffer.BlockCopy(chunk, 0, dBuf, offset * 2, chunk.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[SubPlc M{Id}] D read failed at offset {Offset}: {Msg}", id_machine, offset, ex.Message);
                        dOk = false;
                    }
                }

                if (dOk)
                {
                    int Bidx(int word) => (word - dStart) * 2;

                    // ── Production counters ──────────────────────────────────
                    result["D10"]  = SubReadInt(dBuf,    Bidx(10),  totalDWords);  // QC_VISUAL
                    result["D12"]  = SubReadInt(dBuf,    Bidx(12),  totalDWords);  // QC_MEASURE
                    result["D35"]  = SubReadFloat(dBuf,  Bidx(35),  totalDWords);  // PART_WEIGHT_KG
                    result["D40"]  = SubReadInt(dBuf,    Bidx(40),  totalDWords);  // SHOT_RUN
                    result["D42"]  = SubReadInt(dBuf,    Bidx(42),  totalDWords);  // SHOT_OTHERS
                    result["D44"]  = SubReadFloat(dBuf,  Bidx(44),  totalDWords);  // SHOT_FLOAT
                    result["D48"]  = SubReadInt(dBuf,    Bidx(48),  totalDWords);  // SHOT_TOTAL
                    result["D50"]  = SubReadInt(dBuf,    Bidx(50),  totalDWords);  // SHOT_ACCUM
                    result["D70"]  = SubReadFloat(dBuf,  Bidx(70),  totalDWords);  // LAST_CT
                    result["D80"]  = SubReadFloat(dBuf,  Bidx(80),  totalDWords);  // ACCUM_CT
                    result["D90"]  = SubReadFloat(dBuf,  Bidx(90),  totalDWords);  // ACT_CT

                    // ── Stop / Mould category ─────────────────────────────────────
                    result["D110"] = SubReadInt(dBuf,    Bidx(110), totalDWords);  // STOP_CAT_NO
                    result["D115"] = SubReadInt(dBuf,    Bidx(115), totalDWords);  // STOP_CAT_TEMP
                    result["D120"] = SubReadInt(dBuf,    Bidx(120), totalDWords);  // MOULD_CAT_NO
                    result["D180"] = SubReadInt(dBuf,    Bidx(180), totalDWords);  // HMI_STOP_CAT_COLOR
                    result["D190"] = SubReadInt(dBuf,    Bidx(190), totalDWords);  // HMI_PAGE

                    // ── Strings (100 bytes each = 50 words each) ──────────────────
                    result["D200"] = SubReadString(dBuf, Bidx(200), totalDWords);  // MODEL
                    result["D300"] = SubReadString(dBuf, Bidx(300), totalDWords);  // PACKER
                    result["D400"] = SubReadString(dBuf, Bidx(400), totalDWords);  // STOP_CATEGORY
                    result["D500"] = SubReadString(dBuf, Bidx(500), totalDWords);  // REMARKS

                    // ── Reject kg (current shot) ──────────────────────────────────
                    result["D600"] = SubReadFloat(dBuf,  Bidx(600), totalDWords);  // KG_PANELLING
                    result["D605"] = SubReadFloat(dBuf,  Bidx(605), totalDWords);  // TEMP_PANELLING
                    result["D610"] = SubReadFloat(dBuf,  Bidx(610), totalDWords);  // KG_LUMPY
                    result["D615"] = SubReadFloat(dBuf,  Bidx(615), totalDWords);  // TEMP_LUMPY
                    result["D620"] = SubReadFloat(dBuf,  Bidx(620), totalDWords);  // KG_BLACK_DOT
                    result["D625"] = SubReadFloat(dBuf,  Bidx(625), totalDWords);  // TEMP_BLACK_DOT
                    result["D630"] = SubReadFloat(dBuf,  Bidx(630), totalDWords);  // KG_BURST
                    result["D635"] = SubReadFloat(dBuf,  Bidx(635), totalDWords);  // TEMP_BURST
                    result["D640"] = SubReadFloat(dBuf,  Bidx(640), totalDWords);  // KG_STARTUP
                    result["D645"] = SubReadFloat(dBuf,  Bidx(645), totalDWords);  // TEMP_STARTUP
                    result["D650"] = SubReadFloat(dBuf,  Bidx(650), totalDWords);  // KG_PREFORM
                    result["D655"] = SubReadFloat(dBuf,  Bidx(655), totalDWords);  // TEMP_PREFORM
                    result["D660"] = SubReadFloat(dBuf,  Bidx(660), totalDWords);  // KG_PURGING
                    result["D665"] = SubReadFloat(dBuf,  Bidx(665), totalDWords);  // TEMP_PURGING
                    result["D670"] = SubReadFloat(dBuf,  Bidx(670), totalDWords);  // KG_OTHERS
                    result["D675"] = SubReadFloat(dBuf,  Bidx(675), totalDWords);  // TEMP_OTHERS

                    // ── Reject kg (total accumulated) ────────────────────────────
                    result["D700"] = SubReadFloat(dBuf,  Bidx(700), totalDWords);  // TOT_KG_PANELLING
                    result["D705"] = SubReadFloat(dBuf,  Bidx(705), totalDWords);  // TOT_KG_LUMPY
                    result["D710"] = SubReadFloat(dBuf,  Bidx(710), totalDWords);  // TOT_KG_BLACK_DOT
                    result["D715"] = SubReadFloat(dBuf,  Bidx(715), totalDWords);  // TOT_KG_BURST
                    result["D720"] = SubReadFloat(dBuf,  Bidx(720), totalDWords);  // TOT_KG_STARTUP
                    result["D725"] = SubReadFloat(dBuf,  Bidx(725), totalDWords);  // TOT_KG_PREFORM
                    result["D730"] = SubReadFloat(dBuf,  Bidx(730), totalDWords);  // TOT_KG_PURGING
                    result["D735"] = SubReadFloat(dBuf,  Bidx(735), totalDWords);  // TOT_KG_OTHERS

                    // ── Reject pcs (accumulated) ──────────────────────────────────
                    result["D740"] = SubReadFloat(dBuf,  Bidx(740), totalDWords);  // PCS_PANELLING
                    result["D745"] = SubReadFloat(dBuf,  Bidx(745), totalDWords);  // PCS_LUMPY
                    result["D750"] = SubReadFloat(dBuf,  Bidx(750), totalDWords);  // PCS_BLACK_DOT
                    result["D755"] = SubReadFloat(dBuf,  Bidx(755), totalDWords);  // PCS_BURST
                    result["D760"] = SubReadFloat(dBuf,  Bidx(760), totalDWords);  // PCS_STARTUP
                    result["D765"] = SubReadFloat(dBuf,  Bidx(765), totalDWords);  // PCS_PREFORM
                    result["D770"] = SubReadFloat(dBuf,  Bidx(770), totalDWords);  // PCS_PURGING
                    result["D775"] = SubReadFloat(dBuf,  Bidx(775), totalDWords);  // PCS_OTHERS
                }

                // ── W Memory: read words 0–79 ──────────────────
                const ushort wStart     = 0;
                const int    totalWWords = 80;
                int          totalWBits  = totalWWords * 16;

                try
                {
                    byte[] wChunk = plc.Read(wStart, (ushort)totalWBits, 0, MemoryAreaBits.Work);
                    if (wChunk == null || wChunk.Length < totalWBits)
                    {
                        _logger.LogWarning("[SubPlc M{Id}] W short read: expected {Exp}, got {Got}",
                            id_machine, totalWBits, wChunk?.Length ?? 0);
                    }
                    else
                    {
                        // Helper: absolute word + bit → index in wChunk
                        bool Wbit(int word, int bit)
                        {
                            int idx = (word - wStart) * 16 + bit;
                            return idx >= 0 && idx < wChunk.Length && wChunk[idx] != 0;
                        }

                        // Local signals (W0–W9)
                        result["W0.00"] = Wbit(0, 0);   // POWER_SUPPLY_SIGNAL
                        result["W1.00"] = Wbit(1, 0);   // START_AUTO_SIGNAL
                        result["W2.00"] = Wbit(2, 0);   // PROD_RUN_SIGNAL
                        result["W3.00"] = Wbit(3, 0);   // SHOT_SIGNAL
                        result["W4.00"] = Wbit(4, 0);   // CT_TIMER_SIGNAL
                        result["W5.00"] = Wbit(5, 0);   // CHANGE_SHIFT_SIGNAL
                        result["W5.01"] = Wbit(5, 1);   // RESET_SIGNAL
                        result["W6.00"] = Wbit(6, 0);   // REMARK_SIGNAL
                        result["W7.00"] = Wbit(7, 0);   // REJECT_SIGNAL
                        result["W8.00"] = Wbit(8, 0);   // STOP_CAT_SIGNAL
                        result["W9.00"] = Wbit(9, 0);   // QC_SIGNAL

                        // Central (master) status mirror (W20)
                        result["W20.00"] = Wbit(20, 0); // CENTRAL_STATUS_START
                        result["W20.01"] = Wbit(20, 1); // CENTRAL_STATUS_OFF
                        result["W20.02"] = Wbit(20, 2); // CENTRAL_PROD_RUN
                        result["W20.03"] = Wbit(20, 3); // CENTRAL_QC
                        result["W20.04"] = Wbit(20, 4); // CENTRAL_DONE
                        result["W20.05"] = Wbit(20, 5); // CENTRAL_REMARK
                        result["W20.06"] = Wbit(20, 6); // CENTRAL_REJECT

                        result["W30.00"] = Wbit(30, 0); // HMI Done

                        // Utility run signals (W60)
                        result["W60.00"] = Wbit(60, 0); // Barrel
                        result["W60.01"] = Wbit(60, 1); // Hyd. Motor
                        result["W60.02"] = Wbit(60, 2); // Dehumidifier
                        result["W60.03"] = Wbit(60, 3); // Chiller
                        result["W60.04"] = Wbit(60, 4); // Material
                        result["W60.05"] = Wbit(60, 5); // Dry Cycle

                        // Alarm signals (W70–W77)
                        result["W70.00"] = Wbit(70, 0); // Alarm Barrel
                        result["W71.00"] = Wbit(71, 0); // Alarm Hyd. Motor
                        result["W72.00"] = Wbit(72, 0); // Alarm Dehumidifier
                        result["W73.00"] = Wbit(73, 0); // Alarm Chiller
                        result["W74.00"] = Wbit(74, 0); // Alarm Material
                        result["W75.00"] = Wbit(75, 0); // Alarm General
                        result["W76.00"] = Wbit(76, 0); // Alarm Utility
                        result["W77.00"] = Wbit(77, 0); // Reset Alarm
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[SubPlc M{Id}] W read failed: {Msg}", id_machine, ex.Message);
                }

                // ── H (Holding) Memory: words 0–39 ───────────────────────────────
                const ushort hStart = 0;
                const int totalHWords = 40;

                try
                {
                    byte[] hBuf = plc.Read(hStart, (ushort)totalHWords, 0, (MemoryAreaBits)0xB2);
                    if (hBuf == null || hBuf.Length < totalHWords * 2)
                    {
                        _logger.LogWarning(
                            "[SubPlc M{Id}] H short read: expected {Exp}, got {Got}",
                            id_machine, totalHWords * 2, hBuf?.Length ?? 0);
                    }
                    else
                    {
                        int Hidx(int word) => word * 2;

                        result["H0"]  = SubReadFloat(hBuf,  Hidx(0),  totalHWords); // CT_CONSTANT
                        result["H5"]  = SubReadInt(hBuf,    Hidx(5),  totalHWords); // ZEROI_CONSTANT
                        result["H10"] = SubReadFloat(hBuf,  Hidx(10), totalHWords); // ZEROF_CONSTANT
                        result["H15"] = SubReadString(hBuf, Hidx(15), totalHWords); // STRING_CONSTANT
                        result["H20"] = SubReadInt(hBuf,    Hidx(20), totalHWords); // IP_NODE
                        result["H30"] = SubReadInt(hBuf,    Hidx(30), totalHWords); // PROD_PASS
                        result["H32"] = SubReadInt(hBuf,    Hidx(32), totalHWords); // TECH_PASS
                        result["H34"] = SubReadInt(hBuf,    Hidx(34), totalHWords); // MAIN_PASS
                        result["H36"] = SubReadInt(hBuf,    Hidx(36), totalHWords); // QC_PASS
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

                int    id_machine = (int)master.id_machine; // 0-based
                int    ipNode     = 220 + id_machine;
                ushort dBase      = (ushort)(500 + id_machine * 500);

                WriteIntOmron(plc, 10, ipNode); //temp data to send to sub plc

                ushort addrPartWeight = (ushort)(dBase + 25);  // D525, D1025, ...
                ushort addrType       = (ushort)(dBase + 100); // D600, D1100, ...
                ushort addrPacker     = (ushort)(dBase + 200); // D700, D1200, ...

                WriteFloatOmron(plc, 25, master.part_weight); //temp data to send to sub plc
                WriteFloatOmron(plc, addrPartWeight,    master.part_weight);

                WriteStringOmron(plc, 100, master.type); //temp data to send to sub plc
                WriteStringOmron(plc, addrType,         master.type);

                WriteStringOmron(plc, 200, master.packer); //temp data to send to sub plc
                WriteStringOmron(plc, addrPacker,       master.packer);

                WriteBoolOmron(plc, 4, 0, true); //To trigger function block to send to sub plc
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

                int id_machine = master.id_machine; // 0-based
                int ipNode = 220 + id_machine;
                ushort dBase = (ushort)(500 + id_machine * 500);

                WriteIntOmron(plc, 20, ipNode); //temp data to send to sub plc

                WriteIntOmron(plc, 27, master.measure_qc); //temp data to send to sub plc
                WriteIntOmron(plc, dBase, master.measure_qc);

                WriteBoolOmron(plc, 3, 0, true); //To trigger function block to send to sub plc
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

            int machineCount = _cachedMachines.Count;
            if (machineCount == 0)
            {
                _logger.LogWarning("[PlcService] TryReadSnapshot: no machines in cache, skipping.");
                return false;
            }

            // ── D Memory ─────────────────────────────────────────────────────────────
            const ushort dBase = 500;
            const int dPerMachine = 500;
            const int maxChunk = 500;

            ushort dStart = dBase;
            int totalDWords = machineCount * dPerMachine;
            byte[] buffer = new byte[totalDWords * 2];

            for (int offset = 0; offset < totalDWords; offset += maxChunk)
            {
                ushort chunkStart = (ushort)(dStart + offset);
                ushort chunkSize = (ushort)Math.Min(maxChunk, totalDWords - offset);
                int expectedBytes = chunkSize * 2;

                try
                {
                    byte[] chunk = plc.Read(chunkStart, chunkSize, 0, MemoryAreaBits.DataMemory);
                    if (chunk == null || chunk.Length < expectedBytes)
                    {
                        _logger.LogWarning(
                            "[PlcService] D short read at offset {Offset}: expected {Exp}, got {Got}",
                            offset, expectedBytes, chunk?.Length ?? 0);
                        return false;
                    }
                    Buffer.BlockCopy(chunk, 0, buffer, offset * 2, chunk.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[PlcService] D read failed at offset {Offset}: {Msg}",
                        offset, ex.Message);
                    return false;
                }
            }

            // W area: words 5–81
            const ushort wBase = 5;
            const int wPerMachine = 3;

            ushort wStart = wBase;
            int totalWWords = machineCount * wPerMachine;
            int totalWBits = totalWWords * 16;
            bool[] bits = new bool[totalWBits];

            try
            {
                byte[] chunk = plc.Read(wStart, (ushort)totalWBits, 0, MemoryAreaBits.Work);
                if (chunk == null || chunk.Length < totalWBits)
                {
                    _logger.LogWarning(
                        "[PlcService] W short read: expected {Exp}, got {Got}",
                        totalWBits, chunk?.Length ?? 0);
                    return false;
                }
                for (int i = 0; i < totalWBits; i++)
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
            int machineCount = _cachedMachines.Count;
            const int dPerMachine = 500;
            const int wPerMachine = 3;

            for (int id_machine = 0; id_machine < machineCount; id_machine++)
            {
                int dByteBase = id_machine * dPerMachine * 2;
                int Didx(int absoluteWord) => dByteBase + (absoluteWord - 500) * 2;
                int Widx(int wordOffset, int bit) => (id_machine * wPerMachine + wordOffset) * 16 + bit;

                bool Wb1(int wo, int b) { int idx = Widx(wo, b); return idx < w1.Length && w1[idx]; }
                bool Wb2(int wo, int b) { int idx = Widx(wo, b); return idx < w2.Length && w2[idx]; }

                if (Wb1(0, 0) != Wb2(0, 0)) return false; // status_start
                if (Wb1(0, 1) != Wb2(0, 1)) return false; // status_off
                if (Wb1(0, 2) != Wb2(0, 2)) return false; // production_running
                if (Wb1(0, 3) != Wb2(0, 3)) return false; // qc_signal
                if (Wb1(0, 4) != Wb2(0, 4)) return false; // done
                if (Wb1(0, 5) != Wb2(0, 5)) return false; // remark_signal
                if (Wb1(0, 6) != Wb2(0, 6)) return false; // reject_signal
                if (Wb1(1, 0) != Wb2(1, 0)) return false; // util_barrel
                if (Wb1(1, 1) != Wb2(1, 1)) return false; // util_hyd_motor
                if (Wb1(1, 2) != Wb2(1, 2)) return false; // util_dehumidifier
                if (Wb1(1, 3) != Wb2(1, 3)) return false; // util_chiller
                if (Wb1(1, 4) != Wb2(1, 4)) return false; // util_material
                if (Wb1(1, 5) != Wb2(1, 5)) return false; // util_dry_cycle

                if (ReadStringAt(d1, Didx(800), 100) != ReadStringAt(d2, Didx(800), 100)) return false;
                if (ReadStringAt(d1, Didx(900), 100) != ReadStringAt(d2, Didx(900), 100)) return false;
            }

            return true;
        }

        // ── PLC Read ────────────────────────────────────

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
            while (length < available && buf[byteIdx + length] != 0)
                length++;
            return length == 0 ? string.Empty : Encoding.ASCII.GetString(buf, byteIdx, length).Trim();
        }

        private static int SubReadInt(byte[] buf, int byteIdx, int totalWords)
        {
            int limit = totalWords * 2;
            if (byteIdx < 0 || byteIdx + 4 > limit || byteIdx + 4 > buf.Length) return 0;
            byte[] r = { buf[byteIdx + 1], buf[byteIdx], buf[byteIdx + 3], buf[byteIdx + 2] };
            return BitConverter.ToInt32(r, 0);
        }

        private static float SubReadFloat(byte[] buf, int byteIdx, int totalWords)
        {
            int limit = totalWords * 2;
            if (byteIdx < 0 || byteIdx + 4 > limit || byteIdx + 4 > buf.Length) return 0f;
            byte[] r = { buf[byteIdx + 1], buf[byteIdx], buf[byteIdx + 3], buf[byteIdx + 2] };
            return BitConverter.ToSingle(r, 0);
        }

        private static string SubReadString(byte[] buf, int byteIdx, int totalWords, int maxBytes = 100)
        {
            int limit = totalWords * 2;
            if (byteIdx < 0 || byteIdx >= limit || byteIdx >= buf.Length) return string.Empty;
            int available = Math.Min(maxBytes, Math.Min(limit - byteIdx, buf.Length - byteIdx));
            int length = 0;
            while (length < available && buf[byteIdx + length] != 0)
                length++;
            return length == 0 ? string.Empty : Encoding.ASCII.GetString(buf, byteIdx, length).Trim();
        }

        private static Dictionary<string, object> BuildResult(byte[] dBuffer, bool[] wBits)
            => new() { ["Data"] = new Dictionary<string, object> { ["D_RAW"] = dBuffer, ["W_RAW"] = wBits } };

        // ── PLC Write ─────────────────────────────────────────

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
    }
}