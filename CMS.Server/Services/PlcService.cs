using CMS.Server.Models;
using PLC_Omron_Standard;
using PLC_Omron_Standard.Enums;
using System.Collections.Concurrent;
using System.Text;

namespace CMS.Server.Services
{
    public class PlcService : BackgroundService
    {
        private static readonly ConcurrentDictionary<string, PlcOmron> _plcConnections = new();

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PlcService> _logger;
        private int _iterationCount = 0;

        private const string READ_KEY = "172.17.86.80_read";
        private const string WRITE_KEY = "172.17.86.80_write";
        public PlcService(IConfiguration config, IServiceScopeFactory scopeFactory, ILogger<PlcService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PlcMonitorService is starting at {Time}", DateTime.Now);
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("PlcMonitorService is stopping at {Time}", DateTime.Now);
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {

                try
                {
                    _iterationCount++;

                    if (_iterationCount % 200 == 0)
                        _logger.LogInformation("Heartbeat - Iteration: {Count}, Time: {Time}", _iterationCount, DateTime.Now);

                    var plcResults = ReadAllPlcs();
                    var time = DateTime.Now;

                    if (!plcResults.TryGetValue("Data", out var dataObj) || dataObj is not Dictionary<string, object> combinedData)
                    {
                        _logger.LogWarning("Missing or invalid Data from PLC at {Time}", time);
                        continue;
                    }

                    var dRaw = combinedData.GetValueOrDefault("D_RAW") as byte[] ?? Array.Empty<byte>();
                    var wRaw = combinedData.GetValueOrDefault("W_RAW") as bool[] ?? Array.Empty<bool>();

                    var allPlcData = new List<object>(26);
                    for (int i = 0; i < 26; i++)
                    {
                        int Doffset = i * 500;
                        int Woffset = i * 3;

                        allPlcData.Add(new
                        {
                            // Read D Memory values
                            id_machine = i + 1,
                            time = time,
                            shot = Math.Min(ReadIntFromD(dRaw, 30 + Doffset), 10000),
                            shot_accum = Math.Min(ReadIntFromD(dRaw, 32 + Doffset), 10000),
                            act_ct = Math.Min(ReadFloatFromD(dRaw, 60 + Doffset), 1000f),
                            mould_category_no = Math.Min(ReadIntFromD(dRaw, 90 + Doffset), 10),
                            stop_category = ReadStringFromD(dRaw, 300 + Doffset),
                            remark = ReadStringFromD(dRaw, 400 + Doffset),

                            reject_panelling = Math.Min(ReadFloatFromD(dRaw, 250 + Doffset), 10000f),
                            reject_lumpy = Math.Min(ReadFloatFromD(dRaw, 255 + Doffset), 10000f),
                            reject_black_dot = Math.Min(ReadFloatFromD(dRaw, 260 + Doffset), 10000f),
                            reject_burst = Math.Min(ReadFloatFromD(dRaw, 265 + Doffset), 10000f),
                            reject_startup = Math.Min(ReadFloatFromD(dRaw, 270 + Doffset), 10000f),
                            reject_preform = Math.Min(ReadFloatFromD(dRaw, 275 + Doffset), 10000f),
                            reject_purging = Math.Min(ReadFloatFromD(dRaw, 280 + Doffset), 10000f),
                            reject_others = Math.Min(ReadFloatFromD(dRaw, 285 + Doffset), 10000f),

                            // Read W Memory values (bits)
                            status_start = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 0),
                            status_off = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 1),
                            production_running = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 2),
                            visual_qc = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 3),
                            done = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 4),
                            remark_signal = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 5),
                            reject_signal = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 6),

                            util_barrel = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 0),
                            util_hyd_motor = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 1),
                            util_dehumidifier = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 2),
                            util_chiller = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 3),
                            util_material = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 4),
                            util_dry_cycle = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 5),
                        });
                        //DisplayMasterData(allPlcData[i], i);
                    }

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var dataService = scope.ServiceProvider.GetRequiredService<BaseService>();

                        foreach (var p in allPlcData)
                        {
                            await dataService.insertMachineMaster(p);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PlcMonitorService at iteration {Count}", _iterationCount);
                }

                await Task.Delay(200, stoppingToken);

            }

            _logger.LogWarning("PlcMonitorService ExecuteAsync loop ended. Total iterations: {Count}", _iterationCount);
        }
        private int ReadIntFromD(byte[] buffer, int wordIndex)
        {
            int byteIndex = wordIndex * 2;
            if (byteIndex + 4 > buffer.Length) return 0;
            byte[] reordered = { buffer[byteIndex + 1], buffer[byteIndex], buffer[byteIndex + 3], buffer[byteIndex + 2] };
            int value = BitConverter.ToInt32(reordered, 0);
            return value;
        }
        private float ReadFloatFromD(byte[] buffer, int wordIndex)
        {
            int byteIndex = wordIndex * 2;
            if (byteIndex + 4 > buffer.Length) return 0f;

            byte[] reordered = { buffer[byteIndex + 1], buffer[byteIndex], buffer[byteIndex + 3], buffer[byteIndex + 2] };
            float value = BitConverter.ToSingle(reordered, 0);

            return value;
        }
        private string ReadStringFromD(byte[] buffer, int wordIndex)
        {
            int byteIndex = wordIndex * 2;

            if (byteIndex >= buffer.Length) return string.Empty;

            if (buffer[byteIndex] == 0) return string.Empty;

            return Encoding.ASCII.GetString(buffer, byteIndex, 100).Trim('\0', ' ');
        }
        private bool ReadBitFromW(bool[] buffer, int wordIndex, int bit)
        {
            int index = wordIndex * 16 + bit;
            if (index < 0 || index >= buffer.Length) return false;
            return buffer[index];
        }
        private void DisplayMasterData(dynamic plcData, int index)
        {
            int Doffset = index * 500;
            int Woffset = index * 3;

            Console.WriteLine($"=== Master.{plcData.id_machine} Data ===");
            Console.WriteLine($"Machine Info    : ID={plcData.id_machine}");

            //Console.WriteLine($"Production      : Shot=D{530 + Doffset} ({plcData.shot}), Shot_Accum=D{532 + Doffset} ({plcData.shot_accum}), " +
            //                  $"CT=D{560 + Doffset} ({plcData.act_ct:F2}s)");

            Console.WriteLine($"Status          : Start=W{5 + Woffset}.0 ({plcData.status_start}), " +
                              $"Off=W{5 + Woffset}.1 ({plcData.status_off}), " +
                              $"Visual_QC=W{5 + Woffset}.3 ({plcData.visual_qc}), " +
                              $"Remark Signal=W{5 + Woffset}.5 ({plcData.remark_signal}), " +
                              $"Reject Signal=W{5 + Woffset}.6 ({plcData.reject_signal})");

            //Console.WriteLine($"Quality         : Visual_QC=W{5 + Woffset}.3 ({plcData.visual_qc}), " +
            //                  $"Measure_QC=W{7 + Woffset}.0 ({plcData.measure_qc})");

            //Console.WriteLine($"Rejects         : Panelling=D{750 + Doffset} ({plcData.reject_panelling:F1}), " +
            //                  $"Lumpy=D{755 + Doffset} ({plcData.reject_lumpy:F1}), " +
            //                  $"Black Dot=D{760 + Doffset} ({plcData.reject_black_dot:F1}), " +
            //                  $"Burst=D{765 + Doffset} ({plcData.reject_burst:F1}), " +
            //                  $"Startup=D{770 + Doffset} ({plcData.reject_startup:F1}), " +
            //                  $"Preform=D{775 + Doffset} ({plcData.reject_preform:F1}), " +
            //                  $"Purging=D{780 + Doffset} ({plcData.reject_purging:F1}), " +
            //                  $"Others=D{785 + Doffset} ({plcData.reject_others:F1})");

            //Console.WriteLine($"Utilities       : Barrel=W{6 + Woffset}.0 ({plcData.util_barrel}), " +
            //                  $"Motor=W{6 + Woffset}.1 ({plcData.util_hyd_motor}), " +
            //                  $"Dehum=W{6 + Woffset}.2 ({plcData.util_dehumidifier}), " +
            //                  $"Chiller=W{6 + Woffset}.3 ({plcData.util_chiller}), " +
            //                  $"Material=W{6 + Woffset}.4 ({plcData.util_material}), " +
            //                  $"Dry Cycle=W{6 + Woffset}.5 ({plcData.util_dry_cycle}), ");

            Console.WriteLine($"Remark=D{900 + Doffset} ({plcData.remark}), " +
                              $"StopCat=D{800 + Doffset} ({plcData.stop_category})");

            Console.WriteLine($"Time            : {plcData.time:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
        }

        public Dictionary<string, object> ReadAllPlcs()
        {
            var ip = "172.17.86.80";
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(ip, out plc) || plc == null)
                {
                    plc = new PlcOmron(ip, 9600, false, 80, 136);
                    _plcConnections[ip] = plc;
                }

                plc.Connect();

                // Read up to 3 times
                byte[] dBuffer1 = null, dBuffer2 = null, dBuffer3 = null;
                bool[] wBits1 = null, wBits2 = null, wBits3 = null;

                if (!TryReadSnapshot(plc, out dBuffer1, out wBits1))
                {
                    Console.WriteLine("[PlcService] Read attempt 1 failed");
                    return new Dictionary<string, object>();
                }

                if (!TryReadSnapshot(plc, out dBuffer2, out wBits2))
                {
                    Console.WriteLine("[PlcService] Read attempt 2 failed");
                    return new Dictionary<string, object>();
                }

                // Compare event signals between read 1 and read 2
                if (AreSignalsConsistent(wBits1, wBits2, dBuffer1, dBuffer2))
                {
                    // Use second read
                    return BuildResult(dBuffer2, wBits2);
                }

                // Reads 1 and 2 disagree
                Console.WriteLine("[PlcService] Signal inconsistency detected between reads, taking tiebreaker read");

                if (!TryReadSnapshot(plc, out dBuffer3, out wBits3))
                {
                    Console.WriteLine("[PlcService] Read attempt 3 failed, using read 2");
                    return BuildResult(dBuffer2, wBits2);
                }

                // Majority vote
                if (AreSignalsConsistent(wBits2, wBits3, dBuffer2, dBuffer3))
                    return BuildResult(dBuffer3, wBits3); // reads 2+3 agree

                if (AreSignalsConsistent(wBits1, wBits3, dBuffer1, dBuffer3))
                    return BuildResult(dBuffer3, wBits3); // reads 1+3 agree

                Console.WriteLine("[PlcService] All 3 reads inconsistent — skipping iteration");
                return new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlcService] Critical PLC Connection Error: {ex.Message}");
                if (plc != null)
                {
                    _plcConnections.TryRemove(ip, out _);
                    try { plc.Disconnect(); } catch { }
                }
                return new Dictionary<string, object>();
            }
        }
        public Dictionary<string, object?> ReadSubPlcSignals(int machineId)
        {
            if (machineId < 1 || machineId > 26)
                throw new ArgumentOutOfRangeException(nameof(machineId), "Machine ID must be 1–26.");

            var ip = $"172.17.86.{219 + machineId}";
            var result = new Dictionary<string, object?>();
            PlcOmron? plc = null;

            try
            {
                var cacheKey = $"sub_{ip}";
                if (!_plcConnections.TryGetValue(cacheKey, out plc) || plc == null)
                {
                    byte remoteNode = (byte)(219 + machineId); // 220=M1 … 245=M26
                    plc = new PlcOmron(ip, 9600, false, remoteNode, 136);
                    _plcConnections[cacheKey] = plc;
                }
                plc.Connect();

                // ── D Memory (words 30–775) ───────────────────────────────────────
                const ushort dStart = 30;
                const ushort dEnd = 775;
                const int totalDWords = dEnd - dStart + 1; // 746 words
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
                        Console.WriteLine($"[SubPlc M{machineId}] D read failed at offset {offset}: {ex.Message}");
                        dOk = false;
                    }
                }

                if (dOk)
                {
                    int Didx(int wordAddr) => (wordAddr - dStart) * 2;

                    // Floats
                    result["D30"] = ReadFloatAt(dBuf, Didx(30));
                    result["D90"] = ReadFloatAt(dBuf, Didx(90));
                    result["D700"] = ReadFloatAt(dBuf, Didx(700));
                    result["D705"] = ReadFloatAt(dBuf, Didx(705));
                    result["D710"] = ReadFloatAt(dBuf, Didx(710));
                    result["D715"] = ReadFloatAt(dBuf, Didx(715));
                    result["D720"] = ReadFloatAt(dBuf, Didx(720));
                    result["D725"] = ReadFloatAt(dBuf, Didx(725));
                    result["D730"] = ReadFloatAt(dBuf, Didx(730));
                    result["D735"] = ReadFloatAt(dBuf, Didx(735));
                    result["D740"] = ReadFloatAt(dBuf, Didx(740));
                    result["D745"] = ReadFloatAt(dBuf, Didx(745));
                    result["D750"] = ReadFloatAt(dBuf, Didx(750));
                    result["D755"] = ReadFloatAt(dBuf, Didx(755));
                    result["D760"] = ReadFloatAt(dBuf, Didx(760));
                    result["D765"] = ReadFloatAt(dBuf, Didx(765));
                    result["D770"] = ReadFloatAt(dBuf, Didx(770));
                    result["D775"] = ReadFloatAt(dBuf, Didx(775));

                    // Ints
                    result["D40"] = ReadIntAt(dBuf, Didx(40));
                    result["D50"] = ReadIntAt(dBuf, Didx(50));
                    result["D110"] = ReadIntAt(dBuf, Didx(110));
                    result["D120"] = ReadIntAt(dBuf, Didx(120));
                    result["D190"] = ReadIntAt(dBuf, Didx(190));

                    // Strings (200 bytes each)
                    result["D200"] = ReadStringAt(dBuf, Didx(200), 200);
                    result["D300"] = ReadStringAt(dBuf, Didx(300), 200);
                    result["D400"] = ReadStringAt(dBuf, Didx(400), 200);
                    result["D500"] = ReadStringAt(dBuf, Didx(500), 200);
                }

                // ── W Memory bits (words 5–65) ────────────────────────────────────
                try
                {
                    const ushort wStart = 5;
                    ushort wBitCount = (ushort)((65 - 5 + 1) * 16); // 976 bits
                    byte[] wChunk = plc.Read(wStart, wBitCount, 0, MemoryAreaBits.Work);

                    bool Wbit(int word, int bit)
                    {
                        int idx = (word - wStart) * 16 + bit;
                        return idx >= 0 && idx < wChunk.Length && wChunk[idx] != 0;
                    }

                    result["W5.00"] = Wbit(5, 0);
                    result["W6.00"] = Wbit(6, 0);
                    result["W7.00"] = Wbit(7, 0);
                    result["W8.00"] = Wbit(8, 0);
                    result["W9.00"] = Wbit(9, 0);
                    result["W60.00"] = Wbit(60, 0);
                    result["W61.00"] = Wbit(61, 0);
                    result["W62.00"] = Wbit(62, 0);
                    result["W63.00"] = Wbit(63, 0);
                    result["W63.01"] = Wbit(63, 1);
                    result["W64.00"] = Wbit(64, 0);
                    result["W65.00"] = Wbit(65, 0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SubPlc M{machineId}] W read failed: {ex.Message}");
                }

                // ── Holding Memory H30–H34 ────────────────────────────────────────
                try
                {
                    byte[] hChunk = plc.Read(30, 6, 0, (MemoryAreaBits)0xB2);
                    result["H30"] = ReadIntAt(hChunk, 0);
                    result["H32"] = ReadIntAt(hChunk, 4);
                    result["H34"] = ReadIntAt(hChunk, 8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SubPlc M{machineId}] H read failed: {ex.Message}");
                }

                // ── Input bits IN 0.00–0.08 ───────────────────────────────────────
                try
                {
                    byte[] inChunk = plc.Read(0, 16, 0, (MemoryAreaBits)0x80);
                    bool INbit(int bit) => bit < inChunk.Length && inChunk[bit] != 0;

                    result["IN0.00"] = INbit(0);
                    result["IN0.01"] = INbit(1);
                    result["IN0.02"] = INbit(2);
                    result["IN0.03"] = INbit(3);
                    result["IN0.04"] = INbit(4);
                    result["IN0.05"] = INbit(5);
                    result["IN0.06"] = INbit(6);
                    result["IN0.07"] = INbit(7);
                    result["IN0.08"] = INbit(8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SubPlc M{machineId}] IN read failed: {ex.Message}");
                }

                // ── Output bits OUT 100.00 ────────────────────────────────────────
                try
                {
                    byte[] outChunk = plc.Read(100, 16, 0, (MemoryAreaBits)0x82);
                    result["OUT100.00"] = outChunk.Length > 0 && outChunk[0] != 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SubPlc M{machineId}] OUT read failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SubPlc M{machineId}] Critical error: {ex.Message}");
                if (plc != null)
                {
                    _plcConnections.TryRemove($"sub_{ip}", out _);
                    try { plc.Disconnect(); } catch { }
                }
                throw;
            }

            return result;
        }
        public void UpdatePLCS(dynamic master)
        {
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(WRITE_KEY, out plc) || plc == null)
                {
                    plc = new PlcOmron("172.17.86.80", 9600, false, 80, 1);
                    _plcConnections[WRITE_KEY] = plc;
                }
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
                Console.WriteLine($"[PlcService] Error in UpdatePLCS: {ex.Message}");
            }
        }
        public void UpdatePacker(dynamic staffList)
        {
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(WRITE_KEY, out plc) || plc == null)
                {
                    plc = new PlcOmron("172.17.86.80", 9600, false, 80, 1);
                    _plcConnections[WRITE_KEY] = plc;
                }
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
                Console.WriteLine($"[PlcService] Error in UpdatePLCS: {ex.Message}");
            }
        }
        public void UpdateMeasureQC(machine_master master)
        {
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(WRITE_KEY, out plc) || plc == null)
                {
                    plc = new PlcOmron("172.17.86.80", 9600, false, 80, 1);
                    _plcConnections[WRITE_KEY] = plc;
                }
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
                Console.WriteLine($"[PlcService] Error in UpdateMeasureQC: {ex.Message}");
            }
        }
        public void ChangePassword(Dictionary<string, int> passwords)
        {
            var departmentAddressMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                ["maintenance"] = 10,
                ["technician"] = 12,
                ["production"] = 14,
            };

            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(WRITE_KEY, out plc) || plc == null)
                {
                    plc = new PlcOmron("172.17.86.80", 9600, false, 80, 1);
                    _plcConnections[WRITE_KEY] = plc;
                }
                plc.Connect();

                foreach (var (department, password) in passwords)
                {
                    if (!departmentAddressMap.TryGetValue(department, out ushort address))
                    {
                        continue;
                    }
                    WriteHolding(plc, address, password);
                }

                WriteBoolOmron(plc, wordAddress: 1, bit: 1, value: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlcService] Error in ChangePassword: {ex.Message}");
            }
        }
        private bool TryReadSnapshot(PlcOmron plc, out byte[] dBuffer, out bool[] wBits)
        {
            dBuffer = null;
            wBits = null;

            // Read D area
            ushort dStart = 500;
            ushort dEnd = 13500;
            int totalDWords = dEnd - dStart + 1;
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
                        Console.WriteLine($"[PlcService] D area short read at offset {offset}: " +
                                          $"expected {expectedBytes}, got {chunk?.Length ?? 0}");
                        return false;
                    }

                    Buffer.BlockCopy(chunk, 0, buffer, offset * 2, chunk.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PlcService] D area read failed at offset {offset}: {ex.Message}");
                    return false;
                }
            }

            ushort wStart = 5;
            ushort wEnd = 81;
            int totalWWords = wEnd - wStart + 1;
            int totalBits = totalWWords * 16;
            bool[] bits = new bool[totalBits];

            try
            {
                byte[] chunk = plc.Read(wStart, (ushort)totalBits, 0, MemoryAreaBits.Work);

                if (chunk == null || chunk.Length < totalBits)
                {
                    Console.WriteLine($"[PlcService] W area short read: " +
                                      $"expected {totalBits}, got {chunk?.Length ?? 0}");
                    return false;
                }

                for (int i = 0; i < totalBits; i++)
                    bits[i] = chunk[i] != 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlcService] W area read failed: {ex.Message}");
                return false;
            }

            dBuffer = buffer;
            wBits = bits;
            return true;
        }
        private bool AreSignalsConsistent(bool[] w1, bool[] w2, byte[] d1, byte[] d2)
        {
            for (int i = 0; i < 26; i++)
            {
                int Woffset = i * 3;
                int Doffset = i * 500;

                // W area bit signals
                if (ReadBit(w1, Woffset, 0) != ReadBit(w2, Woffset, 0)) return false; // status_start
                if (ReadBit(w1, Woffset, 1) != ReadBit(w2, Woffset, 1)) return false; // status_off
                if (ReadBit(w1, Woffset, 5) != ReadBit(w2, Woffset, 5)) return false; // remark_signal
                if (ReadBit(w1, Woffset, 6) != ReadBit(w2, Woffset, 6)) return false; // reject_signal
                if (ReadBit(w1, Woffset + 1, 0) != ReadBit(w2, Woffset + 1, 0)) return false; // util_barrel
                if (ReadBit(w1, Woffset + 1, 1) != ReadBit(w2, Woffset + 1, 1)) return false; // util_hyd_motor
                if (ReadBit(w1, Woffset + 1, 2) != ReadBit(w2, Woffset + 1, 2)) return false; // util_dehumidifier
                if (ReadBit(w1, Woffset + 1, 3) != ReadBit(w2, Woffset + 1, 3)) return false; // util_chiller
                if (ReadBit(w1, Woffset + 1, 4) != ReadBit(w2, Woffset + 1, 4)) return false; // util_material
                if (ReadBit(w1, Woffset + 1, 5) != ReadBit(w2, Woffset + 1, 5)) return false; // util_dry_cycle

                // D area string signals
                string cat1 = ReadString(d1, 300 + Doffset);
                string cat2 = ReadString(d2, 300 + Doffset);
                if (cat1 != cat2) return false;
            }

            return true;
        }
        private bool ReadBit(bool[] buffer, int wordIndex, int bit)
        {
            int index = wordIndex * 16 + bit;
            if (index < 0 || index >= buffer.Length) return false;
            return buffer[index];
        }
        private string ReadString(byte[] buffer, int wordIndex)
        {
            int byteIndex = wordIndex * 2;
            if (byteIndex >= buffer.Length) return string.Empty;
            if (buffer[byteIndex] == 0) return string.Empty;
            return Encoding.ASCII.GetString(buffer, byteIndex, 100).Trim('\0', ' ');
        }
        private static Dictionary<string, object> BuildResult(byte[] dBuffer, bool[] wBits)
        {
            return new Dictionary<string, object>
            {
                ["Data"] = new Dictionary<string, object>
                {
                    ["D_RAW"] = dBuffer,
                    ["W_RAW"] = wBits
                }
            };
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
        private static bool WriteIntOmron(PlcOmron plc, ushort address, int value)
        {
            var bytes = BitConverter.GetBytes(value);

            byte[] reorderedBytes = new byte[] { bytes[1], bytes[0], bytes[3], bytes[2] };

            return plc.Write(address, reorderedBytes, 0, 2, MemoryAreaBits.DataMemory);
        }
        private static bool WriteFloatOmron(PlcOmron plc, ushort address, float value)
        {
            var bytes = BitConverter.GetBytes(value);

            byte[] reorderedBytes = new byte[] { bytes[1], bytes[0], bytes[3], bytes[2] };

            return plc.Write(address, reorderedBytes, 0, 2, MemoryAreaBits.DataMemory);
        }
        private static bool WriteStringOmron(PlcOmron plc, ushort address, string value, int maxLength = 100)
        {
            var stringBytes = Encoding.ASCII.GetBytes(value ?? "");
            var bytes = new byte[maxLength];

            Array.Copy(stringBytes, bytes, Math.Min(stringBytes.Length, maxLength));

            if (bytes.Length % 2 != 0)
            {
                Array.Resize(ref bytes, bytes.Length + 1);
            }

            ushort wordCount = (ushort)(bytes.Length / 2);

            return plc.Write(address, bytes, 0, wordCount, MemoryAreaBits.DataMemory);
        }
        private static bool WriteBoolOmron(PlcOmron plc, ushort wordAddress, byte bit, bool value)
        {
            byte bitValue = (byte)(value ? 1 : 0);

            return plc.Write(wordAddress, new byte[] { bitValue }, bit, 1, MemoryAreaBits.Work);
        }
        private static bool WriteHolding(PlcOmron plc, ushort address, int value)
        {
            var bytes = BitConverter.GetBytes(value);

            byte[] reorderedBytes = new byte[] { bytes[1], bytes[0], bytes[3], bytes[2] };

            return plc.Write(address, reorderedBytes, 0, 2, (MemoryAreaBits)0xB2);
        }
    }
}
